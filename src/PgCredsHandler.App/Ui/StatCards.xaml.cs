using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PgCredsHandler.Presentation;

namespace PgCredsHandler.App.Ui;

/// <summary>The three counts above the project list: projects, apps and databases.</summary>
public sealed partial class StatCards : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(MainViewModel), typeof(StatCards), new PropertyMetadata(null));

    public StatCards() => InitializeComponent();

    public MainViewModel? ViewModel
    {
        get => (MainViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
}
