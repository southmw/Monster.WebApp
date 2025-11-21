using MudBlazor;

namespace Monster.WebApp.Shared;

public static class CustomTheme
{
    public static MudTheme Theme => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#6366f1",
            Secondary = "#ec4899",
            Success = "#10b981",
            Info = "#06b6d4",
            Warning = "#f59e0b",
            Error = "#ef4444",
            AppbarBackground = "#ffffff",
            AppbarText = "#1f2937",
            DrawerBackground = "#f9fafb",
            DrawerText = "#1f2937",
            Background = "#ffffff",
            Surface = "#ffffff",
            TextPrimary = "#1f2937",
            TextSecondary = "#6b7280"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#818cf8",
            Secondary = "#f472b6",
            Success = "#34d399",
            Info = "#22d3ee",
            Warning = "#fbbf24",
            Error = "#f87171",
            AppbarBackground = "#1e293b",
            AppbarText = "#e2e8f0",
            DrawerBackground = "#1e293b",
            DrawerText = "#e2e8f0",
            Background = "#0f172a",
            Surface = "#1e293b",
            TextPrimary = "#e2e8f0",
            TextSecondary = "#94a3b8"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px"
        },
        Shadows = new Shadow
        {
            Elevation = new[]
            {
                "none",
                "0 1px 2px 0 rgba(0, 0, 0, 0.05)",
                "0 1px 3px 0 rgba(0, 0, 0, 0.1), 0 1px 2px 0 rgba(0, 0, 0, 0.06)",
                "0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06)",
                "0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05)",
                "0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)",
                "0 25px 50px -12px rgba(0, 0, 0, 0.25)",
                "0 2px 4px -1px rgba(0, 0, 0, 0.06), 0 4px 6px -1px rgba(0, 0, 0, 0.1)",
                "0 4px 8px -2px rgba(0, 0, 0, 0.1), 0 6px 12px -2px rgba(0, 0, 0, 0.1)",
                "0 8px 16px -4px rgba(0, 0, 0, 0.1), 0 6px 16px -4px rgba(0, 0, 0, 0.1)",
                "0 12px 24px -6px rgba(0, 0, 0, 0.1), 0 8px 20px -6px rgba(0, 0, 0, 0.1)",
                "0 16px 32px -8px rgba(0, 0, 0, 0.1), 0 10px 24px -8px rgba(0, 0, 0, 0.1)",
                "0 20px 40px -10px rgba(0, 0, 0, 0.1), 0 12px 28px -10px rgba(0, 0, 0, 0.1)",
                "0 24px 48px -12px rgba(0, 0, 0, 0.15), 0 14px 32px -12px rgba(0, 0, 0, 0.1)",
                "0 28px 56px -14px rgba(0, 0, 0, 0.15), 0 16px 36px -14px rgba(0, 0, 0, 0.1)",
                "0 32px 64px -16px rgba(0, 0, 0, 0.15), 0 18px 40px -16px rgba(0, 0, 0, 0.1)",
                "0 36px 72px -18px rgba(0, 0, 0, 0.15), 0 20px 44px -18px rgba(0, 0, 0, 0.1)",
                "0 40px 80px -20px rgba(0, 0, 0, 0.15), 0 22px 48px -20px rgba(0, 0, 0, 0.1)",
                "0 44px 88px -22px rgba(0, 0, 0, 0.15), 0 24px 52px -22px rgba(0, 0, 0, 0.1)",
                "0 48px 96px -24px rgba(0, 0, 0, 0.15), 0 26px 56px -24px rgba(0, 0, 0, 0.1)",
                "0 52px 104px -26px rgba(0, 0, 0, 0.15), 0 28px 60px -26px rgba(0, 0, 0, 0.1)",
                "0 56px 112px -28px rgba(0, 0, 0, 0.15), 0 30px 64px -28px rgba(0, 0, 0, 0.1)",
                "0 60px 120px -30px rgba(0, 0, 0, 0.15), 0 32px 68px -30px rgba(0, 0, 0, 0.1)",
                "0 64px 128px -32px rgba(0, 0, 0, 0.15), 0 34px 72px -32px rgba(0, 0, 0, 0.1)",
                "0 68px 136px -34px rgba(0, 0, 0, 0.15), 0 36px 76px -34px rgba(0, 0, 0, 0.1)",
                "0 72px 144px -36px rgba(0, 0, 0, 0.15), 0 38px 80px -36px rgba(0, 0, 0, 0.1)"
            }
        }
    };
}
