# VitalStream

Real-time patient vital signs monitoring platform built as a .NET microservices system. Medical devices push readings to an ingest API; the data flows through Kafka into TimescaleDB for persistence and Redis for live streaming to connected clients via Server-Sent Events.

---

## Architecture Overview

```
Medical Devices
      │  POST /ingest/{deviceId}
      ▼
┌─────────────┐   Kafka (vital-readings)   ┌──────────────────┐
│   Ingest    │ ─────────────────────────► │   Aggregator     │
│  :8080      │                            │  (BackgroundSvc) │
└─────────────┘                            └────────┬─────────┘
                                                    │
                               ┌────────────────────┤
                               │                    │
                    Redis pub/sub             TimescaleDB
                  vitals:{patientId}          (bulk insert)
                               │
               ┌───────────────┴──────────┐
               │                          │
        ┌──────▼──────┐           ┌───────▼──────┐
        │    Query    │           │    Alerts    │
        │  :8081      │           │ (BackgroundSvc)│
        └─────────────┘           └──────────────┘
               │
        SSE /stream
        REST /patients/{id}/vitals/summary
        PATCH /alerts/{id}/ack
```

### Data Flow

1. **Device → Ingest**: Device sends a vital reading over HTTP with an HMAC-SHA256 signature in `X-Signature-256`. The Ingest service validates the signature and publishes the payload to Kafka using an idempotent producer (key = `deviceId`).

2. **Kafka → Aggregator**: The Aggregator consumes from the `vital-readings` topic (group `aggregator`). For each message it:
   - Immediately publishes to Redis channel `vitals:{patientId}` for low-latency fanout.
   - Accumulates into a batch of up to 100 readings and bulk-inserts into TimescaleDB via `unnest()`. Kafka offsets are committed only after a successful DB flush; on failure the batch is retried without committing.

3. **Redis → Query**: The Query service subscribes to the `vitals:*` Redis pattern. Incoming readings are placed into per-client `BoundedChannel<VitalReading>` queues and streamed to SSE consumers. Slow consumers drop the oldest entries rather than block the broadcaster.

4. **Redis → Alerts**: The Alerts service also subscribes to `vitals:*`. Per-patient `RingBuffer`s hold up to 20,000 readings. A `TrendDetector` evaluates a 10-minute sliding window: if the second-half SpO2 mean has fallen more than 2 percentage points below the first-half mean (min. 4 readings), a Critical alert is fired. A 10-minute per-patient cooldown prevents alert storms. Alerts are stored in Redis and can be acknowledged via the Query API.

---

## Services

| Service | Port (local) | Role |
|---|---|---|
| `VitalStream.Ingest` | 8080 | Device-facing HTTP ingest |
| `VitalStream.Aggregator` | — | Kafka consumer → TimescaleDB + Redis |
| `VitalStream.Query` | 8081 | Client-facing HTTP query + SSE |
| `VitalStream.Alerts` | — | Trend detection and alert management |
| `VitalStream.Shared` | — | Shared models, enums, domain logic |

---

## API Reference

### Ingest Service (`POST :8080`)

| Method | Path | Description |
|---|---|---|
| `POST` | `/ingest/{deviceId}` | Ingest a single vital reading. Validates `X-Signature-256` HMAC header. |
| `POST` | `/ingest/batch/{deviceId}` | Ingest a batch of readings (strict HMAC validation). |
| `GET` | `/healthz` | Liveness probe |
| `GET` | `/ready` | Readiness probe |

**Request body** (`POST /ingest/{deviceId}`):
```json
{
  "patientId": "uuid",
  "deviceId": "string",
  "deviceType": "string",
  "type": "HeartRate | SpO2 | BloodPressureSystolic | BloodPressureDiastolic | Temperature",
  "value": 98.6,
  "unit": "string",
  "recordedAt": "ISO 8601",
  "correlationId": "uuid",
  "notes": "string | null"
}
```

**Security**: `X-Signature-256: sha256=<hmac>` — HMAC-SHA256 of the raw request body using the device-specific secret from `DeviceSecrets:{deviceId}` configuration.

---

### Query Service (`GET/PATCH :8081`)

| Method | Path | Description |
|---|---|---|
| `GET` | `/stream` | Server-Sent Events stream of live vital readings |
| `GET` | `/patients/{patientId}/vitals/summary` | 4-hour rolling statistics per vital type |
| `PATCH` | `/alerts/{alertId}/ack` | Acknowledge a clinical alert (idempotent) |
| `GET` | `/metrics` | Prometheus gauge: `sse_active_connections` |
| `GET` | `/healthz` | Liveness probe |
| `GET` | `/ready` | Readiness probe |

**SSE stream query parameters** (`GET /stream`):

| Parameter | Type | Description |
|---|---|---|
| `patientId` | `uuid` | Filter by patient (optional) |
| `type` | `VitalType` | Filter by vital type (optional) |
| `severity` | `VitalSeverity` | Filter by severity level (optional) |

