using NAudio.Wave;

namespace VoiceTyper;

/// <summary>
/// Диалог первого запуска:
///   - выбор движка (VOSK / Whisper / GigaAM v2)
///   - выбор микрофона из всех доступных устройств
///
/// Движок отображается недоступным (серым), если его файл модели не найден.
/// </summary>
public sealed class FirstRunForm : Form
{
    // ── результаты выбора ─────────────────────────────────────────────────────
    public string SelectedEngine     { get; private set; } = "vosk";
    public int    SelectedMicDevice  { get; private set; } = -1;

    // ── контролы ──────────────────────────────────────────────────────────────
    private readonly RadioButton _rbVosk;
    private readonly RadioButton _rbGigaAm3;
    private readonly RadioButton _rbWhisper;
    private readonly Label       _lblVoskInfo;
    private readonly Label       _lblGigaAmInfo;
    private readonly Label       _lblWhisperInfo;
    private readonly ComboBox    _cbMic;
    private readonly Button      _btnOk;

    public FirstRunForm()
    {
        // ── окно ──────────────────────────────────────────────────────────────
        Text            = "Voice Typer — Первый запуск";
        ClientSize      = new Size(480, 440);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        BackColor       = SystemColors.Window;

        int m = 20, y = m;

        // ── заголовок ─────────────────────────────────────────────────────────
        Add(new Label
        {
            Text     = "Выберите движок распознавания речи",
            Font     = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(m, y),
            Size     = new Size(440, 28),
        });
        y += 38;

        // ── VOSK ──────────────────────────────────────────────────────────────
        _rbVosk = Add(new RadioButton
        {
            Text     = "VOSK  —  быстрый, русский",
            Font     = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(m, y),
            Size     = new Size(440, 24),
        });
        y += 26;

        _lblVoskInfo = Add(new Label
        {
            Text      = "  Размер: 88 МБ  •  Задержка: ~0.5 сек  •  Качество: хорошее",
            Font      = new Font("Segoe UI", 9),
            ForeColor = Color.DimGray,
            Location  = new Point(m + 18, y),
            Size      = new Size(440, 18),
        });
        y += 32;

        // ── GigaAM v3 ─────────────────────────────────────────────────────────
        _rbGigaAm3 = Add(new RadioButton
        {
            Text     = "GigaAM v3  —  точнее, русский",
            Font     = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(m, y),
            Size     = new Size(440, 24),
        });
        y += 26;

        _lblGigaAmInfo = Add(new Label
        {
            Text      = "  Размер: 225 МБ  •  Задержка: ~1.5 сек  •  Качество: WER 3.3%",
            Font      = new Font("Segoe UI", 9),
            ForeColor = Color.DimGray,
            Location  = new Point(m + 18, y),
            Size      = new Size(440, 18),
        });
        y += 32;

        // ── Whisper ───────────────────────────────────────────────────────────
        _rbWhisper = Add(new RadioButton
        {
            Text     = "Whisper Small  —  мультиязычный",
            Font     = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(m, y),
            Size     = new Size(440, 24),
        });
        y += 26;

        _lblWhisperInfo = Add(new Label
        {
            Text      = "  Размер: 466 МБ  •  Задержка: ~1.5 сек (CPU)  •  Качество: хорошее",
            Font      = new Font("Segoe UI", 9),
            ForeColor = Color.DimGray,
            Location  = new Point(m + 18, y),
            Size      = new Size(440, 18),
        });
        y += 36;

        // ── разделитель ───────────────────────────────────────────────────────
        Add(new Label
        {
            BorderStyle = BorderStyle.Fixed3D,
            Location    = new Point(m, y),
            Size        = new Size(440, 2),
        });
        y += 14;

        // ── микрофон ──────────────────────────────────────────────────────────
        Add(new Label
        {
            Text     = "Микрофон:",
            Font     = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(m, y),
            Size     = new Size(200, 22),
        });
        y += 26;

        _cbMic = Add(new ComboBox
        {
            Location      = new Point(m, y),
            Size          = new Size(440, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font          = new Font("Segoe UI", 9),
        });
        y += 48;

        // ── кнопка OK ─────────────────────────────────────────────────────────
        _btnOk = Add(new Button
        {
            Text      = "Начать работу",
            Location  = new Point((ClientSize.Width - 180) / 2, y),
            Size      = new Size(180, 36),
            Font      = new Font("Segoe UI", 10, FontStyle.Bold),
            BackColor = Color.FromArgb(30, 120, 255),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        });
        _btnOk.FlatAppearance.BorderSize = 0;
        _btnOk.Click += BtnOk_Click;
        AcceptButton = _btnOk;

        // ── заполнение данных ─────────────────────────────────────────────────
        FillMicrophones();
        CheckModelAvailability();
    }

    // ── вспомогательный метод для добавления контролов ────────────────────────
    private T Add<T>(T ctrl) where T : Control { Controls.Add(ctrl); return ctrl; }

    // ── микрофоны ─────────────────────────────────────────────────────────────
    private void FillMicrophones()
    {
        _cbMic.Items.Add("Системный по умолчанию");
        for (int i = 0; i < WaveIn.DeviceCount; i++)
            _cbMic.Items.Add($"{WaveIn.GetCapabilities(i).ProductName}");
        _cbMic.SelectedIndex = 0;
    }

    // ── проверка наличия моделей ──────────────────────────────────────────────
    private void CheckModelAvailability()
    {
        bool voskOk    = Directory.Exists(AppConfig.VoskModelDir);
        bool gigaamOk  = File.Exists(AppConfig.GigaAmV3Model) && File.Exists(AppConfig.GigaAmV3Vocab);
        bool whisperOk = File.Exists(AppConfig.WhisperModel);

        _rbVosk.Enabled = voskOk;
        if (!voskOk)
        {
            _lblVoskInfo.Text      = "  ⚠  Папка «model» не найдена рядом с VoiceTyper.exe";
            _lblVoskInfo.ForeColor = Color.Red;
        }

        _rbGigaAm3.Enabled = gigaamOk;
        if (!gigaamOk)
        {
            _lblGigaAmInfo.Text      = "  ⚠  Файл «giga-am-v3.onnx» не найден рядом с VoiceTyper.exe";
            _lblGigaAmInfo.ForeColor = Color.Red;
        }

        _rbWhisper.Enabled = whisperOk;
        if (!whisperOk)
        {
            _lblWhisperInfo.Text      = "  ⚠  Файл «ggml-small.bin» не найден рядом с VoiceTyper.exe";
            _lblWhisperInfo.ForeColor = Color.Red;
        }

        // выбор по умолчанию: GigaAM → VOSK → Whisper
        if      (gigaamOk)                       _rbGigaAm3.Checked = true;
        else if (voskOk)                         _rbVosk.Checked    = true;
        else if (whisperOk)                      _rbWhisper.Checked = true;
        else { _btnOk.Enabled = false; Text += "  [ОШИБКА: модели не найдены]"; }
    }

    // ── OK ────────────────────────────────────────────────────────────────────
    private void BtnOk_Click(object? sender, EventArgs e)
    {
        SelectedEngine    = _rbWhisper.Checked ? "whisper" : _rbGigaAm3.Checked ? "gigaam" : "vosk";
        SelectedMicDevice = _cbMic.SelectedIndex - 1;   // 0 → -1 (default), 1 → 0, 2 → 1, …
        DialogResult      = DialogResult.OK;
    }
}
