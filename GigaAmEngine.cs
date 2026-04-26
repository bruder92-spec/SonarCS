using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace VoiceTyper;

/// <summary>
/// GigaAM v3 — прямой ONNX-инференс без sherpa-onnx прослойки.
/// Модель: istupakov/gigaam-v3-onnx → v3_e2e_ctc.int8.onnx (225 МБ, WER ≈ 3.3%).
/// E2E-модель: выдаёт текст с пунктуацией, заглавными буквами и цифрами.
///
/// Preprocessing: n_fft=320, hop=160, 64 mel-фильтра HTK, center=false,
/// периодическое окно Ханна, ln(max(e, 1e-9)), per-feature normalization.
/// DFT реализован вручную: n_fft=320 не является степенью 2.
///
/// Vocab (257 классов): загружается из giga-am-v3-vocab.txt.
///   ▁ = пробел, &lt;blk&gt; = CTC blank (индекс 256).
/// </summary>
public sealed class GigaAmEngine : IDisposable
{
    private InferenceSession? _session;
    private string[]?         _vocab;    // index → token string
    private int               _nVocab;
    private int               _blankId;

    private const int SampleRate = 16_000;
    private const int NFft       = 320;
    private const int HopLength  = 160;
    private const int NMel       = 64;

    private static readonly float[]  s_hann;
    private static readonly float[,] s_melFb;
    private static readonly float[,] s_twCos;   // [NFft/2+1, NFft]
    private static readonly float[,] s_twSin;

    static GigaAmEngine()
    {
        s_hann  = MakeHann(NFft);
        s_melFb = MakeMelFilterbank(NMel, NFft, SampleRate);

        int half = NFft / 2 + 1;
        s_twCos = new float[half, NFft];
        s_twSin = new float[half, NFft];
        for (int k = 0; k < half; k++)
            for (int n = 0; n < NFft; n++)
            {
                double a = -2.0 * Math.PI * k * n / NFft;
                s_twCos[k, n] = (float)Math.Cos(a);
                s_twSin[k, n] = (float)Math.Sin(a);
            }
    }

    public void Load(string modelPath, string vocabPath)
    {
        (_vocab, _nVocab, _blankId) = LoadVocab(vocabPath);
        var opts = new SessionOptions { ExecutionMode = ExecutionMode.ORT_SEQUENTIAL };
        _session = new InferenceSession(modelPath, opts);
    }

    /// <param name="pcm16le">PCM 16 кГц, 16 бит, моно, little-endian</param>
    public string Transcribe(byte[] pcm16le)
    {
        if (_session is null) throw new InvalidOperationException("Модель не загружена");

        float[] samples = PcmToFloat(pcm16le);
        if (samples.Length < NFft) return "";

        float[,] mel = ComputeMel(samples);
        int T = mel.GetLength(1);
        if (T == 0) return "";

        var feat = new DenseTensor<float>(new[] { 1, NMel, T });
        for (int m = 0; m < NMel; m++)
            for (int t = 0; t < T; t++)
                feat[0, m, t] = mel[m, t];

        var len = new DenseTensor<long>(new[] { 1 });
        len[0] = T;

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("features",        feat),
            NamedOnnxValue.CreateFromTensor("feature_lengths", len),
        };

