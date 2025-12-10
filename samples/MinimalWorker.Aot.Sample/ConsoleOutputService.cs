namespace MinimalWorker.Aot.Sample;

public interface IConsoleOutputService
{
    Task WriteLineAsync(string message);
}

public class ConsoleOutputService : IConsoleOutputService
{
    private readonly Guid _guid;
    public ConsoleOutputService()
    {
        _guid = Guid.NewGuid();
    }
    public async Task WriteLineAsync(string message)
    {
        await Task.Delay(1);
        System.Console.WriteLine(message + " - Scoped Guid: " + _guid );
    }
}


public interface IMissingDependency
{
    Task WriteLineAsync(string message);
}

public class MissingDependency : IMissingDependency
{
    public Task WriteLineAsync(string message)
    {
        Console.WriteLine(message);
        return Task.CompletedTask;
    }
}