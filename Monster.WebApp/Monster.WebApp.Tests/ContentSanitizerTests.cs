using Monster.WebApp.Shared;

namespace Monster.WebApp.Tests;

/// <summary>
/// ContentSanitizer — 리치 에디터 HTML 저장 시 새니타이징 화이트리스트 검증.
/// 화이트리스트를 변경하면 반드시 이 테스트를 함께 갱신할 것.
/// </summary>
public class ContentSanitizerTests
{
    private readonly ContentSanitizer _sanitizer = new();

    // ---- 위험 요소 제거 ----

    [Fact]
    public void Script_태그는_제거된다()
    {
        var result = _sanitizer.Sanitize("<p>안녕</p><script>alert(1)</script>");

        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<p>안녕</p>", result);
    }

    [Fact]
    public void 이벤트_핸들러_속성은_제거된다()
    {
        var result = _sanitizer.Sanitize("<img src=\"/uploads/temp/a.png\" onerror=\"alert(1)\">");

        Assert.DoesNotContain("onerror", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/uploads/temp/a.png", result);
    }

    [Fact]
    public void Javascript_스킴_링크는_제거된다()
    {
        var result = _sanitizer.Sanitize("<a href=\"javascript:alert(1)\">클릭</a>");

        Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Data_URI_이미지는_제거된다()
    {
        var result = _sanitizer.Sanitize("<img src=\"data:image/png;base64,iVBORw0KGgo=\">");

        Assert.DoesNotContain("data:", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void 외부_도메인_이미지는_노드_자체가_제거된다()
    {
        // src만 지우면 빈 <img>가 남아 IsEmptyHtml의 미디어 판정을 통과하는 빈 글이 가능 — 노드째 제거
        var result = _sanitizer.Sanitize("<p>본문</p><img src=\"https://evil.example.com/track.png\">");

        Assert.DoesNotContain("evil.example.com", result);
        Assert.DoesNotContain("<img", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<p>본문</p>", result);
    }

    [Fact]
    public void 외부_이미지만_있는_본문은_빈_본문이_된다()
    {
        var result = _sanitizer.Sanitize("<p><img src=\"https://external.example.com/pic.jpg\"></p>");

        Assert.True(HtmlContentHelper.IsEmptyHtml(result));
    }

    [Fact]
    public void 인라인_style은_제거된다()
    {
        var result = _sanitizer.Sanitize("<p style=\"background:url(javascript:alert(1))\">텍스트</p>");

        Assert.DoesNotContain("style", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("텍스트", result);
    }

    // ---- iframe (동영상 링크 임베드 — 신뢰 플랫폼 임베드 URL만, 파일 업로드 미지원) ----

    [Fact]
    public void 업로드_경로_iframe은_제거된다()
    {
        // 동영상은 링크 임베드 전용 — /uploads/ 경로 iframe을 만드는 기능이 없으므로 불허
        var result = _sanitizer.Sanitize("<iframe class=\"ql-video\" src=\"/uploads/posts/1/movie.mp4\"></iframe><p>본문</p>");

        Assert.DoesNotContain("iframe", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<p>본문</p>", result);
    }

    [Fact]
    public void 유튜브_임베드_iframe은_보존된다()
    {
        var html = "<iframe class=\"ql-video\" src=\"https://www.youtube.com/embed/dQw4w9WgXcQ\" frameborder=\"0\" allowfullscreen=\"true\"></iframe>";
        var result = _sanitizer.Sanitize(html);

        Assert.Contains("https://www.youtube.com/embed/dQw4w9WgXcQ", result);
        Assert.Contains("ql-video", result);
    }

    [Fact]
    public void 비메오_임베드_iframe은_보존된다()
    {
        var result = _sanitizer.Sanitize("<iframe class=\"ql-video\" src=\"https://player.vimeo.com/video/12345\"></iframe>");

        Assert.Contains("https://player.vimeo.com/video/12345", result);
    }

    [Fact]
    public void 유튜브_watch_URL_iframe은_제거된다()
    {
        // 임베드 전용 URL이 아닌 일반 페이지 URL은 불허 (Quill이 임베드 URL로 변환해 삽입함)
        var result = _sanitizer.Sanitize("<iframe src=\"https://www.youtube.com/watch?v=xyz\"></iframe><p>본문</p>");

        Assert.DoesNotContain("iframe", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<p>본문</p>", result);
    }

    [Fact]
    public void 외부_iframe은_노드_자체가_제거된다()
    {
        var result = _sanitizer.Sanitize("<p>본문</p><iframe src=\"https://evil.example.com/frame\"></iframe>");

        Assert.DoesNotContain("iframe", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evil.example.com", result);
        Assert.Contains("<p>본문</p>", result);
    }

    [Fact]
    public void Src_없는_iframe은_노드_자체가_제거된다()
    {
        var result = _sanitizer.Sanitize("<iframe></iframe><p>본문</p>");

        Assert.DoesNotContain("iframe", result, StringComparison.OrdinalIgnoreCase);
    }

    // ---- 허용 요소 보존 (Quill 2 출력 정합) ----

    [Fact]
    public void 기본_서식_태그는_보존된다()
    {
        var html = "<h1>제목</h1><p><strong>굵게</strong> <em>기울임</em> <u>밑줄</u> <s>취소선</s></p>"
                 + "<blockquote>인용</blockquote><p><a href=\"https://example.com\">링크</a></p>";
        var result = _sanitizer.Sanitize(html);

        Assert.Contains("<h1>제목</h1>", result);
        Assert.Contains("<strong>굵게</strong>", result);
        Assert.Contains("<em>기울임</em>", result);
        Assert.Contains("<u>밑줄</u>", result);
        Assert.Contains("<s>취소선</s>", result);
        Assert.Contains("<blockquote>인용</blockquote>", result);
        Assert.Contains("href=\"https://example.com\"", result);
    }

    [Fact]
    public void Quill_목록_구조는_보존된다()
    {
        var html = "<ol><li data-list=\"bullet\"><span class=\"ql-ui\"></span>항목1</li>"
                 + "<li data-list=\"ordered\"><span class=\"ql-ui\"></span>항목2</li></ol>";
        var result = _sanitizer.Sanitize(html);

        Assert.Contains("data-list=\"bullet\"", result);
        Assert.Contains("data-list=\"ordered\"", result);
        Assert.Contains("ql-ui", result);
    }

    [Fact]
    public void Quill_코드블록_구조는_보존된다()
    {
        var html = "<div class=\"ql-code-block-container\"><div class=\"ql-code-block\">var x = 1;</div></div>";
        var result = _sanitizer.Sanitize(html);

        Assert.Contains("ql-code-block-container", result);
        Assert.Contains("ql-code-block", result);
        Assert.Contains("var x = 1;", result);
    }

    [Fact]
    public void 업로드_경로_이미지는_보존된다()
    {
        var result = _sanitizer.Sanitize("<img src=\"/uploads/posts/3/pic.webp\" alt=\"사진\" width=\"300\">");

        Assert.Contains("/uploads/posts/3/pic.webp", result);
        Assert.Contains("alt=\"사진\"", result);
    }

    [Fact]
    public void 화이트리스트_외_class는_제거된다()
    {
        var result = _sanitizer.Sanitize("<p class=\"evil-class\">텍스트</p><span class=\"ql-ui\"></span>");

        Assert.DoesNotContain("evil-class", result);
        Assert.Contains("ql-ui", result);
    }

    [Fact]
    public void 빈_입력은_빈_문자열을_반환한다()
    {
        Assert.Equal(string.Empty, _sanitizer.Sanitize(null));
        Assert.Equal(string.Empty, _sanitizer.Sanitize(""));
    }

    // ---- ToPlainText (검색 컬럼용) ----

    [Fact]
    public void ToPlainText는_태그를_제거하고_텍스트를_보존한다()
    {
        var text = ContentSanitizer.ToPlainText("<h1>제목</h1><p><strong>굵은</strong> 내용입니다</p>");

        Assert.DoesNotContain("<", text);
        Assert.Contains("제목", text);
        Assert.Contains("굵은 내용입니다", text);
    }

    [Fact]
    public void ToPlainText는_블록_경계에서_단어가_붙지_않는다()
    {
        var text = ContentSanitizer.ToPlainText("<p>첫줄</p><p>둘째줄</p>");

        Assert.Contains("첫줄 둘째줄", text);
    }

    [Fact]
    public void ToPlainText는_HTML_엔티티를_복원한다()
    {
        var text = ContentSanitizer.ToPlainText("<p>1 &lt; 2 &amp;&nbsp;테스트</p>");

        Assert.Contains("1 < 2 &", text);
        Assert.Contains("테스트", text);
    }
}
