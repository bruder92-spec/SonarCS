using System.Runtime.InteropServices;

namespace VoiceTyper;

/// <summary>
/// Вставляет текст в активное окно через буфер обмена + Ctrl+V.
///
/// Алгоритм:
///   1. Сохраняем старое содержимое буфера обмена
///   2. Копируем текст в буфер
///   3. Симулируем Ctrl+V через keybd_event
///   4. Восстанавливаем буфер обмена
///
/// Clipboard.* требует STA-поток — создаём явно.
/// keybd_event проще SendInput и работает из любого потока.
/// </summary>
public static class TextTyper
{
    private const byte VK_CONTROL   = 0x11;
    private const byte VK_V         = 0x56;
    private const uint KEYEVENTF_UP = 0x0002;

    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtra);

    public static void Type(string text)
    {
        var sta = new Thread(() =>
        {
            string? prev = null;
            try
            {
                if (Clipboard.ContainsText()) prev = Clipboard.GetText();
                Clipboard.SetText(text);
                Thread.Sleep(80);

                // Ctrl+V
                keybd_event(VK_CONTROL, 0, 0,            UIntPtr.Zero);
                keybd_event(VK_V,       0, 0,            UIntPtr.Zero);
                keybd_event(VK_V,       0, KEYEVENTF_UP, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_UP, UIntPtr.Zero);

                Thread.Sleep(150);
            }
            finally
            {
                try
                {
                    if (prev is not null) Clipboard.SetText(prev);
                    else                 Clipboard.Clear();
                }
                catch { /* игнорируем если не получилось восстановить */ }
            }
        });

        sta.SetApartmentState(ApartmentState.STA);
        sta.IsBackground = true;
        sta.Start();
        sta.Join(millisecondsTimeout: 2000);
    }
}
