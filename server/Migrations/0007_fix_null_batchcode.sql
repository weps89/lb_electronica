UPDATE StockEntries
SET BatchCode = 'LOTE-' || printf('%06d', Id)
WHERE BatchCode IS NULL OR trim(BatchCode) = '';
