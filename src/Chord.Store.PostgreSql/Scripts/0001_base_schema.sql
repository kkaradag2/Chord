CREATE TABLE IF NOT EXISTS chord_flow_instances (
    id uuid PRIMARY KEY,
    flow_name text NOT NULL,
    correlation_id text NOT NULL UNIQUE,
    status text NOT NULL,
    started_at timestamptz NOT NULL,
    completed_at timestamptz NULL,
    duration_ms bigint NULL
);

CREATE TABLE IF NOT EXISTS chord_step_instances (
    id uuid PRIMARY KEY,
    flow_instance_id uuid NOT NULL REFERENCES chord_flow_instances(id) ON DELETE CASCADE,
    step_id text NOT NULL,
    status text NOT NULL,
    started_at timestamptz NOT NULL,
    completed_at timestamptz NULL,
    duration_ms bigint NULL
);

CREATE TABLE IF NOT EXISTS chord_message_logs (
    id uuid PRIMARY KEY,
    flow_instance_id uuid NOT NULL REFERENCES chord_flow_instances(id) ON DELETE CASCADE,
    step_id text NULL,
    direction text NOT NULL,
    queue_name text NOT NULL,
    headers jsonb NULL,
    created_at timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS chord_schema_version (
    version int PRIMARY KEY,
    applied_at timestamptz NOT NULL
);
