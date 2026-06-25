using NAudio.Wave;

namespace Sonar;

/// <summary>
/// Окно настроек Sonar.
/// Секции: микрофон, горячая клавиша, автозапуск, словари замен.
/// «Применить» сохраняет sonar.json и перезапускает процесс.
/// Перехват горячей клавиши — через ProcessCmdKey (WM_KEYDOWN / WM_SYSKEYDOWN).
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly AppConfig _cfg;

    private readonly ComboBox _cbMic;
    private readonly Button   _btnHotkey;
    private readonly Label    _lblHotkeyHint;
    private readonly CheckBox _chkAutoStart;
    private readonly CheckBox _chkOilGas;
    private readonly CheckBox _chkLegal;
    private readonly CheckBox _chkEconomy;
    private readonly CheckBox _chkCommands;
    private readonly TextBox  _txtTriggerWord;

    private bool _capturing;
    private int  _capturedVk;

    public SettingsForm(AppConfig cfg)
    {
        _cfg        = cfg;
        _capturedVk = cfg.HotkeyVk;

        Text            = "Sonar — Настройки";
        ClientSize      = new Size(460, 600);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        BackColor       = SystemColors.Window;
        KeyPreview      = true;

        int m = 20, y = m;

        // ── Микрофон ──────────────────────────────────────────────────────────
        Add(Header("Микрофон", ref y));

        _cbMic = Add(new ComboBox
        {
            Location      = new Point(m, y),
            Size          = new Size(420, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font          = new Font("Segoe UI", 9),
        });
        FillMicrophones();
        y += 40;

        Add(Separator(ref y));

        // ── Горячая клавиша ───────────────────────────────────────────────────
        Add(Header("Горячая клавиша", ref y));

        _btnHotkey = Add(new Button
        {
            Text      = VkName(_capturedVk),
            Location  = new Point(m, y),
            Size      = new Size(200, 32),
            Font      = new Font("Segoe UI", 10),
            FlatStyle = FlatStyle.System,
        });
        _btnHotkey.Click += BtnHotkey_Click;
        y += 36;

        _lblHotkeyHint = Add(new Label
        {
            Text      = "Нажмите кнопку выше, затем нажмите нужную клавишу",
            Font      = new Font("Segoe UI", 9),
            ForeColor = Color.DimGray,
            Location  = new Point(m, y),
            Size      = new Size(420, 18),
        });
        y += 22;

        Add(new Label
        {
            Text      = "Нельзя назначить: Shift, Ctrl",
            Font      = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(180, 60, 60),
            Location  = new Point(m, y),
            Size      = new Size(420, 18),
        });
        y += 30;

        Add(Separator(ref y));

        // ── Автозапуск ────────────────────────────────────────────────────────
        _chkAutoStart = Add(new CheckBox
        {
            Text     = "Запускать автоматически при входе в Windows",
            Font     = new Font("Segoe UI", 10),
            Checked  = AutoStartManager.IsEnabled,
            Location = new Point(m, y),
            Size     = new Size(420, 22),
        });
        y += 30;

        Add(Separator(ref y));

        // ── Словари замен ─────────────────────────────────────────────────────
        Add(Header("Словари замен", ref y));

        _chkOilGas = Add(MakeDictCheckBox(
            "Нефтегазовый  (ЭЦН, ГРП, НДПИ…)",
            AppConfig.DictOilGasFile, cfg.DictOilGas, m, y));
        y += 28;

        _chkLegal = Add(MakeDictCheckBox(
            "Юридический  (ГК, УК, ЕГРЮЛ…)",
            AppConfig.DictLegalFile, cfg.DictLegal, m, y));
        y += 28;

        _chkEconomy = Add(MakeDictCheckBox(
            "Экономика / Финансы / Бухгалтерия  (НДС, ФОТ, МРОТ…)",
            AppConfig.DictEconomyFile, cfg.DictEconomy, m, y));
        y += 28;

        Add(Separator(ref y));

        // ── Голосовые команды ─────────────────────────────────────────────────
        Add(Header("Голосовые команды", ref y));

        _chkCommands = Add(new CheckBox
        {
            Text     = "Включить управление компьютером голосом",
            Font     = new Font("Segoe UI", 10),
            Checked  = cfg.CommandsEnabled,
            Location = new Point(m, y),
            Size     = new Size(420, 22),
        });
        y += 28;

        Add(new Label
        {
            Text      = "Слово-триггер:",
            Font      = new Font("Segoe UI", 9),
            ForeColor = Color.DimGray,
            Location  = new Point(m, y),
            Size      = new Size(130, 18),
        });
        _txtTriggerWord = Add(new TextBox
        {
            Text     = cfg.TriggerWord,
            Font     = new Font("Segoe UI", 10),
            Location = new Point(m + 135, y - 2),
            Size     = new Size(150, 24),
        });
        y += 32;

        Add(new Label
        {
            Text      = "Скажите «компьютер, открой браузер» — Sonar выполнит команду.\n" +
                        "Примеры: «запусти хром», «выключи компьютер», «сделай тише».",
            Font      = new Font("Segoe UI", 9, FontStyle.Italic),
            ForeColor = Color.DimGray,
            Location  = new Point(m, y),
            Size      = new Size(420, 34),
        });
        y += 38;

        // ── Кнопки ────────────────────────────────────────────────────────────
        var btnApply = new Button
        {
            Text      = "Применить и перезапустить",
            Location  = new Point(m, ClientSize.Height - 56),
            Size      = new Size(230, 36),
            Font      = new Font("Segoe UI", 10, FontStyle.Bold),
            BackColor = Color.FromArgb(30, 120, 255),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        btnApply.FlatAppearance.BorderSize = 0;
        btnApply.Click += BtnApply_Click;
        Controls.Add(btnApply);
        AcceptButton = btnApply;

        var btnCancel = new Button
        {
            Text      = "Отмена",
            Location  = new Point(m + 240, ClientSize.Height - 56),
            Size      = new Size(100, 36),
            Font      = new Font("Segoe UI", 10),
            FlatStyle = FlatStyle.System,
        };
        btnCancel.Click += (_, _) => Close();
        Controls.Add(btnCancel);
        CancelButton = btnCancel;
    }

    // ── Захват горячей клавиши ────────────────────────────────────────────────
    private void BtnHotkey_Click(object? sender, EventArgs e)
    {
        _capturing           = true;
        _btnHotkey.Text      = "Нажмите клавишу…";
        _lblHotkeyHint.Text  = "Esc — отмена";
        _btnHotkey.BackColor = Color.FromArgb(255, 240, 180);
        ActiveControl        = null;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        const int WM_KEYDOWN    = 0x0100;
        const int WM_SYSKEYDOWN = 0x0104;

        if (_capturing && (msg.Msg == WM_KEYDOWN || msg.Msg == WM_SYSKEYDOWN))
        {
            int vk = (int)msg.WParam;

            if (vk == 0x1B)
            {
                FinishCapture(_cfg.HotkeyVk);
                return true;
            }

            if (vk == 0x12)
            {
                bool extended = ((int)msg.LParam & 0x01000000) != 0;
                FinishCapture(extended ? 0xA5 : 0xA4);
                return true;
            }

            if (vk != 0x10 && vk != 0x11)
            {
                FinishCapture(vk);
                return true;
            }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void FinishCapture(int vk)
    {
        _capturing           = false;
        _capturedVk          = vk;
        _btnHotkey.Text      = VkName(vk);
        _btnHotkey.BackColor = SystemColors.Control;
        _lblHotkeyHint.Text  = "Нажмите кнопку выше, затем нажмите нужную клавишу";
    }

    // ── Применить ─────────────────────────────────────────────────────────────
    private void BtnApply_Click(object? sender, EventArgs e)
    {
        _cfg.MicrophoneDevice = _cbMic.SelectedIndex - 1;
        _cfg.HotkeyVk         = _capturedVk;
        _cfg.DictOilGas       = _chkOilGas.Checked;
        _cfg.DictLegal        = _chkLegal.Checked;
        _cfg.DictEconomy      = _chkEconomy.Checked;
        _cfg.CommandsEnabled  = _chkCommands.Checked;
        _cfg.TriggerWord      = string.IsNullOrWhiteSpace(_txtTriggerWord.Text)
                                ? "компьютер" : _txtTriggerWord.Text.Trim().ToLower();
        _cfg.Save();

        if (_chkAutoStart.Checked) AutoStartManager.Enable();
        else                       AutoStartManager.Disable();

        var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (exe is not null) System.Diagnostics.Process.Start(exe);
        Application.Exit();
    }

    // ── Микрофоны ─────────────────────────────────────────────────────────────
    private void FillMicrophones()
    {
        _cbMic.Items.Add("Системный по умолчанию");
        for (int i = 0; i < WaveIn.DeviceCount; i++)
            _cbMic.Items.Add(WaveIn.GetCapabilities(i).ProductName);

        int sel = _cfg.MicrophoneDevice + 1;
        _cbMic.SelectedIndex = sel >= 0 && sel < _cbMic.Items.Count ? sel : 0;
    }

    // ── VK → имя ─────────────────────────────────────────────────────────────
    internal static string VkName(int vk) => vk switch
    {
        0xA0 => "Shift (левый)",
        0xA1 => "Shift (правый)",
        0xA2 => "Ctrl (левый)",
        0xA3 => "Ctrl (правый)",
        0xA4 => "Alt (левый)",
        0xA5 => "Alt (правый)",
        0x70 => "F1",  0x71 => "F2",  0x72 => "F3",  0x73 => "F4",
        0x74 => "F5",  0x75 => "F6",  0x76 => "F7",  0x77 => "F8",
        0x78 => "F9",  0x79 => "F10", 0x7A => "F11", 0x7B => "F12",
        _    => ((Keys)vk).ToString(),
    };

    // ── Вспомогательные методы построения UI ─────────────────────────────────
    private T Add<T>(T ctrl) where T : Control { Controls.Add(ctrl); return ctrl; }

    private static CheckBox MakeDictCheckBox(string label, string fullPath, bool cfgValue, int m, int y)
    {
        bool exists = File.Exists(fullPath);
        return new CheckBox
        {
            Text      = exists ? label : $"{label}  (файл не найден)",
            Font      = new Font("Segoe UI", 10),
            Checked   = cfgValue && exists,
            Enabled   = exists,
            ForeColor = exists ? SystemColors.ControlText : Color.DimGray,
            Location  = new Point(m, y),
            Size      = new Size(420, 22),
        };
    }

    private Label Header(string text, ref int y)
    {
        var l = new Label
        {
            Text     = text,
            Font     = new Font("Segoe UI", 11, FontStyle.Bold),
            Location = new Point(20, y),
            Size     = new Size(420, 24),
        };
        y += 28;
        return l;
    }

    private Label Separator(ref int y)
    {
        var l = new Label
        {
            BorderStyle = BorderStyle.Fixed3D,
            Location    = new Point(20, y),
            Size        = new Size(420, 2),
        };
        y += 14;
        return l;
    }
}
