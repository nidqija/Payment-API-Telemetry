# 🚀 PaymentGateway.Api - v1.4.0

> **OpenTelemetry Distributed Tracing, Observability Pipeline & Automated Dev Releases**  
> *Released: Oct 09 - Oct 18, 2026* • *Trigger: Merged to `master`* • *Commit: `70d7232`*

---

### 📋 Developer Release Notes & Highlights

#### 🔭 OpenTelemetry Tracing & Domain Metrics

- **End-to-End OpenTelemetry SDK & OTLP Exporter Integration**
  Integrated official OpenTelemetry runtime packages (`OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, and `Npgsql.OpenTelemetry`). Registered unified `PaymentGateway.Api` service identity and configured OTLP export via gRPC to `http://otel:4317`. Automatically suppresses noisy background polling on `/health` and `/dashboard` endpoints to guarantee high-signal telemetry.

- **Domain Performance Metrics via System.Diagnostics.Metrics**
  Built static `ApiTracingService` exposing a dedicated `PaymentGateway.Core` Meter. Tracks throughput via `payment.processed.count` (Counter), measures cache efficiency via `invoice.cache.hit.count` and `invoice.cache.miss.count` (Counters), and records granular PDF generation latencies with `invoice.render.duration_ms` and `invoice.render.amount_ms` (Histograms).

- **Fine-Grained Activity Tracing & Events in Invoice Engine**
  Instrumented `InvoiceService.GetOrGenerateInvoicePdf` using `ActivitySource.StartActivity("GetOrGenerateInvoicePdf")`. Injects semantic tags (`payment.id`, `cache.hit`, `invoice.size_bytes`, `pdf.render.duration_ms`), emits chronological activity events (`redis_cache_lookup_started`, `pdf_generation_started`), and sets explicit `ActivityStatusCode.Ok` / `Error` statuses for end-to-end distributed trace tracking across Redis and PostgreSQL.

#### 🐳 Telemetry Infrastructure & Orchestration

- **OpenTelemetry Collector Service & Multi-Port Networking**
  Integrated `otel/opentelemetry-collector-contrib:latest` into `docker-compose.yml`. Exposes OTLP gRPC (`4317`), OTLP HTTP (`4318`), Prometheus internal metrics (`8888`), and health check (`13133`) ports. Authored `configs/otel-collector-config.yaml` with batch processing pipelines (1s timeout, 256 batch size) and detailed debug exporters.

- **Nginx Reverse Proxy Architecture & Tuning Annotations**
  Documented `configs/nginx.conf` with inline explanations for reverse proxy routing, keepalive connection pooling (32 connections), 10-second client timeouts, standard reverse proxy forwarding headers, and custom buffer allocations specifically sized for QuestPDF document transmission.

#### 🚀 Developer Release Automation & CI/CD

- **Automated Dev Release Pipeline via GitHub Actions**
  Created `.github/workflows/dev_release.yml` running on merges to `master`. Automates .NET 9 compilation, binary zip packaging (`paymentgateway-api-{version}.zip`), Docker dual-tag builds (`:dev` and `:{version}`), and seamless GitHub Release generation with release notes.

- **Dynamic Release Notes Generator Tooling**
  Built `scripts/generate_release_notes.py` to dynamically parse the latest release section from `CHANGELOG.html`, generating formatted `RELEASE_NOTES.md` and setting GITHUB_OUTPUT environment values for release jobs.


---

### 🔨 Commits in this Release
- `70d7232` - added new port expose to docker compose and refined code structure via comment as explanation for otel service
- `51f80c6` - added otel config on program mount file and defined metric variables for tracing
- `00a6846` - added comments on nginx config and init file for ApiTracingServices
- `a1c8a72` - fix: resolve yaml syntax error by externalizing release notes generator script
- `a42e748` - added dev release github action scripts

---

### 📦 Artifacts
- **Binary Package**: `paymentgateway-api-v1.4.0.zip`
- **Interactive Changelog**: `CHANGELOG.html`
- **Docker Tags**: `paymentgateway-api:dev`, `paymentgateway-api:v1.4.0`
