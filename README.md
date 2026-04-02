
# **OmniStat: Real-Time Sports Arbitrage Engine**

**OmniStat** is a distributed data pipeline designed to identify betting market inefficiencies in real-time. The system synchronizes live **NBA game data** with **market odds** to calculate "Arb Alerts" when a significant discrepancy is detected between the live game state and the bookmaker's expectations.

## **System Architecture**

The project is built using a **microservices approach** to ensure low latency and service isolation:

* **Data Ingestors (Python):** Independent scripts that poll external **NBA** and **Betting APIs**. These services act as the entry point for raw data.
* **Message Broker (Redis):** Serves as a high-speed data bus. Python services **publish** raw JSON to **Redis channels**, which are then consumed by the backend.
* **Backend & Logic (.NET 9):** A **C# service** that subscribes to Redis, performs **data normalization**, and executes the "Arbiter" logic to determine high-value betting opportunities.
* **Real-Time Delivery (SignalR):** Utilizes **WebSockets** to push processed updates to the frontend instantly, eliminating the need for client-side polling.
* **Containerization (Docker):** The entire stack is orchestrated via **Docker Compose**, ensuring a consistent environment for the database, backend, and ingestors.

## **Tech Stack**

| Layer | Technology |
| :--- | :--- |
| **Languages** | **C#, Python, JavaScript** |
| **Backend Framework** | **.NET 9, ASP.NET Core** |
| **Real-Time Communication** | **SignalR (WebSockets)** |
| **Data Streaming** | **Redis Pub/Sub** |
| **Environment** | **Docker, Docker Compose** |


## **Setup and Installation**

1. Clone the repository.
2. Ensure **Docker Desktop** is running.
3. Run the following command in the root directory:
   ```bash
   docker-compose up --build
   ```
4. Access the dashboard at `http://localhost:5116`.
