namespace Sonar;

/// <summary>
/// Окно «О программе»: версия, описание, кнопка открытия NOTICES.txt.
/// </summary>
public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text            = "Sonar — О программе";
        ClientSize      = new Size(400, 280);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        BackColor       = SystemColors.Window;

        int m = 28, y = 28;

        Add(new Label
        {
            Text     = "Sonar",
            Font     = new Font("Segoe UI", 18, FontStyle.Bold),
            Location = new Point(m, y),
            Size     = new Size(344, 38),
        });
        y += 42;

        Add(new Label
        {
            Text      = $"Версия {AppConfig.Version}",
            Font      = new Font("Segoe UI", 10),
            ForeColor = Color.DimGray,
            Location  = new Point(m, y),
            Size      = new Size(344, 22),
        });
        y += 26;

        Add(new Label
        {
            Text      = "Офлайн голосовой ввод текста для Windows",
            Font      = new Font("Segoe UI", 9),
            ForeColor = Color.DimGray,
            Location  = new Point(m, y),
            Size      = new Size(344, 20),
        });
        y += 22;

        Add(new Label
        {
            Text      = "© 2026 Varzakov Stanislav",
            Font      = new Font("Segoe UI", 9),
            ForeColor = Color.DimGray,
            Location  = new Point(m, y),
            Size      = new Size(344, 20),
        });
        y += 24;

        Add(new Label
        {
            BorderStyle = BorderStyle.Fixed3D,
            Location    = new Point(m, y),
            Size        = new Size(344, 2),
        });
        y += 16;

        Add(new Label
        {
            Text      = "Движок: GigaAM v3  ·  Отраслевые словари замен",
            Font      = new Font("Segoe UI", 9),
            ForeColor = Color.DimGray,
            Location  = new Point(m, y),
            Size      = new Size(344, 20),
        });
        y += 24;

        var btnNotices = Add(new Button
        {
            Text      = "Лицензии сторонних компонентов (NOTICES.txt)",
            Location  = new Point(m, y),
            Size      = new Size(344, 28),
            Font      = new Font("Segoe UI", 9),
            FlatStyle = FlatStyle.System,
        });
        btnNotices.Click += BtnNotices_Click;
        y += 44;

        var btnClose = Add(new Button
        {
            Text         = "Закрыть",
            Location     = new Point((ClientSize.Width - 100) / 2, y),
            Size         = new Size(100, 32),
            Font         = new Font("Segoe UI", 10),
            FlatStyle    = FlatStyle.System,
            DialogResult = DialogResult.OK,
        });
        AcceptButton  = btnClose;
        CancelButton  = btnClose;
    }

    private void BtnNotices_Click(object? sender, EventArgs e)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "NOTICES.txt");
        if (File.Exists(path))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
                { UseShellExecute = true });
        else
            MessageBox.Show("Файл NOTICES.txt не найден рядом с Sonar.exe",
                "Файл не найден", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private T Add<T>(T ctrl) where T : Control { Controls.Add(ctrl); return ctrl; }
}
