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

    [Fact]
    public void ToDisplayHtml_IsHtmlTrue_ReturnsContentAsIs()
    {
        // IsHtml=true 콘텐츠는 저장 시 ContentSanitizer로 정제됐다는 계약 하에 그대로 반환
        var html = "<p><strong>굵게</strong></p>";
        Assert.Equal(html, HtmlContentHelper.ToDisplayHtml(html, isHtml: true));
    }

    [Fact]
    public void ToDisplayHtml_IsHtmlFalse_EncodesLegacyPlainText()
    {
        var result = HtmlContentHelper.ToDisplayHtml("<b>평문</b>\n둘째줄", isHtml: false);
        Assert.Equal("&lt;b&gt;평문&lt;/b&gt;<br>둘째줄", result);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("<p><br></p>", true)]           // Quill 빈 값
    [InlineData("<p>&nbsp;</p>", true)]          // 공백만
    [InlineData("<p>내용</p>", false)]
    [InlineData("<p><img src=\"/uploads/temp/a.png\"></p>", false)]   // 이미지만 있는 본문은 유효
    [InlineData("<iframe class=\"ql-video\" src=\"/uploads/temp/a.mp4\"></iframe>", false)] // 동영상만 있는 본문은 유효
    public void IsEmptyHtml_DetectsEffectivelyEmptyContent(string? input, bool expected)
    {
        Assert.Equal(expected, HtmlContentHelper.IsEmptyHtml(input));
    }
}
