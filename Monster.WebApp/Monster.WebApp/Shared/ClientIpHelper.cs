namespace Monster.WebApp.Shared;

/// <summary>
/// 클라이언트 IP 추출 헬퍼.
/// X-Forwarded-For 헤더를 직접 파싱하지 않는다 — 클라이언트가 임의로 조작할 수 있어
/// 로그인 잠금/추천 중복 방지 우회에 악용될 수 있기 때문.
/// 신뢰할 수 있는 프록시 뒤에 배포하는 경우 Program.cs의 ForwardedHeaders 미들웨어가
/// RemoteIpAddress를 재작성하므로(설정: ForwardedHeaders:KnownProxies) 여기서는 항상
/// RemoteIpAddress만 사용한다.
/// </summary>
public static class ClientIpHelper
{
    public static string? GetClientIp(HttpContext? httpContext)
        => httpContext?.Connection.RemoteIpAddress?.ToString();
}
