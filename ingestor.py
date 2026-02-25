import time
import json
import redis
from nba_api.live.nba.endpoints import scoreboard

print("--- DEBUG: Starting Script ---")

try:
    r = redis.Redis(host='localhost', port=6379, decode_responses=True)
    r.ping() # This checks if Redis is actually alive
    print("--- DEBUG: Redis Connected Successfully ---")
except Exception as e:
    print(f"--- DEBUG: Redis Connection Failed: {e} ---")

def fetch_live_scores():
    print("--- DEBUG: Attempting to fetch NBA data... ---")
    try:
        # 2. Ask the NBA for today's games
        board = scoreboard.ScoreBoard()
        data = board.get_dict()
        
        # 3. Check if there are actually games today
        live_games = data['scoreboard']['games']
        print(f"--- DEBUG: Found {len(live_games)} games today ---")
        
        if len(live_games) == 0:
            print("--- DEBUG: No games found in the API response. ---")

        for game in live_games:
            simplified_game = {
                "id": game['gameId'],
                "home": game['homeTeam']['teamName'],
                "away": game['awayTeam']['teamName'],
                "score": f"{game['awayTeam']['score']} - {game['homeTeam']['score']}",
                "status": game['gameStatusText']
            }
            
            r.publish('nba_scores', json.dumps(simplified_game))
            print(f"Success: {simplified_game['away']} vs {simplified_game['home']}")

    except Exception as e:
        print(f"--- DEBUG: NBA API Error: {e} ---")

# The Loop
while True:
    fetch_live_scores()
    print("--- DEBUG: Sleeping for 10 seconds... ---")
    time.sleep(10)