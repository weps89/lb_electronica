import { FormEvent, useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '../api/http';
import { formatInt, formatMoney, formatQty } from '../lib/numberFormat';
import { selectAllOnFocus } from '../lib/inputHelpers';
import type { Product } from '../types';

type LotItem = {
  productId?: number;
  productName: string;
  category?: string;
  qty: number;
  purchaseUnitCostUsd: number;
  marginPercent?: number;
};

export function StockPage() {
  const qc = useQueryClient();
  const [supplier, setSupplier] = useState('');
  const [documentNumber, setDocumentNumber] = useState('');
  const [logisticsUsd, setLogisticsUsd] = useState(0);
  const [exchangeRateArs, setExchangeRateArs] = useState(1450);
  const [notes, setNotes] = useState('');

  const [selectedProductId, setSelectedProductId] = useState<number | undefined>(undefined);
  const [newProductName, setNewProductName] = useState('');
  const [newCategory, setNewCategory] = useState('');
  const [qty, setQty] = useState(1);
  const [purchaseUnitCostUsd, setPurchaseUnitCostUsd] = useState(1);
  const [marginPercent, setMarginPercent] = useState(80);
  const [items, setItems] = useState<LotItem[]>([]);

  const [outProductId, setOutProductId] = useState<number | undefined>(undefined);
  const [outQty, setOutQty] = useState(1);
  const [outReason, setOutReason] = useState('');

  const [adjustProductId, setAdjustProductId] = useState<number | undefined>(undefined);
  const [adjustQty, setAdjustQty] = useState(0);
  const [adjustReason, setAdjustReason] = useState('');

  const { data: products, isLoading: loadingProducts, isError: productsError, error: productsErrorData, refetch: refetchProducts } = useQuery({
    queryKey: ['products'],
    queryFn: () => http<Product[]>('/api/products'),
  });
  const { data: lots, isError: lotsError, error: lotsErrorData, refetch: refetchLots } = useQuery({
    queryKey: ['stock-lots'],
    queryFn: () => http<any[]>('/api/stock/lots'),
  });
  const { data: categories } = useQuery({
    queryKey: ['cfg-categories'],
    queryFn: () => http<any[]>('/api/config/categories'),
  });
  const { data: suppliers } = useQuery({
    queryKey: ['cfg-suppliers'],
    queryFn: () => http<any[]>('/api/config/suppliers'),
  });

  const selectedProduct = useMemo(() => products?.find((p) => p.id === selectedProductId), [products, selectedProductId]);

  const addItem = () => {
    if (selectedProductId) {
      setItems((prev) => [...prev, {
        productId: selectedProductId,
        productName: selectedProduct?.name || '',
        category: selectedProduct?.category || '',
        qty,
        purchaseUnitCostUsd,
        marginPercent,
      }]);
      setSelectedProductId(undefined);
      setQty(1);
      setPurchaseUnitCostUsd(1);
      setMarginPercent(80);
      return;
    }

    if (!newProductName.trim()) return;
    setItems((prev) => [...prev, {
      productName: newProductName.trim(),
      category: newCategory.trim() || undefined,
      qty,
      purchaseUnitCostUsd,
      marginPercent,
    }]);
    setNewProductName('');
    setNewCategory('');
    setQty(1);
    setPurchaseUnitCostUsd(1);
    setMarginPercent(80);
  };

  const removeItem = (idx: number) => setItems((prev) => prev.filter((_, i) => i !== idx));

  const submitLot = async (e: FormEvent) => {
    e.preventDefault();
    await http('/api/stock/entries', {
      method: 'POST',
      body: JSON.stringify({
        date: new Date().toISOString(),
        supplier,
        documentNumber,
        notes,
        logisticsUsd,
        exchangeRateArs,
        items,
      }),
    });
    setItems([]);
    setSupplier('');
    setDocumentNumber('');
    setNotes('');
    setLogisticsUsd(0);
    setExchangeRateArs(1450);
    setSelectedProductId(undefined);
    setNewProductName('');
    setNewCategory('');
    setQty(1);
    setPurchaseUnitCostUsd(1);
    setMarginPercent(80);
    await qc.invalidateQueries({ queryKey: ['products'] });
    await qc.invalidateQueries({ queryKey: ['stock-lots'] });
  };

  const registerOut = async (e: FormEvent) => {
    e.preventDefault();
    if (!outProductId) return;
    await http('/api/stock/out', {
      method: 'POST',
      body: JSON.stringify({ productId: outProductId, qty: outQty, reason: outReason }),
    });
    setOutProductId(undefined);
    setOutQty(1);
    setOutReason('');
    await qc.invalidateQueries({ queryKey: ['products'] });
  };

  const registerAdjust = async (e: FormEvent) => {
    e.preventDefault();
    if (!adjustProductId) return;
    await http('/api/stock/adjust', {
      method: 'POST',
      body: JSON.stringify({ productId: adjustProductId, qty: adjustQty, notes: adjustReason }),
    });
    setAdjustProductId(undefined);
    setAdjustQty(0);
    setAdjustReason('');
    await qc.invalidateQueries({ queryKey: ['products'] });
  };

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Ingresos de Stock por Lote</h1>

      <form onSubmit={submitLot} className="card space-y-3">
        <div className="grid md:grid-cols-4 gap-2">
          <label className="text-sm">
            <div className="mb-1">Nro Comprobante</div>
            <input className="input" value={documentNumber} onChange={(e) => setDocumentNumber(e.target.value)} placeholder="Ej: FAC-12345" />
          </label>
          <label className="text-sm">
            <div className="mb-1">Proveedor</div>
            <input
              className="input"
              list="stock-suppliers"
              value={supplier}
              onChange={(e) => setSupplier(e.target.value)}
              placeholder="Nombre proveedor"
            />
            <datalist id="stock-suppliers">
              {suppliers?.map((s) => (
                <option key={s.id} value={s.name} />
              ))}
            </datalist>
          </label>
          <label className="text-sm">
            <div className="mb-1">Logística Lote (USD)</div>
            <input className="input" type="number" value={logisticsUsd} onFocus={selectAllOnFocus} onChange={(e) => setLogisticsUsd(Number(e.target.value))} placeholder="0" />
          </label>
          <label className="text-sm">
            <div className="mb-1">Cotización (ARS/USD)</div>
            <input className="input" type="number" value={exchangeRateArs} onFocus={selectAllOnFocus} onChange={(e) => setExchangeRateArs(Number(e.target.value))} placeholder="1450" />
          </label>
        </div>
        <label className="text-sm block">
          <div className="mb-1">Notas</div>
          <input className="input" value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Observaciones del lote" />
        </label>

        <div className="border rounded-md p-3 space-y-2">
          <div className="font-medium">Agregar ítem al lote</div>
          <div className="grid md:grid-cols-6 gap-2">
            <label className="text-sm">
              <div className="mb-1">Producto existente</div>
              <select className="input" value={selectedProductId ?? ''} onChange={(e) => setSelectedProductId(e.target.value ? Number(e.target.value) : undefined)}>
                <option value="">Seleccionar</option>
                {products?.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
              </select>
            </label>
            <label className="text-sm">
              <div className="mb-1">Nuevo producto</div>
              <input className="input" value={newProductName} onChange={(e) => setNewProductName(e.target.value)} placeholder="Si no existe" />
            </label>
            <label className="text-sm">
              <div className="mb-1">Categoría</div>
              <select className="input" value={newCategory} onChange={(e) => setNewCategory(e.target.value)}>
                <option value="">Seleccionar categoría</option>
                {categories?.map((c) => <option key={c.id} value={c.name}>{c.name}</option>)}
              </select>
            </label>
            <label className="text-sm">
              <div className="mb-1">Cantidad</div>
              <input className="input" type="number" value={qty} onFocus={selectAllOnFocus} onChange={(e) => setQty(Number(e.target.value))} placeholder="0" />
            </label>
            <label className="text-sm">
              <div className="mb-1">Costo compra (USD)</div>
              <input className="input" type="number" value={purchaseUnitCostUsd} onFocus={selectAllOnFocus} onChange={(e) => setPurchaseUnitCostUsd(Number(e.target.value))} placeholder="0" />
            </label>
            <label className="text-sm">
              <div className="mb-1">Margen (%)</div>
              <input className="input" type="number" value={marginPercent} onFocus={selectAllOnFocus} onChange={(e) => setMarginPercent(Number(e.target.value))} placeholder="80" />
            </label>
          </div>
          <button className="btn-secondary" type="button" onClick={addItem}>Agregar al lote</button>
        </div>
        {loadingProducts && <div className="text-sm text-slate-500">Cargando productos existentes...</div>}
        {productsError && (
          <div className="text-sm text-red-700">
            Error cargando productos: {String((productsErrorData as any)?.message || productsErrorData)}
            <button className="btn-secondary ml-2" type="button" onClick={() => void refetchProducts()}>Reintentar</button>
          </div>
        )}

        <div className="overflow-auto">
          <table className="table">
            <thead><tr><th>Producto</th><th>Cant.</th><th>Costo USD</th><th>Margen</th><th></th></tr></thead>
            <tbody>
              {items.map((i, idx) => (
                <tr key={idx}>
                  <td>{i.productName}</td>
                  <td>{formatQty(i.qty)}</td>
                  <td>{formatMoney(i.purchaseUnitCostUsd)}</td>
                  <td>{formatMoney(i.marginPercent)}</td>
                  <td><button className="btn-secondary" type="button" onClick={() => removeItem(idx)}>Quitar</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <button className="btn-primary" type="submit" disabled={!items.length}>Registrar ingreso de lote</button>
      </form>

      <div className="grid lg:grid-cols-2 gap-3">
        <form onSubmit={registerOut} className="card space-y-2">
          <h2 className="font-semibold">Baja de producto (egreso)</h2>
          <select className="input" value={outProductId ?? ''} onChange={(e) => setOutProductId(e.target.value ? Number(e.target.value) : undefined)} required>
            <option value="">Producto</option>
            {products?.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
          </select>
          <input className="input" type="number" value={outQty} onFocus={selectAllOnFocus} onChange={(e) => setOutQty(Number(e.target.value))} placeholder="Cantidad" />
          <input className="input" value={outReason} onChange={(e) => setOutReason(e.target.value)} placeholder="Motivo" required />
          <button className="btn-primary">Registrar baja</button>
        </form>

        <form onSubmit={registerAdjust} className="card space-y-2">
          <h2 className="font-semibold">Ajuste / anulación de stock</h2>
          <select className="input" value={adjustProductId ?? ''} onChange={(e) => setAdjustProductId(e.target.value ? Number(e.target.value) : undefined)} required>
            <option value="">Producto</option>
            {products?.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
          </select>
          <input className="input" type="number" value={adjustQty} onFocus={selectAllOnFocus} onChange={(e) => setAdjustQty(Number(e.target.value))} placeholder="Ajuste (+/-)" />
          <input className="input" value={adjustReason} onChange={(e) => setAdjustReason(e.target.value)} placeholder="Motivo" required />
          <button className="btn-primary">Registrar ajuste</button>
        </form>
      </div>

      <div className="card overflow-auto">
        <h2 className="font-semibold mb-2">Reporte de stock por lote</h2>
        {lotsError && (
          <div className="text-sm text-red-700 mb-2">
            Error cargando lotes: {String((lotsErrorData as any)?.message || lotsErrorData)}
            <button className="btn-secondary ml-2" type="button" onClick={() => void refetchLots()}>Reintentar</button>
          </div>
        )}
        <table className="table">
          <thead>
            <tr><th>Lote ID</th><th>Fecha</th><th>Comprobante</th><th>Proveedor</th><th>Logística USD</th><th>Costo ARS</th><th>Ítems</th></tr>
          </thead>
          <tbody>
            {lots?.map((l) => (
              <tr key={l.id}>
                <td>{l.batchCode}</td>
                <td>{new Date(l.date).toLocaleString('es-AR')}</td>
                <td>{l.documentNumber}</td>
                <td>{l.supplier}</td>
                <td>{formatMoney(l.logisticsUsd)}</td>
                <td>{formatMoney(l.totalCostArs)}</td>
                <td>{formatInt(l.totalItems)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
