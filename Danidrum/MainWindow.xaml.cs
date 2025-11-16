using Microsoft.Extensions.DependencyInjection;

namespace Danidrum;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = App.Services.GetRequiredService<MainWindowViewModel>();
    }
}