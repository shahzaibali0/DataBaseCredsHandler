using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DbConnectHub.Presentation;

namespace DbConnectHub.App.Ui;

/// <summary>The project window's content: ports, database connections, stack and key packages.</summary>
public sealed partial class ProjectDetailPanel : UserControl
{
    public static readonly DependencyProperty ProjectProperty =
        DependencyProperty.Register(nameof(Project), typeof(WorkspaceViewModel), typeof(ProjectDetailPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(MainViewModel), typeof(ProjectDetailPanel), new PropertyMetadata(null));

    public ProjectDetailPanel() => InitializeComponent();

    public WorkspaceViewModel? Project
    {
        get => (WorkspaceViewModel?)GetValue(ProjectProperty);
        set => SetValue(ProjectProperty, value);
    }

    public MainViewModel? ViewModel
    {
        get => (MainViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => ViewModel?.CloseProjectCommand.Execute(null);

    // A port row's template can't reach the viewmodel through x:Bind, so its button routes here.
    private void OnOpenUrlClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url }) ViewModel?.OpenLocalUrlCommand.Execute(url);
    }
}
