namespace Monster.WebApp.Shared;

/// <summary>
/// UTC 시각 → 상대시간 표시 문자열 ("방금 전", "N분 전" 등). 7일 이상은 날짜로 표시.
/// </summary>
public static class TimeDisplayHelper
{
    public static string ToRelative(DateTime utc)
    {
        var elapsed = DateTime.UtcNow - utc;

        if (elapsed < TimeSpan.Zero)
        {
            return utc.ToLocalTime().ToString("MM/dd");
        }
        if (elapsed.TotalMinutes < 1)
        {
            return "방금 전";
        }
        if (elapsed.TotalHours < 1)
        {
            return $"{(int)elapsed.TotalMinutes}분 전";
        }
        if (elapsed.TotalDays < 1)
        {
            return $"{(int)elapsed.TotalHours}시간 전";
        }
        if (elapsed.TotalDays < 7)
        {
            return $"{(int)elapsed.TotalDays}일 전";
        }
        return utc.ToLocalTime().ToString("MM/dd");
    }
}
