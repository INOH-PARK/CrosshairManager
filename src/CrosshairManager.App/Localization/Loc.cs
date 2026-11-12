using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace CrosshairManager.App.Localization;

/// <summary>
/// Localized strings from <c>Resources/Strings*.resx</c>. XAML binds <c>{Binding [Key], Source={x:Static loc:Loc.Instance}}</c>;
/// <see cref="SetLanguage"/> raises <c>PropertyChanged("Item[]")</c> so every binding refreshes. A missing key returns the key.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public const string ResourceBaseName = "CrosshairManager.App.Resources.Strings";

    private static readonly ResourceManager Resources = new(ResourceBaseName, typeof(Loc).Assembly);
    private static readonly CultureInfo Korean = CultureInfo.GetCultureInfo("ko");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private readonly CultureInfo _system = CultureInfo.CurrentUICulture;
    private CultureInfo _culture;

    private Loc()
    {
        _culture = Resolve("system", _system);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static Loc Instance { get; } = new();

    public CultureInfo Culture => _culture;

    public string this[string key] => Resources.GetString(key, _culture) ?? key;

    /// <summary>"system" (OS UI language: Korean if ko, else English), "ko" or "en".</summary>
    public void SetLanguage(string language)
    {
        _culture = Resolve(language, _system);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    public string Format(string key, params object?[] args) => string.Format(_culture, this[key], args);

    public static CultureInfo Resolve(string language, CultureInfo system)
    {
        ArgumentNullException.ThrowIfNull(system);
        return language switch
        {
            "ko" => Korean,
            "en" => English,
            _ => system.TwoLetterISOLanguageName == "ko" ? Korean : English,
        };
    }
}
