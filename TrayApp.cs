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
    private AppConfig?         _config;
    private VoskEngine?        _vosk;
    private WhisperEngine?     _whisper;
    private GigaAmEngine?      _gigaam;
    private AudioCapture?      _audio;
    private KeyboardHook?      _hook;
    private bool               _settingsOpen;

    // ── очередь фраз ──────────────────────────────────────────────────────────
    private byte[]? _pendingPcm;        // PCM следующей фразы, ждёт завершения текущего распознавания
    private bool    _capturingForQueue; // идёт запись «в очередь» пока Recognizing

    // ── overlay ───────────────────────────────────────────────────────────────
    private OverlayForm? _overlay;

    private static readonly Image s_gearIcon = MakeGearIcon();

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

        _overlay = new OverlayForm();
        _ = _overlay.Handle; // принудительно создаём HWND на STA-потоке
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
            else if (_config.Engine == "gigaam")
            {
                if (!File.Exists(AppConfig.GigaAmV3Model))
                    throw new FileNotFoundException("giga-am-v3.onnx не найден", AppConfig.GigaAmV3Model);
                if (!File.Exists(AppConfig.GigaAmV3Vocab))
                    throw new FileNotFoundException("giga-am-v3-vocab.txt не найден", AppConfig.GigaAmV3Vocab);
                _gigaam = new GigaAmEngine();
                await Task.Run(() => _gigaam.Load(AppConfig.GigaAmV3Model, AppConfig.GigaAmV3Vocab));
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
    private string GetEngineLabel() => _config?.Engine switch
    {
        "whisper" => "Whisper Small" + (_whisper?.IsGpu == true ? " [GPU]" : " [CPU]"),
        "gigaam"  => "GigaAM v3",
        _         => "VOSK",
    };

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
        UpdateOverlay(s);
    }

    private void UpdateOverlay(State s)
    {
        if (_overlay is null || !_overlay.IsHandleCreated) return;
        _overlay.BeginInvoke(() =>
        {
            switch (s)
            {
                case State.Recording:   _overlay.ShowRecording();   break;
                case State.Recognizing: _overlay.ShowRecognizing(); break;
                default:                _overlay.HideOverlay();     break;
            }
        });
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
        var settingsItem = new ToolStripMenuItem("Настройки…", s_gearIcon, (_, _) => OpenSettings())
            { ImageScaling = ToolStripItemImageScaling.None };
        menu.Items.Add(settingsItem);
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
        bool normalRec, queuedRec;
        lock (_lock)
        {
            normalRec = _state == State.Ready;
            // начать запись «в очередь»: идёт распознавание, очередь пуста, запись не ведётся
            queuedRec = _state == State.Recognizing && !_capturingForQueue && _pendingPcm is null;
            if (!normalRec && !queuedRec) return;
            if (normalRec) _state = State.Recording;
            else           _capturingForQueue = true;
        }
        try
        {
            _audio!.StartRecording();
            if (normalRec) SetState(State.Recording, "Запись…");
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                if (normalRec) _state = State.Ready;
                else           _capturingForQueue = false;
            }
            if (normalRec)
            {
                SetState(State.Error, "Ошибка микрофона");
                _tray.BalloonTipTitle = "Voice Typer — Ошибка микрофона";
                _tray.BalloonTipText  = ex.Message;
                _tray.BalloonTipIcon  = ToolTipIcon.Error;
                _tray.ShowBalloonTip(4000);
                _ = Task.Delay(4000).ContinueWith(_ => SetState(State.Ready, "Готов  [" + GetEngineLabel() + "]"));
            }
        }
    }

    private void OnKeyUp()
    {
        byte[]? launchPcm = null;
        lock (_lock)
        {
            if (_state == State.Recording)
            {
                _state    = State.Recognizing;
                launchPcm = _audio!.StopRecording();
            }
            else if (_capturingForQueue)
            {
                _capturingForQueue = false;
                var queued = _audio!.StopRecording();
                if (_state == State.Recognizing)
                    _pendingPcm = queued; // распознавание ещё идёт — отложить
                else
                {
                    // распознавание уже завершилось (state == Recording, выставленный в RecognizeAsync finally)
                    _state    = State.Recognizing;
                    launchPcm = queued;
                }
            }
            else return;
        }
        if (launchPcm is not null)
        {
            SetState(State.Recognizing, "Распознавание…");
            _ = RecognizeAsync(launchPcm);
        }
    }

    private async Task RecognizeAsync(byte[] pcm)
    {
        string engLabel = GetEngineLabel();
        try
        {
            if (pcm.Length > 0)
            {
                string text = _config?.Engine switch
                {
                    "whisper" => await _whisper!.TranscribeAsync(pcm),
                    "gigaam"  => await Task.Run(() => _gigaam!.Transcribe(pcm)),
                    _         => await Task.Run(() => _vosk!.Transcribe(pcm)),
                };

                if (!string.IsNullOrWhiteSpace(text))
                {
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
            byte[]? pending;
            bool    capturingNow;
            lock (_lock)
            {
                pending      = _pendingPcm;
                _pendingPcm  = null;
                capturingNow = _capturingForQueue;
                if (pending is null && !capturingNow)
                    _state = State.Ready;
                else if (capturingNow)
                    _state = State.Recording; // пользователь записывает следующую фразу
            }
            if (pending is not null)
                _ = RecognizeAsync(pending);
            else if (capturingNow)
                SetState(State.Recording, "Запись…");
            else
                SetState(State.Ready, $"Готов  [{engLabel}]");
        }
    }

    // ── завершение ────────────────────────────────────────────────────────────
    private void Shutdown()
    {
        _hook?.Dispose();
        _audio?.Dispose();
        _whisper?.Dispose();
        _gigaam?.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        Application.Exit();
    }

    // ── иконка шестерёнки 16×16 для пункта «Настройки…» ─────────────────────
    private static Bitmap MakeGearIcon()
    {
        const int S = 16;
        var bmp = new Bitmap(S, S, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        const float Cx = 7.5f, Cy = 7.5f, Ro = 7f, Ri = 4.8f, Rh = 2.1f;
        const int   N  = 8;
        double step = Math.PI * 2 / N;
        double half = step * 0.34;

        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        for (int i = 0; i < N; i++)
        {
            double a    = i * step;
            double a0   = a - half, a1 = a - half * 0.35, a2 = a + half * 0.35, a3 = a + half;
            path.AddLine(
                Cx + Ri * (float)Math.Cos(a0), Cy + Ri * (float)Math.Sin(a0),
                Cx + Ro * (float)Math.Cos(a1), Cy + Ro * (float)Math.Sin(a1));
            path.AddLine(
                Cx + Ro * (float)Math.Cos(a1), Cy + Ro * (float)Math.Sin(a1),
                Cx + Ro * (float)Math.Cos(a2), Cy + Ro * (float)Math.Sin(a2));
            path.AddLine(
                Cx + Ro * (float)Math.Cos(a2), Cy + Ro * (float)Math.Sin(a2),
                Cx + Ri * (float)Math.Cos(a3), Cy + Ri * (float)Math.Sin(a3));
            double a4 = (i + 1) * step - half;
            path.AddArc(Cx - Ri, Cy - Ri, Ri * 2, Ri * 2,
                (float)(a3 * 180 / Math.PI),
                (float)((a4 - a3) * 180 / Math.PI));
        }
        path.CloseFigure();

        using var brush = new SolidBrush(Color.FromArgb(75, 95, 130));
        g.FillPath(brush, path);

        // центральное отверстие — прозрачное
        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        g.FillEllipse(new SolidBrush(Color.Transparent), Cx - Rh, Cy - Rh, Rh * 2, Rh * 2);

        return bmp;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Shutdown();
        base.Dispose(disposing);
    }
}
