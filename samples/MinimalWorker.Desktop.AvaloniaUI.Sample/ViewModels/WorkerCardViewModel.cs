using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MinimalWorker.Desktop.AvaloniaUI.Sample.ViewModels;

public class WorkerCardViewModel(string label) : INotifyPropertyChanged
{
    private int _executionCount;
    private string _lastRunTime = "--:--:--";
    private string _statusMessage = "Waiting...";

    public string Label { get; } = label;

    public int ExecutionCount
    {
        get => _executionCount;
        set => SetField(ref _executionCount, value);
    }

    public string LastRunTime
    {
        get => _lastRunTime;
        set => SetField(ref _lastRunTime, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public void UpdateFrom(WorkerInfo info)
    {
        ExecutionCount = info.ExecutionCount;
        LastRunTime = info.LastRunTime?.ToString("HH:mm:ss") ?? "--:--:--";
        StatusMessage = info.StatusMessage;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}