using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace Dashboard;

public class RedisSubscriberService : BackgroundService
{
    private readonly IHubContext<ScoreHub> _hubContext;
    private readonly IConnectionMultiplexer _redis;

    public RedisSubscriberService(IHubContext<ScoreHub> hubContext)
    {
        _hubContext = hubContext;
        _redis = ConnectionMultiplexer.Connect("localhost");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = _redis.GetSubscriber();

        // This listens to the EXACT same channel Python is shouting into
        await db.SubscribeAsync("nba_scores", async (channel, message) =>
        {
            // The moment Python 'Publishes', this code runs!
            await _hubContext.Clients.All.SendAsync("ReceiveScore", message.ToString());
        });

        // Keep the service alive
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}