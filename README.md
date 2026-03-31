# OmniStat

Real-time NBA **scores**, **betting lines**, and a small **“Arbiter”** layer on top: SignalR live updates, a simple **win-probability** readout, and a **Historical Value** feed for rare player stat lines. Python and .NET share state through **Redis** (pub/sub + key/value).

---

## What it does

| Piece | Role |
|--------|------|
| **Score ingestor** (`Ingestor/ingestor.py`) | Polls live NBA data (`nba_api`), normalizes games, publishes to Redis channel `nba_scores`, stores `latest_nba_score`. Optionally includes per-game **leaders** (points / rebounds / assists) for feat detection. |
| **Odds ingestor** (`Ingestor/odds_ingestor.py`) | Fetches spreads from **The Odds API**, stores `latest_nba_odds`, publishes `nba_odds` so the dashboard can refresh without waiting for the next score tick. |
| **Dashboard** (`Dashboard/`) | .NET 9 app: subscribes to Redis, merges scores + odds, runs light **edge / feat** logic, broadcasts JSON over **SignalR** (`/scoreHub`), exposes REST (`/scores`, `/api/history`, `/ping`). |
| **UI** (`index.html`) | Static page (e.g. Live Server) that connects to `http://localhost:5116/scoreHub` and renders cards + history. |

---

## Tech stack

- **Python 3** — `nba_api`, `redis`, `requests`
- **Redis** — pub/sub (`nba_scores`, `nba_odds`) + strings/lists (`latest_nba_score`, `latest_nba_odds`, `arb_history`, feat de-dupe keys)
- **.NET 9** — Minimal API, SignalR, StackExchange.Redis, Newtonsoft.Json
- **Docker / Docker Compose** — one command to run Redis + dashboard + both ingestors

---

## Quick start (recommended)

From the repo root:

```bash
docker compose up --build
```

Services:

- **Redis:** `localhost:6379`
- **Dashboard:** `http://localhost:5116`
- **Score ingestor** and **odds ingestor** run against `REDIS_HOST=redis`

Open `index.html` in the browser (or VS Code Live Server). The page expects the API at **`http://localhost:5116`** (SignalR hub: `/scoreHub`).

### Odds API key

Set `ODDS_API_KEY` for the odds service (e.g. in `docker-compose.yml` under `odds-ingestor.environment`, or your shell). The ingestor falls back to a placeholder if unset; a missing or invalid key will prevent useful lines.

### Polling

- Scores: ~every **10s** (see `Ingestor/ingestor.py`).
- Odds: **`ODDS_POLL_SECONDS`** (default **300**; compose example uses **120** for fresher updates — uses more Odds API quota).

---

## Configuration (high level)

| Variable | Where | Purpose |
|----------|--------|---------|
| `REDIS_HOST`, `REDIS_PORT` | Python + optional .NET | Redis hostname/port (`redis` in Docker, `localhost` locally). |
| `DEMO_MODE` | `Ingestor/ingestor.py` | `True` = single mock game (good for demos when no live slate). |
| `ODDS_API_KEY` | `Ingestor/odds_ingestor.py` | The Odds API key. |
| `ODDS_POLL_SECONDS` | `Ingestor/odds_ingestor.py` | Seconds between odds fetches. |

---

## Local development (without Compose)

1. Start Redis (e.g. `docker run --name sports-redis -p 6379:6379 -d redis`).
2. **Scores:** `cd Ingestor && pip install -r requirements.txt && python3 ingestor.py`
3. **Odds:** `python3 odds_ingestor.py` (set `ODDS_API_KEY` if needed).
4. **Dashboard:** `cd Dashboard && dotnet run` → listens on **`http://localhost:5116`** (see `Properties/launchSettings.json`).

---

## HTTP endpoints

| Route | Description |
|--------|-------------|
| `GET /scores` | Latest JSON snapshot from Redis key `latest_nba_score`. |
| `GET /api/history` | Last entries from Redis list `arb_history` (alerts + rare feats). |
| `GET /ping` | Health check. |
| SignalR hub `/scoreHub` | Event `ReceiveScore` — composite payload: live games, market odds, optional history, timestamps, `TriggeredBy` (`scores` \| `odds`). |

---

## Features (current behavior)

- **Live scoreboard** with team logos (Loodibee + NBA CDN fallback) and spread / FAV vs UND display.
- **Model win probability** on each card: **logistic prior from the home spread** (negative spread = home favored, so pregame leans that way) plus a **live margin** term — demo-oriented, not a production sportsbook model.
- **Historical Value** (`arb_history`): logs rare **player** lines when leader stats cross thresholds (**50+ PTS**, **20+ REB**, **15+ AST**), with Redis de-duplication per game/player/stat. Older “HIGH” line-edge rows may still appear if present in the list.
- **TriggeredBy** in the payload so you can see whether the last push was driven by scores or odds.

---

## Repository layout (main files)

```
OmniStat/
├── docker-compose.yml      # redis + dashboard + ingestors
├── index.html              # dashboard UI (SignalR client)
├── Ingestor/
│   ├── ingestor.py         # scores + leaders
│   ├── odds_ingestor.py    # odds → Redis + pub/sub
│   ├── requirements.txt
│   └── Dockerfile
└── Dashboard/
    ├── Program.cs          # CORS, Redis, SignalR, controllers
    ├── RedisSubscriberService.cs
    ├── Controllers/HistoryController.cs
    └── Dockerfile
```

---

## What I learned

- **Pub/sub + shared keys** let you add ingestors without tightly coupling them to the web tier.
- **Cross-language services** (Python producers, .NET consumer) are a practical pattern for streaming sports data.
- **Docker Compose** collapses multi-terminal setup into a single reproducible stack.
