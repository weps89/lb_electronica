import { FormEvent, useEffect, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '../api/http';
import { selectAllOnFocus } from '../lib/inputHelpers';

type Customer = { id: number; dni: string; name?: string; phone?: string; active: boolean };
type Supplier = { id: number; name: string; taxId?: string; phone?: string; address?: string; active: boolean };
type Category = { id: number; name: string; active: boolean };
type BackupCfg = {
  provider: string;
  remoteName: string;
  remoteFolder: string;
  keepLocalDays: number;
  keepRemoteDays: number;
  scheduleAt: string;
  enabled: boolean;
};

export function SettingsPage() {
  const qc = useQueryClient();
  const [tab, setTab] = useState<'customers' | 'suppliers' | 'categories' | 'exchange' | 'backup'>('customers');

  const [customer, setCustomer] = useState({ id: 0, dni: '', name: '', phone: '', active: true });
  const [supplier, setSupplier] = useState({ id: 0, name: '', taxId: '', phone: '', address: '', active: true });
  const [category, setCategory] = useState({ id: 0, name: '', active: true });
  const [exchangeRate, setExchangeRate] = useState('');
  const [backupCfg, setBackupCfg] = useState<BackupCfg>({
    provider: 'Google Drive',
    remoteName: 'gdrive',
    remoteFolder: 'LBElectronica/backups',
    keepLocalDays: 30,
    keepRemoteDays: 90,
    scheduleAt: '22:00',
    enabled: false,
  });
  const [backupMsg, setBackupMsg] = useState('');

  const { data: customers } = useQuery({ queryKey: ['cfg-customers'], queryFn: () => http<Customer[]>('/api/config/customers') });
  const { data: suppliers } = useQuery({ queryKey: ['cfg-suppliers'], queryFn: () => http<Supplier[]>('/api/config/suppliers') });
  const { data: categories } = useQuery({ queryKey: ['cfg-categories'], queryFn: () => http<Category[]>('/api/config/categories') });
  const { data: currentRate } = useQuery({ queryKey: ['cfg-rate'], queryFn: () => http<any>('/api/system/exchange-rate') });
  const { data: backupCfgData } = useQuery({
    queryKey: ['cfg-backup-cloud'],
    queryFn: () => http<BackupCfg>('/api/system/backup-cloud-config'),
  });
  useEffect(() => {
    if (!backupCfgData) return;
    setBackupCfg({
      provider: backupCfgData.provider || 'Google Drive',
      remoteName: backupCfgData.remoteName || 'gdrive',
      remoteFolder: backupCfgData.remoteFolder || 'LBElectronica/backups',
      keepLocalDays: Number(backupCfgData.keepLocalDays ?? 30),
      keepRemoteDays: Number(backupCfgData.keepRemoteDays ?? 90),
      scheduleAt: backupCfgData.scheduleAt || '22:00',
      enabled: Boolean(backupCfgData.enabled),
    });
  }, [backupCfgData]);

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

  const saveBackupCfg = async (e: FormEvent) => {
    e.preventDefault();
    setBackupMsg('');
    await http('/api/system/backup-cloud-config', { method: 'POST', body: JSON.stringify(backupCfg) });
    setBackupMsg('Configuración de backup guardada.');
    await qc.invalidateQueries({ queryKey: ['cfg-backup-cloud'] });
  };

  const testBackupConnection = async () => {
    setBackupMsg('');
    try {
      const r = await http<any>('/api/system/backup-cloud-config/test', { method: 'POST' });
      setBackupMsg(r?.message || 'Conexión correcta.');
    } catch (e: any) {
      setBackupMsg(String(e?.message || 'No se pudo probar conexión.'));
    }
  };

  const runBackupNow = async () => {
    setBackupMsg('');
    try {
      const r = await http<any>('/api/system/backup-cloud-run-now', { method: 'POST' });
      setBackupMsg(r?.message || 'Respaldo ejecutado.');
    } catch (e: any) {
      setBackupMsg(String(e?.message || 'No se pudo ejecutar respaldo ahora.'));
    }
  };

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Configuraciones</h1>
      <div className="card flex gap-2">
        <button className={tab === 'customers' ? 'btn-primary' : 'btn-secondary'} onClick={() => setTab('customers')}>ABM Clientes</button>
        <button className={tab === 'suppliers' ? 'btn-primary' : 'btn-secondary'} onClick={() => setTab('suppliers')}>ABM Proveedores</button>
        <button className={tab === 'categories' ? 'btn-primary' : 'btn-secondary'} onClick={() => setTab('categories')}>ABM Categorías</button>
        <button className={tab === 'exchange' ? 'btn-primary' : 'btn-secondary'} onClick={() => setTab('exchange')}>Ajustar Cotización</button>
        <button className={tab === 'backup' ? 'btn-primary' : 'btn-secondary'} onClick={() => setTab('backup')}>Backup en nube</button>
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

      {tab === 'backup' && (
        <div className="grid lg:grid-cols-2 gap-3">
          <form className="card space-y-2" onSubmit={saveBackupCfg}>
            <h2 className="font-semibold">Configuración de cuenta de backup (Solo Admin)</h2>
            <input className="input" value={backupCfg.provider} onChange={(e) => setBackupCfg({ ...backupCfg, provider: e.target.value })} placeholder="Proveedor (ej: Google Drive)" required />
            <input className="input" value={backupCfg.remoteName} onChange={(e) => setBackupCfg({ ...backupCfg, remoteName: e.target.value })} placeholder="Remote name (rclone)" required />
            <input className="input" value={backupCfg.remoteFolder} onChange={(e) => setBackupCfg({ ...backupCfg, remoteFolder: e.target.value })} placeholder="Carpeta remota" required />
            <div className="grid grid-cols-3 gap-2">
              <label className="text-xs text-slate-600 space-y-1">
                <span>Días retención local</span>
                <input className="input" type="number" min={1} value={backupCfg.keepLocalDays} onChange={(e) => setBackupCfg({ ...backupCfg, keepLocalDays: Number(e.target.value) })} placeholder="Ej: 30" />
              </label>
              <label className="text-xs text-slate-600 space-y-1">
                <span>Días retención nube</span>
                <input className="input" type="number" min={1} value={backupCfg.keepRemoteDays} onChange={(e) => setBackupCfg({ ...backupCfg, keepRemoteDays: Number(e.target.value) })} placeholder="Ej: 90" />
              </label>
              <label className="text-xs text-slate-600 space-y-1">
                <span>Hora automática</span>
                <input className="input" value={backupCfg.scheduleAt} onChange={(e) => setBackupCfg({ ...backupCfg, scheduleAt: e.target.value })} placeholder="HH:mm" />
              </label>
            </div>
            <div className="text-xs text-slate-500">
              `30` = conservar backups locales por 30 días. `90` = conservar backups en la nube por 90 días.
            </div>
            <label className="text-sm flex items-center gap-2"><input type="checkbox" checked={backupCfg.enabled} onChange={(e) => setBackupCfg({ ...backupCfg, enabled: e.target.checked })} /> Backup automático habilitado</label>
            <div className="flex gap-2">
              <button className="btn-primary">Guardar configuración</button>
              <button type="button" className="btn-secondary" onClick={() => void testBackupConnection()}>Probar conexión</button>
              <button type="button" className="btn-secondary" onClick={() => void runBackupNow()}>Respaldar ahora</button>
            </div>
            {backupMsg && <div className="text-sm text-slate-600">{backupMsg}</div>}
          </form>
          <div className="card space-y-2 text-sm text-slate-600">
            <h2 className="font-semibold text-slate-800">Notas</h2>
            <p>Esta sección guarda la configuración de la cuenta de backup para administración.</p>
            <p>La configuración requiere tener <code>rclone</code> instalado y autenticado en el servidor host.</p>
            <p>Para automatizar la ejecución diaria, usa:</p>
            <code>.\scripts\install-backup-task.ps1</code>
          </div>
        </div>
      )}
    </div>
  );
}
