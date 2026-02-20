using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalWorker;
using MinimalWorker.Desktop.Wpf.Sample.ViewModels;

namespace MinimalWorker.Desktop.Wpf.Sample;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder(e.Args);
        builder.Services.AddSingleton<WorkerStateService>();

        _host = builder.Build();

        // 1) Periodic worker — every 1 second
        _host.RunPeriodicBackgroundWorker(TimeSpan.FromSeconds(1), (WorkerStateService state) =>
        {
            state.RecordExecution("Periodic", $"Tick at {DateTime.Now:HH:mm:ss}");
        });

        // 2) Slow worker — every 5 seconds (simulates heavier work)
        _host.RunPeriodicBackgroundWorker(TimeSpan.FromSeconds(5), async (WorkerStateService state) =>
        {
            await Task.Delay(200); // simulate async work
            state.RecordExecution("SlowTask", $"Completed at {DateTime.Now:HH:mm:ss}");
        });

        // 3) Cron worker — every minute ("* * * * *")
        _host.RunCronBackgroundWorker("* * * * *", (WorkerStateService state) =>
        {
            state.RecordExecution("Cron", $"Fired at {DateTime.Now:HH:mm:ss}");
        });

        await _host.StartAsync();

        var workerState = _host.Services.GetRequiredService<WorkerStateService>();
        var mainWindow = new MainWindow
        {
            DataContext = new MainWindowViewModel(workerState)
        };
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
