import time
import json
import redis
import logging
from nba_api.live.nba.endpoints import scoreboard

# 1. SETUP LOGGING
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler("ingestor.log"),
        logging.StreamHandler()
    ]
)

logging.info("--- 🚀 Day 2: Starting Resilient Ingestor ---")

# 2. REDIS CONNECTION
try:
    r = redis.Redis(host='localhost', port=6379, decode_responses=True)
    r.ping()
    logging.info("✅ Redis Connection: SUCCESS")
except redis.ConnectionError as e:
    logging.error(f"❌ Redis Connection: FAILED. Is Docker running? Error: {e}")
    exit()

def fetch_live_scores():
    logging.info("Attempting to fetch NBA data...")
    try:
        # 3. ASK NBA FOR DATA
        board = scoreboard.ScoreBoard()
        data = board.get_dict()
        
        # 4. SAFETY CHECK
        if 'scoreboard' not in data or 'games' not in data['scoreboard']:
            logging.warning("NBA API returned empty or unexpected data.")
            return 

        live_games = data['scoreboard']['games']
        
        if not live_games:
            logging.info("No games currently scheduled.")
            return

        # 5. PROCESS THE GAMES (Fixed Indentation here!)
        for game in live_games:
            # 1. CLEANING
            home_team = game.get('homeTeam', {}).get('teamName', 'Unknown').upper()
            away_team = game.get('awayTeam', {}).get('teamName', 'Unknown').upper()
            
            # 2. SCHEMA
            simplified_game = {
                "game_id": game.get('gameId'),
                "home": home_team,
                "away": away_team,
                "score": f"{game.get('awayTeam', {}).get('score', 0)} - {game.get('homeTeam', {}).get('score', 0)}",
                "status": game.get('gameStatusText', 'N/A'),
                "last_update": time.strftime("%H:%M:%S")
            }
            
            r.publish('nba_scores', json.dumps(simplified_game))
            logging.info(f"✅ Published: {away_team} vs {home_team} at {simplified_game['last_update']}")

    except Exception as e:
        logging.error(f"Connection issue or API timeout: {e}. Retrying soon...")

# 7. THE HEARTBEAT LOOP
if __name__ == "__main__":
    while True:
        fetch_live_scores()
        logging.info("Sleeping for 10 seconds...")
        time.sleep(10)