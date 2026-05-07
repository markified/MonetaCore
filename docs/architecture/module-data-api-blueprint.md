# MonetaCore Module Data and API Blueprint

This blueprint defines how each core and complementary module maps to storage and service contracts.

## 1. Core Module Blueprint

### 1.1 Invoice Generation and Tracking
Business Ownership:
- Invoice lifecycle, line-item composition, total/balance authority, aging state.

Data Model:
- Existing: Invoice, InvoiceLineItem
- Additions: InvoiceTimelineEntry, InvoiceTemplateVersion, InvoiceAgingSnapshot, RecurrenceSchedule

API Contracts:
- Existing baseline: CRUD and tracking endpoints for invoices
- Additions:
  - GET /api/v2/invoices/{invoiceId}/timeline
  - POST /api/v2/invoices/{invoiceId}/issue
  - POST /api/v2/invoices/{invoiceId}/cancel
  - POST /api/v2/invoices/{invoiceId}/send

Published Events:
- InvoiceDraftCreated
- InvoiceIssued
- InvoiceOverdue
- InvoiceCancelled
- InvoiceBalanceUpdated

### 1.2 Payment Processing
Business Ownership:
- Payment initiation, gateway routing, settlement status, receipt issuance.

Data Model:
- Existing: PaymentTransaction
- Additions: PaymentAttempt, PaymentMethodToken, PaymentSettlement, PaymentWebhookEvent

API Contracts:
- Existing baseline: payment create/process endpoints
- Additions:
  - POST /api/v2/payments/attempts
  - POST /api/v2/payments/{paymentId}/capture
  - POST /api/v2/payments/webhooks/{provider}
  - GET /api/v2/payments/{paymentId}/receipt

Published Events:
- PaymentAttempted
- PaymentAuthorized
- PaymentCompleted
- PaymentFailed
- PaymentRefunded

### 1.3 Credit and Debit Management
Business Ownership:
- Credits/debits, approvals, impacts on invoice balance and revenue corrections.

Data Model:
- Existing: CreditDebitAdjustment
- Additions: AdjustmentApprovalStep, AdjustmentPolicySnapshot

API Contracts:
- Existing baseline: create/list adjustments
- Additions:
  - POST /api/v2/adjustments/{id}/approve
  - POST /api/v2/adjustments/{id}/reject
  - GET /api/v2/adjustments/{id}/history

Published Events:
- AdjustmentCreated
- AdjustmentApproved
- AdjustmentRejected
- AdjustmentApplied

### 1.4 Revenue Monitoring
Business Ownership:
- Revenue realization, receivable exposure, profitability and trend intelligence.

Data Model:
- Existing: derived from invoices/payments/adjustments
- Additions: RevenueDailyFact, ReceivablesSnapshot, ProfitabilitySnapshot

API Contracts:
- Existing baseline: dashboard and P&L reporting
- Additions:
  - GET /api/v2/revenue/kpis
  - GET /api/v2/revenue/forecast
  - GET /api/v2/revenue/aging

Published Events:
- RevenueSnapshotCreated
- RevenueThresholdBreached
- ForecastGenerated

### 1.5 Account Integration
Business Ownership:
- External accounting and business system sync with observable reliability state.

Data Model:
- Existing: AccountIntegrationEvent
- Additions: IntegrationConnector, IntegrationMapping, IntegrationSyncRun, IntegrationDeadLetter

API Contracts:
- Existing baseline: integration trigger/status workflows
- Additions:
  - POST /api/v2/integrations/{connector}/sync
  - GET /api/v2/integrations/runs/{runId}
  - POST /api/v2/integrations/dead-letter/{messageId}/replay

Published Events:
- IntegrationSyncQueued
- IntegrationSyncSucceeded
- IntegrationSyncFailed
- IntegrationReplayRequested

## 2. Complementary Module Blueprint

### 2.1 Customer Self-Service Portal
Data Model:
- PortalPreference, PortalReceiptAccessLog, CustomerDispute

API/UI Contracts:
- GET /portal/invoices
- GET /portal/payments
- GET /portal/receipts/{receiptId}
- POST /portal/disputes

Consumes:
- InvoiceIssued, PaymentCompleted, ReceiptGenerated

### 2.2 Multi-Currency and Tax Compliance Engine
Data Model:
- CurrencyRate, CurrencySourceSnapshot, TaxJurisdiction, TaxRule, TaxDeterminationRecord

API Contracts:
- POST /api/v2/compliance/tax/calculate
- GET /api/v2/compliance/tax/rules
- GET /api/v2/compliance/currency/rates

Consumes:
- InvoiceDraftCreated, InvoiceIssued, PaymentCompleted

Publishes:
- TaxCalculated, CurrencyConverted, ComplianceDocumentGenerated

### 2.3 Subscription and Usage-Based Billing
Data Model:
- BillingPlan, SubscriptionContract, UsageMeter, UsageRecord, BillingCycleRun, DunningRun

API Contracts:
- POST /api/v2/subscriptions
- POST /api/v2/subscriptions/{id}/pause
- POST /api/v2/usage/ingest
- POST /api/v2/subscriptions/run-billing

Consumes:
- PaymentCompleted, PaymentFailed, InvoiceOverdue

Publishes:
- SubscriptionRenewalDue, UsageChargeCalculated, DunningStarted

### 2.4 AI Fraud Detection and Risk Scoring
Data Model:
- FraudModelVersion, FraudRiskDecision, FraudAlert, FraudCase

API Contracts:
- POST /api/v2/fraud/score
- GET /api/v2/fraud/alerts
- POST /api/v2/fraud/cases/{id}/resolve

Consumes:
- PaymentAttempted

Publishes:
- FraudRiskScored, FraudReviewRequired

### 2.5 Advanced Analytics and Forecasting
Data Model:
- ForecastRun, ForecastScenario, KpiMetricSnapshot, CohortFact

API Contracts:
- GET /api/v2/analytics/revenue-forecast
- GET /api/v2/analytics/dso
- GET /api/v2/analytics/churn-risk

Consumes:
- All core financial events and integration events

Publishes:
- ForecastGenerated, ForecastDriftDetected

## 3. Cross-Cutting Contracts
1. Event Envelope
- eventId, eventType, occurredAtUtc, producer, correlationId, causationId, tenantId, payloadVersion, payload

2. Idempotency
- Required header: Idempotency-Key on mutating APIs
- Write deduplication by key plus request hash

3. Audit
- Every financial mutation captures who, when, what changed, and policy outcome

4. Versioning
- Backward-compatible API and event schema evolution using explicit versions

## 4. Data Lifecycle
1. OLTP records are authoritative for operational transactions.
2. Outbox table guarantees event publication after transactional commits.
3. Read models power dashboards and portal low-latency queries.
4. Warehouse facts enable forecasting, cohorts, and executive analytics.
5. Compliance artifacts are retained according to jurisdiction and policy.
