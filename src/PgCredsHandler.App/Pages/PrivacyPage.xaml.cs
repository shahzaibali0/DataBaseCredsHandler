using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PgCredsHandler.Presentation;

namespace PgCredsHandler.App.Pages;

/// <summary>What the app keeps, where, and how to verify nothing leaves the PC.</summary>
public sealed partial class PrivacyPage : Page
{
    public PrivacyPage() => InitializeComponent();

    public MainViewModel ViewModel { get; private set; } = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel = (MainViewModel)e.Parameter;
}
