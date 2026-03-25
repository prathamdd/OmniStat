# OmniStat: Real-Time Arbitrage Engine

**OmniStat** is a high-frequency data pipeline and analytics dashboard designed to identify market discrepancies (Arbitrage) in live sports betting. By synchronizing live **"Ground Truth"** scores with **"Market Expectation"** odds, the system identifies high-value opportunities as they happen.



---

## Architecture
The system is built as a **containerized microservices architecture** to ensure sub-second latency and fault tolerance:

* **Data Ingestors (Python):** Concurrent workers fetching live NBA scores and betting spreads from external REST APIs.
* **State Management (Redis):** Acts as a high-speed unified data bus between the Python workers and the .NET backend.
* **Arbiter Core (.NET 9):** A background service that monitors Redis for updates, normalizes team data, and calculates **"Arb Alerts"** based on live scoring deltas.
* **Real-Time UI (SignalR & JS):** A reactive dashboard that uses **Server-Sent Events** to push updates to the client without page refreshes.

---

## Tech Stack
| Layer | Technology |
| :--- | :--- |
| **Backend** | .NET 9 (C#), SignalR |
| **Data Workers** | Python 3.9, Requests |
| **Database/Cache** | Redis (Dockerized) |
| **Frontend** | Vanilla JS, Modern CSS (Grid/Flexbox) |
| **Orchestration** | Docker, Docker Compose |

---

## The Philosophy of the Arbiter
The system treats **Expectation** ($E$) and **Reality** ($R$) as two distinct data streams. An **"Arb Alert"** is triggered when the delta $|R - E|$ exceeds a predefined threshold, signaling that the market (the betting line) has failed to account for a sudden shift in the live game state.



---

## Quick Start
Ensure you have **Docker** installed, then run:

```bash
docker-compose up --build
