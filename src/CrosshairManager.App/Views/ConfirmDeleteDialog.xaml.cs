using System.Windows;
using CrosshairManager.App.Localization;

namespace CrosshairManager.App.Views;

public partial class ConfirmDeleteDialog : Window
{
    public ConfirmDeleteDialog(string presetName)
    {
        InitializeComponent();
        Prompt.Text = Loc.Instance.Format("Preset.DeletePrompt", presetName);
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
