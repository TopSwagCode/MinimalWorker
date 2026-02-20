using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace MinimalWorker.Desktop.Wpf.Sample.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly WorkerStateService _state;
    private readonly Dispatcher _dispatcher;
    private string _uptime = "00:00:00";
    private string _latestActivity = "Starting up...";

    public WorkerCardViewModel Periodic { get; } = new("Periodic (1s)");
    public WorkerCardViewModel SlowTask { get; } = new("Slow Task (5s)");
    public WorkerCardViewModel Cron { get; } = new("Cron (every min)");

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
        _dispatcher = Dispatcher.CurrentDispatcher;

        _state.StateChanged += workerName =>
        {
            _dispatcher.BeginInvoke(() =>
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
