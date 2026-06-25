namespace Sonar;

/// <summary>
/// Полупрозрачная плашка поверх всех окон, следующая за курсором мыши.
/// Показывает текущее состояние: запись, распознавание, ошибка команды.
/// Не захватывает фокус и скрыта из Alt+Tab.
/// </summary>
public sealed class OverlayForm : Form
{
    private readonly Label _label;
    private readonly System.Windows.Forms.Timer _posTimer;
    private readonly System.Windows.Forms.Timer _errorTimer;
    private bool _isShowingError;

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
            AutoSize  = false,
        };
        Controls.Add(_label);

        _posTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _posTimer.Tick += (_, _) => FollowCursor();

        _errorTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _errorTimer.Tick += (_, _) =>
        {
            _errorTimer.Stop();
            _isShowingError = false;
            Size = new Size(168, 28);
            _posTimer.Stop();
            Hide();
        };

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
        ClearError();
        _label.Text      = "● Запись…";
        _label.ForeColor = Color.FromArgb(255, 90, 90);
        FollowCursor();
        if (!Visible) { Show(); _posTimer.Start(); }
    }

    public void ShowRecognizing()
    {
        ClearError();
        _label.Text      = "◌ Распознавание…";
        _label.ForeColor = Color.FromArgb(255, 170, 0);
        if (!Visible) { FollowCursor(); Show(); _posTimer.Start(); }
    }

    public void ShowCommandRecording()
    {
        ClearError();
        _label.Text      = "⚡ Команда…";
        _label.ForeColor = Color.FromArgb(80, 220, 80);
        FollowCursor();
        if (!Visible) { Show(); _posTimer.Start(); }
    }

    public void ShowCommandExecuting()
    {
        ClearError();
        _label.Text      = "⚙ Выполнение…";
        _label.ForeColor = Color.FromArgb(0, 210, 200);
        if (!Visible) { FollowCursor(); Show(); _posTimer.Start(); }
    }

    public void ShowError(string message)
    {
        _isShowingError = true;
        _label.Text      = message;
        _label.ForeColor = Color.FromArgb(255, 80, 80);
        Size             = new Size(220, 28);
        FollowCursor();
        if (!Visible) { Show(); _posTimer.Start(); }
        _errorTimer.Stop();
        _errorTimer.Start();
    }

    public void ShowCommandError(string commandText)
    {
        _isShowingError = true;
        string truncated = commandText.Length > 50 ? commandText[..50] + "…" : commandText;
        _label.Text      = $"✗ Команда не найдена\n«{truncated}»";
        _label.ForeColor = Color.FromArgb(255, 80, 80);
        Size             = new Size(260, 44);
        FollowCursor();
        if (!Visible) { Show(); _posTimer.Start(); }
        _errorTimer.Stop();
        _errorTimer.Start();
    }

    public void HideOverlay()
    {
        if (_isShowingError) return; // ошибка сама скроется через _errorTimer
        if (!Visible) return;
        _posTimer.Stop();
        Hide();
    }

    private void ClearError()
    {
        if (!_isShowingError) return;
        _isShowingError = false;
        _errorTimer.Stop();
        Size = new Size(168, 28);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _posTimer.Dispose();
            _errorTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
