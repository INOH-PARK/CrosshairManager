using System.Windows.Data;
using System.Windows.Markup;
using CrosshairManager.App.Localization;

namespace CrosshairManager.App.Views;

/// <summary>
/// <c>{v:Str Key}</c> = <c>{Binding [Key], Source={x:Static loc:Loc.Instance}}</c>: a one-way binding to the Loc indexer,
/// so every string follows <see cref="Loc.SetLanguage"/> at run time. User-visible text is never written in XAML.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class StrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding("[" + Key + "]") { Source = Loc.Instance, Mode = BindingMode.OneWay };
        return binding.ProvideValue(serviceProvider);
    }
}
