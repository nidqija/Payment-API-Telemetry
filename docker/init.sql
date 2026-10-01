CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Matches Model.Merchant
CREATE TABLE IF NOT EXISTS merchants (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    email VARCHAR(255) NOT NULL UNIQUE,
    webhook_url TEXT,
    webhook_secret VARCHAR(64) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Matches Model.ApiKey
CREATE TABLE IF NOT EXISTS api_keys (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    merchant_id UUID NOT NULL REFERENCES merchants(id) ON DELETE CASCADE,
    key_prefix VARCHAR(16) NOT NULL,
    short_id VARCHAR(16) NOT NULL,
    hashed_secret VARCHAR(64) NOT NULL,
    environment VARCHAR(10) NOT NULL DEFAULT 'test',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_used_at TIMESTAMPTZ
);

-- Index for the auth middleware lookup
CREATE INDEX IF NOT EXISTS idx_api_keys_short_id ON api_keys(short_id) WHERE is_active = TRUE;


