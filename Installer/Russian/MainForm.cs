namespace FF14AccessibilityInstaller.Russian;

public sealed class MainForm : Form
{
    private readonly TextBox journal = new()
    {
        Multiline = true, ReadOnly = true, AcceptsTab = false, ScrollBars = ScrollBars.Vertical,
        WordWrap = true, Dock = DockStyle.Fill, TabIndex = 0, TabStop = true,
        Font = new Font(SystemFonts.MessageBoxFont!.FontFamily, 11),
        AccessibleName = "Журнал установки",
        AccessibleDescription = "Сообщения без времени. Стрелки перемещают по тексту, Control Home и Control End — в начало и конец. Tab — к кнопкам."
    };
    private readonly Button install = MakeButton("Установить / обновить мод", 0);
    private readonly Button russify = MakeButton("Установить русификацию игры", 1);
    private readonly Button russifyMod = MakeButton("Установить русификацию мода", 2);
    private readonly Button check = MakeButton("Проверить обновления", 3);
    private readonly Button removeTranslation = MakeButton("Удалить русификацию игры", 4);
    private readonly Button removeModTranslation = MakeButton("Удалить русификацию мода", 5);
    private readonly Button removeMod = MakeButton("Полностью удалить мод", 6);
    private readonly Button removeBoth = MakeButton("Полностью удалить мод и русификацию игры", 7);
    private readonly Button cancel = MakeButton("Отменить", 8);
    private readonly Button close = MakeButton("Закрыть", 9);
    private readonly InstallerService service;
    private CancellationTokenSource? operation;
    private bool busy;
    private readonly string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FF14AccessibilityInstaller", "logs", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".txt");
    private bool logFailed;
    private bool persistLog = true;

    public MainForm(InstallerService? installer = null)
    {
        service = installer ?? new InstallerService();
        var version = typeof(MainForm).Assembly.GetName().Version?.ToString(3);
        Text = $"FF14 Accessibility — установщик русской версии {version}";
        AccessibleName = Text;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(960, 620);
        MinimumSize = new Size(720, 480);
        StartPosition = FormStartPosition.CenterScreen;
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(8),
            TabIndex = 1, TabStop = false
        };
        panel.Controls.AddRange([install, russify, russifyMod, check, removeTranslation, removeModTranslation, removeMod, removeBoth, cancel, close]);
        cancel.Enabled = false;
        Controls.Add(journal);
        Controls.Add(panel);
        service.Log = message =>
        {
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke(() => Append(message));
            else Append(message);
        };
        service.Ask = question =>
        {
            var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            BeginInvoke(() => answer.SetResult(MessageBox.Show(this, question, "Установка vnavmesh",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes));
            return answer.Task;
        };
        install.Click += async (_, _) => await Run(installing: true);
        russify.Click += async (_, _) => await Run(installing: true, translating: true);
        russifyMod.Click += async (_, _) => await Run(installing: true, translatingMod: true);
        check.Click += async (_, _) => await Run(installing: false);
        removeMod.Click += async (_, _) => await ConfirmRemoval(mod: true, translation: false);
        removeTranslation.Click += async (_, _) => await ConfirmRemoval(mod: false, translation: true);
        removeModTranslation.Click += async (_, _) => await ConfirmRemoval(mod: false, translation: false, modTranslation: true);
        removeBoth.Click += async (_, _) => await ConfirmRemoval(mod: true, translation: true);
        cancel.Click += (_, _) =>
        {
            operation?.Cancel();
            cancel.Enabled = false;
            Append("Запрошена отмена. Если изменение файлов уже началось, сначала будет завершена текущая операция или восстановлены прежние файлы.");
        };
        close.Click += (_, _) => Close();
        FormClosing += (_, e) => { if (busy) { e.Cancel = true; Append("Дождитесь завершения или нажмите «Отменить»."); } };
        Shown += async (_, _) =>
        {
            Append("Перед установкой закройте игру и XIVLauncher. Tab переключает журнал и кнопки; Shift+Tab — назад.");
            Append("Русификация игры переводит текст FFXIV через XIV Rus и Penumbra; нужен английский язык клиента. Русификация мода включает русский язык сообщений и речи.");
            Append("Журнал сохраняется в %LOCALAPPDATA%\\FF14AccessibilityInstaller\\logs. Время сообщений и имя пользователя скрыты.");
            await Run(installing: false);
        };
    }

