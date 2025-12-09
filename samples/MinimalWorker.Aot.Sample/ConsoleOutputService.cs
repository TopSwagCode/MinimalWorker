namespace MinimalWorker.Aot.Sample;

public interface IConsoleOutputService
{
    Task WriteLineAsync(string message);
}

public class ConsoleOutputService : IConsoleOutputService
{
    private Guid _guid;
    public ConsoleOutputService()
    {
        _guid = Guid.NewGuid();
    }
    public async Task WriteLineAsync(string message)
    {
        await Task.Delay(1);
        System.Console.WriteLine(message + _guid );
    }
}
