import redis
import json
import logging
import time

# 1. Setup Logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(message)s')

# 2. Connect to Redis
try:
    r = redis.Redis(host='localhost', port=6379, decode_responses=True)
    r.ping()
    logging.info("✅ Listener connected to Redis.")
except Exception as e:
    logging.error(f"❌ Listener could not connect: {e}")
    exit()

# 3. Setup the Radio (PubSub)
pubsub = r.pubsub()
pubsub.subscribe('nba_scores')

logging.info("--- 🎧 LISTENER ACTIVE: Waiting for NBA scores... ---")

# 4. The "Blocking" Loop
# This keeps the script running forever, waiting for a message
while True:
    try:
        # Check for a message every 1 second
        message = pubsub.get_message()
        
        if message and message['type'] == 'message':
            # We found a message!
            data = json.loads(message['data'])
            
            # Clean Print
            print(f"🏀 {data['last_update']} | {data['away']} ({data['score']}) @ {data['home']} | {data['status']}")
            
        # Don't melt the CPU, wait 1 second before checking again
        time.sleep(1)
        
    except Exception as e:
        logging.error(f"Listener Error: {e}")
        time.sleep(5)