using LBElectronica.Server.Models;

namespace LBElectronica.Server.DTOs;

public record LoginRequest(string Username, string Password);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record CreateUserRequest(string Username, string Password, UserRole Role);
public record UpdateUserRequest(UserRole Role, string? NewPassword);
public record ResetPasswordRequest(int UserId, string NewPassword);
public record ProductUpsertRequest(
    string? Barcode,
    string Name,
    string? Category,
    string? Brand,
    string? Model,
    string? ImeiOrSerial,
    decimal? CostPrice,
    decimal? MarginPercent,
    decimal? SalePrice,
    decimal StockQuantity,
    int StockMinimum,
    bool Active
);

public record StockEntryItemRequest(
    int? ProductId,
    string? ProductName,
    string? Category,
    decimal Qty,
    decimal PurchaseUnitCostUsd,
    decimal? MarginPercent
);

public record SupplierUpsertRequest(string Name, string? TaxId, string? Phone, string? Address, bool Active);
public record CustomerAdminUpsertRequest(string Dni, string? Name, string? Phone, bool Active);
public record ProductCategoryUpsertRequest(string Name, bool Active);

public record CreateStockEntryRequest(
    DateTime Date,
    string? Supplier,
    string? DocumentNumber,
    string? Notes,
    decimal LogisticsUsd,
    decimal? ExchangeRateArs,
    List<StockEntryItemRequest> Items
);

public record PosItemRequest(int ProductId, decimal Qty, decimal UnitPrice, decimal Discount, string? ImeiOrSerial);
public record CustomerUpsertRequest(string? Dni, string? Name, string? Phone);
public record CreateSaleRequest(PaymentMethod PaymentMethod, List<PosItemRequest> Items, decimal? GlobalDiscount, CustomerUpsertRequest? Customer);
public record CollectInvoiceRequest(int SaleId, PaymentMethod PaymentMethod, decimal? ReceivedAmount, string? OperationNumber, bool Verified, CustomerUpsertRequest? Customer);
public record AnnulInvoiceRequest(int SaleId, string Reason);

public record OpenCashSessionRequest(decimal OpeningAmount);
public record CashMovementRequest(CashMovementType Type, decimal Amount, string Reason, string? Category);
public record CloseCashSessionRequest(decimal CountedCash);

public record DateRangeQuery(DateTime? StartDate, DateTime? EndDate, string? Preset);

public record ManualAdjustStockRequest(int ProductId, decimal Qty, string Notes);
public record StockOutRequest(int ProductId, decimal Qty, string Reason);
public record ExchangeRateRequest(decimal ArsPerUsd);
public record BackupCloudConfigRequest(string Provider, string RemoteName, string RemoteFolder, int KeepLocalDays, int KeepRemoteDays, string ScheduleAt, bool Enabled);
