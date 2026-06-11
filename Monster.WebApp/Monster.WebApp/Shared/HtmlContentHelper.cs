using System.Net;
using System.Text.RegularExpressions;

namespace Monster.WebApp.Shared;

/// <summary>
/// 게시글/댓글 본문 출력 유틸리티.
/// - 레거시 평문(IsHtml=false): 모든 특수문자를 인코딩하여 스크립트 주입을 차단 (ToSafeHtml)
/// - 리치 에디터 HTML(IsHtml=true): 저장 시 ContentSanitizer로 정제된 상태이므로 그대로 출력 (ToDisplayHtml)
/// </summary>
public static class HtmlContentHelper
{
    /// <summary>
    /// 평문 텍스트를 XSS 안전한 HTML 문자열로 변환한다.
    /// 모든 HTML 특수문자(&lt; &gt; &amp; " ')를 인코딩한 뒤 줄바꿈을 &lt;br&gt;로 치환한다.
    /// </summary>
    public static string ToSafeHtml(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        // 1) 모든 특수문자 인코딩 → <script> 등 태그/이벤트 핸들러가 실행될 수 없음
        var encoded = WebUtility.HtmlEncode(plainText);

        // 2) 줄바꿈 보존 (CRLF → CR → LF 순서로 치환)
        encoded = encoded
            .Replace("\r\n", "<br>")
            .Replace("\r", "<br>")
            .Replace("\n", "<br>");

        return encoded;
    }

    /// <summary>
    /// 본문 출력용 HTML을 반환한다.
    /// isHtml=true인 콘텐츠는 저장 시점에 ContentSanitizer.Sanitize()를 거쳤다는 계약 하에
    /// 그대로 반환한다 — 새니타이즈를 거치지 않은 HTML을 이 경로로 출력하지 말 것.
    /// </summary>
    public static string ToDisplayHtml(string? content, bool isHtml)
        => isHtml ? content ?? string.Empty : ToSafeHtml(content);

    /// <summary>
    /// 에디터 HTML이 실질적으로 비어 있는지 검사한다 (Quill 빈 값: &lt;p&gt;&lt;br&gt;&lt;/p&gt;).
    /// 단, 이미지/동영상만 있는 본문은 비어 있지 않은 것으로 취급한다.
    /// </summary>
    public static bool IsEmptyHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return true;

        // 미디어 임베드가 있으면 텍스트가 없어도 유효한 본문
        if (Regex.IsMatch(html, @"<(img|iframe)\b", RegexOptions.IgnoreCase))
            return false;

        var text = Regex.Replace(html, "<[^>]+>", string.Empty);
        text = WebUtility.HtmlDecode(text); // &nbsp;(U+00A0)도 IsNullOrWhiteSpace가 공백으로 취급
        return string.IsNullOrWhiteSpace(text);
    }
}
