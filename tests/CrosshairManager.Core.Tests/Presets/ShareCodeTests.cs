using System.IO.Compression;
using System.Text;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Presets;
using Xunit.Abstractions;

namespace CrosshairManager.Core.Tests.Presets;

public class ShareCodeTests(ITestOutputHelper output)
{
    [Fact]
    public void Built_in_presets_round_trip_through_encode_and_decode()
    {
        foreach (var preset in BuiltInPresets.All)
        {
            var code = ShareCode.Encode(preset.Name, preset.Crosshair);

            Assert.True(ShareCode.TryDecode(code, out var name, out var spec, out var warnings, out var error), error);
            Assert.Equal(preset.Name, name);
            Assert.Equal(preset.Crosshair, spec);
            Assert.Empty(warnings);
        }
    }

    [Fact]
    public void Default_preset_code_is_shorter_than_200_characters()
    {
        var code = ShareCode.Encode("Default", CrosshairSpec.Default);

        output.WriteLine($"default code: {code} (length {code.Length})");
        Assert.StartsWith("CHM1:", code);
        Assert.True(code.Length < 200, code);
    }

    [Fact]
    public void Canonical_json_omits_default_values_and_keeps_schema_order()
    {
        Assert.Equal("{\"name\":\"Default\",\"crosshair\":{}}", Inflate(ShareCode.Encode("Default", CrosshairSpec.Default)));

        var changed = CrosshairSpec.Default with { Color = Rgb.Parse("#FF0000"), Dot = new DotSpec(Size: 4) };
        Assert.Equal(
            "{\"name\":\"X\",\"crosshair\":{\"color\":\"#FF0000\",\"dot\":{\"size\":4}}}",
            Inflate(ShareCode.Encode("X", changed)));
    }

    [Fact]
    public void Decode_trims_surrounding_whitespace()
    {
        var code = ShareCode.Encode("Trim", CrosshairSpec.Default);

        Assert.True(ShareCode.TryDecode($"  \t{code}\r\n", out var name, out _, out _, out _));
        Assert.Equal("Trim", name);
    }

    [Fact]
    public void Decoded_spec_is_normalized_and_reports_warnings()
    {
        var code = ShareCode.Encode("Wide", CrosshairSpec.Default with { Opacity = 150 });

        Assert.True(ShareCode.TryDecode(code, out _, out var spec, out var warnings, out _));
        Assert.Equal(100, spec.Opacity);
        Assert.Equal(new[] { "Opacity: 150 clamped to 100" }, warnings);
    }

    [Fact]
    public void Rejects_wrong_prefix_with_prefix_key()
    {
        AssertRejected("CHM2:x", "ShareCode.Error.Prefix");
    }

    [Fact]
    public void Rejects_invalid_base64url_with_base64_key()
    {
        AssertRejected("CHM1:!!!", "ShareCode.Error.Base64");
    }

    [Fact]
    public void Rejects_valid_base64_that_is_not_deflate_with_inflate_key()
    {
        // 0x07 0x00 is base64url "BwA"; BFINAL=1 with reserved BTYPE=11 is invalid deflate.
        AssertRejected("CHM1:BwA", "ShareCode.Error.Inflate");
    }

    [Fact]
    public void Rejects_compressed_non_json_with_json_key()
    {
        AssertRejected(Payload(Deflate(Encoding.UTF8.GetBytes("not json"))), "ShareCode.Error.Json");
    }

    [Fact]
    public void Rejects_inflated_payload_over_16_kb_with_too_large_key()
    {
        AssertRejected(Payload(Deflate(new byte[16 * 1024 + 1])), "ShareCode.Error.TooLarge");
    }

    private static void AssertRejected(string code, string expectedKey)
    {
        Assert.False(ShareCode.TryDecode(code, out var name, out var spec, out var warnings, out var errorKey));
        Assert.Equal(expectedKey, errorKey);
        Assert.Equal("", name);
        Assert.Equal(CrosshairSpec.Default, spec);
        Assert.Empty(warnings);
    }

    private static string Inflate(string code)
    {
        var base64 = code["CHM1:".Length..].Replace('-', '+').Replace('_', '/');
        base64 += new string('=', (4 - base64.Length % 4) % 4);
        using var input = new DeflateStream(new MemoryStream(Convert.FromBase64String(base64)), CompressionMode.Decompress);
        using var reader = new StreamReader(input, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Payload(byte[] deflated) =>
        "CHM1:" + Convert.ToBase64String(deflated).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Deflate(byte[] raw)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(raw);
        }

        return output.ToArray();
    }
}
