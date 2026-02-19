import { FormEvent, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '../api/http';
import { selectAllOnFocus } from '../lib/inputHelpers';

type Customer = { id: number; dni: string; name?: string; phone?: string; active: boolean };
type Supplier = { id: number; name: string; taxId?: string; phone?: string; address?: string; active: boolean };
type Category = { id: number; name: string; active: boolean };

export function SettingsPage() {
  const qc = useQueryClient();
  const [tab, setTab] = useState<'customers' | 'suppliers' | 'categories' | 'exchange'>('customers');

  const [customer, setCustomer] = useState({ id: 0, dni: '', name: '', phone: '', active: true });
  const [supplier, setSupplier] = useState({ id: 0, name: '', taxId: '', phone: '', address: '', active: true });
  const [category, setCategory] = useState({ id: 0, name: '', active: true });
  const [exchangeRate, setExchangeRate] = useState('');

  const { data: customers } = useQuery({ queryKey: ['cfg-customers'], queryFn: () => http<Customer[]>('/api/config/customers') });
  const { data: suppliers } = useQuery({ queryKey: ['cfg-suppliers'], queryFn: () => http<Supplier[]>('/api/config/suppliers') });
  const { data: categories } = useQuery({ queryKey: ['cfg-categories'], queryFn: () => http<Category[]>('/api/config/categories') });
  const { data: currentRate } = useQuery({ queryKey: ['cfg-rate'], queryFn: () => http<any>('/api/system/exchange-rate') });

  const saveCustomer = async (e: FormEvent) => {
    e.preventDefault();
    const payload = { dni: customer.dni, name: customer.name, phone: customer.phone, active: customer.active };
    if (customer.id) await http(`/api/config/customers/${customer.id}`, { method: 'PUT', body: JSON.stringify(payload) });
    else await http('/api/config/customers', { method: 'POST', body: JSON.stringify(payload) });
    setCustomer({ id: 0, dni: '', name: '', phone: '', active: true });
    await qc.invalidateQueries({ queryKey: ['cfg-customers'] });
  };

  const saveSupplier = async (e: FormEvent) => {
    e.preventDefault();
    const payload = { name: supplier.name, taxId: supplier.taxId, phone: supplier.phone, address: supplier.address, active: supplier.active };
    if (supplier.id) await http(`/api/config/suppliers/${supplier.id}`, { method: 'PUT', body: JSON.stringify(payload) });
    else await http('/api/config/suppliers', { method: 'POST', body: JSON.stringify(payload) });
    setSupplier({ id: 0, name: '', taxId: '', phone: '', address: '', active: true });
    await qc.invalidateQueries({ queryKey: ['cfg-suppliers'] });
  };

  const saveCategory = async (e: FormEvent) => {
    e.preventDefault();
    const payload = { name: category.name, active: category.active };
    if (category.id) await http(`/api/config/categories/${category.id}`, { method: 'PUT', body: JSON.stringify(payload) });
    else await http('/api/config/categories', { method: 'POST', body: JSON.stringify(payload) });
    setCategory({ id: 0, name: '', active: true });
    await qc.invalidateQueries({ queryKey: ['cfg-categories'] });
  };

  const saveRate = async (e: FormEvent) => {
    e.preventDefault();
    await http('/api/system/exchange-rate', { method: 'POST', body: JSON.stringify({ arsPerUsd: Number(exchangeRate) }) });
    setExchangeRate('');
    await qc.invalidateQueries({ queryKey: ['cfg-rate'] });
  };

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Configuraciones</h1>
      <div className="card flex gap-2">
        <button className={tab === 'customers' ? 'btn-primary' : 'btn-secondary'} onClick={() => setTab('customers')}>ABM Clientes</button>
        <button className={tab === 'suppliers' ? 'btn-primary' : 'btn-secondary'} onClick={() => setTab('suppliers')}>ABM Proveedores</button>
        <button className={tab === 'categories' ? 'btn-primary' : 'btn-secondary'} onClick={() => setTab('categories')}>ABM Categorías</button>
        <button className={tab === 'exchange' ? 'btn-primary' : 'btn-secondary'} onClick={() => setTab('exchange')}>Ajustar Cotización</button>
      </div>

      {tab === 'customers' && (
        <div className="grid lg:grid-cols-2 gap-3">
          <form className="card space-y-2" onSubmit={saveCustomer}>
            <h2 className="font-semibold">{customer.id ? 'Editar cliente' : 'Nuevo cliente'}</h2>
            <input className="input" placeholder="DNI" value={customer.dni} onChange={(e) => setCustomer({ ...customer, dni: e.target.value })} required />
            <input className="input" placeholder="Nombre" value={customer.name} onChange={(e) => setCustomer({ ...customer, name: e.target.value })} />
            <input className="input" placeholder="Teléfono" value={customer.phone} onChange={(e) => setCustomer({ ...customer, phone: e.target.value })} />
            <label className="text-sm flex items-center gap-2"><input type="checkbox" checked={customer.active} onChange={(e) => setCustomer({ ...customer, active: e.target.checked })} /> Activo</label>
            <button className="btn-primary">{customer.id ? 'Guardar cambios' : 'Crear cliente'}</button>
          </form>

          <div className="card overflow-auto">
            <table className="table">
              <thead><tr><th>DNI</th><th>Nombre</th><th>Teléfono</th><th>Activo</th><th></th></tr></thead>
              <tbody>
                {customers?.map((c) => (
                  <tr key={c.id}>
                    <td>{c.dni}</td><td>{c.name}</td><td>{c.phone}</td><td>{c.active ? 'Sí' : 'No'}</td>
                    <td><button className="btn-secondary" onClick={() => setCustomer({ id: c.id, dni: c.dni, name: c.name || '', phone: c.phone || '', active: c.active })}>Editar</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {tab === 'suppliers' && (
        <div className="grid lg:grid-cols-2 gap-3">
          <form className="card space-y-2" onSubmit={saveSupplier}>
            <h2 className="font-semibold">{supplier.id ? 'Editar proveedor' : 'Nuevo proveedor'}</h2>
            <input className="input" placeholder="Nombre" value={supplier.name} onChange={(e) => setSupplier({ ...supplier, name: e.target.value })} required />
            <input className="input" placeholder="CUIT / Tax ID" value={supplier.taxId} onChange={(e) => setSupplier({ ...supplier, taxId: e.target.value })} />
            <input className="input" placeholder="Teléfono" value={supplier.phone} onChange={(e) => setSupplier({ ...supplier, phone: e.target.value })} />
            <input className="input" placeholder="Dirección" value={supplier.address} onChange={(e) => setSupplier({ ...supplier, address: e.target.value })} />
            <label className="text-sm flex items-center gap-2"><input type="checkbox" checked={supplier.active} onChange={(e) => setSupplier({ ...supplier, active: e.target.checked })} /> Activo</label>
            <button className="btn-primary">{supplier.id ? 'Guardar cambios' : 'Crear proveedor'}</button>
          </form>

          <div className="card overflow-auto">
            <table className="table">
              <thead><tr><th>Nombre</th><th>CUIT</th><th>Teléfono</th><th>Activo</th><th></th></tr></thead>
              <tbody>
                {suppliers?.map((s) => (
                  <tr key={s.id}>
                    <td>{s.name}</td><td>{s.taxId}</td><td>{s.phone}</td><td>{s.active ? 'Sí' : 'No'}</td>
                    <td><button className="btn-secondary" onClick={() => setSupplier({ id: s.id, name: s.name, taxId: s.taxId || '', phone: s.phone || '', address: s.address || '', active: s.active })}>Editar</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {tab === 'categories' && (
        <div className="grid lg:grid-cols-2 gap-3">
          <form className="card space-y-2" onSubmit={saveCategory}>
            <h2 className="font-semibold">{category.id ? 'Editar categoría' : 'Nueva categoría'}</h2>
            <input className="input" placeholder="Nombre categoría" value={category.name} onChange={(e) => setCategory({ ...category, name: e.target.value })} required />
            <label className="text-sm flex items-center gap-2"><input type="checkbox" checked={category.active} onChange={(e) => setCategory({ ...category, active: e.target.checked })} /> Activa</label>
            <button className="btn-primary">{category.id ? 'Guardar cambios' : 'Crear categoría'}</button>
          </form>
          <div className="card overflow-auto">
            <table className="table">
              <thead><tr><th>Nombre</th><th>Activa</th><th></th></tr></thead>
              <tbody>
                {categories?.map((c) => (
                  <tr key={c.id}>
                    <td>{c.name}</td><td>{c.active ? 'Sí' : 'No'}</td>
                    <td><button className="btn-secondary" onClick={() => setCategory({ id: c.id, name: c.name, active: c.active })}>Editar</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {tab === 'exchange' && (
        <div className="grid lg:grid-cols-2 gap-3">
          <div className="card space-y-2">
            <h2 className="font-semibold">Cotización actual</h2>
            <div className="text-2xl font-bold">{currentRate?.arsPerUsd ?? currentRate?.ArsPerUsd ?? '-'}</div>
            <div className="text-xs text-slate-500">Fecha: {currentRate?.effectiveDate ? new Date(currentRate.effectiveDate).toLocaleString('es-AR') : '-'}</div>
          </div>
          <form className="card space-y-2" onSubmit={saveRate}>
            <h2 className="font-semibold">Actualizar cotización del día</h2>
            <input className="input" type="number" step="0.0001" min="0.0001" placeholder="Ej: 1450" value={exchangeRate} onFocus={selectAllOnFocus} onChange={(e) => setExchangeRate(e.target.value)} required />
            <button className="btn-primary">Guardar cotización</button>
          </form>
        </div>
      )}
    </div>
  );
}
