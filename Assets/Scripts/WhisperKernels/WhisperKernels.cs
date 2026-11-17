using Unity.Burst;
using Unity.Mathematics;

[BurstCompile(FloatMode = FloatMode.Fast)]
public static unsafe class WhisperKernels
{
    public static void Linear(float[] input, int rowStart, int rowEnd, int inputSize, float[] weights, float[] bias,
        int outputStart, int outputEnd, int outputSize, float[] output)
    {
        fixed (float* inputPointer = input)
        fixed (float* weightPointer = weights)
        fixed (float* biasPointer = bias)
        fixed (float* outputPointer = output)
        {
            LinearBurst(inputPointer, rowStart, rowEnd, inputSize, weightPointer, biasPointer, outputStart,
                outputEnd, outputSize, outputPointer);
        }
    }

    public static void MatVec(float[] weights, float[] input, int inputOffset, int size, int start, int end,
        float[] output)
    {
        fixed (float* weightPointer = weights)
        fixed (float* inputPointer = input)
        fixed (float* outputPointer = output)
        {
            MatVecBurst(weightPointer, inputPointer + inputOffset, size, start, end, outputPointer);
        }
    }

    public static void Conv(float[] input, int inputRows, int inputSize, float[] weights, float[] bias,
        int outputSize, int stride, int rowStart, int rowEnd, float[] output)
    {
        fixed (float* inputPointer = input)
        fixed (float* weightPointer = weights)
        fixed (float* biasPointer = bias)
        fixed (float* outputPointer = output)
        {
            ConvBurst(inputPointer, inputRows, inputSize, weightPointer, biasPointer, outputSize, stride, rowStart,
                rowEnd, outputPointer);
        }
    }

    public static void Attention(float[] q, float[] k, float[] v, float[] scores, int limit, int state,
        int headSize, int headOffset, int queryStart, float scale, float[] output)
    {
        fixed (float* qPointer = q)
        fixed (float* kPointer = k)
        fixed (float* vPointer = v)
        fixed (float* scorePointer = scores)
        fixed (float* outputPointer = output)
        {
            AttentionBurst(qPointer, kPointer, vPointer, scorePointer, limit, state, headSize, headOffset,
                queryStart, scale, outputPointer);
        }
    }

    public static void Gelu(float[] data, int start, int end)
    {
        fixed (float* dataPointer = data)
        {
            GeluBurst(dataPointer, start, end);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast)]
    public static void LinearBurst(float* input, int rowStart, int rowEnd, int inputSize, float* weights,
        float* bias, int outputStart, int outputEnd, int outputSize, float* output)
    {
        for (int o = outputStart; o < outputEnd; o++)
        {
            float* weightRow = weights + (long)o * inputSize;
            float offset = bias != null ? bias[o] : 0f;
            int t = rowStart;
            for (; t + 3 < rowEnd; t += 4)
            {
                float* row0 = input + (long)t * inputSize;
                float* row1 = row0 + inputSize;
                float* row2 = row1 + inputSize;
                float* row3 = row2 + inputSize;
                float sum0 = 0f;
                float sum1 = 0f;
                float sum2 = 0f;
                float sum3 = 0f;
                for (int i = 0; i < inputSize; i++)
                {
                    float weight = weightRow[i];
                    sum0 += weight * row0[i];
                    sum1 += weight * row1[i];
                    sum2 += weight * row2[i];
                    sum3 += weight * row3[i];
                }

                output[(long)t * outputSize + o] = offset + sum0;
                output[(long)(t + 1) * outputSize + o] = offset + sum1;
                output[(long)(t + 2) * outputSize + o] = offset + sum2;
                output[(long)(t + 3) * outputSize + o] = offset + sum3;
            }

            for (; t < rowEnd; t++)
                output[(long)t * outputSize + o] = offset + Dot(weightRow, input + (long)t * inputSize, inputSize);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast)]
    public static void MatVecBurst(float* weights, float* input, int size, int start, int end, float* output)
    {
        for (int row = start; row < end; row++)
            output[row] = Dot(weights + (long)row * size, input, size);
    }

    [BurstCompile(FloatMode = FloatMode.Fast)]
    public static void ConvBurst(float* input, int inputRows, int inputSize, float* weights, float* bias,
        int outputSize, int stride, int rowStart, int rowEnd, float* output)
    {
        for (int o = 0; o < outputSize; o++)
        for (int t = rowStart; t < rowEnd; t++)
        {
            float sum = bias[o];
            for (int k = 0; k < 3; k++)
            {
                int row = t * stride + k - 1;
                if (row < 0 || row >= inputRows) continue;
                sum += Dot(weights + (long)(o * 3 + k) * inputSize, input + (long)row * inputSize, inputSize);
            }

            output[(long)t * outputSize + o] = sum;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast)]
    public static void AttentionBurst(float* q, float* k, float* v, float* scores, int limit, int state,
        int headSize, int headOffset, int queryStart, float scale, float* output)
    {
        float* query = q + queryStart;
        float max = float.NegativeInfinity;
        for (int s = 0; s < limit; s++)
        {
            float score = Dot(query, k + (long)s * state + headOffset, headSize) * scale;
            scores[s] = score;
            max = math.max(max, score);
        }

        float sum = 0f;
        for (int s = 0; s < limit; s++)
        {
            float weight = math.exp(scores[s] - max);
            scores[s] = weight;
            sum += weight;
        }

        float* result = output + queryStart;
        for (int j = 0; j < headSize; j++)
            result[j] = 0f;
        float inverse = 1f / sum;
        for (int s = 0; s < limit; s++)
        {
            float weight = scores[s] * inverse;
            float* value = v + (long)s * state + headOffset;
            for (int j = 0; j < headSize; j++)
                result[j] += weight * value[j];
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast)]
    public static void GeluBurst(float* data, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            float value = data[i];
            data[i] = 0.5f * value * (1f + math.tanh(0.7978846f * (value + 0.044715f * value * value * value)));
        }
    }

    private static float Dot(float* a, float* b, int length)
    {
        float sum = 0f;
        for (int i = 0; i < length; i++)
            sum += a[i] * b[i];
        return sum;
    }
}
