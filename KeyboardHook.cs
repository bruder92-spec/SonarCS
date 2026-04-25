using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VoiceTyper;

/// <summary>
/// Глобальный перехват нажатий через WH_KEYBOARD_LL.
/// Хук устанавливается на выделенном потоке с собственным message loop —
/// это обязательное условие для WH_KEYBOARD_LL (коллбэк вызывается через PostMessage
/// в поток установки, который должен крутить GetMessage).
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private const int  WH_KEYBOARD_LL = 13;
    private const int  WM_KEYDOWN     = 0x0100;
    private const int  WM_KEYUP       = 0x0101;
    private const int  WM_SYSKEYDOWN  = 0x0104;
    private const int  WM_SYSKEYUP    = 0x0105;
    private const uint WM_QUIT        = 0x0012;

    private readonly int _vk;
    private readonly LowLevelKeyboardProc _proc;   // держим от GC
    private IntPtr _hook;
    private bool   _isDown;
    private int    _win32ThreadId;

    public event Action? Pressed;
    public event Action? Released;

    public KeyboardHook(int vkCode)
    {
        _vk   = vkCode;
        _proc = Callback;

        Exception? threadEx = null;
        var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                _win32ThreadId = GetCurrentThreadId();
                // Для WH_KEYBOARD_LL параметр hModule игнорируется Windows — передаём IntPtr.Zero.
                _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);
                if (_hook == IntPtr.Zero)
                    threadEx = new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось установить хук клавиатуры");
            }
            catch (Exception ex) { threadEx = ex; }
            finally { ready.Set(); }

            if (threadEx is not null) return;

            // Собственный message loop — без него коллбэк не вызывается
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
        })
        { IsBackground = true };
        thread.Start();
        ready.Wait();   // ждём, пока хук установлен (или упал)
        if (threadEx is not null) throw threadEx;
    }

    private IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            int  vk   = Marshal.ReadInt32(lParam);
            bool down = wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN;
            bool up   = wParam == WM_KEYUP   || wParam == WM_SYSKEYUP;

            if (vk == _vk)
            {
                if (down && !_isDown) { _isDown = true;  Pressed?.Invoke(); }
                if (up   &&  _isDown) { _isDown = false; Released?.Invoke(); }
                return (IntPtr)1;   // подавляем клавишу
            }
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_win32ThreadId != 0)
        {
            PostThreadMessage(_win32ThreadId, WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
            _win32ThreadId = 0;
        }
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr  hwnd;
        public uint    message;
        public UIntPtr wParam;
        public IntPtr  lParam;
        public int     time;
        public int     ptX, ptY;
    }

    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, LowLevelKeyboardProc fn, IntPtr hmod, uint tid);
    [DllImport("user32.dll")]   static extern bool   UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")]   static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")]   static extern int    GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")]   static extern bool   TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")]   static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")]   static extern bool   PostThreadMessage(int tid, uint msg, UIntPtr wp, IntPtr lp);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string name);
    [DllImport("kernel32.dll")] static extern int    GetCurrentThreadId();
}
