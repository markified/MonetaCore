# MonetaCore Event Catalog

This catalog defines key domain events used to connect core modules with complementary subsystems.

## 1. Event Envelope Standard
Every event uses this envelope:
- eventId (GUID)
- eventType (string)
- payloadVersion (string)
- occurredAtUtc (ISO timestamp)
- producer (module name)
- correlationId (trace across workflow)
- causationId (parent event or command)
- tenantId (optional for multi-tenant deployments)
- idempotencyKey (consumer dedupe key)
- payload (event-specific object)

## 2. Topic Naming Convention
- monetacore.core.invoice.v1
- monetacore.core.payment.v1
- monetacore.core.adjustment.v1
- monetacore.core.revenue.v1
- monetacore.core.integration.v1
- monetacore.compliance.v1
- monetacore.subscription.v1
- monetacore.fraud.v1
- monetacore.analytics.v1

## 3. Core Module Events

### Invoice Module
1. InvoiceDraftCreated
- Producer: Invoice Generation and Tracking
- Consumers: Compliance Engine, Subscription Engine, Analytics
- Key Payload Fields: invoiceId, clientId, currencyCode, subtotal, taxAmount, totalAmount, dueDate

2. InvoiceIssued
- Producer: Invoice Generation and Tracking
- Consumers: Portal, Notifications, Account Integration, Revenue
- Key Payload Fields: invoiceId, invoiceNumber, issuedAtUtc, totalAmount, balanceDue, dueDate

3. InvoiceBalanceUpdated
- Producer: Invoice Generation and Tracking
- Consumers: Revenue, Portal, Analytics
- Key Payload Fields: invoiceId, previousBalance, newBalance, reasonCode

4. InvoiceOverdue
- Producer: Invoice Generation and Tracking
- Consumers: Dunning, Portal, Revenue
- Key Payload Fields: invoiceId, overdueDays, balanceDue, escalationLevel

5. InvoiceCancelled
- Producer: Invoice Generation and Tracking
- Consumers: Account Integration, Analytics
- Key Payload Fields: invoiceId, cancelledAtUtc, cancelledBy, cancelReason

### Payment Module
1. PaymentAttempted
- Producer: Payment Processing
- Consumers: Fraud Engine, Analytics
- Key Payload Fields: paymentAttemptId, invoiceId, amount, method, gateway, customerId

2. PaymentAuthorized
- Producer: Payment Processing
- Consumers: Revenue, Portal
- Key Payload Fields: paymentId, invoiceId, authorizedAmount, gatewayTransactionId

3. PaymentCompleted
- Producer: Payment Processing
- Consumers: Invoice, Revenue, Portal, Integration, Analytics
- Key Payload Fields: paymentId, invoiceId, settledAmount, settledAtUtc, gatewayReference

4. PaymentFailed
- Producer: Payment Processing
- Consumers: Dunning, Fraud, Portal
- Key Payload Fields: paymentAttemptId, invoiceId, failureCode, failureReason

5. PaymentRefunded
- Producer: Payment Processing
- Consumers: Invoice, Revenue, Integration
- Key Payload Fields: paymentId, invoiceId, refundedAmount, refundReason

### Credit and Debit Module
1. AdjustmentCreated
- Producer: Credit and Debit Management
- Consumers: Approval Workflow, Analytics
- Key Payload Fields: adjustmentId, invoiceId, type, amount, reason, requestedBy

2. AdjustmentApproved
- Producer: Credit and Debit Management
- Consumers: Invoice, Revenue, Integration
- Key Payload Fields: adjustmentId, invoiceId, approvedBy, approvedAtUtc

3. AdjustmentRejected
- Producer: Credit and Debit Management
- Consumers: Portal, Analytics
- Key Payload Fields: adjustmentId, rejectedBy, rejectedAtUtc, rejectionReason

4. AdjustmentApplied
- Producer: Credit and Debit Management
- Consumers: Invoice, Revenue
- Key Payload Fields: adjustmentId, invoiceId, netImpactAmount, resultingBalance

