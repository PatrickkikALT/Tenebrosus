using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

public class VoiceRecognition : MonoBehaviour
{
    private const int WhisperSampleRate = 16000;
    private const int MicrophoneLoopSeconds = 2;
    private const int MaxPendingUtterances = 3;

    [Header("Model")] [SerializeField] private string modelFileName = "ggml-tiny.en.bin";
    [SerializeField] private string language = "en";
    [SerializeField] private bool fullContext;
    [SerializeField] private int contextPadding = 100;
    [SerializeField] private int minimumContext = 1000;
    [SerializeField] [Range(0f, 1f)] private float noSpeechThreshold = 0.6f;
    [SerializeField] [Range(1, 8)] private int beamSize = 5;

    [Header("Audio")] [SerializeField] [Range(0.05f, 1f)]
    private float targetPeak = 0.6f;

    [SerializeField] private float maxGain = 30f;

    [Header("Realtime")] [SerializeField] private bool partialResults = true;
    [SerializeField] private float partialInterval = 0.5f;

    [Header("Microphone")] [SerializeField] private string microphoneDevice;
    [SerializeField] private bool pushToTalk;

    [Header("Voice Detection")] [SerializeField] private float minimumNoiseFloor = 0.002f;
    [SerializeField] private float startThreshold = 3f;
    [SerializeField] private float stopThreshold = 1.8f;
    [SerializeField] [Range(0f, 1f)] private float noiseAdaptRate = 0.05f;
    [SerializeField] private int startHops = 3;
    [SerializeField] private int silenceHops = 40;
    [SerializeField] private int preRollHops = 30;
    [SerializeField] private int trailingHopsKept = 30;
    [SerializeField] private float minimumDuration = 0.3f;
    [SerializeField] private float maximumDuration = 10f;

    [Header("Debug")] [SerializeField] private bool logResults = true;
    [SerializeField] private bool saveUtterances;

    private readonly Queue<float[]> _pending = new();
    private readonly List<float> _preRoll = new();
    private readonly List<float> _utterance = new();

    private AudioClip _clip;
    private string _device;
    private float[] _hopBuffer;
    private int _hopLength;
    private float _lastPartialTime;
    private int _loudHops;
    private float _noiseFloor;
    private CancellationTokenSource _partialCancel;
    private int _quietHops;
    private int _readPosition;
    private bool _releaseRequested;
    private int _sampleRate;
    private int _savedCount;
    private Task<string> _transcription;
    private bool _transcriptionIsPartial;
    private float _transcriptionStart;
    private int _transcriptionUtterance;
    private int _utteranceId;
    private Whisper _whisper;

    public event Action<string> PartialTextRecognized;
    public event Action<string> TextRecognized;
    public event Action<string[]> WordsRecognized;

    public string Language
    {
        get => language;
        set => language = value;
    }

    public bool IsReady => _whisper != null;
    public bool IsSpeaking { get; private set; }
    public bool IsProcessing => _transcription != null || _pending.Count > 0;
    public float InputLevel { get; private set; }
    public string PartialText { get; private set; }
    public string LastText { get; private set; }
    public string[] LastWords { get; private set; } = Array.Empty<string>();

    private float NoiseFloorLevel => Mathf.Max(_noiseFloor, minimumNoiseFloor);

    private void Awake()
    {
        string path = Path.Combine(Application.streamingAssetsPath, modelFileName);
        if (!File.Exists(path))
        {
            Debug.LogError(
                $"VoiceRecognition: model not found at {path}. Download {modelFileName} from https://huggingface.co/ggerganov/whisper.cpp/tree/main and place it in Assets/StreamingAssets.");
            return;
        }

        LoadModel(path);
    }

    private void Update()
    {
        ReadMicrophone();
        UpdateTranscription();
    }

