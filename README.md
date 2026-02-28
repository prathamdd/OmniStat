# 🏀 OmniStat: The Live Odds Arbiter
**A Real-Time Sports Data Pipeline using Python, Redis, and .NET 9.**

## 🎯 The Goal
To build a high-performance, distributed system that ingests live NBA scores and broadcasts them to a real-time dashboard. This project demonstrates mastery of **Message Brokers**, **Asynchronous Ingestion**, and **Cross-Platform Systems**.

---

## 🏗️ The Architecture (Week 1: The Bridge)
Current progress: **Phase 1 Complete**. The system follows a **Producer/Subscriber (Pub/Sub)** pattern.

1.  **The Ingestor (Python):** A resilient script that polls the NBA-API every 10 seconds. It cleans, standardizes, and timestamps the data.
2.  **The Broker (Redis):** A high-performance, in-memory data store running in a **Docker** container. It acts as the "Radio Tower" for live updates.
3.  **The Listener (Python/Integration Test):** A secondary service that subscribes to Redis and processes live updates instantly without polling the database.



---

## 🛠️ Tech Stack (Week 1)
* **Language:** Python 3.9+
* **Database:** Redis (running via Docker)
* **Libraries:** `nba_api`, `redis-py`, `logging`
* **DevOps:** Docker, Git/GitHub

---

## 🚀 How to Run (Local Dev)

### 1. Start the Data Broker (Redis)
Ensure Docker is running and execute:
```bash
docker run --name sports-redis -p 6379:6379 -d redis