    private static Button MakeButton(string label, int index) => new()
    {
        Text = label, AccessibleName = label, AutoSize = true, Padding = new Padding(8, 5, 8, 5), TabIndex = index
    };

    private void Append(string message)
    {
        // Preserve the reading position when the user is browsing earlier log lines.
        var start = journal.SelectionStart;
        var length = journal.SelectionLength;
        var reading = journal.Focused && start < journal.TextLength;
        var line = JournalText.Format(message);
        journal.AppendText(line);
        if (!logFailed && persistLog)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, line, new System.Text.UTF8Encoding(true));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logFailed = true;
                journal.AppendText(JournalText.Format("Не удалось сохранить журнал в файл: " + ex.Message));
            }
        }
        if (reading) { journal.Select(start, length); journal.ScrollToCaret(); }
    }

    private async Task ConfirmRemoval(bool mod, bool translation, bool modTranslation = false)
    {
        if (busy) return;
        var question = modTranslation
            ? "Удалить русификацию мода?\r\n\r\nЯзык сообщений и речи станет английским. Мод, личные клавиши, маршруты и перевод игры сохраняются. Закройте игру и XIVLauncher."
            : "Полностью удалить " + (mod && translation ? "мод и русификацию игры" : mod ? "мод" : "русификацию игры") + "?\r\n\r\n" +
            (mod ? "Будут удалены все копии FF14Accessibility, его настройки, личные клавиши, маршруты и резервные копии.\r\n" : "") +
            (translation ? "Будут удалены все установленные XIV Rus, записи перевода в Penumbra, кеш и прежние копии перевода игры.\r\n" : "") +
            "После завершения восстановить удалённые данные нельзя. Другие плагины и моды сохраняются. Закройте игру и XIVLauncher.";
        if (MessageBox.Show(this, question, modTranslation ? "Удаление русификации мода" : "Полное удаление", MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Purging the installer logs must not immediately recreate the same files.
        if (!modTranslation) persistLog = false;
        await Run(installing: true, removingMod: mod, removingTranslation: translation, removingModTranslation: modTranslation);
    }

    private async Task Run(bool installing, bool translating = false, bool removingMod = false, bool removingTranslation = false,
        bool translatingMod = false, bool removingModTranslation = false)
    {
        if (busy) return;
        busy = true;
        var source = removingMod && removingTranslation ? removeBoth : removingMod ? removeMod : removingTranslation ? removeTranslation :
            removingModTranslation ? this.removeModTranslation : translating ? russify : translatingMod ? russifyMod : installing ? install : check;
        var buttons = new[] { install, russify, russifyMod, check, removeMod, removeTranslation, this.removeModTranslation, removeBoth, close };
        foreach (var button in buttons) button.Enabled = false;
        cancel.Enabled = true;
        journal.Focus();
        Append("— " + source.Text + " —");
        using var tokenSource = new CancellationTokenSource();
        operation = tokenSource;
        try
        {
            if (installing)
            {
                var result = await Task.Run(() => removingModTranslation ? Task.FromResult(service.RemoveModTranslation(tokenSource.Token)) :
                    removingMod || removingTranslation ? Task.FromResult(service.Uninstall(removingMod, removingTranslation, tokenSource.Token)) :
                    translating ? service.RussifyGame(tokenSource.Token) : translatingMod ? service.RussifyMod(tokenSource.Token) : service.Install(tokenSource.Token));
                Append(result);
                MessageBox.Show(this, JournalText.Format(result).Trim(), source.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else await Task.Run(() => service.Check(tokenSource.Token));
        }
        catch (OperationCanceledException)
        { Append(tokenSource.IsCancellationRequested ? "Операция отменена." : "Сервер не ответил вовремя. Повторите попытку."); }
        catch (Exception ex)
        {
            Append("Ошибка: " + ex.Message);
            Append("Действие не завершено. После устранения причины повторите его той же кнопкой.");
            MessageBox.Show(this, JournalText.Format(ex.Message).Trim(), "Операция не завершена", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            operation = null;
            busy = false;
            foreach (var button in buttons) button.Enabled = true;
            cancel.Enabled = false;
            source.Focus();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) service.Dispose();
        base.Dispose(disposing);
    }
}
