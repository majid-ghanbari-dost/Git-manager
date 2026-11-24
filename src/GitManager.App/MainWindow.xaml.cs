using GitManager.App.ViewModels;
using Microsoft.Win32;

namespace GitManager.App;

public partial class MainWindow : System.Windows.Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
    }
}
