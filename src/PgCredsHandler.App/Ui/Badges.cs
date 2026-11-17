using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
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
}
