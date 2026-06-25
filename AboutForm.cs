namespace Sonar;

/// <summary>
/// Окно «О программе» с двумя вкладками: общая информация и справка по работе с программой.
/// </summary>
public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text            = "Sonar — О программе";
        ClientSize      = new Size(560, 596);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        BackColor       = SystemColors.Window;

        var tabs = new TabControl
        {
            Location = new Point(8, 8),
            Size     = new Size(544, 536),
            Font     = new Font("Segoe UI", 9),
        };
        Controls.Add(tabs);

        var pageAbout = new TabPage("О программе");
        tabs.TabPages.Add(pageAbout);
        FillAbout(pageAbout);

        var pageHelp = new TabPage("Справка");
        tabs.TabPages.Add(pageHelp);
        FillHelp(pageHelp);

        var btnClose = new Button
        {
            Text         = "Закрыть",
            Location     = new Point((560 - 100) / 2, 554),
            Size         = new Size(100, 32),
            Font         = new Font("Segoe UI", 10),
            FlatStyle    = FlatStyle.System,
            DialogResult = DialogResult.OK,
        };
        Controls.Add(btnClose);
        AcceptButton = btnClose;
        CancelButton = btnClose;
    }

    private static void FillAbout(TabPage tab)
    {
        int m = 16, y = 16;
        void Add(Control ctrl) => tab.Controls.Add(ctrl);

        Add(new Label
        {
            Text     = "Sonar",
            Font     = new Font("Segoe UI", 18, FontStyle.Bold),
            Location = new Point(m, y),
            Size     = new Size(510, 38),
        });
        y += 42;

        Add(new Label
        {
            Text      = $"Версия {AppConfig.Version}",
            Font      = new Font("Segoe UI", 10),
            ForeColor = Color.DimGray,
            Location  = new Point(m, y),
            Size      = new Size(510, 22),
        });
        y += 26;

        Add(new Label
        {
            Text      = "Офлайн голосовой ввод текста для Windows",
            Font      = new Font("Segoe UI", 9),
            ForeColor = Color.DimGray,
            Location  = new Point(m, y),
            Size      = new Size(510, 20),
        });
        y += 22;

        Add(new Label
        {
            Text      = "© 2026 Varzakov Stanislav",
            Font      = new Font("Segoe UI", 9),
            ForeColor = Color.DimGray,
            Location  = new Point(m, y),
            Size      = new Size(510, 20),
        });
        y += 24;

        Add(new Label
        {
            BorderStyle = BorderStyle.Fixed3D,
            Location    = new Point(m, y),
            Size        = new Size(510, 2),
        });
        y += 16;

        Add(new Label
        {
            Text      = "Движок: GigaAM v3 (ONNX, int8, офлайн, русский)",
            Font      = new Font("Segoe UI", 9),
            ForeColor = Color.DimGray,
            Location  = new Point(m, y),
            Size      = new Size(510, 20),
        });
        y += 22;

        Add(new Label
        {
            Text      = "Словари замен: нефтегазовый, юридический, экономический",
            Font      = new Font("Segoe UI", 9),
            ForeColor = Color.DimGray,
            Location  = new Point(m, y),
            Size      = new Size(510, 20),
        });
        y += 22;

        Add(new Label
        {
            Text      = "Голосовые команды Windows: 100+ действий, без сети",
            Font      = new Font("Segoe UI", 9),
            ForeColor = Color.DimGray,
            Location  = new Point(m, y),
            Size      = new Size(510, 20),
        });
        y += 32;

        var btnNotices = new Button
        {
            Text      = "Лицензии сторонних компонентов (NOTICES.txt)",
            Location  = new Point(m, y),
            Size      = new Size(510, 28),
            Font      = new Font("Segoe UI", 9),
            FlatStyle = FlatStyle.System,
        };
        btnNotices.Click += (_, _) =>
        {
            string path = Path.Combine(AppContext.BaseDirectory, "NOTICES.txt");
            if (File.Exists(path))
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            else
                MessageBox.Show("Файл NOTICES.txt не найден рядом с Sonar.exe",
                    "Файл не найден", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
        Add(btnNotices);
    }

    private static void FillHelp(TabPage tab)
    {
        var rtb = new RichTextBox
        {
            Dock        = DockStyle.Fill,
            ReadOnly    = true,
            BackColor   = SystemColors.Window,
            BorderStyle = BorderStyle.None,
            Font        = new Font("Segoe UI", 9),
            ScrollBars  = RichTextBoxScrollBars.Vertical,
        };
        tab.Controls.Add(rtb);
        FillHelpContent(rtb);
        rtb.SelectionStart = 0;
    }

    private static void FillHelpContent(RichTextBox rtb)
    {
        bool first = true;

        void Section(string title)
        {
            if (!first) rtb.AppendText("\n");
            first = false;
            int s = rtb.TextLength;
            rtb.AppendText(title + "\n");
            rtb.Select(s, title.Length);
            rtb.SelectionFont  = new Font("Segoe UI", 10, FontStyle.Bold);
            rtb.SelectionColor = Color.FromArgb(30, 80, 180);
        }

        void Item(string text)
        {
            int s = rtb.TextLength;
            rtb.AppendText("  • " + text + "\n");
            rtb.Select(s, 4 + text.Length);
            rtb.SelectionFont  = new Font("Segoe UI", 9);
            rtb.SelectionColor = SystemColors.ControlText;
        }

        void Note(string text)
        {
            int s = rtb.TextLength;
            rtb.AppendText("    ↳ " + text + "\n");
            rtb.Select(s, 6 + text.Length);
            rtb.SelectionFont  = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            rtb.SelectionColor = Color.DimGray;
        }

        Section("Голосовой ввод текста");
        Item("Нажмите и удерживайте хоткей (по умолчанию: левый Alt)");
        Item("Говорите — запись идёт пока хоткей зажат");
        Item("Отпустите хоткей — текст появится там, где стоит курсор");
        Item("Работает полностью офлайн, данные не покидают ПК");
        Item("Нажмите хоткей снова во время распознавания — фраза встанет в очередь");

        Section("Режим голосовых команд");
        Item("Включите в меню трея → Настройки → вкладка Команды");
        Item("Произнесите слово-триггер (по умолчанию «компьютер»), затем команду");
        Note("компьютер, запусти браузер  — открывает браузер");
        Note("компьютер, открой документы  — открывает папку Документы");
        Note("компьютер, сделай тише  — уменьшает громкость на 10%");
        Note("компьютер, закрыть окно  — закрывает активное окно");
        Note("компьютер, выключить компьютер  — завершение работы");
        Item("Любое приложение из меню Пуск запускается по имени (нечёткий поиск)");
        Note("компьютер, запусти фотошоп  — найдёт Adobe Photoshop и откроет");
        Note("компьютер, запусти ворд  — найдёт Microsoft Word и откроет");
        Item("Полный список команд: меню трея → Список команд");

        Section("Пользовательские команды");
        Item("Файл user/commands_user.txt рядом с Sonar.exe");
        Item("Формат: фраза = цель  (URL, путь к папке, файлу или программе)");
        Note("открой рабочую почту = https://mail.company.ru");
        Note("рабочая папка = C:\\Projects\\2026");
        Note("открой инструкцию = D:\\Documents\\Инструкция.pdf");
        Item("Пользовательские команды перекрывают встроенные");
        Item("После изменения файла перезапустите Sonar");

        Section("Словари замен");
        Item("Нефтегазовый, юридический, экономический — включаются в Настройках");
        Item("Пользовательский: user/dictionary_user.txt рядом с Sonar.exe");
        Note("Формат строки: исходная фраза = замена");
        Item("Словари применяются после распознавания, с проверкой границ слов");

        Section("Иконка в трее — цвета состояний");
        Item("Серая — загрузка модели");
        Item("Синяя — готов к работе");
        Item("Красная — идёт запись голосового ввода");
        Item("Оранжевая — распознавание речи");
        Item("Зелёная — идёт запись голосовой команды");
        Item("Бирюзовая — выполнение команды");
        Item("Фиолетовая — ошибка");

        Section("Настройки");
        Item("Хоткей — кнопка активации записи голоса");
        Item("Микрофон — выбор устройства записи");
        Item("Словари — включение отраслевых словарей замен");
        Item("Команды — включение режима голосовых команд");
        Item("Слово-триггер — ключевое слово для входа в командный режим");
        Item("Автозапуск — запуск Sonar вместе с Windows");

        Section("Файлы программы");
        Item("sonar.json — настройки программы (создаётся автоматически)");
        Item("sonar.log — журнал работы (ротируется при >1 МБ)");
        Item("user/commands_user.txt — пользовательские голосовые команды");
        Item("user/dictionary_user.txt — пользовательский словарь замен слов");
        Item("user/dictionary_oil_gas.txt — нефтегазовый словарь замен");
        Item("user/dictionary_legal.txt — юридический словарь замен");
        Item("user/dictionary_economy.txt — экономический словарь замен");
    }
}
