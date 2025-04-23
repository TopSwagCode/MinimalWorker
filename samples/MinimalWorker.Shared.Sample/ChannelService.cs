using System.Threading.Channels;

namespace MinimalWorker.Shared.Sample;

public class ChannelService
{
    private readonly Channel<string> _stringChannel = System.Threading.Channels.Channel.CreateUnbounded<string>();

    public async Task SendNotificationAsync(string notification)
    {
        await _stringChannel.Writer.WriteAsync(notification);
    }

    public IAsyncEnumerable<string> ReadAllNotificationsAsync(System.Threading.CancellationToken stoppingToken)
    {
        return _stringChannel.Reader.ReadAllAsync(stoppingToken);
    }
}