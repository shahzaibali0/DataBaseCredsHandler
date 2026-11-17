using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PgCredsHandler.Presentation;

namespace PgCredsHandler.App.Pages;

/// <summary>Where to scan, appearance and about.</summary>
public sealed partial class SettingsPage : Page
{
    public SettingsPage() => InitializeComponent();

    public MainViewModel ViewModel { get; private set; } = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel = (MainViewModel)e.Parameter;

    // A folder row's template can't reach the page's viewmodel through x:Bind, so its button routes here.
    private void OnRemoveFolderClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string folder }) ViewModel.RemoveFolderCommand.Execute(folder);
    }
}
