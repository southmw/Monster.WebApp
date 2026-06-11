namespace Monster.WebApp.Services.Board;

/// <summary>
/// 익명 게시글 수정 시 PostDetail → PostEdit으로 비밀번호를 전달하는 서킷 범위 상태.
/// 비밀번호를 URL 쿼리(?verify=)로 노출하면 브라우저 히스토리/로그에 남으므로,
/// 같은 서킷 내 내비게이션(forceLoad 아님)에서만 유효한 메모리 전달을 사용한다.
/// Blazor Server에서 Scoped = 서킷 단위이므로 다른 사용자/탭과 공유되지 않는다.
/// </summary>
public class PostEditVerificationState
{
    private int? _postId;
    private string? _password;

    public void Set(int postId, string password)
    {
        _postId = postId;
        _password = password;
    }

    /// <summary>해당 게시글의 비밀번호를 1회 소비한다 (반환 즉시 클리어).</summary>
    public string? Consume(int postId)
    {
        if (_postId != postId)
            return null;

        var password = _password;
        _postId = null;
        _password = null;
        return password;
    }
}
