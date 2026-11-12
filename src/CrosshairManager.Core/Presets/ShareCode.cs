using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrosshairManager.Core.Model;

namespace CrosshairManager.Core.Presets;

/// <summary>Text share code: <c>CHM1:</c> + base64url(no padding)(raw deflate(canonical UTF-8 JSON <c>{name, crosshair}</c>)).</summary>
public static class ShareCode
{
    public const string Prefix = "CHM1:";
    public const int MaxInflatedBytes = 16 * 1024;

    public const string ErrorPrefix = "ShareCode.Error.Prefix";
    public const string ErrorBase64 = "ShareCode.Error.Base64";
    public const string ErrorInflate = "ShareCode.Error.Inflate";
    public const string ErrorJson = "ShareCode.Error.Json";
    public const string ErrorTooLarge = "ShareCode.Error.TooLarge";

    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Canonical JSON omits every value equal to <see cref="CrosshairSpec.Default"/>; decode fills them back in.</summary>
    public static string Encode(string name, CrosshairSpec spec)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(spec);

        var crosshair = Prune(ToCrosshairObject(spec), ToCrosshairObject(CrosshairSpec.Default));
        var root = new JsonObject
        {
            ["name"] = name,
            ["crosshair"] = crosshair,
        };

        var json = root.ToJsonString(CanonicalOptions);
        return Prefix + ToBase64Url(Deflate(Encoding.UTF8.GetBytes(json)));
    }

    /// <summary>On failure, <paramref name="errorKey"/> is one of the <c>ShareCode.Error.*</c> keys; the other outputs are defaults.</summary>
    public static bool TryDecode(string code, out string name, out CrosshairSpec spec, out IReadOnlyList<string> warnings, out string errorKey)
    {
        ArgumentNullException.ThrowIfNull(code);

        name = "";
        spec = CrosshairSpec.Default;
        warnings = Array.Empty<string>();
        errorKey = "";

        var text = code.Trim();
        if (!text.StartsWith(Prefix, StringComparison.Ordinal))
        {
            errorKey = ErrorPrefix;
            return false;
        }

        var deflated = FromBase64Url(text[Prefix.Length..]);
        if (deflated is null)
        {
            errorKey = ErrorBase64;
            return false;
        }

        using var inflated = new MemoryStream();
        try
        {
            using var deflate = new DeflateStream(new MemoryStream(deflated), CompressionMode.Decompress);
            var chunk = new byte[4096];
            int read;
            while ((read = deflate.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (inflated.Length + read > MaxInflatedBytes)
                {
                    errorKey = ErrorTooLarge;
                    return false;
                }

                inflated.Write(chunk, 0, read);
            }
        }
        catch (InvalidDataException)
        {
            errorKey = ErrorInflate;
            return false;
        }

        PresetDocument? doc;
        CrosshairSpec decoded;
        try
        {
            doc = JsonSerializer.Deserialize(StrictUtf8.GetString(inflated.ToArray()), PresetJson.Default.PresetDocument);
            if (doc?.Name is null || doc.Crosshair is null)
            {
                errorKey = ErrorJson;
                return false;
            }

            decoded = doc.Crosshair.ToSpec();
        }
        catch (Exception ex) when (ex is JsonException or FormatException or DecoderFallbackException)
        {
            errorKey = ErrorJson;
            return false;
        }

        var (normalized, clamps) = decoded.Normalize();
        name = doc.Name;
        spec = normalized;
        warnings = clamps;
        return true;
    }

    private static JsonObject ToCrosshairObject(CrosshairSpec spec) =>
        JsonSerializer.SerializeToNode(new PresetDocument { Crosshair = CrosshairDocument.From(spec) }, PresetJson.Default.PresetDocument)!["crosshair"]!.AsObject();

    private static JsonObject Prune(JsonObject value, JsonObject defaults)
    {
        var result = new JsonObject();
        foreach (var (key, node) in value)
        {
            var defaultNode = defaults[key];
            if (node is JsonObject child && defaultNode is JsonObject defaultChild)
            {
                var pruned = Prune(child, defaultChild);
                if (pruned.Count > 0)
                {
                    result[key] = pruned;
                }
            }
            else if (!JsonNode.DeepEquals(node, defaultNode))
            {
                result[key] = node?.DeepClone();
            }
        }

        return result;
    }

    private static byte[] Deflate(byte[] raw)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(raw);
        }

        return output.ToArray();
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[]? FromBase64Url(string text)
    {
        if (text.Length == 0 || !text.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            return null;
        }

        var base64 = text.Replace('-', '+').Replace('_', '/');
        base64 += new string('=', (4 - base64.Length % 4) % 4);
        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
