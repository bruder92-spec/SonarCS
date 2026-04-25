using NAudio.Wave;

namespace VoiceTyper;

/// <summary>
/// Захват звука с выбранного микрофона через NAudio.
/// DeviceNumber = -1 означает системный микрофон по умолчанию.
/// Можно менять на лету — следующее StartRecording() применит новое значение.
/// </summary>
public sealed class AudioCapture : IDisposable
{
    private readonly int          _rate;
    private WaveInEvent?          _waveIn;
    private readonly List<byte[]> _chunks = [];
    private readonly object       _lock   = new();
    private bool                  _active;

    /// <summary>Индекс устройства: -1 = по умолчанию, 0..N = конкретный микрофон.</summary>
    public int DeviceNumber { get; set; } = -1;

    public AudioCapture(int sampleRate) => _rate = sampleRate;

    public void StartRecording()
    {
        lock (_lock) { _chunks.Clear(); _active = true; }

        // DeviceNumber -1 = WAVE_MAPPER (WinMM -1 as UINT = 0xFFFFFFFF).
        // NAudio передаёт значение напрямую в waveInOpen — -1 корректно интерпретируется как системный по умолчанию.
        _waveIn = new WaveInEvent
        {
            DeviceNumber       = DeviceNumber < 0 ? -1 : DeviceNumber,
            WaveFormat         = new WaveFormat(_rate, 16, 1),
            BufferMilliseconds = 100,
        };
        _waveIn.DataAvailable += (_, e) =>
        {
            if (!_active) return;
            var buf = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, buf, 0, e.BytesRecorded);
            lock (_lock) { _chunks.Add(buf); }
        };
        _waveIn.RecordingStopped += (_, _) => { };
        _waveIn.StartRecording();
    }

    public byte[] StopRecording()
    {
        _active = false;
        _waveIn?.StopRecording();
        _waveIn?.Dispose();
        _waveIn = null;

        lock (_lock)
        {
            using var ms = new MemoryStream();
            foreach (var c in _chunks) ms.Write(c, 0, c.Length);
            _chunks.Clear();
            return ms.ToArray();
        }
    }

    /// <summary>Список доступных микрофонов: индекс → название.</summary>
    public static IReadOnlyList<(int Index, string Name)> GetDevices()
    {
        var list = new List<(int, string)>();
        for (int i = 0; i < WaveIn.DeviceCount; i++)
            list.Add((i, WaveIn.GetCapabilities(i).ProductName));
        return list;
    }

    public void Dispose()
    {
        _active = false;
        _waveIn?.Dispose();
        _waveIn = null;
    }
}
