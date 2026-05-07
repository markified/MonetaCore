namespace MonetaCore.Models;

public static class DomainValues
{
    public static class InvoiceStatus
    {
        public const string Draft = "Draft";
        public const string Issued = "Issued";
        public const string PartiallyPaid = "PartiallyPaid";
        public const string Paid = "Paid";
        public const string Overdue = "Overdue";
        public const string Cancelled = "Cancelled";
    }

    public static class PaymentStatus
    {
        public const string Pending = "Pending";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Refunded = "Refunded";
    }

    public static class PaymentMethod
    {
        public const string Cash = "Cash";
        public const string BankTransfer = "Bank Transfer";
        public const string GCash = "GCash";
        public const string Card = "Card";
        public const string PayMongo = "PayMongo";
    }

    public static class PayMongoFlow
    {
        public const string Checkout = "Checkout";
        public const string Card = "Card";
    }

    public static class AdjustmentType
    {
        public const string CreditNote = "CreditNote";
        public const string DebitMemo = "DebitMemo";
    }

    public static class SyncStatus
    {
        public const string Pending = "Pending";
        public const string Success = "Success";
        public const string Failed = "Failed";
    }

    public static class OutboxStatus
    {
        public const string Pending = "Pending";
        public const string Dispatched = "Dispatched";
        public const string Failed = "Failed";
    }

    public static class DisputeStatus
    {
        public const string Submitted = "Submitted";
        public const string UnderReview = "UnderReview";
        public const string Resolved = "Resolved";
        public const string Rejected = "Rejected";
    }

    public static class IntegrationAuthMode
    {
        public const string None = "None";
        public const string ApiKey = "ApiKey";
        public const string Bearer = "Bearer";
    }
}
