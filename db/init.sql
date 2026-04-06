-- VitalStream TimescaleDB schema
-- Runs automatically on first container start via the docker-entrypoint-initdb.d mount.

CREATE EXTENSION IF NOT EXISTS timescaledb;

-- Main hypertable — partitioned by recorded_at (1-day chunks)
CREATE TABLE IF NOT EXISTS vital_readings (
    patient_id      UUID            NOT NULL,
    device_id       TEXT            NOT NULL,
    device_type     TEXT            NOT NULL,
    type            INTEGER         NOT NULL,   -- VitalType enum value
    value           DOUBLE PRECISION NOT NULL,
    unit            TEXT            NOT NULL,
    recorded_at     TIMESTAMPTZ     NOT NULL,
    received_at     TIMESTAMPTZ     NOT NULL,
    severity        INTEGER         NOT NULL,   -- VitalSeverity enum value
    correlation_id  UUID            NOT NULL,
    notes           TEXT,
    UNIQUE (patient_id, recorded_at, correlation_id)
);

SELECT create_hypertable('vital_readings', by_range('recorded_at'), if_not_exists => TRUE);

-- Query patterns: per-patient time-series and per-patient+type lookups
CREATE INDEX IF NOT EXISTS idx_vr_patient_time
    ON vital_readings (patient_id, recorded_at DESC);

CREATE INDEX IF NOT EXISTS idx_vr_patient_type_time
    ON vital_readings (patient_id, type, recorded_at DESC);