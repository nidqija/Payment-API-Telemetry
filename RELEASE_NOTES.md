# 🚀 PaymentGateway.Api - v1.5.0

> **Merchant Webhook Callback Engine, Live Polling Dashboard & Dynamic Cloudflare Ingress Resolution**  
> *Released: Oct 25, 2026* • *Trigger: Merged to `master`* • *Commit: `92c93a5`*

---

### 📋 Developer Release Notes & Highlights

#### 🔔 Webhook Callback Engine & Delivery Pipeline

- **Asynchronous Merchant Webhook Delivery with HMAC-SHA256 Signatures**
  Built the `WebhookDispatch` service using typed `HttpClient` to deliver cryptographically signed webhook notifications (`X-Signature` with HMAC-SHA256, `X-Event-Type`) upon payment completion. Automatically logs delivery duration, HTTP status code, request headers, payload, and response bodies into PostgreSQL's `webhook_callback` table for full delivery auditability.

- **Dynamic Ingress & Ephemeral Cloudflare Tunnel Resolution**
  Resolved runtime ingress mapping by mounting `./configs:/app/configs:ro` into the API container. Dynamically reads active `APP_BASE_URL` from `configs/.env` on request to support ephemeral Cloudflare Tunnel endpoints without container rebuilds or stale boot-time environment variables. Safely falls back to internal `http://nginx:80` for container-to-container routing.

- **Live Webhook Deliveries Dashboard & Polling API**
  Implemented `GET /v1/webhooks` to fetch recent deliveries and created an interactive, dark-themed real-time monitoring page (`/webhooks` via Nginx) with automatic 2-second live polling, status badge indicators (`status-2xx`, `status-err`), formatted JSON payloads, and delivery latency metrics. Added `POST /test-webhook` mock receiver endpoint for zero-dependency end-to-end verification.


---

### 🔨 Commits in this Release
- `92c93a5` - feat(webhook): add webhook callback dispatch, logging dashboard, and dynamic tunnel resolution
- `e0c9e4f` - added webhook callback insertion upon completed payment transactions
- `ba19425` - added webhook dispatch and sql transactions for webhook operations
- `70dd0c9` - added callback url dispatch load init

---

### 📦 Artifacts
- **Binary Package**: `paymentgateway-api-v1.5.0.zip`
- **Interactive Changelog**: `CHANGELOG.html`
- **Docker Tags**: `paymentgateway-api:dev`, `paymentgateway-api:v1.5.0`
