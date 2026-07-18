using System.Reflection;

namespace FF14AccessibilityInstaller;

/// <summary>
/// Barrierefreies Hauptfenster: Titel-Label, ein fokussierbares mehrzeiliges
/// readonly Log-Textfeld, ein Haupt-Button "Installieren / Aktualisieren" und
/// ein "Beenden"-Button. Logische TabIndex-Reihenfolge, alle Controls mit
/// AccessibleName versehen. Wichtige Meldungen (Fertig/Fehler) werden
/// zusätzlich per MessageBox angesagt.
/// </summary>
public sealed class MainForm : Form
{
    private readonly Label _titleLabel;
    private readonly TextBox _logBox;
    private readonly Button _installButton;
    private readonly Button _exitButton;
    private readonly InstallerService _service = new();
    private readonly bool _justUpdated;
    private bool _restarting;

    /// <param name="justUpdated">true, wenn dieser Start aus einem Selbst-Update
    /// des Installers stammt. Dann meldet das Fenster das Update und startet die
    /// Installation ohne weiteren Tastendruck.</param>
    public MainForm(bool justUpdated = false)
    {
        _justUpdated = justUpdated;

        var ownVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? Loc.Get("UnknownVersion");

        Text = Loc.Get("WindowTitle");
        ClientSize = new Size(760, 480);
        MinimumSize = new Size(560, 360);
        StartPosition = FormStartPosition.CenterScreen;

        _titleLabel = new Label
        {
            Text = Loc.Get("MainTitleWithVersion", ownVersion),
            AccessibleName = Loc.Get("MainTitleAccessibleName", ownVersion),
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 32,
            TextAlign = ContentAlignment.MiddleLeft,
            TabStop = false,
            TabIndex = 0,
        };

        _logBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            TabIndex = 1,
            AccessibleName = Loc.Get("LogBoxAccessibleName"),
            AccessibleDescription = Loc.Get("LogBoxAccessibleDescription"),
            Font = new Font(FontFamily.GenericMonospace, 9f),
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8),
        };

        _installButton = new Button
        {
            Text = Loc.Get("InstallButtonText"),
            AccessibleName = Loc.Get("InstallButtonAccessibleName"),
            AutoSize = true,
            Padding = new Padding(12, 6, 12, 6),
            TabIndex = 2,
        };
        _installButton.Click += async (_, _) => await OnInstallClickedAsync();

        _exitButton = new Button
        {
            Text = Loc.Get("ExitButtonText"),
            AccessibleName = Loc.Get("ExitButtonAccessibleName"),
            AutoSize = true,
            Padding = new Padding(12, 6, 12, 6),
            TabIndex = 3,
        };
        _exitButton.Click += (_, _) => Close();

        buttonPanel.Controls.Add(_installButton);
        buttonPanel.Controls.Add(_exitButton);

        Controls.Add(_logBox);
        Controls.Add(buttonPanel);
        Controls.Add(_titleLabel);

        AcceptButton = _installButton;

        _service.LogMessage += OnServiceLogMessage;
        _service.AskYesNo = OnServiceAskYesNo;
        _service.RestartRequested += OnRestartRequested;

        Load += (_, _) => _installButton.Focus();

        // Shown statt Load: der Hinweis-Dialog soll ueber einem bereits sichtbaren
        // Fenster erscheinen, sonst haengt er vor einem noch leeren Bildschirm.
        Shown += async (_, _) =>
        {
            if (!_justUpdated) return;

            // Screenreader lesen den Dialog vor, bevor die Installation loslegt -
            // so weiss der Nutzer, dass der Neustart Absicht war.
            AppendLog(Loc.Get("InstallerUpdatedTo", ownVersion));
            MessageBox.Show(
                Loc.Get("InstallerUpdatedMessage", ownVersion),
                Loc.Get("WindowTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            await OnInstallClickedAsync();
        };
    }

    private void OnRestartRequested()
    {
        // Das Fenster schliessen, damit die neue Instanz die Datei ersetzen kann.
        _restarting = true;
        if (InvokeRequired) BeginInvoke(new Action(Close));
        else Close();
    }

    private void OnServiceLogMessage(string message)
    {
        if (_logBox.InvokeRequired)
        {
            _logBox.BeginInvoke(new Action(() => AppendLog(message)));
        }
        else
        {
            AppendLog(message);
        }
    }

    private void AppendLog(string message)
    {
        _logBox.AppendText(message + Environment.NewLine);
    }

    private bool OnServiceAskYesNo(string question)
    {
        // Läuft bereits auf dem UI-Thread (RunAsync wird ohne ConfigureAwait(false)
        // ausgeführt), daher ist ein direkter MessageBox.Show hier sicher.
        var result = MessageBox.Show(
            question,
            Loc.Get("WindowTitle"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        return result == DialogResult.Yes;
    }

    private async Task OnInstallClickedAsync()
    {
        _installButton.Enabled = false;
        _exitButton.Enabled = false;
        try
        {
            var restarting = await _service.RunAsync();
            if (restarting || _restarting)
                return;   // Selbst-Update laeuft: keine Abschlussmeldung, Fenster schliesst sich.

            MessageBox.Show(
                Loc.Get("OperationCompleted"),
                Loc.Get("WindowTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Loc.Get("UnexpectedError", ex.Message),
                Loc.Get("WindowTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _installButton.Enabled = true;
            _exitButton.Enabled = true;
        }
    }
}
