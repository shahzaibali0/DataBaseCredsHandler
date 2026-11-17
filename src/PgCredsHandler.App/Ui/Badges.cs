using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PgCredsHandler.Core.Models;
using PgCredsHandler.Core.Util;

namespace PgCredsHandler.App.Ui;

/// <summary>Small view helpers used from x:Bind, so templates need no converters.</summary>
public static class Badges
{
    /// <summary>The badge colour for a host kind, from Themes/Styles.xaml.</summary>
    public static Brush HostBrush(HostKind hostKind) => (Brush)Application.Current.Resources[hostKind switch
    {
        HostKind.Local => "HostLocalBrush",
        HostKind.DockerService => "HostDockerBrush",
        HostKind.Private => "HostPrivateBrush",
        _ => "HostRemoteBrush",
    }];

    public static string FormatCount(int count) => count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);

    public static Visibility IsFalse(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Segoe Fluent Icons: an eye to show, a struck-out eye to hide.</summary>
    public static string EyeGlyph(bool isRevealed) => isRevealed ? "" : "";

    /// <summary>Code for a backend, a globe for a frontend, a folder for anything else.</summary>
    public static string RoleGlyph(AppRole role) => role switch
    {
        AppRole.Backend => "",
        AppRole.Frontend => "",
        _ => "",
    };
}
