using System.Windows.Forms;

namespace Sonar;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Единственный экземпляр. WaitOne до 2 сек — даём время предыдущему
        // процессу завершиться при перезапуске из «Применить и перезапустить».
        using var mutex = new Mutex(false, "Sonar_SingleInstance_1A2B3C");
        if (!mutex.WaitOne(TimeSpan.FromSeconds(2)))
        {
            MessageBox.Show("Sonar уже запущен.", "Sonar",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.ThreadException += (_, e) =>
            Logger.Error("Необработанное исключение UI-потока", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.Error("Необработанное исключение фонового потока",
                e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));

        Logger.Info("══════════════════════════════════════════════");
        Logger.Info("Запуск Sonar");
        Logger.Info($"ОС: {Environment.OSVersion.VersionString} {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");
        Logger.Info($".NET: {Environment.Version}");
        Logger.Info($"Путь: {AppContext.BaseDirectory}");
        Logger.Info($"GigaAM: модель={File.Exists(AppConfig.GigaAmV3Model)}, vocab={File.Exists(AppConfig.GigaAmV3Vocab)}");

        Application.Run(new TrayApp());

        Logger.Info("Завершение");
    }
}
