using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using Newtonsoft.Json;

namespace Dashboard;

public class RedisSubscriberService : BackgroundService
{
    private readonly IHubContext<ScoreHub> _hubContext;
    private readonly IConnectionMultiplexer _redis;

    public RedisSubscriberService(IHubContext<ScoreHub> hubContext, IConnectionMultiplexer redis)
    {
        _hubContext = hubContext;
        _redis = redis;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _redis.GetSubscriber();

        // Subscribe to the same channel the Python ingestor publishes to
        await subscriber.SubscribeAsync("nba_scores", async (channel, message) =>
        {
            // Each time Python publishes live scores, process and enrich with odds
            await ProcessMessage(message.ToString());
        });

        // Keep the service alive
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessMessage(string scoreJson)
    {
        var db = _redis.GetDatabase();

        // 1. Fetch the 'Expectation' (Odds) while we have the 'Reality' (Scores)
        string? oddsJson = await db.StringGetAsync("latest_nba_odds");

        // 2. Create a "Composite" object - this is the Arbiter's payload
        var arbiterPayload = new
        {
            LiveScores = JsonConvert.DeserializeObject(scoreJson),
            MarketOdds = string.IsNullOrEmpty(oddsJson) ? null : JsonConvert.DeserializeObject(oddsJson),
            Timestamp = DateTime.Now.ToString("HH:mm:ss")
        };

        // 3. Broadcast the combined data to the Frontend via SignalR
        var finalJson = JsonConvert.SerializeObject(arbiterPayload);
        await _hubContext.Clients.All.SendAsync("ReceiveScore", finalJson);
    }
}