    private void OnEnable()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("VoiceRecognition: no microphone found.");
            return;
        }

        _device = string.IsNullOrEmpty(microphoneDevice) ? Microphone.devices[0] : microphoneDevice;
        Microphone.GetDeviceCaps(_device, out int minFrequency, out int maxFrequency);
        int frequency = maxFrequency == 0
            ? WhisperSampleRate
            : Mathf.Clamp(WhisperSampleRate, minFrequency, maxFrequency);
        _clip = Microphone.Start(_device, true, MicrophoneLoopSeconds, frequency);
        _sampleRate = _clip.frequency;
        _hopLength = _sampleRate / 100;
        _hopBuffer = new float[_hopLength];
        _readPosition = 0;
        _noiseFloor = minimumNoiseFloor;
        _loudHops = 0;
        _quietHops = 0;
        IsSpeaking = false;
        _preRoll.Clear();
        _utterance.Clear();
    }

    private void OnDisable()
    {
        _partialCancel?.Cancel();
        if (_clip == null) return;
        Microphone.End(_device);
        _clip = null;
        IsSpeaking = false;
    }

    public void OnPushToTalk(InputAction.CallbackContext ctx)
    {
        if (!pushToTalk || _clip == null) return;
        if (ctx.started) BeginUtterance();
        else if (ctx.canceled) _releaseRequested = true;
    }

    private async void LoadModel(string path)
    {
        try
        {
            _whisper = await Task.Run(() => new Whisper(path));
            if (logResults) Debug.Log("VoiceRecognition: model loaded.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void ReadMicrophone()
    {
        if (_clip == null) return;
        int available = Microphone.GetPosition(_device) - _readPosition;
        if (available < 0) available += _clip.samples;
        while (available >= _hopLength)
        {
            _clip.GetData(_hopBuffer, _readPosition);
            _readPosition = (_readPosition + _hopLength) % _clip.samples;
            available -= _hopLength;
            ProcessHop();
        }

        if (!_releaseRequested) return;
        _releaseRequested = false;
        if (IsSpeaking) EndUtterance();
    }

    private void UpdateTranscription()
    {
        if (_transcription != null)
        {
            if (!_transcription.IsCompleted) return;
            if (_transcription.IsFaulted) Debug.LogException(_transcription.Exception.GetBaseException());
            else if (_transcription.IsCanceled) PartialText = null;
            else if (_transcriptionIsPartial) PublishPartial(_transcription.Result);
            else Publish(_transcription.Result);
            _transcription = null;
        }

        if (_whisper == null) return;
        if (_pending.Count > 0)
        {
            StartTranscription(_pending.Dequeue(), false, minimumContext, beamSize, CancellationToken.None);
            return;
        }

        if (!partialResults || !IsSpeaking) return;
        if (Time.realtimeSinceStartup - _lastPartialTime < partialInterval) return;
        if (_utterance.Count < minimumDuration * _sampleRate) return;
        _lastPartialTime = Time.realtimeSinceStartup;
        _partialCancel?.Dispose();
        _partialCancel = new CancellationTokenSource();
        StartTranscription(Prepare(_utterance), true, 0, 1, _partialCancel.Token);
    }

    private void StartTranscription(float[] audio, bool partial, int minimum, int beams, CancellationToken token)
    {
        Whisper whisper = _whisper;
        int padding = contextPadding;
        bool full = fullContext;
        string languageCode = language;
        float threshold = noSpeechThreshold;
        _transcriptionIsPartial = partial;
        _transcriptionUtterance = _utteranceId;
        _transcriptionStart = Time.realtimeSinceStartup;
        _transcription =
            Task.Run(() => whisper.Transcribe(audio, padding, minimum, full, languageCode, threshold, beams, token),
                token);
    }

    private void PublishPartial(string text)
    {
        if (!IsSpeaking || _transcriptionUtterance != _utteranceId) return;
        if (string.IsNullOrEmpty(text) || text == PartialText) return;
        PartialText = text;
        if (logResults) Debug.Log($"VoiceRecognition: ...{text}");
        PartialTextRecognized?.Invoke(text);
    }

    private void Publish(string text)
    {
        float duration = Time.realtimeSinceStartup - _transcriptionStart;
        if (string.IsNullOrEmpty(text))
        {
            if (logResults) Debug.Log($"VoiceRecognition: no speech ({duration:F1}s).");
            return;
        }

        LastText = text;
        LastWords = SplitWords(text);
        if (logResults) Debug.Log($"VoiceRecognition: \"{text}\" ({duration:F1}s).");
        TextRecognized?.Invoke(text);
        WordsRecognized?.Invoke(LastWords);
    }

    private void ProcessHop()
    {
        float sum = 0f;
        foreach (float sample in _hopBuffer)
            sum += sample * sample;
        InputLevel = Mathf.Sqrt(sum / _hopLength);

        if (IsSpeaking)
        {
            _utterance.AddRange(_hopBuffer);
            if (_utterance.Count >= maximumDuration * _sampleRate)
            {
                if (!pushToTalk) _noiseFloor = Mathf.Max(_noiseFloor, Rms(_utterance) * 0.5f);
                EndUtterance();
                return;
            }

            if (pushToTalk) return;
            _quietHops = InputLevel < NoiseFloorLevel * stopThreshold ? _quietHops + 1 : 0;
            if (_quietHops >= silenceHops) EndUtterance();
            return;
        }

        _preRoll.AddRange(_hopBuffer);
        int preRollLength = preRollHops * _hopLength;
        if (_preRoll.Count > preRollLength)
            _preRoll.RemoveRange(0, _preRoll.Count - preRollLength);
        if (pushToTalk) return;

        if (InputLevel > NoiseFloorLevel * startThreshold)
        {
            if (++_loudHops >= startHops) BeginUtterance();
            return;
        }

        _loudHops = 0;
        _noiseFloor = Mathf.Lerp(_noiseFloor, InputLevel, noiseAdaptRate);
    }

    private void BeginUtterance()
    {
        _utterance.Clear();
        _utterance.AddRange(_preRoll);
        _preRoll.Clear();
        _loudHops = 0;
        _quietHops = 0;
        _utteranceId++;
        _lastPartialTime = Time.realtimeSinceStartup;
        PartialText = null;
        IsSpeaking = true;
    }

    private void EndUtterance()
    {
        IsSpeaking = false;
        if (_transcriptionIsPartial) _partialCancel?.Cancel();
        int trailing = Mathf.Min(_utterance.Count, Mathf.Max(0, _quietHops - trailingHopsKept) * _hopLength);
        _utterance.RemoveRange(_utterance.Count - trailing, trailing);
        _quietHops = 0;
        if (_utterance.Count >= minimumDuration * _sampleRate)
        {
            float[] audio = Prepare(_utterance);
            if (saveUtterances) SaveWav(audio);
            _pending.Enqueue(audio);
            while (_pending.Count > MaxPendingUtterances)
                _pending.Dequeue();
        }

        _utterance.Clear();
    }

    private float[] Prepare(List<float> samples)
    {
        float[] audio = Resample(samples);
        float peak = 0f;
        foreach (float sample in audio)
            peak = Mathf.Max(peak, Mathf.Abs(sample));
        if (peak <= 0f) return audio;
        float gain = Mathf.Min(targetPeak / peak, maxGain);
        for (int i = 0; i < audio.Length; i++)
            audio[i] *= gain;
        return audio;
    }

    private void SaveWav(float[] audio)
    {
        string folder = Path.Combine(Application.persistentDataPath, "VoiceDebug");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"utterance_{++_savedCount:000}.wav");
        using (BinaryWriter writer = new(File.Create(path)))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + audio.Length * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(WhisperSampleRate);
            writer.Write(WhisperSampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(audio.Length * 2);
            foreach (float sample in audio)
                writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(sample * 32767f), -32768, 32767));
        }

        Debug.Log($"VoiceRecognition: saved {path}");
    }

    private float[] Resample(List<float> samples)
    {
        if (_sampleRate == WhisperSampleRate) return samples.ToArray();
        int length = (int)((long)samples.Count * WhisperSampleRate / _sampleRate);
        float step = (float)_sampleRate / WhisperSampleRate;
        float[] output = new float[length];
        for (int i = 0; i < length; i++)
        {
            float position = i * step;
            int index = (int)position;
            float fraction = position - index;
            float a = samples[index];
            float b = index + 1 < samples.Count ? samples[index + 1] : a;
            output[i] = a + (b - a) * fraction;
        }

        return output;
    }

    private static float Rms(List<float> samples)
    {
        float sum = 0f;
        foreach (float sample in samples)
            sum += sample * sample;
        return Mathf.Sqrt(sum / samples.Count);
    }

    private static string[] SplitWords(string text)
    {
        List<string> words = new();
        StringBuilder current = new();
        foreach (char character in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character) || character == '\'')
            {
                current.Append(character);
                continue;
            }

            if (current.Length == 0) continue;
            words.Add(current.ToString());
            current.Clear();
        }

        if (current.Length > 0) words.Add(current.ToString());
        return words.ToArray();
    }
}
