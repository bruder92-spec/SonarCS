namespace Sonar;

/// <summary>
/// Окно со списком всех доступных голосовых команд.
/// Открывается командой «покажи команды».
/// </summary>
public sealed class CommandsForm : Form
{
    public CommandsForm()
    {
        Text            = "Sonar — Голосовые команды";
        ClientSize      = new Size(560, 640);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        BackColor       = SystemColors.Window;

        var hint = new Label
        {
            Text      = "Скажите слово-триггер, затем одну из команд ниже.",
            Font      = new Font("Segoe UI", 9, FontStyle.Italic),
            ForeColor = Color.DimGray,
            Location  = new Point(12, 10),
            Size      = new Size(536, 18),
        };
        Controls.Add(hint);

        var rtb = new RichTextBox
        {
            Location   = new Point(12, 34),
            Size       = new Size(536, 560),
            ReadOnly   = true,
            BackColor  = SystemColors.Window,
            BorderStyle = BorderStyle.None,
            Font       = new Font("Segoe UI", 9),
            ScrollBars = RichTextBoxScrollBars.Vertical,
        };
        Controls.Add(rtb);

        FillCommands(rtb);
        rtb.SelectionStart = 0;
    }

    private static void FillCommands(RichTextBox rtb)
    {
        // Раздел "Приложения" берётся из IntentMatcher.AppNames — всегда актуален
        var appItems = IntentMatcher.AppNames.Select(a => $"запусти {a}").ToArray();

        var sections = new (string Title, string[] Items)[]
        {
            ("Приложения", appItems),
            ("Папки", [
                "открой документы",
                "открой загрузки",
                "открой рабочий стол",
                "открой изображения / фото",
                "открой музыку / видео",
                "открой корзину",
            ]),
            ("Управление окнами", [
                "свернуть все / сверни все",
                "восстановить все",
                "свернуть окно / сверни окно",
                "развернуть окно / разверни окно",
                "закрыть окно / закрой окно",
                "прикрепить влево / вправо",
                "на весь экран",
                "следующий монитор / предыдущий монитор",
                "переключить окно",
            ]),
            ("Виртуальные рабочие столы", [
                "новый рабочий стол",
                "закрыть рабочий стол",
                "следующий рабочий стол",
                "предыдущий рабочий стол",
                "обзор задач",
            ]),
            ("Звук и медиа", [
                "сделай громче / сделай тише",
                "выключить звук / включить звук",
                "громкость 50  (0–100)",
                "следующий трек / предыдущий трек",
                "поставь на паузу / воспроизведи",
            ]),
            ("Скриншоты и запись", [
                "скриншот",
                "скриншот окна",
                "скриншот области",
                "запись экрана / остановить запись",
                "игровая панель",
            ]),
            ("Питание и система", [
                "выключить компьютер",
                "перезагрузить компьютер",
                "заблокировать экран",
                "спящий режим",
                "гибернация",
                "выключить монитор",
                "диспетчер задач",
                "панель управления",
                "параметры windows",
            ]),
            ("Настройки Windows", [
                "настройки дисплея / экрана",
                "настройки звука",
                "настройки сети / интернета",
                "настройки bluetooth",
                "центр обновлений",
                "настройки питания",
                "настройки аккаунта",
            ]),
            ("Клавиатурные действия", [
                "выделить всё",
                "скопировать / вырезать / вставить",
                "отменить действие / повторить действие",
                "сохранить / сохранить как",
                "печать / поиск в файле",
                "удалить / переименовать",
                "новый документ",
                "жирный / курсив / подчёркивание",
                "прокрутить вверх / вниз",
                "в начало страницы / в конец страницы",
            ]),
            ("Браузер", [
                "новая вкладка / закрыть вкладку",
                "открыть закрытую вкладку",
                "следующая вкладка / предыдущая вкладка",
                "инкогнито",
                "обновить страницу",
                "вернуться назад / перейти вперёд",
                "адресная строка",
                "история браузера / загрузки браузера",
                "инструменты разработчика",
                "увеличить масштаб / уменьшить масштаб",
                "полный экран",
            ]),
            ("Утилиты", [
                "ножницы",
                "эмодзи",
                "стикеры",
                "экранная клавиатура",
                "таблица символов",
                "история буфера обмена",
                "очистить буфер обмена",
                "caps lock / капс лок",
                "диктор",
                "поиск",
                "выполнить",
                "меню пуск",
                "центр уведомлений / быстрые настройки",
            ]),
            ("Набор текста", [
                "напечатай [текст]",
                "напиши [текст]",
                "введи [текст]",
                "набери [текст]",
            ]),
            ("Справка", [
                "покажи команды",
                "список команд",
                "что ты умеешь",
            ]),
        };

        bool first = true;
        foreach (var (title, items) in sections)
        {
            if (!first) rtb.AppendText("\n");
            first = false;

            int start = rtb.TextLength;
            rtb.AppendText(title + "\n");
            rtb.Select(start, title.Length);
            rtb.SelectionFont  = new Font("Segoe UI", 10, FontStyle.Bold);
            rtb.SelectionColor = Color.FromArgb(30, 80, 180);

            foreach (var item in items)
            {
                int s = rtb.TextLength;
                rtb.AppendText("  • " + item + "\n");
                rtb.Select(s, 4 + item.Length);
                rtb.SelectionFont  = new Font("Segoe UI", 9);
                rtb.SelectionColor = SystemColors.ControlText;
            }
        }
    }
}
