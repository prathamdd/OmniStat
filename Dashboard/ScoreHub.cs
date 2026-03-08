using Microsoft.AspNetCore.SignalR;

namespace Dashboard;

// This class is the 'Radio Tower'
public class ScoreHub : Hub
{
    public async Task SendScore(string scoreJson)
    {
        // This sends the score to EVERYONE connected
        await Clients.All.SendAsync("ReceiveScore", scoreJson);
    }
}