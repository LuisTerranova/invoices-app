using MudBlazor;

namespace invoices.front.blazor.Theme;

public static class CustomTheme
{
    public static readonly MudTheme Default = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#C87A4F",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#2A2F33",
            SecondaryContrastText = "#FFFFFF",
            Tertiary = "#8D8070",
            TertiaryContrastText = "#FFFFFF",
            Background = "#F4F0E6",
            BackgroundGray = "#EAE4D5",
            Surface = "#FFFFFF",
            AppbarBackground = "rgba(244, 240, 230, 0.8)",
            AppbarText = "#2A2F33",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#36332E",
            DrawerIcon = "#C87A4F",
            TextPrimary = "#36332E",
            TextSecondary = "#7A746B",
            TextDisabled = "#A69F94",
            Error = "#D05353",
            ErrorContrastText = "#FFFFFF",
            Info = "#5682A3",
            InfoContrastText = "#FFFFFF",
            Success = "#52796F",
            SuccessContrastText = "#FFFFFF",
            Warning = "#D89A4D",
            WarningContrastText = "#FFFFFF",
            Dark = "#2A2F33",
            DarkContrastText = "#F4F0E6",
            ActionDefault = "#C87A4F",
            ActionDisabled = "#A69F94",
            ActionDisabledBackground = "#E0D7C6",
            LinesDefault = "#E0D7C6",
            LinesInputs = "#D1C6B4",
            TableLines = "#E0D7C6",
            TableStriped = "#F0EAE0",
            TableHover = "#EAE4D5",
            Divider = "#E0D7C6",
            DividerLight = "#EAE4D5",
            Skeleton = "#E0D7C6",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Inter", "Helvetica", "Arial", "sans-serif" },
            },
            H1 = new H1Typography { FontFamily = new[] { "Outfit", "sans-serif" }, FontWeight = "700" },
            H2 = new H2Typography { FontFamily = new[] { "Outfit", "sans-serif" }, FontWeight = "700" },
            H3 = new H3Typography { FontFamily = new[] { "Outfit", "sans-serif" }, FontWeight = "700" },
            H4 = new H4Typography { FontFamily = new[] { "Outfit", "sans-serif" }, FontWeight = "700" },
            H5 = new H5Typography { FontFamily = new[] { "Outfit", "sans-serif" }, FontWeight = "600" },
            H6 = new H6Typography { FontFamily = new[] { "Outfit", "sans-serif" }, FontWeight = "600", FontSize = "1.125rem" },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "16px",
        },
    };
}
