using MudBlazor;

namespace Monster.WebApp.Shared;

public static class CustomTheme
{
    public static MudTheme Theme => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#6366F1", // Indigo 500
            Secondary = "#94A3B8", // Slate 400
            Success = "#10b981",
            Info = "#06b6d4",
            Warning = "#f59e0b",
            Error = "#ef4444",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#0F172A", // Slate 900
            DrawerBackground = "#F8FAFC", // Slate 50 — 흰 헤더/회색 콘텐츠 사이의 옅은 톤 (경계는 테두리로 구분)
            DrawerText = "#334155", // Slate 700
            DrawerIcon = "#64748B", // Slate 500
            Background = "#F1F5F9", // Slate 100 (Light Gray Background)
            Surface = "#FFFFFF",
            TextPrimary = "#0F172A", // Slate 900
            TextSecondary = "#64748B", // Slate 500
            ActionDefault = "#64748B",
            ActionDisabled = "#94A3B8",
            ActionDisabledBackground = "#E2E8F0",
            LinesDefault = "#E2E8F0", // Slate 200 — 카드 테두리/구분선
            Divider = "#E2E8F0",
            TableLines = "#F1F5F9" // Slate 100 — 목록 행 구분선 (더 옅게)
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#818CF8", // Indigo 400
            Secondary = "#94A3B8",
            Success = "#34d399",
            Info = "#22d3ee",
            Warning = "#fbbf24",
            Error = "#f87171",
            AppbarBackground = "#1E293B", // Slate 800
            AppbarText = "#E2E8F0",
            DrawerBackground = "#0F172A", // Slate 900
            DrawerText = "#E2E8F0",
            DrawerIcon = "#94A3B8",
            Background = "#0F172A", // Slate 900
            Surface = "#1E293B", // Slate 800
            TextPrimary = "#E2E8F0",
            TextSecondary = "#94A3B8",
            ActionDefault = "#94A3B8",
            ActionDisabled = "#475569",
            ActionDisabledBackground = "#334155",
            LinesDefault = "#334155", // Slate 700
            Divider = "#334155",
            TableLines = "#1E293B"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
            DrawerWidthLeft = "260px"
        },
        Shadows = new Shadow
        {
            Elevation = new[]
            {
                "none",
                "0 1px 2px 0 rgba(0, 0, 0, 0.05)", // Soft shadow
                "0 1px 3px 0 rgba(0, 0, 0, 0.1), 0 1px 2px 0 rgba(0, 0, 0, 0.06)",
                "0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06)",
                "0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05)",
                "0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)",
                "0 25px 50px -12px rgba(0, 0, 0, 0.25)",
                "none", "none", "none", "none", "none", "none", "none", "none", "none", "none", "none", "none", "none", "none", "none", "none", "none", "none", "none" // Clear higher elevations to enforce flat look if needed
            }
        },
        Typography = new Typography
        {
            Default = new Default
            {
                // Pretendard Variable — 한글 최적화 가변 폰트 (App.razor에서 CDN 로드)
                FontFamily = new[] { "Pretendard Variable", "Pretendard", "-apple-system", "BlinkMacSystemFont", "Segoe UI", "Roboto", "sans-serif" },
                FontSize = ".875rem",
                FontWeight = 400,
                LineHeight = 1.6,
                LetterSpacing = "-0.01em" // Pretendard 권장 자간 (한글)
            },
            H1 = new H1 { FontSize = "2.5rem", FontWeight = 800, LineHeight = 1.2, LetterSpacing = "-0.02em" },
            H2 = new H2 { FontSize = "2rem", FontWeight = 700, LineHeight = 1.2, LetterSpacing = "-0.02em" },
            H3 = new H3 { FontSize = "1.75rem", FontWeight = 700, LineHeight = 1.25, LetterSpacing = "-0.02em" },
            H4 = new H4 { FontSize = "1.5rem", FontWeight = 700, LineHeight = 1.3, LetterSpacing = "-0.02em" },
            H5 = new H5 { FontSize = "1.25rem", FontWeight = 600, LineHeight = 1.3, LetterSpacing = "-0.01em" },
            H6 = new H6 { FontSize = "1rem", FontWeight = 600, LineHeight = 1.4, LetterSpacing = "-0.01em" },
            Subtitle1 = new Subtitle1 { FontWeight = 600 },
            Button = new Button { FontWeight = 600, TextTransform = "none" } // No uppercase buttons
        }
    };
}
