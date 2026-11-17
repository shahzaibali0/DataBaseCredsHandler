using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using PgCredsHandler.App.Pages;
using PgCredsHandler.Presentation;
using WinUIEx;

namespace PgCredsHandler.App;

/// <summary>The window: title bar, sidebar navigation and the page frame. All state lives in the viewmodel.</summary>
public sealed partial class MainWindow : Window
{
    private const int StartWidth = 1240;
    private const int StartHeight = 820;
    private const int MinimumWidth = 900;
    private const int MinimumHeight = 620;

    private static readonly Dictionary<string, Type> PagesByTag = new(StringComparer.Ordinal)
    {
        ["Databases"] = typeof(DatabasesPage),
        ["Privacy"] = typeof(PrivacyPage),
        ["Settings"] = typeof(SettingsPage),
    };

    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var windowManager = WindowManager.Get(this);
        windowManager.MinWidth = MinimumWidth;
        windowManager.MinHeight = MinimumHeight;
        this.SetWindowSize(StartWidth, StartHeight);
        this.CenterOnScreen();

        Navigation.SelectedItem = Navigation.MenuItems[0];
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem { Tag: string tag }) return;
        if (!PagesByTag.TryGetValue(tag, out var pageType) || ContentFrame.CurrentSourcePageType == pageType) return;

        ContentFrame.Navigate(pageType, _viewModel, new EntranceNavigationTransitionInfo());
    }
}
