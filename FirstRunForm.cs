using NAudio.Wave;

namespace Sonar;

/// <summary>
/// Диалог первого запуска: выбор микрофона.
/// Предупреждает, если модель GigaAM v3 не найдена.
/// </summary>
public sealed class FirstRunForm : Form
{
    public int SelectedMicDevice { get; private set; } = -1;

    private readonly ComboBox _cbMic;

    public FirstRunForm()
    {
        Text            = "Sonar — Первый запуск";
        ClientSize      = new Size(440, 260);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        BackColor       = SystemColors.Window;

        int m = 20, y = m;

        Add(new Label
        {
            Text     = "Добро пожаловать в Sonar",
            Font     = new Font("Segoe UI", 14, FontStyle.Bold),
            Location = new Point(m, y),
            Size     = new Size(400, 30),
        });
        y += 38;

        Add(new Label
        {
            Text      = "Офлайн голосовой ввод текста  ·  GigaAM v3",
            Font      = new Font("Segoe UI", 9),
            ForeColor = Color.DimGray,
            Location  = new Point(m, y),
            Size      = new Size(400, 20),
        });
        y += 30;

        bool modelOk = File.Exists(AppConfig.GigaAmV3Model) && File.Exists(AppConfig.GigaAmV3Vocab);
        if (!modelOk)
        {
            Add(new Label
            {
                Text      = "⚠  Файл giga-am-v3.onnx не найден рядом с Sonar.exe.\r\n" +
                            "   Поместите модель в папку с программой перед запуском.",
                Font      = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(180, 60, 0),
                Location  = new Point(m, y),
                Size      = new Size(400, 36),
            });
            y += 44;
        }

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
            Size          = new Size(400, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font          = new Font("Segoe UI", 9),
        });
        FillMicrophones();
        y += 44;

        var btnOk = Add(new Button
        {
            Text      = "Начать работу",
            Location  = new Point((ClientSize.Width - 180) / 2, y),
            Size      = new Size(180, 36),
            Font      = new Font("Segoe UI", 10, FontStyle.Bold),
            BackColor = Color.FromArgb(30, 120, 255),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        });
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += (_, _) =>
        {
            SelectedMicDevice = _cbMic.SelectedIndex - 1;
            DialogResult      = DialogResult.OK;
        };
        AcceptButton = btnOk;
    }

    private void FillMicrophones()
    {
        _cbMic.Items.Add("Системный по умолчанию");
        for (int i = 0; i < WaveIn.DeviceCount; i++)
            _cbMic.Items.Add(WaveIn.GetCapabilities(i).ProductName);
        _cbMic.SelectedIndex = 0;
    }

    private T Add<T>(T ctrl) where T : Control { Controls.Add(ctrl); return ctrl; }
}
