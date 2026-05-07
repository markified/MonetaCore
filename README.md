# MonetaCore

MonetaCore is a web-based billing, invoicing, and revenue management system built on ASP.NET Core MVC.

## Recreated System Architecture

The recreated modular architecture package is documented here:

- docs/architecture/system-architecture.md
- docs/architecture/module-data-api-blueprint.md
- docs/architecture/event-catalog.md
- docs/architecture/implementation-roadmap.md

## Phase 0 and 1 Scaffold APIs

The initial implementation scaffold adds:

- Customer portal endpoints under `/api/portal`
   - `GET /api/portal/invoices`
   - `GET /api/portal/payments`
   - `GET /api/portal/receipts/{paymentId}`
   - `GET /api/portal/disputes`
   - `POST /api/portal/disputes`
- Compliance endpoints under `/api/compliance`
   - `POST /api/compliance/tax/calculate`
   - `GET /api/compliance/tax/rules`
   - `GET /api/compliance/currency/rates`

Phase 0 outbox foundation is implemented via `OutboxMessage` and `IEventOutboxService`, with `OutboxDispatcherBackgroundService` dispatching pending events at runtime.

## Quick Start (Local)

1. Restore and build:
   - dotnet build
2. Configure local secrets (recommended):
   - dotnet user-secrets set "PayMongo:SecretKey" "<your-secret-key>"
3. Run the app:
   - dotnet run
4. Open the app in your browser using the URL shown in the console.

## Default Accounts

- Super Admin: superadmin@monetacore.local / SuperAdmin@123
- Main Admin: admin@monetacore.local / Admin@123
- Finance Manager: finance@monetacore.local / Finance@123
- Billing Staff: billing@monetacore.local / Billing@123
- Accountant: accountant@monetacore.local / Accountant@123
- Auditor: auditor@monetacore.local / Auditor@123
- Client: client@monetacore.local / Client@123

## Configuration

Use secure configuration sources in this order:

1. System Configuration (Super Admin UI)
2. User Secrets (development)
3. Environment Variables
4. appsettings.json (non-sensitive defaults only)

Do not commit secrets to source control.

- ConnectionStrings:DefaultConnection
- PayMongo:BaseUrl
- PayMongo:SecretKey
- Integrations:AccountingApiBaseUrl
- Integrations:OutboxEventsApiUrl
- Integrations:AuthMode
- Integrations:ApiKeyHeaderName
- Integrations:ApiKey
- Integrations:BearerToken
- OutboxDispatcher:RetryBaseDelaySeconds
- OutboxDispatcher:RetryMaxDelaySeconds
- OutboxDispatcher:RetryJitterSeconds

Environment variable examples (prefixed):

- MONETACORE_ConnectionStrings__DefaultConnection
- MONETACORE_PayMongo__SecretKey
- MONETACORE_Integrations__ApiKey
- MONETACORE_Integrations__BearerToken

System Configuration values override appsettings for PayMongo Secret Key and Accounting API base URL.

`ApiDocumentation:Enabled` controls Swagger UI in non-development environments.

Outbox dispatch posts domain events to `Integrations:OutboxEventsApiUrl` when configured. If this is empty, the dispatcher falls back to `Integrations:AccountingApiBaseUrl`.
Connector authentication supports `None`, `ApiKey`, and `Bearer` via the `Integrations:AuthMode` setting.

## API Integration Notes

- PayMongo payments run live only when a valid secret key is provided.
- Accounting sync runs live only when AccountingApiBaseUrl is provided.
- Swagger/OpenAPI is available at `/api/docs` when enabled.
- Liveness endpoint is available at `/healthz`.

### Payments API Method Rules

- `POST /api/payments` accepts only `Cash` and `PayMongo` for `method`.
- When `method` is `PayMongo`, `payMongoFlow` accepts `Checkout` or `Card`.

Example payload (cash):

```json
{
   "invoiceId": 101,
   "amount": 1250.00,
   "method": "Cash",
   "referenceNumber": "OR-000123",
   "notes": "Counter payment"
}
```

Example payload (PayMongo checkout):

```json
{
   "invoiceId": 101,
   "amount": 1250.00,
   "method": "PayMongo",
   "referenceNumber": "",
   "notes": "Online payment",
   "payMongoFlow": "Checkout"
}
```

## Outbox Dead-Letter API

The Integrations outbox API provides filtered/paged dead-letter listing and replay operations with correlation IDs.

- `GET /api/integrations/outbox/dead-letters`
   - Query params:
      - `eventType` (optional): partial event type filter
      - `search` (optional): matches EventType, Producer, CorrelationId, LastError, or exact EventId GUID
      - `page` (optional, default `1`)
      - `pageSize` (optional, range `1-100`, default `20`)
- `POST /api/integrations/outbox/{id}/replay`
- `POST /api/integrations/outbox/replay-all`

Example dead-letter query:

```http
GET /api/integrations/outbox/dead-letters?eventType=Invoice&search=REPLAY-BATCH&page=1&pageSize=20
```

Example dead-letter page response:

```json
{
   "items": [
      {
         "id": 51,
         "eventId": "c5cf8aa3-3cf0-47c0-b87e-20dbdc17983f",
         "eventType": "InvoiceCreated",
         "correlationId": "REPLAY-BATCH-20260501174500-3f4c7b52b8db4fce80ca5486366a06f1",
         "attemptCount": 4,
         "lastError": "Connector timeout",
         "createdAtUtc": "2026-05-01T17:25:10Z",
         "lastAttemptedAtUtc": "2026-05-01T17:44:59Z",
         "nextAttemptAtUtc": null
      }
   ],
   "page": 1,
   "pageSize": 20,
   "totalCount": 3,
   "totalPages": 1,
   "eventTypeFilter": "Invoice",
   "searchFilter": "REPLAY-BATCH"
}
```

Example replay response:

```json
{
   "id": 51,
   "status": "Pending",
   "attemptCount": 0,
   "nextAttemptAtUtc": "2026-05-01T17:45:00Z",
   "replayCorrelationId": "REPLAY-SINGLE-20260501174500-4fe0b2e3591e41d79ce83a6d5d86f2a9",
   "message": "Dead-letter event queued for replay. Correlation ID: REPLAY-SINGLE-20260501174500-4fe0b2e3591e41d79ce83a6d5d86f2a9"
}
```

The same replay correlation ID is persisted on replayed outbox messages and copied into resulting integration events for end-to-end tracing.

## Publish / Deployment

1. Build a release package:
   - dotnet publish -c Release -o publish
2. Run the published app:
   - dotnet publish\MonetaCore.dll
3. Set ASPNETCORE_URLS if you want a custom port or binding.

For hosting, use IIS, a Windows service, or a supported Linux hosting provider.

### CI Pipeline

GitHub Actions workflow at `.github/workflows/ci.yml` runs restore, build, and tests on pushes and pull requests.
