using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace CrosshairManager.App.Controls;

/// <summary>
/// Slider + TextBox pair for an integer field (Must have: the AutomationId goes on the TextBox, via <see cref="FieldId"/>).
/// An optional caption label sits on the left. Out-of-range typed values are left to the view model (Normalize clamps them).
/// </summary>
public partial class NumberField : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(NumberField), new PropertyMetadata(null, (d, _) => ((NumberField)d).UpdateLabel()));

    public static readonly DependencyProperty FieldIdProperty = DependencyProperty.Register(
        nameof(FieldId), typeof(string), typeof(NumberField), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(int), typeof(NumberField), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(int), typeof(NumberField), new PropertyMetadata(0));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(int), typeof(NumberField), new PropertyMetadata(100));

    public NumberField()
    {
        InitializeComponent();
        UpdateLabel();
    }

    public string? Label
    {
        get => (string?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>AutomationId of the inner TextBox.</summary>
    public string FieldId
    {
        get => (string)GetValue(FieldIdProperty);
        set => SetValue(FieldIdProperty, value);
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int Minimum
    {
        get => (int)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public int Maximum
    {
        get => (int)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CustomControlPeer(this, AutomationControlType.Group, nameof(NumberField));

    private void UpdateLabel() => LabelText.Visibility = string.IsNullOrEmpty(Label) ? Visibility.Collapsed : Visibility.Visible;
}
