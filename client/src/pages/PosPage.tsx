import { useEffect, useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '../api/http';
import { formatMoney, formatQty } from '../lib/numberFormat';
import { selectAllOnFocus } from '../lib/inputHelpers';
import type { Product } from '../types';

type CartItem = {
  productId: number;
  name: string;
  qty: number;
  unitPrice: number;
  discount: number;
  priceCashArs: number;
  priceCardArs: number;
  priceTransferArs: number;
};

export function PosPage() {
  const qc = useQueryClient();
  const [q, setQ] = useState('');
  const [qtyToAdd, setQtyToAdd] = useState(1);
  const [cart, setCart] = useState<CartItem[]>([]);
  const [paymentMethod, setPaymentMethod] = useState('Efectivo');
  const [globalDiscount, setGlobalDiscount] = useState(0);
  const [customerDni, setCustomerDni] = useState('');
  const [customerName, setCustomerName] = useState('');
  const [customerPhone, setCustomerPhone] = useState('');
  const [customerSearch, setCustomerSearch] = useState('');
  const [receiptUrl, setReceiptUrl] = useState('');
  const [whatsapp, setWhatsapp] = useState('');
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const { data: products } = useQuery({ queryKey: ['products', q], queryFn: () => http<Product[]>(`/api/products?q=${encodeURIComponent(q)}`) });
  const { data: customerMatches } = useQuery({
    queryKey: ['pos-customer-search', customerSearch],
    queryFn: () => http<any[]>(`/api/cash/customer-search?q=${encodeURIComponent(customerSearch)}`),
    enabled: customerSearch.trim().length >= 2,
  });

  const getArsPrices = (p: any) => {
    const cash = Number(p.priceCashArs ?? p.salePrice ?? 0);
    const card = Number(p.priceCardArs ?? (cash * 1.36) ?? 0);
    const transfer = Number(p.priceTransferArs ?? (cash * 1.10) ?? 0);
    return { cash, card, transfer };
  };

  const getUnitPriceByMethod = (item: CartItem, method: string) => {
    if (method === 'Tarjeta') return item.priceCardArs;
    if (method === 'Transferencia') return item.priceTransferArs;
    return item.priceCashArs;
  };

  const add = (p: Product) => {
    const safeQty = Number.isFinite(qtyToAdd) && qtyToAdd > 0 ? qtyToAdd : 1;
    const prices = getArsPrices(p as any);
    setCart((prev) => {
      const existing = prev.find((x) => x.productId === p.id);
      if (existing) return prev.map((x) => x.productId === p.id ? { ...x, qty: x.qty + safeQty } : x);
      return [...prev, {
        productId: p.id,
        name: p.name,
        qty: safeQty,
        unitPrice: paymentMethod === 'Tarjeta' ? prices.card : paymentMethod === 'Transferencia' ? prices.transfer : prices.cash,
        discount: 0,
        priceCashArs: prices.cash,
        priceCardArs: prices.card,
        priceTransferArs: prices.transfer,
      }];
    });
    setQ('');
  };

  const removeLine = (productId: number) => {
    setCart((prev) => prev.filter((x) => x.productId !== productId));
  };

  const subtotal = useMemo(() => cart.reduce((a, i) => a + (i.qty * i.unitPrice) - i.discount, 0), [cart]);
  const total = useMemo(() => Math.max(0, subtotal - globalDiscount), [subtotal, globalDiscount]);

  useEffect(() => {
    setCart((prev) => prev.map((i) => ({ ...i, unitPrice: getUnitPriceByMethod(i, paymentMethod) })));
  }, [paymentMethod]);

  const finalize = async () => {
    setError('');
    setSaving(true);
    try {
      const methodMap: Record<string, number> = { Efectivo: 1, Transferencia: 2, Tarjeta: 3 };
      const payloadItems = cart.map((x) => ({
        productId: x.productId,
        qty: Number(x.qty),
        unitPrice: Number(x.unitPrice),
        discount: Number(x.discount),
      }));

      const sale = await http<any>('/api/sales', {
        method: 'POST',
        body: JSON.stringify({
          paymentMethod: methodMap[paymentMethod] ?? 1,
          items: payloadItems,
          globalDiscount,
          customer: (customerDni.trim() || customerName.trim() || customerPhone.trim())
            ? { dni: customerDni.trim(), name: customerName.trim(), phone: customerPhone.trim() }
            : null,
        }),
      });
      setReceiptUrl(`/api/sales/${sale.id}/receipt`);
      const wa = await http<{ url: string }>(`/api/sales/${sale.id}/whatsapp`);
      setWhatsapp(wa.url);
      setCart([]);
      setGlobalDiscount(0);
      setQ('');
      setQtyToAdd(1);
      setPaymentMethod('Efectivo');
      setCustomerDni('');
      setCustomerName('');
      setCustomerPhone('');
      setCustomerSearch('');
      await qc.invalidateQueries({ queryKey: ['products'] });
    } catch (e: any) {
      setError(String(e?.message || 'No se pudo finalizar la venta'));
    } finally {
      setSaving(false);
    }
  };

  const lookupCustomerByDni = async () => {
    const dni = customerDni.trim();
    if (!dni) return;
    try {
      const found = await http<any>(`/api/cash/customer-by-dni/${encodeURIComponent(dni)}`);
      if (!found) return;
      setCustomerName(found.name ?? '');
      setCustomerPhone(found.phone ?? '');
    } catch {
      // no-op
    }
  };

  return (
    <div className="grid lg:grid-cols-3 gap-4">
      <div className="lg:col-span-2 space-y-3">
        <h1 className="text-xl font-semibold">Punto de Venta (POS)</h1>
        <div className="card overflow-auto">
          <table className="table">
            <thead><tr><th>Producto</th><th>Cant.</th><th>Precio ARS</th><th>Desc.</th><th>Total</th><th></th></tr></thead>
            <tbody>
              {cart.map((c, i) => (
                <tr key={i}>
                  <td>{c.name}</td>
                  <td>{formatQty(c.qty)}</td>
                  <td>{formatMoney(c.unitPrice)}</td>
                  <td>{formatMoney(c.discount)}</td>
                  <td>{formatMoney((c.qty * c.unitPrice) - c.discount)}</td>
                  <td>
                    <button className="btn-secondary" onClick={() => removeLine(c.productId)}>Quitar</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="grid md:grid-cols-5 gap-2">
          <input className="input md:col-span-3" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Escanear código o buscar por nombre/código" />
          <input
            className="input"
            type="number"
            min={1}
            step={1}
            value={qtyToAdd}
            onFocus={selectAllOnFocus}
            onChange={(e) => setQtyToAdd(Number(e.target.value))}
            placeholder="Cantidad"
          />
          <div className="text-xs text-slate-500 flex items-center">Buscar por código o nombre</div>
        </div>
        {q.trim().length >= 2 && <div className="card max-h-72 overflow-auto">
          {products?.map((p) => (
            <button className="w-full text-left p-2 border-b hover:bg-slate-50" key={p.id} onClick={() => add(p)}>
              {p.name} ({p.internalCode}) - ARS {formatMoney(getArsPrices(p as any).cash)} | CANT: {formatQty(qtyToAdd)} | STOCK: {formatQty(Number(p.stockQuantity ?? 0))}
            </button>
          ))}
        </div>}
      </div>
      <div className="space-y-3">
        <div className="card space-y-2">
          <div className="rounded-lg border border-slate-200 bg-slate-50 p-3 space-y-2">
            <div className="text-sm font-semibold text-slate-700">Cliente (opcional)</div>
            <input
              className="input"
              value={customerSearch}
              onChange={(e) => setCustomerSearch(e.target.value)}
              placeholder="Buscar por nombre, DNI o teléfono"
            />
            {!!customerMatches?.length && (
              <div className="max-h-24 overflow-auto rounded border border-slate-200 bg-white">
                {customerMatches.map((c) => (
                  <button
                    key={c.id}
                    type="button"
                    className="w-full text-left px-3 py-2 border-b border-slate-100 hover:bg-slate-50"
                    onClick={() => {
                      setCustomerDni(c.dni ?? '');
                      setCustomerName(c.name ?? '');
                      setCustomerPhone(c.phone ?? '');
                      setCustomerSearch('');
                    }}
                  >
                    {c.name || 'Sin nombre'} | DNI: {c.dni} | TEL: {c.phone || '-'}
                  </button>
                ))}
              </div>
            )}
            <div className="grid gap-2">
              <input
                className="input"
                value={customerDni}
                onChange={(e) => setCustomerDni(e.target.value)}
                onBlur={() => void lookupCustomerByDni()}
                onKeyDown={(e) => { if (e.key === 'Tab') void lookupCustomerByDni(); }}
                placeholder="DNI"
              />
              <input
                className="input"
                value={customerName}
                onChange={(e) => setCustomerName(e.target.value)}
                placeholder="Nombre"
              />
              <input
                className="input"
                value={customerPhone}
                onChange={(e) => setCustomerPhone(e.target.value)}
                placeholder="Teléfono"
              />
            </div>
          </div>
          <div className="text-sm text-slate-500">Método de pago</div>
          <select className="input" value={paymentMethod} onChange={(e) => setPaymentMethod(e.target.value)}>
            <option>Efectivo</option><option>Transferencia</option><option>Tarjeta</option>
          </select>
          <div>
            <div className="text-sm text-slate-500 mb-1">Descuento total (no por producto)</div>
            <input className="input" type="number" min={0} step="0.01" value={globalDiscount} onFocus={selectAllOnFocus} onChange={(e) => setGlobalDiscount(Number(e.target.value) || 0)} />
          </div>
          {error && <div className="text-sm text-red-700">{error}</div>}
          <div className="rounded-xl border border-sky-300 bg-gradient-to-b from-sky-100 to-cyan-200 px-4 py-5">
            <div className="text-sm text-sky-900/70">Subtotal: $ {formatMoney(subtotal)}</div>
            <div className="text-sm text-sky-900/70">Descuento total: $ {formatMoney(globalDiscount)}</div>
            <div className="text-xs uppercase tracking-wide text-sky-900/70">Total a pagar</div>
            <div className="text-5xl font-bold leading-none text-sky-950">$ {formatMoney(total)}</div>
          </div>
          <button className="btn-primary w-full" onClick={() => void finalize()} disabled={!cart.length || saving}>
            {saving ? 'Finalizando...' : 'Finalizar venta'}
          </button>
        </div>
        {receiptUrl && (
          <div className="card space-y-2">
            <a className="btn-secondary w-full inline-block text-center" href={receiptUrl} target="_blank">Abrir recibo / Imprimir</a>
            <a className="btn-secondary w-full inline-block text-center" href={whatsapp} target="_blank">Enviar por WhatsApp</a>
          </div>
        )}
      </div>
    </div>
  );
}
