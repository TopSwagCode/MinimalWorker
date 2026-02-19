using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;

namespace MinimalWorker.Desktop.AvaloniaUI.Sample.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly WorkerStateService _state;
    private string _uptime = "00:00:00";
    private string _latestActivity = "Starting up...";

    private WorkerCardViewModel Periodic { get; } = new("Periodic (1s)");
    private WorkerCardViewModel SlowTask { get; } = new("Slow Task (5s)");
    private WorkerCardViewModel Cron { get; } = new("Cron (every min)");

    public string Uptime
    {
        get => _uptime;
        set => SetField(ref _uptime, value);
    }

    public string LatestActivity
    {
        get => _latestActivity;
        set => SetField(ref _latestActivity, value);
    }

    public MainWindowViewModel(WorkerStateService state)
    {
        _state = state;
        Console.WriteLine("[ViewModel] Created and subscribing to state changes.");

        _state.StateChanged += workerName =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var info = _state.GetOrAdd(workerName);
                var card = workerName switch
                {
                    "Periodic" => Periodic,
                    "SlowTask" => SlowTask,
                    "Cron" => Cron,
                    _ => null
                };
                card?.UpdateFrom(info);
                LatestActivity = $"[{workerName}] {info.StatusMessage}";
                Console.WriteLine($"[ViewModel] {workerName} updated — count={info.ExecutionCount}");
            });
        };

        var uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        uptimeTimer.Tick += (_, _) => Uptime = _state.Uptime.ToString(@"hh\:mm\:ss");
        uptimeTimer.Start();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
