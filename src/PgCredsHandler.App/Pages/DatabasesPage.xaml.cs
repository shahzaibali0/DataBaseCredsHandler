using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using PgCredsHandler.Presentation;

namespace PgCredsHandler.App.Pages;

/// <summary>Welcome, scan progress and the databases grouped by project.</summary>
public sealed partial class DatabasesPage : Page
{
    public DatabasesPage() => InitializeComponent();

    public MainViewModel ViewModel { get; private set; } = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel = (MainViewModel)e.Parameter;

    private void OnSearchShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        SearchBox.Focus(FocusState.Keyboard);
        args.Handled = true;
    }
}
