

# OmniStat: Live NBA Data Pipeline

## Overview

OmniStat is a real-time data engineering project designed to fetch, process, and display live NBA scores. The goal of this project was to learn how to bridge different programming languages (**Python** and **C#**) using a high-performance message broker (**Redis**) and containerization (**Docker**).

This project simulates how real-world betting or sports apps handle thousands of live updates per second without crashing the main server.

---

## Tech Stack

* **Backend Ingestion:** Python 3.9+ (using `nba_api`)
* **Data Broker:** Redis (In-memory NoSQL database)
* **Infrastructure:** Docker (for containerizing Redis)
* **Web API:** .NET 9 (C#)
* **Version Control:** Git & GitHub

---

## How it Works

1. **The Ingestor (Python):** A script that polls the NBA API every 10 seconds. It cleans the data (standardizing team names to uppercase) and "shouts" it into a Redis channel.
2. **The Bridge (Redis):** Acts as the middleman. It stores the "latest_nba_score" so the dashboard can grab it instantly without having to for the API.
3. **The Dashboard API (.NET 9):** A high-performance C# web server that connects to Redis and serves the live data to a browser or frontend via a REST endpoint (`/scores`).

---

## Setup & Installation

### 1. Prerequisites

* [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed.
* [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) installed.
* Python installed.

### 2. Start Redis (The Database)

Run the following command to start the Redis container:

```bash
docker run --name sports-redis -p 6379:6379 -d redis

```

### 3. Run the Python Ingestor

Navigate to the `Ingestor` folder, install requirements, and run:

```bash
pip install redis nba_api
python3 ingestor.py

```

### 4. Run the .NET Dashboard

Navigate to the `Dashboard` folder and start the API:

```bash
dotnet run

```

You can now view the live data at: `http://localhost:5116/scores`

---

## What I Learned

* **Fault Tolerance:** I implemented logic to ensure the Python script doesn't crash if the internet or database goes down; it simply retries until the connection is restored.
* **Microservices:** Instead of one giant program, I split the project into two independent services that talk to each other through a broker.
* **Cross-Platform Integration:** Successfully shared data between a Python environment and a .NET environment.

---