        using var results = _session.Run(inputs);
        float[] lp = results[0].AsEnumerable<float>().ToArray();
        int T2 = lp.Length / _nVocab;
        return CtcGreedyDecode(lp, T2);
    }

    // ── загрузка словаря из файла ─────────────────────────────────────────────
    // Формат: «токен индекс» (по одной паре на строку).
    private static (string[] vocab, int nVocab, int blankId) LoadVocab(string path)
    {
        var lines = File.ReadAllLines(path, System.Text.Encoding.UTF8);
        int maxId = 0;
        var pairs = new List<(int id, string token)>(lines.Length);
        foreach (var line in lines)
        {
            var s = line.AsSpan().Trim();
            if (s.IsEmpty) continue;
            int sp = s.LastIndexOf(' ');
            if (sp < 0) continue;
            if (!int.TryParse(s[(sp + 1)..], out int id)) continue;
            string token = s[..sp].ToString();
            pairs.Add((id, token));
            if (id > maxId) maxId = id;
        }

        var vocab  = new string[maxId + 1];
        int blankId = maxId;   // fallback: last index
        foreach (var (id, token) in pairs)
        {
            // ▁ (U+2581) — разделитель слов → нормализуем в обычный пробел
            vocab[id] = token.Length == 1 && token[0] == '▁' ? " " : token;
            if (token == "<blk>") blankId = id;
        }
        return (vocab, maxId + 1, blankId);
    }

    // ── PCM int16 → float32 [-1, 1] ──────────────────────────────────────────
    private static float[] PcmToFloat(byte[] pcm)
    {
        var f = new float[pcm.Length / 2];
        for (int i = 0; i < f.Length; i++)
            f[i] = BitConverter.ToInt16(pcm, i * 2) / 32768f;
        return f;
    }

    // ── Mel-спектрограмма с per-feature нормализацией ─────────────────────────
    private static float[,] ComputeMel(float[] samples)
    {
        int nFrames = (samples.Length - NFft) / HopLength + 1;
        if (nFrames <= 0) return new float[NMel, 0];

        int half      = NFft / 2 + 1;
        var mel       = new float[NMel, nFrames];
        var power     = new float[half];
        var windowed  = new float[NFft];

        for (int frame = 0; frame < nFrames; frame++)
        {
            int offset = frame * HopLength;
            for (int n = 0; n < NFft; n++)
                windowed[n] = samples[offset + n] * s_hann[n];

            for (int k = 0; k < half; k++)
            {
                float re = 0, im = 0;
                for (int n = 0; n < NFft; n++)
                {
                    float x = windowed[n];
                    re += x * s_twCos[k, n];
                    im += x * s_twSin[k, n];
                }
                power[k] = re * re + im * im;
            }

            for (int m = 0; m < NMel; m++)
            {
                float e = 0;
                for (int k = 0; k < half; k++)
                    e += s_melFb[m, k] * power[k];
                mel[m, frame] = MathF.Log(MathF.Max(e, 1e-9f));
            }
        }

        // per-feature normalization (NeMo: normalize=per_feature)
        for (int m = 0; m < NMel; m++)
        {
            float sum = 0;
            for (int t = 0; t < nFrames; t++) sum += mel[m, t];
            float mean = sum / nFrames;

            float sq = 0;
            for (int t = 0; t < nFrames; t++) { float d = mel[m, t] - mean; sq += d * d; }
            float std    = MathF.Sqrt(sq / nFrames);
            float invStd = 1f / MathF.Max(std, 1e-5f);

            for (int t = 0; t < nFrames; t++)
                mel[m, t] = (mel[m, t] - mean) * invStd;
        }

        return mel;
    }

    // ── CTC жадное декодирование ──────────────────────────────────────────────
    private string CtcGreedyDecode(float[] logProbs, int T)
    {
        var sb   = new System.Text.StringBuilder();
        int prev = -1;

        for (int t = 0; t < T; t++)
        {
            int   best    = 0;
            float bestVal = logProbs[t * _nVocab];
            for (int c = 1; c < _nVocab; c++)
            {
                float v = logProbs[t * _nVocab + c];
                if (v > bestVal) { bestVal = v; best = c; }
            }
            if (best != _blankId && best != prev)
            {
                string token = _vocab![best] ?? "";
                // пробел уже нормализован в LoadVocab (▁→" "); пропускаем только служебные <tokens>
                if (token.Length > 0 && token[0] != '<')
                    sb.Append(token);
            }
            prev = best;
        }

        // страховка: если ▁ всё же просочился — заменяем на пробел
        return sb.ToString().Replace('▁', ' ').Trim();
    }

    // ── периодическое окно Ханна ──────────────────────────────────────────────
    private static float[] MakeHann(int n)
    {
        var w = new float[n];
        for (int i = 0; i < n; i++)
            w[i] = 0.5f - 0.5f * MathF.Cos(2f * MathF.PI * i / n);
        return w;
    }

    // ── Mel-фильтрбанк (HTK, без нормализации) ────────────────────────────────
    private static float[,] MakeMelFilterbank(int nMel, int nFft, int sr)
    {
        int    half   = nFft / 2 + 1;
        double melMin = FreqToMel(0);
        double melMax = FreqToMel(sr / 2.0);

        var pts = new double[nMel + 2];
        for (int i = 0; i < nMel + 2; i++)
            pts[i] = melMin + (melMax - melMin) * i / (nMel + 1);

        var fb = new float[nMel, half];
        for (int m = 0; m < nMel; m++)
        {
            double fL = MelToFreq(pts[m]);
            double fC = MelToFreq(pts[m + 1]);
            double fR = MelToFreq(pts[m + 2]);
            for (int k = 0; k < half; k++)
            {
                double f = (double)k * sr / nFft;
                float  w;
                if      (f <= fL || f >= fR) w = 0f;
                else if (f <  fC)            w = (float)((f - fL) / (fC - fL));
                else                         w = (float)((fR - f) / (fR - fC));
                fb[m, k] = w;
            }
        }
        return fb;
    }

    private static double FreqToMel(double f)   => 2595.0 * Math.Log10(1.0 + f / 700.0);
    private static double MelToFreq(double mel)  => 700.0 * (Math.Pow(10.0, mel / 2595.0) - 1.0);

    public void Dispose() => _session?.Dispose();
}