**Summary response** (`GET /patients/{patientId}/vitals/summary`):
```json
{
  "patientId": "uuid",
  "windowStart": "ISO 8601",
  "windowEnd": "ISO 8601",
  "byType": [
    {
      "type": "HeartRate",
      "count": 1440,
      "p50": 72.0,
      "p90": 88.0,
      "p95": 94.0,
      "p99": 102.0,
      "min": 58.0,
      "max": 110.0
    }
  ]
}
```

---

## Domain Model

### Vital Types

| Enum | Description |
|---|---|
| `HeartRate` | Beats per minute |
| `SpO2` | Blood oxygen saturation (%) |
| `BloodPressureSystolic` | Systolic blood pressure (mmHg) |
| `BloodPressureDiastolic` | Diastolic blood pressure (mmHg) |
| `Temperature` | Body temperature (°C) |

### Clinical Thresholds

Severity is computed automatically on each `VitalReading` by `ClinicalThresholds.Evaluate`.

| Vital | Warning | Critical |
|---|---|---|
| HeartRate | < 60 or > 100 bpm | < 40 or > 150 bpm |
| SpO2 | < 95% | < 90% |
| BloodPressureSystolic | < 90 or > 140 mmHg | < 70 or > 180 mmHg |
| Temperature | < 36.0 or > 38.0 °C | < 35.0 or > 40.0 °C |

### Alert Detection

The Alerts service fires a `Critical` alert when a **sustained SpO2 fall > 2 percentage points** is detected within a 10-minute sliding window (requires ≥ 4 readings). A per-patient 10-minute cooldown prevents repeated alerts for the same event.

---

## Technology Stack

| Layer | Technology |
|---|---|
| Language / Runtime | C# 13 / .NET 9 |
| Web framework | ASP.NET Core Minimal APIs (`WebApplication.CreateSlimBuilder`) |
| Message broker | Apache Kafka (KRaft mode, 12 partitions) |
| Kafka client | `Confluent.Kafka` |
| Cache / Pub-Sub | Redis 7 (Alpine) via `StackExchange.Redis` |
| Time-series database | TimescaleDB (PostgreSQL 16 extension) |
| Real-time delivery | Server-Sent Events (ASP.NET Core `TypedResults.ServerSentEvents`) |
| Serialization | `System.Text.Json` with AOT-compatible source generators |
| Containerization | Docker / Docker Compose |
| Orchestration | Kubernetes with Horizontal Pod Autoscaler |
| Metrics | Prometheus text format (`/metrics`) |

---

## Infrastructure

### Docker Compose (local development)

```bash
docker compose up --build
```

| Container | Image | Exposed port |
|---|---|---|
| `broker` | `apache/kafka:latest` | 9092 |
| `timescaledb` | `timescale/timescaledb:latest-pg16` | 5432 |
| `redis` | `redis:7-alpine` | 6379 |
| `vitalstream.ingest` | built locally | 8080 |
| `vitalstream.query` | built locally | 8081 |
| `vitalstream.aggregator` | built locally | — |
| `vitalstream.alerts` | built locally | — |

The TimescaleDB schema is applied automatically on first start via `db/init.sql`. The `vital_readings` hypertable is partitioned by `recorded_at` in 1-day chunks with indexes on `(patient_id, recorded_at DESC)` and `(patient_id, type, recorded_at DESC)`.

### Kubernetes

Manifests are in `k8s/`. All services are deployed in the `vitalstream` namespace.

| Manifest | Description |
|---|---|
| `namespace.yaml` | Namespace definition |
| `infra.yaml` | Kafka, Redis, TimescaleDB StatefulSets/Deployments |
| `ingest.yaml` | Ingest Deployment (2 replicas) + Service |
| `aggregator.yaml` | Aggregator Deployment + Service |
| `query.yaml` | Query Deployment + Service |
| `alerts.yaml` | Alerts Deployment + Service |
| `query-hpa.yaml` | HPA for Query service |
| `clinical-thresholds-configmap.yaml` | ConfigMap with threshold configuration |

#### Query Service Auto-Scaling (HPA)

The Query service scales between **2 and 20 replicas** based on two metrics:

- **CPU utilization** — target 70% average.
- **SSE connections** — target 40,000 active connections per pod. The `sse_active_connections` gauge is scraped from `GET /metrics` by a Prometheus adapter and exposed as a custom metric to the HPA controller.

Scale-up adds 2 pods per minute with a 30-second stabilization window. Scale-down removes 1 pod per 2 minutes with a 5-minute stabilization window (SSE sessions are long-lived).

---

## Project Structure

```
vital-stream/
├── src/
│   ├── VitalStream.Shared/          # Domain models, enums, clinical thresholds, HMAC validator
│   ├── VitalStream.Ingest/          # Device-facing ingest API
│   ├── VitalStream.Aggregator/      # Kafka consumer → TimescaleDB + Redis
│   ├── VitalStream.Query/           # Client-facing query API + SSE
│   ├── VitalStream.Alerts/          # Trend detection and alerting
│   └── VItalStream.UnitTests/       # Unit tests (thresholds, HMAC)
├── db/
│   └── init.sql                     # TimescaleDB schema
├── k8s/                             # Kubernetes manifests
├── compose.yaml                     # Docker Compose for local dev
└── vital-stream.sln
```