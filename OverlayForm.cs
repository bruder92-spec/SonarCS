namespace Sonar;

/// <summary>
/// Полупрозрачная плашка поверх всех окон, следующая за курсором мыши.
/// Показывает текущее состояние: запись (красный) или распознавание (оранжевый).
/// Не захватывает фокус и скрыта из Alt+Tab.
/// </summary>
public sealed class OverlayForm : Form
{
    private readonly Label _label;
    private readonly System.Windows.Forms.Timer _posTimer;

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition   = FormStartPosition.Manual;
        TopMost         = true;
        ShowInTaskbar   = false;
        Size            = new Size(168, 28);
        BackColor       = Color.FromArgb(28, 28, 28);
        Opacity         = 0.88;

        _label = new Label
        {
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 9),
            BackColor = Color.Transparent,
        };
        Controls.Add(_label);

        _posTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _posTimer.Tick += (_, _) => FollowCursor();

        Visible = false;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE — не захватывает фокус
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW  — скрыт из Alt+Tab
            return cp;
        }
    }

    private void FollowCursor()
    {
        if (!Visible) return;
        var p   = Cursor.Position;
        int x   = p.X + 20;
        int y   = p.Y + 20;
        var scr = Screen.FromPoint(p).WorkingArea;
        if (x + Width  > scr.Right)  x = p.X - Width  - 5;
        if (y + Height > scr.Bottom) y = p.Y - Height - 5;
        Location = new Point(x, y);
    }

    public void ShowRecording()
    {
        _label.Text      = "● Запись…";
        _label.ForeColor = Color.FromArgb(255, 90, 90);
        FollowCursor();
        if (!Visible) { Show(); _posTimer.Start(); }
    }

    public void ShowRecognizing()
    {
        _label.Text      = "◌ Распознавание…";
        _label.ForeColor = Color.FromArgb(255, 170, 0);
        if (!Visible) { FollowCursor(); Show(); _posTimer.Start(); }
        // если уже видима (переход Recording→Recognizing) — просто меняем текст
    }

    public void HideOverlay()
    {
        if (!Visible) return;
        _posTimer.Stop();
        Hide();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _posTimer.Dispose();
        base.Dispose(disposing);
    }
}
