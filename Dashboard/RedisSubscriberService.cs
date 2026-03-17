using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        var scores = JsonConvert.DeserializeObject<JArray>(scoreJson) ?? new JArray();
        var odds = string.IsNullOrEmpty(oddsJson) ? new JArray() : (JsonConvert.DeserializeObject<JArray>(oddsJson) ?? new JArray());

        foreach (var gameToken in scores)
        {
            var home = (gameToken?["home"]?.ToString() ?? string.Empty).ToUpperInvariant();
            var away = (gameToken?["away"]?.ToString() ?? string.Empty).ToUpperInvariant();
            var scoreStr = gameToken?["score"]?.ToString() ?? "0 - 0";

            var parts = scoreStr.Split(" - ", StringSplitOptions.TrimEntries);
            var awayScore = (parts.Length > 0 && int.TryParse(parts[0], out var a)) ? a : 0;
            var homeScore = (parts.Length > 1 && int.TryParse(parts[1], out var h)) ? h : 0;

            // Match odds by team names (mirrors the frontend's simplified matching)
            var gameOdds = odds.FirstOrDefault(o =>
                ((o?["home_team"]?.ToString() ?? string.Empty).ToUpperInvariant().Contains(home)) ||
                ((o?["away_team"]?.ToString() ?? string.Empty).ToUpperInvariant().Contains(away))
            ) as JObject;

            // Extract the home spread (e.g., -5.5) from spreads market
            var spreadToken =
                gameOdds?["bookmakers"]?.FirstOrDefault()?["markets"]?
                    .FirstOrDefault(m => string.Equals(m?["key"]?.ToString(), "spreads", StringComparison.OrdinalIgnoreCase))?["outcomes"]?
                    .FirstOrDefault(outcome => (outcome?["name"]?.ToString() ?? string.Empty).ToUpperInvariant().Contains(home))?["point"];

            var spread = spreadToken?.Type switch
            {
                JTokenType.Integer => spreadToken.Value<double>(),
                JTokenType.Float => spreadToken.Value<double>(),
                JTokenType.String when double.TryParse(spreadToken.Value<string>(), out var s) => s,
                _ => 0d
            };

            var scoreDiff = Math.Abs(homeScore - awayScore);
            var isUnderdogWinning =
                (spread > 0 && homeScore > awayScore) ||
                (spread < 0 && awayScore > homeScore);

            var arbAlert = isUnderdogWinning && scoreDiff > 5;

            if (arbAlert)
            {
                var historyItem = new
                {
                    Game = $"{away} vs {home}",
                    Value = "HIGH",
                    Time = DateTime.Now.ToString("HH:mm:ss")
                };

                await db.ListLeftPushAsync("arb_history", JsonConvert.SerializeObject(historyItem));
                await db.ListTrimAsync("arb_history", 0, 9);
            }
        }

        var historyRaw = await db.ListRangeAsync("arb_history", 0, 9);
        var history = historyRaw
            .Select(x => x.ToString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x =>
            {
                try { return JsonConvert.DeserializeObject(x); }
                catch { return null; }
            })
            .Where(x => x is not null)
            .ToList();

        // 2. Create a "Composite" object - this is the Arbiter's payload
        var arbiterPayload = new
        {
            LiveScores = scores,
            MarketOdds = odds.Count == 0 ? null : odds,
            History = history,
            Timestamp = DateTime.Now.ToString("HH:mm:ss")
        };

        // 3. Broadcast the combined data to the Frontend via SignalR
        var finalJson = JsonConvert.SerializeObject(arbiterPayload);
        await _hubContext.Clients.All.SendAsync("ReceiveScore", finalJson);
    }
}