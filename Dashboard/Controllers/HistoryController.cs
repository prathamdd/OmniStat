using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using Newtonsoft.Json;

namespace Dashboard.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HistoryController : ControllerBase
{
    private readonly IConnectionMultiplexer _redis;

    public HistoryController(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory()
    {
        var db = _redis.GetDatabase();
        var history = await db.ListRangeAsync("arb_history", 0, 9);

        var result = history
            .Select(x => x.ToString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => JsonConvert.DeserializeObject(x))
            .ToList();

        return Ok(result);
    }
}
