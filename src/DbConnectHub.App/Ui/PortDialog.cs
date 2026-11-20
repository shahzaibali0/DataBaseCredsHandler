using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using DbConnectHub.Presentation;
using DbConnectHub.Scanner;

namespace DbConnectHub.App.Ui;

/// <summary>
/// Asks for a server's new port and shows, while the user types, exactly which file and value will change. The
/// change button only turns on when the change can be made.
/// </summary>
public static class PortDialog
{
    /// <summary>The port the user confirmed, or null when they cancelled.</summary>
    public static async Task<int?> AskAsync(XamlRoot xamlRoot, AppGroupViewModel app)
    {
        var portBox = new TextBox
        {
            Header = "New port",
            Text = app.LocalPort?.ToString() ?? "",
            InputScope = new InputScope { Names = { new InputScopeName(InputScopeNameValue.Number) } },
            MaxLength = 5,
        };
        var preview = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = $"Change the port of {app.Title}",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = $"Now {app.LocalUrlDisplay}, {app.LocalUrlSource.ToLowerInvariant()}.", TextWrapping = TextWrapping.Wrap },
                    portBox,
                    preview,
                },
            },
            PrimaryButtonText = "Change port",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        void ShowPreview()
        {
            var port = ParsePort(portBox.Text);
            var change = port is null ? new PortChange(false, "Type a port number.") : MainViewModel.PreviewPortChange(app, port.Value);
            preview.Text = change.Message;
            dialog.IsPrimaryButtonEnabled = change.IsPossible;
        }

        portBox.TextChanged += (_, _) => ShowPreview();
        ShowPreview();
        var answer = await dialog.ShowAsync();
        return answer == ContentDialogResult.Primary ? ParsePort(portBox.Text) : null;
    }

    private static int? ParsePort(string text) => int.TryParse(text.Trim(), out var port) ? port : null;
}
