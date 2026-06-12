using MudBlazor;

namespace Monster.WebApp.Shared;

/// <summary>
/// 카테고리 슬러그 → 표시용 아이콘 매핑 (NavMenu / Home / Board Index 공용)
/// </summary>
public static class CategoryDisplayHelper
{
    public static string GetIcon(string urlSlug)
    {
        return urlSlug switch
        {
            "free" => Icons.Material.Filled.Chat,
            "questions" => Icons.Material.Filled.Help,
            "info" => Icons.Material.Filled.Info,
            "notice" => Icons.Material.Filled.Announcement,
            _ => Icons.Material.Filled.Article
        };
    }
}
