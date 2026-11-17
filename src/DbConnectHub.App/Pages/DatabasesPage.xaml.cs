using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using DbConnectHub.Presentation;

namespace DbConnectHub.App.Pages;

/// <summary>Welcome, scan progress, the square project cards, and the project window over them.</summary>
public sealed partial class DatabasesPage : Page
{
    public DatabasesPage() => InitializeComponent();

    public MainViewModel ViewModel { get; private set; } = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel = (MainViewModel)e.Parameter;

    // Leaving the page closes the project window and masks every password again.
    protected override void OnNavigatedFrom(NavigationEventArgs e) => ViewModel.CloseProjectCommand.Execute(null);

    private void OnSearchShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        SearchBox.Focus(FocusState.Keyboard);
        args.Handled = true;
    }

    private void OnEscape(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!ViewModel.IsProjectOpen) return;
        ViewModel.CloseProjectCommand.Execute(null);
        args.Handled = true;
    }

    // A card's template can't reach the page's viewmodel through x:Bind, so its click routes here.
    private void OnProjectClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: WorkspaceViewModel project }) ViewModel.OpenProjectCommand.Execute(project);
    }

    private void OnBackdropTapped(object sender, TappedRoutedEventArgs e) => ViewModel.CloseProjectCommand.Execute(null);
}
