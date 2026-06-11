using System.Net;
using System.Text.RegularExpressions;
using Ganss.Xss;

namespace Monster.WebApp.Shared;

/// <summary>
/// 리치 에디터(Quill 2) HTML 본문의 저장 시 새니타이저.
/// 화이트리스트는 게시글/댓글 에디터 툴바 구성과 정합을 유지해야 하며,
/// 여기 없는 태그/속성/클래스를 추가할 때는 반드시 XSS 영향 검토와 테스트를 함께 할 것.
/// Program.cs에 Singleton으로 등록 (구성은 생성자에서 고정 — 이후 변경 금지로 스레드 안전).
/// </summary>
public class ContentSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    // iframe(동영상 임베드) src 허용 목록 — 신뢰할 수 있는 동영상 플랫폼의 임베드 전용 URL만.
    // (watch URL 등 일반 페이지는 불허 — Quill이 임베드 URL로 변환해 삽입)
    private static readonly string[] AllowedIframeSrcPrefixes =
    {
        "https://www.youtube.com/embed/",
        "https://youtube.com/embed/",
        "https://www.youtube-nocookie.com/embed/",
        "https://player.vimeo.com/video/"
    };

    private static bool IsAllowedIframeSrc(string? src)
        => !string.IsNullOrEmpty(src) &&
           AllowedIframeSrcPrefixes.Any(p => src.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    public ContentSanitizer()
    {
        _sanitizer = new HtmlSanitizer();

        // Quill 2 출력에 필요한 최소 태그만 허용
        _sanitizer.AllowedTags.Clear();
        _sanitizer.AllowedTags.UnionWith(new[]
        {
            "p", "br", "strong", "em", "u", "s", "h1", "h2", "h3",
            "ol", "ul", "li", "a", "img", "iframe", "blockquote", "pre", "code", "span", "div"
        });

        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.UnionWith(new[]
        {
            "href", "src", "alt", "width", "height", "class", "data-list", "frameborder", "allowfullscreen"
        });

        // javascript:, data:(base64) 등 차단 — 상대경로는 스킴 검사 대상이 아니므로 통과
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.UnionWith(new[] { "http", "https" });

        // class 화이트리스트 (비워두면 모든 class가 통과하므로 반드시 명시)
        _sanitizer.AllowedClasses.Clear();
        _sanitizer.AllowedClasses.UnionWith(new[]
        {
            "ql-ui", "ql-code-block", "ql-code-block-container", "ql-syntax", "ql-video"
        });

        // 인라인 style 전면 차단
        _sanitizer.AllowedCssProperties.Clear();
        _sanitizer.AllowedAtRules.Clear();

        // img src는 자체 업로드 경로(/uploads/...)만 허용 (외부 핫링크·추적 픽셀 차단)
        // iframe src는 자체 업로드 경로 + 신뢰 동영상 플랫폼 임베드 URL만 허용
        _sanitizer.FilterUrl += (_, e) =>
        {
            var tagName = e.Tag.TagName;
            if (tagName.Equals("IMG", StringComparison.OrdinalIgnoreCase))
            {
                if (e.OriginalUrl == null ||
                    !e.OriginalUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                {
                    e.SanitizedUrl = null; // 속성 제거
                }
            }
            else if (tagName.Equals("IFRAME", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsAllowedIframeSrc(e.OriginalUrl))
                {
                    e.SanitizedUrl = null;
                }
            }
        };

        // src가 제거됐거나 허용 목록 외인 iframe/img는 노드 자체를 제거.
        // (FilterUrl이 외부 src 속성만 지우면 빈 <img>/<iframe>이 잔존해
        //  IsEmptyHtml의 미디어 판정을 통과하는 빈 글이 만들어질 수 있음 — 이중 방어)
        _sanitizer.PostProcessDom += (_, e) =>
        {
            foreach (var iframe in e.Document.QuerySelectorAll("iframe").ToList())
            {
                if (!IsAllowedIframeSrc(iframe.GetAttribute("src")))
                {
                    iframe.Remove();
                }
            }

            foreach (var img in e.Document.QuerySelectorAll("img").ToList())
            {
                if (string.IsNullOrEmpty(img.GetAttribute("src")))
                {
                    img.Remove();
                }
            }
        };
    }

    /// <summary>에디터 HTML을 화이트리스트 기준으로 정제한다. 저장 직전에 호출할 것.</summary>
    public string Sanitize(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        return _sanitizer.Sanitize(html);
    }

    /// <summary>HTML에서 태그를 제거한 평문을 만든다 (검색 컬럼용).</summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        // 태그를 공백으로 치환 (블록 경계 단어가 붙지 않도록) 후 엔티티 복원, 공백 정리
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
}
