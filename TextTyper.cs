using System.Runtime.InteropServices;

namespace Sonar;

/// <summary>
/// Вставляет текст в активное окно через буфер обмена + Ctrl+V.
///
/// Алгоритм:
///   1. Сохраняем полный IDataObject из буфера обмена (текст, картинки, файлы — всё)
///   2. Копируем текст в буфер
///   3. Симулируем Ctrl+V через keybd_event
///   4. Восстанавливаем сохранённый буфер обмена
///
/// Clipboard.* требует STA-поток — создаём явно.
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
        Logger.Info($"TextTyper: вставка {text.Length} симв.");
        var sta = new Thread(() =>
        {
            IDataObject? prev = null;
            try
            {
                // Сохраняем всё содержимое буфера (текст, картинки, файлы)
                try { prev = Clipboard.GetDataObject(); } catch { }

                Clipboard.SetText(text);
                Thread.Sleep(80);

                // Ctrl+V
                keybd_event(VK_CONTROL, 0, 0,            UIntPtr.Zero);
                keybd_event(VK_V,       0, 0,            UIntPtr.Zero);
                keybd_event(VK_V,       0, KEYEVENTF_UP, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_UP, UIntPtr.Zero);

                Thread.Sleep(200);
                Logger.Info("TextTyper: Ctrl+V отправлен");
            }
            catch (Exception ex)
            {
                Logger.Error("TextTyper: ошибка вставки", ex);
            }
            finally
            {
                // Восстанавливаем буфер обмена в исходное состояние
                try
                {
                    if (prev is not null) Clipboard.SetDataObject(prev, copy: true);
                    else                 Clipboard.Clear();
                }
                catch { }
            }
        });

        sta.SetApartmentState(ApartmentState.STA);
        sta.IsBackground = true;
        sta.Start();
        sta.Join(millisecondsTimeout: 2000);
        Logger.Info("TextTyper: поток завершён");
    }
}
