ALTER TABLE SaleItems ADD COLUMN CostPriceSnapshotArs TEXT NOT NULL DEFAULT '0';

-- Backfill para ventas ya realizadas: convierte costo snapshot USD a ARS
-- usando cotizacion vigente a la fecha de la venta (fallbacks incluidos).
UPDATE SaleItems
SET CostPriceSnapshotArs = (
    ROUND(
        CAST(CostPriceSnapshot AS REAL) * COALESCE(
            (
                SELECT CAST(er.ArsPerUsd AS REAL)
                FROM ExchangeRates er
                JOIN Sales s ON s.Id = SaleItems.SaleId
                WHERE er.EffectiveDate <= s.Date
                ORDER BY er.EffectiveDate DESC, er.Id DESC
                LIMIT 1
            ),
            (
                SELECT CAST(p.LastStockExchangeRateArs AS REAL)
                FROM Products p
                WHERE p.Id = SaleItems.ProductId
                  AND CAST(COALESCE(p.LastStockExchangeRateArs, '0') AS REAL) > 1
                LIMIT 1
            ),
            (
                SELECT CAST(er2.ArsPerUsd AS REAL)
                FROM ExchangeRates er2
                ORDER BY er2.EffectiveDate DESC, er2.Id DESC
                LIMIT 1
            ),
            1
        ),
        2
    )
)
WHERE CAST(COALESCE(CostPriceSnapshotArs, '0') AS REAL) <= 0
  AND CAST(COALESCE(CostPriceSnapshot, '0') AS REAL) > 0;
