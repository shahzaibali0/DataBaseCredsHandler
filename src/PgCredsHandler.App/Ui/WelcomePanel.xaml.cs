using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PgCredsHandler.Presentation;

namespace PgCredsHandler.App.Ui;

/// <summary>The first-run hero and the three promise cards.</summary>
public sealed partial class WelcomePanel : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(MainViewModel), typeof(WelcomePanel), new PropertyMetadata(null));

    public WelcomePanel() => InitializeComponent();

    public MainViewModel? ViewModel
    {
        get => (MainViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
}
