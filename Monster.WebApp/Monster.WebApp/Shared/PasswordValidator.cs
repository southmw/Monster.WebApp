namespace Monster.WebApp.Shared;

/// <summary>
/// 비밀번호 정책 검증 유틸리티
/// </summary>
public static class PasswordValidator
{
    /// <summary>
    /// 비밀번호 정책을 검증합니다.
    /// 정책: 최소 8자, 대문자 1개, 소문자 1개, 숫자 1개, 특수문자 1개
    /// </summary>
    /// <param name="password">검증할 비밀번호</param>
    /// <returns>검증 결과 (IsValid: 유효 여부, Message: 오류 메시지)</returns>
    public static (bool IsValid, string Message) Validate(string password)
    {
        if (string.IsNullOrEmpty(password))
            return (false, "비밀번호를 입력하세요.");

        if (password.Length < 8)
            return (false, "비밀번호는 최소 8자 이상이어야 합니다.");

        if (!password.Any(char.IsUpper))
            return (false, "대문자가 1개 이상 포함되어야 합니다.");

        if (!password.Any(char.IsLower))
            return (false, "소문자가 1개 이상 포함되어야 합니다.");

        if (!password.Any(char.IsDigit))
            return (false, "숫자가 1개 이상 포함되어야 합니다.");

        if (!password.Any(c => "!@#$%^&*()_+-=[]{}|;':\",./<>?".Contains(c)))
            return (false, "특수문자가 1개 이상 포함되어야 합니다.");

        return (true, string.Empty);
    }

    /// <summary>
    /// 비밀번호 정책 힌트 텍스트
    /// </summary>
    public const string PolicyHint = "최소 8자, 대문자/소문자/숫자/특수문자 포함";

    /// <summary>
    /// 비밀번호 정책에 대한 상세 체크리스트를 반환합니다.
    /// </summary>
    /// <param name="password">검증할 비밀번호</param>
    /// <returns>각 정책 항목별 충족 여부</returns>
    public static List<(string Rule, bool IsMet)> GetPolicyChecklist(string password)
    {
        password ??= string.Empty;

        return new List<(string Rule, bool IsMet)>
        {
            ("8자 이상", password.Length >= 8),
            ("대문자 포함", password.Any(char.IsUpper)),
            ("소문자 포함", password.Any(char.IsLower)),
            ("숫자 포함", password.Any(char.IsDigit)),
            ("특수문자 포함", password.Any(c => "!@#$%^&*()_+-=[]{}|;':\",./<>?".Contains(c)))
        };
    }
}
