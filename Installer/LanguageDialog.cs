namespace FF14AccessibilityInstaller;

/// <summary>
/// Small, screenreader-friendly language chooser shown before the main
/// window. Two buttons ("Deutsch" / "English"), full keyboard support
/// (Tab to move focus, Enter/Space to activate), and an AccessibleName on
/// every control. The previously saved language (if any) gets focus by
/// default; otherwise the system UI language decides the pre-focused button.
/// This dialog is always shown - the saved/detected language only controls
/// which button is pre-focused, not an automatic skip.
/// </summary>
public sealed class LanguageDialog : Form
{
    public string SelectedLanguage { get; private set; } = Loc.German;

    private readonly Button _germanButton;
    private readonly Button _englishButton;

    public LanguageDialog(string preselectedLanguage)
    {
        Text = "Sprache wählen / Choose language";
        AccessibleName = "Sprache wählen / Choose language";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(360, 160);
        ShowInTaskbar = true;

        var label = new Label
        {
            Text = "Sprache wählen / Choose language:",
            AccessibleName = "Sprache wählen / Choose language",
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter,
            TabStop = false,
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(24, 8, 24, 8),
        };

        _germanButton = new Button
        {
            Text = "Deutsch",
            AccessibleName = "Deutsch",
            AutoSize = false,
            Width = 280,
            Height = 40,
            TabIndex = 0,
        };
        _germanButton.Click += (_, _) => Choose(Loc.German);

        _englishButton = new Button
        {
            Text = "English",
            AccessibleName = "English",
            AutoSize = false,
            Width = 280,
            Height = 40,
            TabIndex = 1,
        };
        _englishButton.Click += (_, _) => Choose(Loc.English);

        buttonPanel.Controls.Add(_germanButton);
        buttonPanel.Controls.Add(_englishButton);

        Controls.Add(buttonPanel);
        Controls.Add(label);

        var preselectGerman = preselectedLanguage == Loc.German;
        AcceptButton = preselectGerman ? _germanButton : _englishButton;

        Load += (_, _) =>
        {
            if (preselectGerman) _germanButton.Focus();
            else _englishButton.Focus();
        };
    }

    private void Choose(string language)
    {
        SelectedLanguage = language;
        DialogResult = DialogResult.OK;
        Close();
    }
}
