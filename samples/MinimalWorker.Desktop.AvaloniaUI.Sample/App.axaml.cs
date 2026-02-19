using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MinimalWorker.Desktop.AvaloniaUI.Sample.ViewModels;

namespace MinimalWorker.Desktop.AvaloniaUI.Sample;

public class App : Application
{
    public required WorkerStateService WorkerState { get; init; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(WorkerState)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
