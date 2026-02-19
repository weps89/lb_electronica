-- Regla de oro: si baja la cotizacion actual, se respeta la cotizacion del lote del producto.
-- Completa LastStockExchangeRateArs para productos legacy en 0/1.
UPDATE Products
SET LastStockExchangeRateArs = COALESCE(
    (
        SELECT se.ExchangeRateArs
        FROM StockEntryItems sei
        JOIN StockEntries se ON se.Id = sei.StockEntryId
        WHERE sei.ProductId = Products.Id
          AND CAST(se.ExchangeRateArs AS REAL) > 1
        ORDER BY se.Date DESC, se.Id DESC
        LIMIT 1
    ),
    (
        SELECT er.ArsPerUsd
        FROM ExchangeRates er
        ORDER BY er.EffectiveDate DESC, er.Id DESC
        LIMIT 1
    ),
    LastStockExchangeRateArs
)
WHERE CAST(COALESCE(LastStockExchangeRateArs, '0') AS REAL) <= 1;
