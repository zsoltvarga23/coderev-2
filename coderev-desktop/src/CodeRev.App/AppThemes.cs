using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace CodeRev.App;

/// <summary>
/// Custom theme variants. Retro falls back to Light for any resource it does
/// not override, so only the era-specific brushes need a Retro entry in
/// App.axaml's theme dictionaries.
/// </summary>
public static class AppThemes
{
    /// <summary>Early-2000s (Windows 2000/XP era) look: beige surfaces, beveled
    /// controls, square corners, Tahoma.</summary>
    public static readonly ThemeVariant Retro = new("Retro", ThemeVariant.Light);

    /// <summary>True while the app runs with the Retro variant.</summary>
    public static bool IsRetro => Application.Current?.RequestedThemeVariant == Retro;

    /// <summary>
    /// Mirrors the active variant onto a window as the "retro" style class.
    /// The variant covers colours app-wide, but the retro <em>shape</em> styles
    /// (bevels, square corners, Tahoma) are scoped to `Window.retro`, so every
    /// window — dialogs included — must call this once when constructed.
    /// </summary>
    public static void ApplyWindowClass(Window window) =>
        window.Classes.Set("retro", IsRetro);
}
