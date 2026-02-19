using LBElectronica.Server.Models;

namespace LBElectronica.Server.Services;

public static class PricingService
{
    public static decimal EffectiveRate(Product p, decimal arsPerUsd)
    {
        var stockRate = p.LastStockExchangeRateArs > 1m ? p.LastStockExchangeRateArs : arsPerUsd;
        return Math.Max(stockRate, arsPerUsd);
    }

    public static decimal CashArs(Product p, decimal arsPerUsd)
    {
        var effectiveRate = EffectiveRate(p, arsPerUsd);
        var baseUsd = p.CostPrice * (1 + (p.MarginPercent / 100m));
        return Math.Round(baseUsd * effectiveRate, 2);
    }

    public static decimal FinalArs(Product p, PaymentMethod method, decimal arsPerUsd)
    {
        var cash = CashArs(p, arsPerUsd);
        return method switch
        {
            PaymentMethod.Card => Math.Round(cash * 1.36m, 2),
            PaymentMethod.Transfer => Math.Round(cash * 1.10m, 2),
            _ => cash
        };
    }
}
