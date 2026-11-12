using CrosshairManager.Core.Model;

namespace CrosshairManager.Core.Tests.Model;

public class RgbTests
{
    [Theory]
    [InlineData("#0f0", 0x00, 0xFF, 0x00)]
    [InlineData("00ff80", 0x00, 0xFF, 0x80)]
    [InlineData("#FFFFFF", 0xFF, 0xFF, 0xFF)]
    [InlineData("#abc", 0xAA, 0xBB, 0xCC)]
    public void TryParse_accepts_supported_forms(string text, byte r, byte g, byte b)
    {
        Assert.True(Rgb.TryParse(text, out var rgb));
        Assert.Equal(new Rgb(r, g, b), rgb);
    }

    [Theory]
    [InlineData("#GG0000")]
    [InlineData("#12345")]
    [InlineData("#")]
    [InlineData("")]
    [InlineData("#00FF00FF")]
    public void TryParse_rejects_malformed_text(string text)
    {
        Assert.False(Rgb.TryParse(text, out _));
    }

    [Fact]
    public void TryParse_rejects_null()
    {
        Assert.False(Rgb.TryParse(null, out _));
    }

    [Fact]
    public void Parse_throws_on_malformed_text()
    {
        Assert.Throws<FormatException>(() => Rgb.Parse("#GG0000"));
    }

    [Fact]
    public void ToString_uses_uppercase_hash_form()
    {
        Assert.Equal("#00FF80", new Rgb(0x00, 0xFF, 0x80).ToString());
        Assert.Equal("#00FF00", Rgb.Parse("#0f0").ToString());
    }
}
