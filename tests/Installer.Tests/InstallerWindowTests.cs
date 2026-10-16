using System.Drawing;
using System.Windows.Forms;
using FF14AccessibilityInstaller.Russian;

namespace Installer.Tests;

public sealed class InstallerWindowTests
{
    [Theory]
    [InlineData(960, 620)]
    [InlineData(720, 480)]
    public async Task SeparateButtonsFitAndHaveAccessibleNamesAndKeyboardOrder(int width, int height)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new MainForm();
                form.CreateControl();
                form.Size = new Size(width, height);
                form.PerformLayout();
                var panel = Assert.Single(form.Controls.OfType<FlowLayoutPanel>());
                panel.PerformLayout();
                var buttons = panel.Controls.OfType<Button>().ToArray();
                Assert.Equal(10, buttons.Length);
                Assert.Equal(new[] { "Установить / обновить мод", "Установить русификацию игры", "Установить русификацию мода",
                    "Проверить обновления", "Удалить русификацию игры", "Удалить русификацию мода", "Полностью удалить мод",
                    "Полностью удалить мод и русификацию игры", "Отменить", "Закрыть" }, buttons.Select(b => b.Text));
                Assert.Equal(Enumerable.Range(0, 10), buttons.Select(b => b.TabIndex));
                foreach (var button in buttons)
                {
                    Assert.Equal(button.Text, button.AccessibleName);
                    Assert.True(button.Bounds.Right <= panel.ClientSize.Width, button.Text);
                    Assert.True(button.Bounds.Bottom <= panel.ClientSize.Height, button.Text);
                }
                var journal = Assert.Single(form.Controls.OfType<TextBox>());
                Assert.True(journal.ReadOnly);
                Assert.True(journal.TabStop);
                Assert.True(journal.WordWrap);
                Assert.Equal(ScrollBars.Vertical, journal.ScrollBars);
                Assert.True(journal.Bounds.Height >= 160);
                completion.SetResult();
            }
            catch (Exception ex) { completion.SetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task;
        thread.Join();
    }
}
