using System.Windows;

namespace CrosshairManager.App.Views;

/// <summary>First-run notice (fullscreen, anti-cheat/ToS, hotkey limits). DisclaimerOk returns true; closing otherwise returns false.</summary>
public partial class DisclaimerDialog : Window
{
    public DisclaimerDialog()
    {
        InitializeComponent();
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
