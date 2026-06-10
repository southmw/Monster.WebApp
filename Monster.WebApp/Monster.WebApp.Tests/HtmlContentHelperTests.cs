using Monster.WebApp.Shared;
using Xunit;

namespace Monster.WebApp.Tests;

public class HtmlContentHelperTests
{
    [Theory]
    [InlineData("<script>alert(1)</script>", "&lt;script&gt;alert(1)&lt;/script&gt;")]
    [InlineData("<img src=x onerror=alert(1)>", "&lt;img src=x onerror=alert(1)&gt;")]
    [InlineData("a & b", "a &amp; b")]
    [InlineData("\"quoted\"", "&quot;quoted&quot;")]
    public void ToSafeHtml_EncodesDangerousCharacters(string input, string expected)
    {
        var result = HtmlContentHelper.ToSafeHtml(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToSafeHtml_DoesNotContainExecutableScriptTag()
    {
        var result = HtmlContentHelper.ToSafeHtml("<script>document.cookie</script>");
        // 인코딩되어 실제 <script> 태그가 남지 않아야 한다
        Assert.DoesNotContain("<script>", result);
    }

    [Theory]
    [InlineData("line1\nline2", "line1<br>line2")]
    [InlineData("line1\r\nline2", "line1<br>line2")]
    [InlineData("line1\rline2", "line1<br>line2")]
    public void ToSafeHtml_ConvertsNewlinesToBr(string input, string expected)
    {
        var result = HtmlContentHelper.ToSafeHtml(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ToSafeHtml_ReturnsEmptyForNullOrEmpty(string? input)
    {
        var result = HtmlContentHelper.ToSafeHtml(input);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ToSafeHtml_PlainTextPassesThroughUnchanged()
    {
        var result = HtmlContentHelper.ToSafeHtml("안녕하세요 일반 텍스트입니다");
        Assert.Equal("안녕하세요 일반 텍스트입니다", result);
    }
}
