using System.Drawing;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace VoiceTyper;

/// <summary>
/// Основной контекст приложения v2.
///
/// Цвета иконки:
///   Синий  (#1E78FF) — готов
///   Красный (#DC2828) — запись
///   Оранжевый (#DC7800) — распознавание
///   Серый  — загрузка
///   Фиолет — ошибка
/// </summary>
public sealed class TrayApp : ApplicationContext
{
    private static readonly Color CLR_LOADING     = Color.Gray;
    private static readonly Color CLR_READY       = Color.FromArgb(30,  120, 255);
    private static readonly Color CLR_RECORDING   = Color.FromArgb(220,  40,  40);
    private static readonly Color CLR_RECOGNIZING = Color.FromArgb(220, 120,   0);
    private static readonly Color CLR_ERROR       = Color.FromArgb(180,   0, 180);

    private readonly NotifyIcon _tray;
    private AppConfig?     _config;
    private VoskEngine?    _vosk;
    private WhisperEngine? _whisper;
    private AudioCapture?  _audio;
    private KeyboardHook?  _hook;
    private bool           _settingsOpen;

    private enum State { Loading, Ready, Recording, Recognizing, Error }
    private volatile State _state = State.Loading;
    private readonly object _lock = new();

    public TrayApp()
    {
        _tray = new NotifyIcon
        {
            Visible          = true,
            Icon             = MakeIcon(CLR_LOADING),
            Text             = "Voice Typer: Загрузка…",
            ContextMenuStrip = BuildMenu(engineLabel: "…", micLabel: "…"),
        };

        _ = Task.Run(StartupAsync);
    }

    // ── запуск ────────────────────────────────────────────────────────────────
    private async Task StartupAsync()
    {
        try
        {
            _config = AppConfig.TryLoad();
            if (_config is null)
            {
                _config = await ShowFirstRunDialogAsync();
                if (_config is null) { Shutdown(); return; }
                _config.Save();
            }

            SetState(State.Loading, "Загрузка модели…");

            if (_config.Engine == "whisper")
            {
                if (!File.Exists(AppConfig.WhisperModel))
                    throw new FileNotFoundException("ggml-small.bin не найден", AppConfig.WhisperModel);
                _whisper = new WhisperEngine(AppConfig.WhisperModel);
                await Task.Run(_whisper.Load);
            }
            else
            {
                if (!Directory.Exists(AppConfig.VoskModelDir))
                    throw new DirectoryNotFoundException($"Папка модели не найдена: {AppConfig.VoskModelDir}");
                _vosk = new VoskEngine(AppConfig.VoskModelDir);
                await Task.Run(_vosk.Load);
            }

            _audio = new AudioCapture(sampleRate: 16_000)
            {
                DeviceNumber = _config.MicrophoneDevice,
            };

            _hook          = new KeyboardHook(vkCode: _config.HotkeyVk);
            _hook.Pressed  += OnKeyDown;
            _hook.Released += OnKeyUp;

            string engineLabel = GetEngineLabel();
            string micLabel    = GetMicName(_config.MicrophoneDevice);
            _tray.ContextMenuStrip = BuildMenu(engineLabel, micLabel);

            SetState(State.Ready, $"Готов  [{engineLabel}]  [{micLabel}]");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[init] {ex}");
            SetState(State.Error, "Ошибка инициализации");
            MessageBox.Show(ex.Message, "Voice Typer — Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ── метка движка ──────────────────────────────────────────────────────────
    private string GetEngineLabel()
    {
        if (_config?.Engine == "whisper")
            return "Whisper Small" + (_whisper?.IsGpu == true ? " [GPU]" : " [CPU]");
        return "VOSK";
    }

    // ── диалог первого запуска ────────────────────────────────────────────────
    private Task<AppConfig?> ShowFirstRunDialogAsync()
    {
        var tcs = new TaskCompletionSource<AppConfig?>();
        var sta = new Thread(() =>
        {
            Application.EnableVisualStyles();
            using var form = new FirstRunForm();
            if (form.ShowDialog() == DialogResult.OK)
                tcs.SetResult(new AppConfig
                {
                    Engine           = form.SelectedEngine,
                    MicrophoneDevice = form.SelectedMicDevice,
                });
            else
                tcs.SetResult(null);
        });
        sta.SetApartmentState(ApartmentState.STA);
        sta.IsBackground = true;
        sta.Start();
        return tcs.Task;
    }

    // ── иконка / состояние ────────────────────────────────────────────────────
    private void SetState(State s, string? tip = null)
    {
        _state = s;
        Color color = s switch
        {
            State.Ready       => CLR_READY,
            State.Recording   => CLR_RECORDING,
            State.Recognizing => CLR_RECOGNIZING,
            State.Error       => CLR_ERROR,
            _                 => CLR_LOADING,
        };
        var old = _tray.Icon;
        _tray.Icon = MakeIcon(color);
        if (tip is not null)
        {
            var text = $"Voice Typer: {tip}";
            _tray.Text = text.Length > 63 ? text[..63] : text;
        }
        old?.Dispose();
    }

    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    private static Icon MakeIcon(Color c)
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var b = new SolidBrush(c);
            g.FillEllipse(b, 1, 1, 13, 13);
        }
        var h = bmp.GetHicon();
        try   { return (Icon)Icon.FromHandle(h).Clone(); }
        finally { DestroyIcon(h); }
    }

    // ── меню трея ─────────────────────────────────────────────────────────────
    private ContextMenuStrip BuildMenu(string engineLabel, string micLabel)
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add($"Voice Typer  [{engineLabel}]").Enabled = false;
        menu.Items.Add(new ToolStripSeparator());

        var micItem = new ToolStripMenuItem($"Микрофон: {micLabel}");
        BuildMicSubmenu(micItem);
        menu.Items.Add(micItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Настройки…", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выйти", null, (_, _) => Shutdown());

        return menu;
    }

