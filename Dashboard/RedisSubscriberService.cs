using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Dashboard;

public class RedisSubscriberService : BackgroundService
{
    private readonly IHubContext<ScoreHub> _hubContext;
    private readonly IConnectionMultiplexer _redis;
    private readonly object _scoreJsonLock = new();
    private string? _lastScoreJson;

    public RedisSubscriberService(IHubContext<ScoreHub> hubContext, IConnectionMultiplexer redis)
    {
        _hubContext = hubContext;
        _redis = redis;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _redis.GetSubscriber();

        await subscriber.SubscribeAsync("nba_scores", async (channel, message) =>
        {
            await ProcessMessage(message.ToString(), pushArbHistory: true, triggeredBy: "scores");
        });

        await subscriber.SubscribeAsync("nba_odds", async (channel, message) =>
        {
            var db = _redis.GetDatabase();
            var scoreSnapshot = await db.StringGetAsync("latest_nba_score");
            string? scoreJson = scoreSnapshot.IsNullOrEmpty ? null : scoreSnapshot.ToString();
            if (string.IsNullOrWhiteSpace(scoreJson))
            {
                lock (_scoreJsonLock)
                {
                    scoreJson = _lastScoreJson;
                }
            }

            if (string.IsNullOrWhiteSpace(scoreJson))
                return;

            await ProcessMessage(scoreJson, pushArbHistory: false, triggeredBy: "odds");
        });

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessMessage(string scoreJson, bool pushArbHistory, string triggeredBy)
    {
        var db = _redis.GetDatabase();

        string? oddsJson = await db.StringGetAsync("latest_nba_odds");

        var scores = JsonConvert.DeserializeObject<JArray>(scoreJson) ?? new JArray();
        if (scores.Count > 0)
        {
            lock (_scoreJsonLock)
            {
                _lastScoreJson = scoreJson;
            }
        }

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

            // Extract the home spread (e.g., -5.5) from spreads market.
            // Robust approach:
            // - prefer the outcome that matches the home team
            // - if home outcome isn't found, derive it as the negative of the away outcome (common spreads symmetry)
            var spreadsMarket = gameOdds?["bookmakers"]?.FirstOrDefault()?["markets"]?
                .FirstOrDefault(m => string.Equals(m?["key"]?.ToString(), "spreads", StringComparison.OrdinalIgnoreCase));

            var outcomes = spreadsMarket?["outcomes"] as JArray;

            static double ParsePoint(JToken? token)
            {
                if (token == null) return 0d;
                return token.Type switch
                {
                    JTokenType.Integer => token.Value<double>(),
                    JTokenType.Float => token.Value<double>(),
                    JTokenType.String when double.TryParse(token.Value<string>(), out var s) => s,
                    _ => 0d
                };
            }

            double spread = 0d;
            if (outcomes != null && outcomes.Count > 0)
            {
                var homeOutcome = outcomes.FirstOrDefault(outcome =>
                    (outcome?["name"]?.ToString() ?? string.Empty).ToUpperInvariant().Contains(home));

                var awayOutcome = outcomes.FirstOrDefault(outcome =>
                    (outcome?["name"]?.ToString() ?? string.Empty).ToUpperInvariant().Contains(away));

                var homePoint = ParsePoint(homeOutcome?["point"]);
                if (homePoint != 0d)
                {
                    spread = homePoint;
                }
                else
                {
                    var awayPoint = ParsePoint(awayOutcome?["point"]);
                    // If away has +5.5 (away gets points), home is -5.5 (home lays points).
                    spread = awayPoint != 0d ? -awayPoint : 0d;
                }
            }

            var scoreDiff = Math.Abs(homeScore - awayScore);
            var isUnderdogWinning =
                (spread > 0 && homeScore > awayScore) ||
                (spread < 0 && awayScore > homeScore);

            var arbAlert = isUnderdogWinning && scoreDiff > 5;

            if (pushArbHistory && arbAlert)
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
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            TriggeredBy = triggeredBy
        };

        // 3. Broadcast the combined data to the Frontend via SignalR
        var finalJson = JsonConvert.SerializeObject(arbiterPayload);
        await _hubContext.Clients.All.SendAsync("ReceiveScore", finalJson);
    }
}