### Revenue Module
1. RevenueSnapshotCreated
- Producer: Revenue Monitoring
- Consumers: Analytics Dashboard, Alerting
- Key Payload Fields: snapshotDate, realizedRevenue, outstandingReceivables, overdueAmount

2. RevenueThresholdBreached
- Producer: Revenue Monitoring
- Consumers: Notifications, Operations
- Key Payload Fields: thresholdType, thresholdValue, observedValue, observedAtUtc

3. ForecastGenerated
- Producer: Revenue Monitoring or Analytics Engine
- Consumers: Executive Dashboard, Planning
- Key Payload Fields: forecastRunId, horizonDays, expectedRevenue, confidenceScore

### Account Integration Module
1. IntegrationSyncQueued
- Producer: Account Integration
- Consumers: Connector Workers, Observability
- Key Payload Fields: syncRunId, connector, entityType, entityId

2. IntegrationSyncSucceeded
- Producer: Account Integration
- Consumers: Dashboard, Audit, Analytics
- Key Payload Fields: syncRunId, connector, durationMs, externalReference

3. IntegrationSyncFailed
- Producer: Account Integration
- Consumers: Retry Worker, Dead-Letter Queue, Alerts
- Key Payload Fields: syncRunId, connector, errorCode, errorMessage

4. IntegrationReplayRequested
- Producer: Account Integration Operations
- Consumers: Connector Workers
- Key Payload Fields: messageId, replayRequestedBy, replayRequestedAtUtc

## 4. Complementary Subsystem Events

### Compliance Engine
1. TaxCalculated
- Producer: Multi-Currency and Tax Compliance Engine
- Consumers: Invoice, Revenue, Audit
- Key Payload Fields: invoiceId, jurisdiction, taxCode, taxableAmount, taxAmount

2. CurrencyConverted
- Producer: Multi-Currency and Tax Compliance Engine
- Consumers: Invoice, Revenue, Analytics
- Key Payload Fields: sourceCurrency, targetCurrency, fxRate, amountBefore, amountAfter

3. ComplianceDocumentGenerated
- Producer: Multi-Currency and Tax Compliance Engine
- Consumers: Portal, Audit, Integration
- Key Payload Fields: documentId, documentType, periodStart, periodEnd, generatedAtUtc

### Subscription Engine
1. SubscriptionRenewalDue
- Producer: Subscription and Usage Billing
- Consumers: Billing Orchestrator
- Key Payload Fields: subscriptionId, customerId, renewalDate, planCode

2. UsageChargeCalculated
- Producer: Subscription and Usage Billing
- Consumers: Invoice Module, Analytics
- Key Payload Fields: subscriptionId, meterId, usageQuantity, billedAmount

3. DunningStarted
- Producer: Subscription and Usage Billing
- Consumers: Notifications, Collections Dashboard
- Key Payload Fields: dunningRunId, invoiceId, stage, nextAttemptAtUtc

### Fraud Engine
1. FraudRiskScored
- Producer: Fraud Detection and Risk Scoring
- Consumers: Payment Policy Layer, Audit
- Key Payload Fields: paymentAttemptId, riskScore, riskBand, reasonCodes

2. FraudReviewRequired
- Producer: Fraud Detection and Risk Scoring
- Consumers: Fraud Operations Queue
- Key Payload Fields: caseId, paymentAttemptId, priority, assignedQueue

### Analytics Engine
1. ForecastDriftDetected
- Producer: Advanced Analytics
- Consumers: Revenue Team Alerts
- Key Payload Fields: forecastRunId, metricName, expectedValue, actualValue, driftPercent

## 5. Delivery Guarantees
1. At-least-once delivery with consumer idempotency.
2. Ordering guaranteed per aggregate key when possible.
3. Poison messages routed to dead-letter queue with replay support.
4. Schema validation required at publish and consume boundaries.
