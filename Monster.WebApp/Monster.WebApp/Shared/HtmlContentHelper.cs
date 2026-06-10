using System.Net;

namespace Monster.WebApp.Shared;

/// <summary>
/// 사용자 입력(평문)을 XSS 안전한 HTML로 변환하는 유틸리티.
/// 게시글/댓글 본문은 일반 텍스트 입력이므로, 모든 특수문자를 인코딩하여
/// 스크립트 주입을 차단하고 줄바꿈만 &lt;br&gt;로 보존한다.
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
}
