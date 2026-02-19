namespace LBElectronica.Server.Models;

public enum UserRole
{
    Admin = 1,
    Cashier = 2
}

public enum LedgerMovementType
{
    In = 1,
    Out = 2,
    Adjust = 3
}

public enum LedgerReferenceType
{
    Purchase = 1,
    Sale = 2,
    ManualAdjust = 3
}

public enum PaymentMethod
{
    Cash = 1,
    Transfer = 2,
    Card = 3
}

public enum CashMovementType
{
    Income = 1,
    Expense = 2
}

public enum SaleStatus
{
    Pending = 1,
    Paid = 2,
    Verified = 3,
    Cancelled = 4
}