    private void BuildMicSubmenu(ToolStripMenuItem parent)
    {
        int current = _config?.MicrophoneDevice ?? -1;

        var def = new ToolStripMenuItem("Системный по умолчанию") { Checked = current < 0 };
        def.Click += (_, _) => SelectMic(-1);
        parent.DropDownItems.Add(def);
        parent.DropDownItems.Add(new ToolStripSeparator());

        foreach (var (idx, name) in AudioCapture.GetDevices())
        {
            var i    = idx;
            var item = new ToolStripMenuItem(name) { Checked = current == i };
            item.Click += (_, _) => SelectMic(i);
            parent.DropDownItems.Add(item);
        }
    }

    private void SelectMic(int deviceIndex)
    {
        if (_config is null || _audio is null) return;
        _config.MicrophoneDevice = deviceIndex;
        _config.Save();
        _audio.DeviceNumber = deviceIndex;
        _tray.ContextMenuStrip = BuildMenu(GetEngineLabel(), GetMicName(deviceIndex));
    }

    private static string GetMicName(int device)
    {
        if (device < 0) return "По умолчанию";
        return AudioCapture.GetDevices().FirstOrDefault(d => d.Index == device).Name
               ?? $"Устройство {device}";
    }

    // ── окно настроек ─────────────────────────────────────────────────────────
    private void OpenSettings()
    {
        if (_config is null || _settingsOpen) return;
        _settingsOpen = true;
        var sta = new Thread(() =>
        {
            Application.EnableVisualStyles();
            using var form = new SettingsForm(_config);
            form.ShowDialog();
            _settingsOpen = false;
        });
        sta.SetApartmentState(ApartmentState.STA);
        sta.IsBackground = true;
        sta.Start();
    }

    // ── обработка клавиши ─────────────────────────────────────────────────────
    private void OnKeyDown()
    {
        lock (_lock)
        {
            if (_state != State.Ready) return;
            _state = State.Recording;
        }
        try
        {
            _audio!.StartRecording();
            SetState(State.Recording, "Запись…");
        }
        catch (Exception ex)
        {
            lock (_lock) { _state = State.Ready; }
            SetState(State.Error, "Ошибка микрофона");
            _tray.BalloonTipTitle = "Voice Typer — Ошибка микрофона";
            _tray.BalloonTipText  = ex.Message;
            _tray.BalloonTipIcon  = ToolTipIcon.Error;
            _tray.ShowBalloonTip(4000);
            // через 4 сек возвращаемся в Ready
            _ = Task.Delay(4000).ContinueWith(_ => SetState(State.Ready, "Готов  [" + GetEngineLabel() + "]"));
        }
    }

    private void OnKeyUp()
    {
        byte[] pcm;
        lock (_lock)
        {
            if (_state != State.Recording) return;
            _state = State.Recognizing;
            pcm = _audio!.StopRecording();
        }
        SetState(State.Recognizing, "Распознавание…");
        _ = RecognizeAsync(pcm);
    }

    private async Task RecognizeAsync(byte[] pcm)
    {
        string engLabel = GetEngineLabel();
        try
        {
            if (pcm.Length > 0)
            {
                string text = _config?.Engine == "whisper"
                    ? await _whisper!.TranscribeAsync(pcm)
                    : await Task.Run(() => _vosk!.Transcribe(pcm));

                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (_config?.PostProcess == true)
                        text = TextProcessor.Process(text);

                    Console.WriteLine($"[{engLabel}] {text}");
                    TextTyper.Type(text + " ");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[recognition] {ex}");
            _tray.BalloonTipTitle = "Voice Typer — Ошибка";
            _tray.BalloonTipText  = ex.Message;
            _tray.BalloonTipIcon  = ToolTipIcon.Error;
            _tray.ShowBalloonTip(5000);
        }
        finally
        {
            SetState(State.Ready, $"Готов  [{engLabel}]");
        }
    }

    // ── завершение ────────────────────────────────────────────────────────────
    private void Shutdown()
    {
        _hook?.Dispose();
        _audio?.Dispose();
        _whisper?.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Shutdown();
        base.Dispose(disposing);
    }
}
