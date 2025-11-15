using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace Danidrum;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = App.Services.GetRequiredService<MainWindowViewModel>();


            
    }
}