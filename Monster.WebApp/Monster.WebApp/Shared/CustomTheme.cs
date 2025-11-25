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
            DrawerBackground = "#1E293B", // Slate 800 (Dark Sidebar)
            DrawerText = "#F8FAFC", // Slate 50
            DrawerIcon = "#CBD5E1", // Slate 300
            Background = "#F1F5F9", // Slate 100 (Light Gray Background)
            Surface = "#FFFFFF",
            TextPrimary = "#0F172A", // Slate 900
            TextSecondary = "#64748B", // Slate 500
            ActionDefault = "#64748B",
            ActionDisabled = "#94A3B8",
            ActionDisabledBackground = "#E2E8F0"
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
            ActionDisabledBackground = "#334155"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
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
                FontFamily = new[] { "Segoe UI", "Roboto", "Helvetica Neue", "Arial", "sans-serif" },
                FontSize = ".875rem",
                FontWeight = 400,
                LineHeight = 1.6,
                LetterSpacing = ".005em"
            },
            H1 = new H1 { FontSize = "2.5rem", FontWeight = 700, LineHeight = 1.2 },
            H2 = new H2 { FontSize = "2rem", FontWeight = 700, LineHeight = 1.2 },
            H3 = new H3 { FontSize = "1.75rem", FontWeight = 700, LineHeight = 1.2 },
            H4 = new H4 { FontSize = "1.5rem", FontWeight = 700, LineHeight = 1.2 },
            H5 = new H5 { FontSize = "1.25rem", FontWeight = 600, LineHeight = 1.2 },
            H6 = new H6 { FontSize = "1rem", FontWeight = 600, LineHeight = 1.2 },
            Button = new Button { FontWeight = 600, TextTransform = "none" } // No uppercase buttons
        }
    };
}
