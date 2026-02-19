import { FormEvent, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '../api/http';
import { formatMoney, formatQty } from '../lib/numberFormat';
import type { Product } from '../types';

const empty = {
  barcode: '', name: '', category: '', brand: '', model: '', imeiOrSerial: '', costPrice: 0, marginPercent: 0, salePrice: 0, stockQuantity: 0, stockMinimum: 0, active: true,
};

export function ProductsPage() {
  const qc = useQueryClient();
  const [form, setForm] = useState<any>(empty);
  const { data, isLoading, isError, error, refetch } = useQuery({
    queryKey: ['products'],
    queryFn: () => http<Product[]>('/api/products'),
  });

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    await http('/api/products', { method: 'POST', body: JSON.stringify(form) });
    setForm(empty);
    await qc.invalidateQueries({ queryKey: ['products'] });
  };

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Productos</h1>
      <form onSubmit={submit} className="card grid md:grid-cols-4 gap-2">
        <input className="input" placeholder="Nombre" value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} required />
        <input className="input" placeholder="Código de barras" value={form.barcode} onChange={e => setForm({ ...form, barcode: e.target.value })} />
        <input className="input" placeholder="Categoría" value={form.category} onChange={e => setForm({ ...form, category: e.target.value })} />
        <input className="input" placeholder="Marca" value={form.brand} onChange={e => setForm({ ...form, brand: e.target.value })} />
        <input className="input" type="number" placeholder="Costo" value={form.costPrice} onChange={e => setForm({ ...form, costPrice: Number(e.target.value) })} />
        <input className="input" type="number" placeholder="Margen %" value={form.marginPercent} onChange={e => setForm({ ...form, marginPercent: Number(e.target.value) })} />
        <input className="input" type="number" placeholder="Precio venta" value={form.salePrice} onChange={e => setForm({ ...form, salePrice: Number(e.target.value) })} />
        <input className="input" type="number" placeholder="Stock" value={form.stockQuantity} onChange={e => setForm({ ...form, stockQuantity: Number(e.target.value) })} />
        <button className="btn-primary md:col-span-4" type="submit">Agregar producto</button>
      </form>
      <div className="card overflow-auto">
        <div className="text-sm text-slate-500 mb-2">Total productos: {data?.length ?? 0}</div>
        {isLoading && <div className="text-sm text-slate-500 mb-2">Cargando productos...</div>}
        {isError && (
          <div className="mb-2 text-sm text-red-700">
            No se pudieron cargar productos: {String((error as any)?.message || error)}
            <button className="btn-secondary ml-2" onClick={() => void refetch()}>Reintentar</button>
          </div>
        )}
        <table className="table">
          <thead><tr><th>Código</th><th>Nombre</th><th>Cód. barras</th><th>Venta</th><th>Stock</th></tr></thead>
          <tbody>
            {data?.map(p => <tr key={p.id}><td>{p.internalCode}</td><td>{p.name}</td><td>{p.barcode}</td><td>{formatMoney(p.salePrice)}</td><td>{formatQty(p.stockQuantity)}</td></tr>)}
            {!isLoading && !isError && !data?.length && <tr><td colSpan={5} className="text-slate-500">No hay productos para mostrar.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  );
}
