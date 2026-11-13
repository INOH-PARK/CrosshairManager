using System.Windows;

namespace CrosshairManager.App.Views;

public partial class RenameDialog : Window
{
    public RenameDialog(string currentName)
    {
        InitializeComponent();
        NameBox.Text = currentName;
        NewName = currentName;
        Loaded += (_, _) =>
        {
            NameBox.SelectAll();
            NameBox.Focus();
        };
    }

    public string NewName { get; private set; }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        NewName = NameBox.Text;
        DialogResult = true;
    }
}
