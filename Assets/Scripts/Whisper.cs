using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class Whisper
{
    private const int FftLength = 400;
    private const int HopLength = 160;
    private const int BlankToken = 220;
    private const int RowBlock = 32;
    private const int ColumnBlock = 64;
    private const int VocabBlock = 1024;
    private const int MaxRepeatSize = 8;
    private const int RepeatCount = 4;
    private const int MaxDecodeTokens = 120;
    private const int MinPhraseTokens = 3;
    private const float LayerNormEpsilon = 1e-5f;

    private static readonly float[] HalfTable = BuildHalfTable();

    private static readonly string[] Languages =
    {
        "en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr", "pl", "ca", "nl", "ar", "sv", "it", "id",
        "hi", "fi", "vi", "he", "uk", "el", "ms", "cs", "ro", "da", "hu", "ta", "no", "th", "ur", "hr", "bg",
        "lt", "la", "mi", "ml", "cy", "sk", "te", "fa", "lv", "bn", "sr", "az", "sl", "kn", "et", "mk", "br",
        "eu", "is", "hy", "ne", "mn", "bs", "kk", "sq", "sw", "gl", "mr", "pa", "si", "km", "sn", "yo", "so",
        "af", "oc", "ka", "be", "tg", "sd", "gu", "am", "yi", "lo", "uz", "fo", "ht", "ps", "tk", "nn", "mt",
        "sa", "lb", "my", "bo", "tl", "mg", "as", "tt", "haw", "ln", "ha", "ba", "jw", "su", "yue"
    };

    private readonly float[] _conv1;
    private readonly float[] _conv2;
    private readonly float[] _cos = new float[FftLength];
    private readonly float[][] _crossKeys;
    private readonly float[][] _crossValues;
    private readonly int _eot;
    private readonly int _filterBins;
    private readonly float[] _hann = new float[FftLength];
    private readonly float[] _melFilters;
    private readonly int _noSpeech;
    private readonly int _noTimestamps;

    private readonly ParallelOptions _options = new()
        { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };

    private readonly float[] _sin = new float[FftLength];
    private readonly int _sot;
    private readonly Dictionary<string, float[]> _tensors = new();
    private readonly int _transcribe;
    private readonly List<byte[]> _vocab = new();

    private float[] _attention;
    private float[][] _beamLogits;
    private float[][][] _cacheKeys;
    private float[][][] _cacheValues;
    private int _encodedRows;
    private float[] _hidden;
    private float[] _k;
    private float[] _norm;
    private float[] _projection;
    private float[] _q;
    private float[] _v;
    private float[] _x;

    public Whisper(string path)
    {
        using (BinaryReader reader = new(new BufferedStream(File.OpenRead(path), 1 << 20)))
        {
            if (reader.ReadUInt32() != 0x67676d6c) throw new InvalidDataException("Not a ggml Whisper model.");
            VocabSize = reader.ReadInt32();
            AudioContext = reader.ReadInt32();
            AudioState = reader.ReadInt32();
            AudioHeads = reader.ReadInt32();
            AudioLayers = reader.ReadInt32();
            TextContext = reader.ReadInt32();
            TextState = reader.ReadInt32();
            TextHeads = reader.ReadInt32();
            TextLayers = reader.ReadInt32();
            MelCount = reader.ReadInt32();
            reader.ReadInt32();

            int filterRows = reader.ReadInt32();
            _filterBins = reader.ReadInt32();
            _melFilters = ReadFloats(reader, filterRows * _filterBins);

            int tokenCount = reader.ReadInt32();
            for (int i = 0; i < tokenCount; i++)
                _vocab.Add(reader.ReadBytes(reader.ReadInt32()));

            Stream stream = reader.BaseStream;
            while (stream.Position < stream.Length)
            {
                int dimensions = reader.ReadInt32();
                int nameLength = reader.ReadInt32();
                int type = reader.ReadInt32();
                int count = 1;
                for (int d = 0; d < dimensions; d++)
                    count *= reader.ReadInt32();
                string name = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
                _tensors[name] = type switch
                {
                    0 => ReadFloats(reader, count),
                    1 => ReadHalves(reader, count),
                    _ => throw new InvalidDataException(
                        $"Tensor {name} uses unsupported type {type}. Use a non-quantized model such as ggml-tiny.en.bin.")
                };
            }
        }

        _conv1 = ReorderConv(Tensor("encoder.conv1.weight"), AudioState, MelCount);
        _conv2 = ReorderConv(Tensor("encoder.conv2.weight"), AudioState, AudioState);
        _tensors.Remove("encoder.conv1.weight");
        _tensors.Remove("encoder.conv2.weight");

        for (int i = 0; i < FftLength; i++)
        {
            float angle = 2f * MathF.PI * i / FftLength;
            _hann[i] = 0.5f - 0.5f * MathF.Cos(angle);
            _cos[i] = MathF.Cos(angle);
            _sin[i] = MathF.Sin(angle);
        }

        _eot = 50256;
        _sot = 50257;
        _transcribe = 50358;
        _noSpeech = 50361;
        _noTimestamps = 50362;
        if (IsMultilingual)
        {
            int shift = VocabSize - 51765 - 1 - 98;
            _eot++;
            _sot++;
            _transcribe += shift;
            _noSpeech += shift;
            _noTimestamps += shift;
        }

        _crossKeys = new float[TextLayers][];
        _crossValues = new float[TextLayers][];
    }

    private int VocabSize { get; }
    private int AudioContext { get; }
    private int AudioState { get; }
    private int AudioHeads { get; }
    private int AudioLayers { get; }
    private int TextContext { get; }
    private int TextState { get; }
    private int TextHeads { get; }
    private int TextLayers { get; }
    private int MelCount { get; }
    private bool IsMultilingual => VocabSize >= 51865;
    private int CachePositions => Math.Min(TextContext, MaxDecodeTokens + 8);

    public string Transcribe(float[] samples, int contextPadding, int minimumContext, bool fullContext,
        string language, float noSpeechThreshold, int beamSize, CancellationToken token)
    {
        int maxSamples = AudioContext * HopLength * 2 - FftLength;
        if (samples.Length > maxSamples) Array.Resize(ref samples, maxSamples);
        int needed = samples.Length / (HopLength * 2) + 1;
        int rows = fullContext
            ? AudioContext
            : Math.Min(AudioContext, Math.Max(minimumContext, needed + Math.Max(0, contextPadding)));

        float[] encoded = Encode(LogMel(samples, rows * 2), rows, token);
        token.ThrowIfCancellationRequested();
        PrepareCrossAttention(encoded, rows);

        int beams = Math.Max(1, beamSize);
        EnsureBeams(beams);
        List<int> prompt = BuildPrompt(language);
        float[] logits = _beamLogits[0];
        Decode(prompt, 0, 1, logits, 0);
        float noSpeech = Probability(logits, _noSpeech);
        Decode(prompt, 1, prompt.Count - 1, logits, 0);

        Hypothesis best = beams == 1 ? Greedy(prompt, token) : BeamSearch(prompt, beams, token);
        List<int> output = best.Tokens.GetRange(prompt.Count, best.Tokens.Count - prompt.Count);
        float averageLogProbability = best.Score / (output.Count + 1);
        if (noSpeech > noSpeechThreshold && averageLogProbability < -1f) return string.Empty;
        TrimRepeats(output);
        return DecodeText(output);
    }

    private Hypothesis Greedy(List<int> prompt, CancellationToken token)
    {
        float[] logits = _beamLogits[0];
        List<int> tokens = new(prompt);
        float score = 0f;
        int maxTokens = Math.Min(TextContext / 2, MaxDecodeTokens);
        for (int step = 0; step < maxTokens && tokens.Count < CachePositions; step++)
        {
            Suppress(logits, step == 0);
            int best = ArgMax(logits);
            score += logits[best] - LogSumExp(logits);
            if (best == _eot) break;
            tokens.Add(best);
            if (Repeats(tokens, prompt.Count)) break;
            token.ThrowIfCancellationRequested();
            Decode(tokens, tokens.Count - 1, 1, logits, 0);
        }

        return new Hypothesis(tokens, score, 0);
    }

    private Hypothesis BeamSearch(List<int> prompt, int beamSize, CancellationToken token)
    {
        List<Hypothesis> beams = new() { new Hypothesis(new List<int>(prompt), 0f, 0) };
        List<Hypothesis> finished = new();
        List<(int parent, int id, float score)> candidates = new();
        int[] top = new int[beamSize + 1];
        int nextSet = 1;
        int maxTokens = Math.Min(TextContext / 2, MaxDecodeTokens);

        for (int step = 0; step < maxTokens; step++)
        {
            candidates.Clear();
            for (int i = 0; i < beams.Count; i++)
            {
                float[] logits = _beamLogits[beams[i].Slot];
                Suppress(logits, step == 0);
                float normalizer = LogSumExp(logits);
                int found = TopK(logits, top);
                for (int j = 0; j < found; j++)
                    candidates.Add((i, top[j], beams[i].Score + logits[top[j]] - normalizer));
            }

            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            List<Hypothesis> next = new();
            foreach ((int parent, int id, float score) in candidates)
            {
                Hypothesis source = beams[parent];
                if (id == _eot)
                {
                    if (finished.Count < beamSize) finished.Add(new Hypothesis(source.Tokens, score, -1));
                    continue;
                }

                List<int> tokens = new(source.Tokens) { id };
                if (Repeats(tokens, prompt.Count) || tokens.Count >= CachePositions)
                {
                    if (finished.Count < beamSize) finished.Add(new Hypothesis(tokens, score, -1));
                    continue;
                }

                int slot = nextSet * beamSize + next.Count;
                CopyCache(source.Slot, slot, source.Tokens.Count);
                next.Add(new Hypothesis(tokens, score, slot));
                if (next.Count == beamSize) break;
            }

            if (finished.Count >= beamSize || next.Count == 0) break;
            foreach (Hypothesis hypothesis in next)
            {
                token.ThrowIfCancellationRequested();
                Decode(hypothesis.Tokens, hypothesis.Tokens.Count - 1, 1, _beamLogits[hypothesis.Slot],
                    hypothesis.Slot);
            }

            beams = next;
            nextSet ^= 1;
        }

        if (finished.Count == 0) finished.AddRange(beams);
        Hypothesis best = finished[0];
        foreach (Hypothesis hypothesis in finished)
            if (hypothesis.Score / Math.Max(1, hypothesis.Tokens.Count - prompt.Count) >
                best.Score / Math.Max(1, best.Tokens.Count - prompt.Count))
                best = hypothesis;
        return best;
    }

    private void Suppress(float[] logits, bool first)
    {
        for (int id = _eot + 1; id < VocabSize; id++)
            logits[id] = float.NegativeInfinity;
        if (!first) return;
        logits[_eot] = float.NegativeInfinity;
        logits[BlankToken] = float.NegativeInfinity;
    }

    private void EnsureBeams(int beamSize)
    {
        int slots = beamSize * 2;
        if (_cacheKeys != null && _cacheKeys.Length >= slots) return;
        int size = CachePositions * TextState;
        _cacheKeys = new float[slots][][];
        _cacheValues = new float[slots][][];
        _beamLogits = new float[slots][];
        for (int slot = 0; slot < slots; slot++)
        {
            _cacheKeys[slot] = new float[TextLayers][];
            _cacheValues[slot] = new float[TextLayers][];
            _beamLogits[slot] = new float[VocabSize];
            for (int layer = 0; layer < TextLayers; layer++)
            {
                _cacheKeys[slot][layer] = new float[size];
                _cacheValues[slot][layer] = new float[size];
            }
        }
    }

    private void CopyCache(int from, int to, int positions)
    {
        int length = positions * TextState;
        for (int layer = 0; layer < TextLayers; layer++)
        {
            Array.Copy(_cacheKeys[from][layer], 0, _cacheKeys[to][layer], 0, length);
            Array.Copy(_cacheValues[from][layer], 0, _cacheValues[to][layer], 0, length);
        }
    }

    private List<int> BuildPrompt(string language)
    {
        List<int> prompt = new() { _sot };
        if (IsMultilingual)
        {
            prompt.Add(_sot + 1 + Math.Max(0, Array.IndexOf(Languages, language)));
            prompt.Add(_transcribe);
        }

        prompt.Add(_noTimestamps);
        return prompt;
    }

    private string DecodeText(List<int> tokens)
    {
        List<byte> bytes = new();
        foreach (int token in tokens)
            if (token < _vocab.Count)
                bytes.AddRange(_vocab[token]);
        return Encoding.UTF8.GetString(bytes.ToArray()).Trim();
    }

    private float[] LogMel(float[] samples, int frames)
    {
        int mels = MelCount;
        int bins = _filterBins;
        float[] mel = new float[frames * mels];
        Parallel.For(0, frames, _options, () => new float[FftLength + bins], (t, _, buffer) =>
        {
            bool silent = true;
            for (int i = 0; i < FftLength; i++)
            {
                int index = t * HopLength + i - FftLength / 2;
                if (index < 0) index = -index;
                float sample = index < samples.Length ? samples[index] : 0f;
                if (sample != 0f) silent = false;
                buffer[i] = sample * _hann[i];
            }

            for (int k = 0; k < bins; k++)
            {
                float power = 0f;
                if (!silent)
                {
                    float real = 0f;
                    float imaginary = 0f;
                    for (int i = 0; i < FftLength; i++)
                    {
                        int phase = k * i % FftLength;
                        real += buffer[i] * _cos[phase];
                        imaginary -= buffer[i] * _sin[phase];
                    }

                    power = real * real + imaginary * imaginary;
                }

                buffer[FftLength + k] = power;
            }

            for (int m = 0; m < mels; m++)
            {
                float sum = 0f;
                int filterOffset = m * bins;
                for (int k = 0; k < bins; k++)
                    sum += _melFilters[filterOffset + k] * buffer[FftLength + k];
                mel[t * mels + m] = MathF.Log10(MathF.Max(sum, 1e-10f));
            }

            return buffer;
        }, _ => { });

        float max = float.NegativeInfinity;
        foreach (float value in mel)
            max = MathF.Max(max, value);
        float floor = max - 8f;
        for (int i = 0; i < mel.Length; i++)
            mel[i] = (MathF.Max(mel[i], floor) + 4f) / 4f;
        return mel;
    }

    private float[] Encode(float[] mel, int rows, CancellationToken token)
    {
        int state = AudioState;
        float[] hidden = Conv(mel, rows * 2, MelCount, _conv1, Tensor("encoder.conv1.bias"), state, 1);
        Gelu(hidden, rows * 2, state);
        float[] x = Conv(hidden, rows * 2, state, _conv2, Tensor("encoder.conv2.bias"), state, 2);
        Gelu(x, rows, state);

        float[] position = Tensor("encoder.positional_embedding");
        for (int i = 0; i < rows * state; i++)
            x[i] += position[i];

        for (int layer = 0; layer < AudioLayers; layer++)
        {
            token.ThrowIfCancellationRequested();
            string prefix = $"encoder.blocks.{layer}.";
            float[] norm = Ensure(ref _norm, rows * state);
            LayerNorm(x, rows, state, Tensor(prefix + "attn_ln.weight"), Tensor(prefix + "attn_ln.bias"), norm);
            float[] q = Ensure(ref _q, rows * state);
            float[] k = Ensure(ref _k, rows * state);
            float[] v = Ensure(ref _v, rows * state);
            Linear(norm, rows, state, Tensor(prefix + "attn.query.weight"), Tensor(prefix + "attn.query.bias"),
                state, q);
            Linear(norm, rows, state, Tensor(prefix + "attn.key.weight"), null, state, k);
            Linear(norm, rows, state, Tensor(prefix + "attn.value.weight"), Tensor(prefix + "attn.value.bias"),
                state, v);
            float[] attention = Ensure(ref _attention, rows * state);
            Attention(q, rows, k, v, rows, state, AudioHeads, false, 0, attention);
            Residual(x, rows, state, attention, prefix + "attn.out");
            FeedForward(x, rows, state, prefix);
        }

        LayerNorm(x, rows, state, Tensor("encoder.ln_post.weight"), Tensor("encoder.ln_post.bias"), x);
        return x;
    }

    private void PrepareCrossAttention(float[] encoded, int rows)
    {
        int state = TextState;
        for (int layer = 0; layer < TextLayers; layer++)
        {
            string prefix = $"decoder.blocks.{layer}.cross_attn.";
            float[] keys = Ensure(ref _crossKeys[layer], rows * state);
            float[] values = Ensure(ref _crossValues[layer], rows * state);
            Linear(encoded, rows, state, Tensor(prefix + "key.weight"), null, state, keys);
            Linear(encoded, rows, state, Tensor(prefix + "value.weight"), Tensor(prefix + "value.bias"), state,
                values);
        }

        _encodedRows = rows;
    }

    private void Decode(List<int> tokens, int start, int count, float[] logits, int slot)
    {
        int state = TextState;
        float[] embedding = Tensor("decoder.token_embedding.weight");
        float[] position = Tensor("decoder.positional_embedding");
        float[] x = Ensure(ref _x, count * state);
        for (int i = 0; i < count; i++)
        {
            int tokenOffset = tokens[start + i] * state;
            int positionOffset = (start + i) * state;
            for (int j = 0; j < state; j++)
                x[i * state + j] = embedding[tokenOffset + j] + position[positionOffset + j];
        }

        for (int layer = 0; layer < TextLayers; layer++)
        {
            string prefix = $"decoder.blocks.{layer}.";
            float[] norm = Ensure(ref _norm, count * state);
            float[] q = Ensure(ref _q, count * state);
            float[] k = Ensure(ref _k, count * state);
            float[] v = Ensure(ref _v, count * state);
            float[] attention = Ensure(ref _attention, count * state);

            LayerNorm(x, count, state, Tensor(prefix + "attn_ln.weight"), Tensor(prefix + "attn_ln.bias"), norm);
            Linear(norm, count, state, Tensor(prefix + "attn.query.weight"), Tensor(prefix + "attn.query.bias"),
                state, q);
            Linear(norm, count, state, Tensor(prefix + "attn.key.weight"), null, state, k);
            Linear(norm, count, state, Tensor(prefix + "attn.value.weight"), Tensor(prefix + "attn.value.bias"),
                state, v);
            Array.Copy(k, 0, _cacheKeys[slot][layer], start * state, count * state);
            Array.Copy(v, 0, _cacheValues[slot][layer], start * state, count * state);
            Attention(q, count, _cacheKeys[slot][layer], _cacheValues[slot][layer], start + count, state, TextHeads,
                true, start,
                attention);
            Residual(x, count, state, attention, prefix + "attn.out");

            LayerNorm(x, count, state, Tensor(prefix + "cross_attn_ln.weight"),
                Tensor(prefix + "cross_attn_ln.bias"), norm);
            Linear(norm, count, state, Tensor(prefix + "cross_attn.query.weight"),
                Tensor(prefix + "cross_attn.query.bias"), state, q);
            Attention(q, count, _crossKeys[layer], _crossValues[layer], _encodedRows, state, TextHeads, false, 0,
                attention);
            Residual(x, count, state, attention, prefix + "cross_attn.out");

            FeedForward(x, count, state, prefix);
        }

        float[] output = Ensure(ref _norm, count * state);
        LayerNorm(x, count, state, Tensor("decoder.ln.weight"), Tensor("decoder.ln.bias"), output);
        int last = (count - 1) * state;
        Parallel.For(0, (VocabSize + VocabBlock - 1) / VocabBlock, _options, block =>
            WhisperKernels.MatVec(embedding, output, last, state, block * VocabBlock,
                Math.Min(VocabSize, (block + 1) * VocabBlock), logits));
    }

    private void Residual(float[] x, int rows, int size, float[] input, string name)
    {
        float[] projection = Ensure(ref _projection, rows * size);
        Linear(input, rows, size, Tensor(name + ".weight"), Tensor(name + ".bias"), size, projection);
        for (int i = 0; i < rows * size; i++)
            x[i] += projection[i];
    }

    private void FeedForward(float[] x, int rows, int size, string prefix)
    {
        float[] norm = Ensure(ref _norm, rows * size);
        LayerNorm(x, rows, size, Tensor(prefix + "mlp_ln.weight"), Tensor(prefix + "mlp_ln.bias"), norm);
        float[] bias = Tensor(prefix + "mlp.0.bias");
        int hiddenSize = bias.Length;
        float[] hidden = Ensure(ref _hidden, rows * hiddenSize);
        Linear(norm, rows, size, Tensor(prefix + "mlp.0.weight"), bias, hiddenSize, hidden);
        Gelu(hidden, rows, hiddenSize);
        float[] projection = Ensure(ref _projection, rows * size);
        Linear(hidden, rows, hiddenSize, Tensor(prefix + "mlp.2.weight"), Tensor(prefix + "mlp.2.bias"), size,
            projection);
        for (int i = 0; i < rows * size; i++)
            x[i] += projection[i];
    }

    private void Attention(float[] q, int queryRows, float[] k, float[] v, int keyRows, int state, int heads,
        bool causal, int queryOffset, float[] output)
    {
        int headSize = state / heads;
        float scale = 1f / MathF.Sqrt(headSize);
        Parallel.For(0, heads * queryRows, _options, () => new float[keyRows], (index, _, scores) =>
        {
            int head = index / queryRows;
            int t = index % queryRows;
            int headOffset = head * headSize;
            int limit = causal ? queryOffset + t + 1 : keyRows;
            WhisperKernels.Attention(q, k, v, scores, limit, state, headSize, headOffset, t * state + headOffset,
                scale, output);
            return scores;
        }, _ => { });
    }

    private void Linear(float[] input, int rows, int inputSize, float[] weights, float[] bias, int outputSize,
        float[] output)
    {
        if (rows < RowBlock)
        {
            Parallel.For(0, (outputSize + ColumnBlock - 1) / ColumnBlock, _options, block =>
                WhisperKernels.Linear(input, 0, rows, inputSize, weights, bias, block * ColumnBlock,
                    Math.Min(outputSize, (block + 1) * ColumnBlock), outputSize, output));
            return;
        }

        Parallel.For(0, (rows + RowBlock - 1) / RowBlock, _options, block =>
            WhisperKernels.Linear(input, block * RowBlock, Math.Min(rows, (block + 1) * RowBlock), inputSize,
                weights, bias, 0, outputSize, outputSize, output));
    }

    private float[] Conv(float[] input, int inputRows, int inputSize, float[] weights, float[] bias,
        int outputSize, int stride)
    {
        int outputRows = inputRows / stride;
        float[] output = new float[outputRows * outputSize];
        Parallel.For(0, (outputRows + RowBlock - 1) / RowBlock, _options, block =>
            WhisperKernels.Conv(input, inputRows, inputSize, weights, bias, outputSize, stride, block * RowBlock,
                Math.Min(outputRows, (block + 1) * RowBlock), output));
        return output;
    }

    private void Gelu(float[] data, int rows, int size)
    {
        Parallel.For(0, (rows + RowBlock - 1) / RowBlock, _options, block =>
            WhisperKernels.Gelu(data, block * RowBlock * size, Math.Min(rows, (block + 1) * RowBlock) * size));
    }

    private float[] Tensor(string name)
    {
        if (_tensors.TryGetValue(name, out float[] tensor)) return tensor;
        throw new InvalidDataException($"Whisper model is missing tensor {name}.");
    }

    private static void LayerNorm(float[] input, int rows, int size, float[] weight, float[] bias, float[] output)
    {
        for (int t = 0; t < rows; t++)
        {
            int offset = t * size;
            float mean = 0f;
            for (int j = 0; j < size; j++)
                mean += input[offset + j];
            mean /= size;

            float variance = 0f;
            for (int j = 0; j < size; j++)
            {
                float difference = input[offset + j] - mean;
                variance += difference * difference;
            }

            float inverse = 1f / MathF.Sqrt(variance / size + LayerNormEpsilon);
            for (int j = 0; j < size; j++)
                output[offset + j] = (input[offset + j] - mean) * inverse * weight[j] + bias[j];
        }
    }

    private static int ArgMax(float[] values)
    {
        int best = 0;
        for (int i = 1; i < values.Length; i++)
            if (values[i] > values[best])
                best = i;
        return best;
    }

    private static float Probability(float[] logits, int token)
    {
        float max = float.NegativeInfinity;
        foreach (float logit in logits)
            max = MathF.Max(max, logit);
        double sum = 0d;
        foreach (float logit in logits)
            sum += Math.Exp(logit - max);
        return (float)(Math.Exp(logits[token] - max) / sum);
    }

    private static float LogSumExp(float[] logits)
    {
        float max = float.NegativeInfinity;
        foreach (float logit in logits)
            max = MathF.Max(max, logit);
        double sum = 0d;
        foreach (float logit in logits)
            sum += Math.Exp(logit - max);
        return max + (float)Math.Log(sum);
    }

    private static int TopK(float[] values, int[] result)
    {
        int filled = 0;
        for (int id = 0; id < values.Length; id++)
        {
            float value = values[id];
            if (float.IsNegativeInfinity(value)) continue;
            if (filled == result.Length && value <= values[result[filled - 1]]) continue;
            int position = filled < result.Length ? filled++ : filled - 1;
            while (position > 0 && values[result[position - 1]] < value)
            {
                result[position] = result[position - 1];
                position--;
            }

            result[position] = id;
        }

        return filled;
    }

    private static bool Repeats(List<int> tokens, int start)
    {
        int end = tokens.Count;
        for (int size = 1; size <= MaxRepeatSize; size++)
        {
            int required = size >= MinPhraseTokens ? 2 : RepeatCount;
            if (end - start < size * required) continue;
            bool repeating = true;
            for (int r = 1; r < required && repeating; r++)
            for (int i = 0; i < size; i++)
            {
                if (tokens[end - 1 - i] == tokens[end - 1 - i - r * size]) continue;
                repeating = false;
                break;
            }

            if (repeating) return true;
        }

        return false;
    }

    private static void TrimRepeats(List<int> tokens)
    {
        bool trimmed = true;
        while (trimmed)
        {
            trimmed = false;
            for (int size = MinPhraseTokens; size * 2 <= tokens.Count; size++)
            {
                int end = tokens.Count;
                bool same = true;
                for (int i = 0; i < size && same; i++)
                    same = tokens[end - 1 - i] == tokens[end - 1 - i - size];
                if (!same) continue;
                tokens.RemoveRange(end - size, size);
                trimmed = true;
                break;
            }
        }
    }

    private static float[] Ensure(ref float[] buffer, int size)
    {
        if (buffer == null || buffer.Length < size) buffer = new float[size];
        return buffer;
    }

    private static float[] ReorderConv(float[] weights, int outputSize, int inputSize)
    {
        float[] reordered = new float[weights.Length];
        for (int o = 0; o < outputSize; o++)
        for (int i = 0; i < inputSize; i++)
        for (int k = 0; k < 3; k++)
            reordered[(o * 3 + k) * inputSize + i] = weights[(o * inputSize + i) * 3 + k];
        return reordered;
    }

    private static float[] ReadFloats(BinaryReader reader, int count)
    {
        byte[] bytes = reader.ReadBytes(count * 4);
        if (bytes.Length != count * 4) throw new EndOfStreamException("Whisper model file is truncated.");
        float[] result = new float[count];
        Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
        return result;
    }

    private static float[] ReadHalves(BinaryReader reader, int count)
    {
        byte[] bytes = reader.ReadBytes(count * 2);
        if (bytes.Length != count * 2) throw new EndOfStreamException("Whisper model file is truncated.");
        float[] result = new float[count];
        for (int i = 0; i < count; i++)
            result[i] = HalfTable[bytes[i * 2] | (bytes[i * 2 + 1] << 8)];
        return result;
    }

    private static float[] BuildHalfTable()
    {
        float[] table = new float[65536];
        for (int half = 0; half < table.Length; half++)
        {
            int exponent = (half >> 10) & 31;
            int mantissa = half & 1023;
            float value;
            if (exponent == 0) value = mantissa * 5.9604645e-8f;
            else if (exponent == 31) value = mantissa == 0 ? float.PositiveInfinity : float.NaN;
            else value = BitConverter.Int32BitsToSingle(((exponent + 112) << 23) | (mantissa << 13));
            table[half] = (half & 0x8000) != 0 ? -value : value;
        }

        return table;
    }

    private sealed class Hypothesis
    {
        public Hypothesis(List<int> tokens, float score, int slot)
        {
            Tokens = tokens;
            Score = score;
            Slot = slot;
        }

        public List<int> Tokens { get; }
        public float Score { get; }
        public int Slot { get; }
    }
}
