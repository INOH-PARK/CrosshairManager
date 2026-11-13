using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrosshairManager.App.Controls;

/// <summary>
/// Bottom bar: the always-present fullscreen/anti-cheat notice plus the newest pending message (with Dismiss).
/// Its UIA Name is <see cref="Text"/>: the message when one is shown, else the notice.
/// </summary>
public partial class InfoBar : UserControl
{
    public static readonly DependencyProperty NoticeProperty = DependencyProperty.Register(
        nameof(Notice), typeof(string), typeof(InfoBar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(InfoBar), new PropertyMetadata(null));

    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(
        nameof(Detail), typeof(string), typeof(InfoBar), new PropertyMetadata(null, (d, _) => ((InfoBar)d).Changed()));

    public static readonly DependencyProperty HasMessageProperty = DependencyProperty.Register(
        nameof(HasMessage), typeof(bool), typeof(InfoBar), new PropertyMetadata(false, (d, _) => ((InfoBar)d).Changed()));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(InfoBar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DismissLabelProperty = DependencyProperty.Register(
        nameof(DismissLabel), typeof(string), typeof(InfoBar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DismissCommandProperty = DependencyProperty.Register(
        nameof(DismissCommand), typeof(ICommand), typeof(InfoBar), new PropertyMetadata(null));

    public static readonly DependencyProperty HasDetailProperty = DependencyProperty.Register(
        nameof(HasDetail), typeof(bool), typeof(InfoBar), new PropertyMetadata(false));

    public InfoBar()
    {
        InitializeComponent();
    }

    public string Notice
    {
        get => (string)GetValue(NoticeProperty);
        set => SetValue(NoticeProperty, value);
    }

    public string? Message
    {
        get => (string?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string? Detail
    {
        get => (string?)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public bool HasMessage
    {
        get => (bool)GetValue(HasMessageProperty);
        set => SetValue(HasMessageProperty, value);
    }

    public bool HasDetail
    {
        get => (bool)GetValue(HasDetailProperty);
        private set => SetValue(HasDetailProperty, value);
    }

    /// <summary>Message when shown, else the notice (bound to the UIA Name).</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string DismissLabel
    {
        get => (string)GetValue(DismissLabelProperty);
        set => SetValue(DismissLabelProperty, value);
    }

    public ICommand? DismissCommand
    {
        get => (ICommand?)GetValue(DismissCommandProperty);
        set => SetValue(DismissCommandProperty, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CustomControlPeer(this, AutomationControlType.StatusBar, nameof(InfoBar));

    private void Changed()
    {
        HasDetail = HasMessage && !string.IsNullOrEmpty(Detail);
        Shell.Background = (System.Windows.Media.Brush)FindResource(HasMessage ? "BrushWarningSoft" : "BrushInfoSoft");
    }
}
