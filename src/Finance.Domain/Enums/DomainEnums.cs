namespace Finance.Domain.Enums;

public enum ClientType
{
    Web,
    Mobile
}

public enum AccountType
{
    Checking,
    Savings,
    Cash,
    Investment
}

public enum CategoryType
{
    Income,
    Expense
}

public enum InvoiceStatus
{
    Open,
    Closed,
    Paid
}

public enum RecurrenceType
{
    Income,
    Expense
}

public enum RecurrenceFrequency
{
    Weekly,
    Monthly,
    Yearly
}

public enum TransactionType
{
    Income,
    Expense,
    TransferIn,
    TransferOut,
    InvoicePayment
}

public enum TransactionStatus
{
    Pending,
    Confirmed
}

public enum SecurityAuditEventType
{
    UserRegistered,
    LoginSuccess,
    LoginFailed,
    LoginLocked,
    TokenRefreshed,
    RefreshTokenReuseDetected,
    Logout,
    SessionRevoked,
    PasswordChanged,
    AccountArchived,
    LargeTransactionCreated,
    TelegramLinkRequested,
    TelegramLinked,
    TelegramUnlinked
}
