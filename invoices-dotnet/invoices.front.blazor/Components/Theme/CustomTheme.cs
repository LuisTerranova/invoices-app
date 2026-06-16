using MudBlazor;

namespace invoices.front.blazor.Theme;

public static class CustomTheme
{
    public static readonly MudTheme Default = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#C17A4A",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#8B7D6B",
            SecondaryContrastText = "#FFFFFF",
            Tertiary = "#6B8E7A",
            TertiaryContrastText = "#FFFFFF",
            Background = "#F0EBE0",
            BackgroundGray = "#E8E2D5",
            Surface = "#FFFFFF",
            AppbarBackground = "#2D2B36",
            AppbarText = "#D6D0C4",
            DrawerBackground = "#2D2B36",
            DrawerText = "#D6D0C4",
            DrawerIcon = "#C17A4A",
            TextPrimary = "#1C1B22",
            TextSecondary = "#5C5A68",
            TextDisabled = "#9D9AAB",
            Error = "#C1404A",
            ErrorContrastText = "#FFFFFF",
            Info = "#5B7FA6",
            InfoContrastText = "#FFFFFF",
            Success = "#4F8B6A",
            SuccessContrastText = "#FFFFFF",
            Warning = "#C17A4A",
            WarningContrastText = "#FFFFFF",
            Dark = "#2D2B36",
            DarkContrastText = "#D6D0C4",
            ActionDefault = "#C17A4A",
            ActionDisabled = "#9D9AAB",
            ActionDisabledBackground = "#E8E2D5",
            LinesDefault = "#E8E2D5",
            LinesInputs = "#C8BFAF",
            TableLines = "#D6CFC0",
            TableStriped = "#F6F2EB",
            TableHover = "#EDE7DC",
            Divider = "#D6CFC0",
            DividerLight = "#E8E2D5",
            Skeleton = "#E8E2D5",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Inter", "Helvetica", "Arial", "sans-serif" },
            },
            H4 = new H4Typography { FontWeight = "700" },
            H5 = new H5Typography { FontWeight = "600" },
            H6 = new H6Typography { FontWeight = "600", FontSize = "1.125rem" },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px",
        },
    };
}
