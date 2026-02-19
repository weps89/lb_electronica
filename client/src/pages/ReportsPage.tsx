import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { http } from '../api/http';
import { formatInt, formatMoney, formatQty } from '../lib/numberFormat';

type ReportType = 'cash' | 'sales-detailed' | 'lot' | 'annulments' | 'utilities-monthly';
const MONTH_OPTIONS = [
  { value: 1, label: 'ENERO' },
  { value: 2, label: 'FEBRERO' },
  { value: 3, label: 'MARZO' },
  { value: 4, label: 'ABRIL' },
  { value: 5, label: 'MAYO' },
  { value: 6, label: 'JUNIO' },
  { value: 7, label: 'JULIO' },
  { value: 8, label: 'AGOSTO' },
  { value: 9, label: 'SEPTIEMBRE' },
  { value: 10, label: 'OCTUBRE' },
  { value: 11, label: 'NOVIEMBRE' },
  { value: 12, label: 'DICIEMBRE' },
];

export function ReportsPage() {
  const [type, setType] = useState<ReportType>('cash');
  const [preset, setPreset] = useState('today');
  const [day, setDay] = useState('');
  const [lotId, setLotId] = useState('');
  const [year, setYear] = useState(new Date().getFullYear());
  const [month, setMonth] = useState(new Date().getMonth() + 1);

  const dateQuery = useMemo(() => {
    if (day) return `?startDate=${encodeURIComponent(day)}&endDate=${encodeURIComponent(day)}`;
    return `?preset=${encodeURIComponent(preset)}`;
  }, [day, preset]);

  const cash = useQuery({ queryKey: ['r-cash', dateQuery], queryFn: () => http<any>(`/api/reports/cash${dateQuery}`), enabled: type === 'cash' });
  const salesDetailed = useQuery({ queryKey: ['r-sales-detail', dateQuery], queryFn: () => http<any>(`/api/reports/sales-detailed${dateQuery}`), enabled: type === 'sales-detailed' });
  const annulments = useQuery({ queryKey: ['r-annulments', dateQuery], queryFn: () => http<any>(`/api/reports/annulments${dateQuery}`), enabled: type === 'annulments' });
  const utilities = useQuery({ queryKey: ['r-utilities', year, month], queryFn: () => http<any>(`/api/reports/utilities-monthly?year=${year}&month=${month}`), enabled: type === 'utilities-monthly' });
  const lot = useQuery({ queryKey: ['r-lot', lotId], queryFn: () => http<any>(`/api/reports/lot/${lotId}`), enabled: type === 'lot' && !!lotId });
  const lots = useQuery({ queryKey: ['r-lot-options'], queryFn: () => http<any[]>('/api/stock/lots'), enabled: type === 'lot' });

  const dateSuffix = day ? `startDate=${encodeURIComponent(day)}&endDate=${encodeURIComponent(day)}` : `preset=${encodeURIComponent(preset)}`;
  const salesRows = salesDetailed.data?.data ?? [];
  const salesUnits = salesRows.reduce((acc: number, r: any) => acc + Number(r.qty ?? 0), 0);
  const salesAmount = salesRows.reduce((acc: number, r: any) => acc + Number(r.total ?? 0), 0);
  const lotRows = lot.data?.items ?? [];
  const lotQty = lotRows.reduce((acc: number, r: any) => acc + Number(r.qty ?? 0), 0);

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Reportes</h1>

      <div className="card grid md:grid-cols-4 gap-2 items-end">
        <div>
          <label className="text-xs text-slate-600">Tipo de reporte</label>
          <select className="input" value={type} onChange={(e) => setType(e.target.value as ReportType)}>
            <option value="cash">Reporte de Caja</option>
            <option value="sales-detailed">Reporte Detallado de Ventas</option>
            <option value="lot">Reporte de Lote</option>
            <option value="annulments">Reporte de Anulaciones</option>
            <option value="utilities-monthly">Reporte de Utilidades (Mensual)</option>
          </select>
        </div>

        {type !== 'lot' && type !== 'utilities-monthly' && (
          <>
            <div>
              <label className="text-xs text-slate-600">Preset</label>
              <select className="input" value={preset} onChange={(e) => setPreset(e.target.value)}>
                <option value="today">Hoy</option>
                <option value="thismonth">Este mes</option>
              </select>
            </div>
            <div>
              <label className="text-xs text-slate-600">Fecha específica</label>
              <input className="input" type="date" value={day} onChange={(e) => setDay(e.target.value)} />
            </div>
          </>
        )}

        {type === 'lot' && (
          <div>
            <label className="text-xs text-slate-600">Lote</label>
            <select className="input" value={lotId} onChange={(e) => setLotId(e.target.value)}>
              <option value="">Seleccionar lote</option>
              {lots.data?.map((l) => <option key={l.id} value={String(l.id)}>{l.batchCode} - {new Date(l.date).toLocaleDateString('es-AR')}</option>)}
            </select>
          </div>
        )}

        {type === 'utilities-monthly' && (
          <>
            <div>
              <label className="text-xs text-slate-600">Año</label>
              <input className="input" type="number" value={year} onChange={(e) => setYear(Number(e.target.value))} />
            </div>
            <div>
              <label className="text-xs text-slate-600">Mes</label>
              <select className="input" value={month} onChange={(e) => setMonth(Number(e.target.value))}>
                {MONTH_OPTIONS.map((m) => <option key={m.value} value={m.value}>{m.label}</option>)}
              </select>
            </div>
          </>
        )}
      </div>

      {type === 'cash' && (
        <div className="card space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="font-semibold">Reporte de Caja</h2>
            <a className="btn-secondary" href={`/api/reports/cash/pdf?${dateSuffix}`} target="_blank">Exportar PDF</a>
          </div>
          <div className="grid md:grid-cols-3 gap-2">
            <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-3">
              <div className="text-xs uppercase tracking-wide text-emerald-800">Ingresos</div>
              <div className="text-2xl font-semibold text-emerald-900">$ {formatMoney(cash.data?.totalIncome)}</div>
            </div>
            <div className="rounded-xl border border-rose-200 bg-rose-50 p-3">
              <div className="text-xs uppercase tracking-wide text-rose-800">Egresos</div>
              <div className="text-2xl font-semibold text-rose-900">$ {formatMoney(cash.data?.totalExpense)}</div>
            </div>
            <div className="rounded-xl border border-sky-200 bg-sky-50 p-3">
              <div className="text-xs uppercase tracking-wide text-sky-800">Saldo</div>
              <div className="text-2xl font-semibold text-sky-900">$ {formatMoney(cash.data?.balance)}</div>
            </div>
          </div>
          <div className="grid md:grid-cols-2 gap-2">
            <div>
              <h3 className="font-medium mb-1">Ingresos</h3>
              <div className="max-h-64 overflow-auto border rounded">
                <table className="table"><thead><tr><th>Fecha</th><th>Ref.</th><th>Monto</th></tr></thead><tbody>
                  {cash.data?.incomes?.map((r: any, i: number) => <tr key={i}><td>{new Date(r.createdAt || r.CreatedAt || r.date || r.Date).toLocaleString('es-AR')}</td><td>{r.reference || r.reason}</td><td>$ {formatMoney(r.income || r.amount)}</td></tr>)}
                </tbody></table>
              </div>
            </div>
            <div>
              <h3 className="font-medium mb-1">Egresos</h3>
              <div className="max-h-64 overflow-auto border rounded">
                <table className="table"><thead><tr><th>Fecha</th><th>Ref.</th><th>Monto</th></tr></thead><tbody>
                  {cash.data?.expenses?.map((r: any, i: number) => <tr key={i}><td>{new Date(r.createdAt || r.CreatedAt || r.date || r.Date).toLocaleString('es-AR')}</td><td>{r.reference || r.reason}</td><td>$ {formatMoney(r.expense || r.amount)}</td></tr>)}
                </tbody></table>
              </div>
            </div>
          </div>
        </div>
      )}

      {type === 'sales-detailed' && (
        <div className="card space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="font-semibold">Reporte Detallado de Ventas</h2>
            <a className="btn-secondary" href={`/api/reports/sales-detailed/pdf?${dateSuffix}`} target="_blank">Exportar PDF</a>
          </div>
          <div className="grid md:grid-cols-3 gap-2">
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">
              <div className="text-xs uppercase tracking-wide text-slate-600">Registros</div>
              <div className="text-2xl font-semibold">{formatInt(salesDetailed.data?.count)}</div>
            </div>
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">
              <div className="text-xs uppercase tracking-wide text-slate-600">Unidades</div>
              <div className="text-2xl font-semibold">{formatQty(salesUnits)}</div>
            </div>
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">
              <div className="text-xs uppercase tracking-wide text-slate-600">Total vendido</div>
              <div className="text-2xl font-semibold">$ {formatMoney(salesAmount)}</div>
            </div>
          </div>
          <div className="overflow-auto max-h-[520px] border rounded">
            <table className="table">
              <thead><tr><th>Fecha</th><th>Ticket</th><th>Producto</th><th>Categoría</th><th>Cant.</th><th>Precio</th><th>Total</th></tr></thead>
              <tbody>
                {salesDetailed.data?.data?.map((r: any, i: number) => (
                  <tr key={i}>
                    <td>{new Date(r.date).toLocaleString('es-AR')}</td>
                    <td>{r.ticket}</td>
                    <td>{r.product}</td>
                    <td>{r.category}</td>
                    <td>{formatQty(r.qty)}</td>
                    <td>$ {formatMoney(r.unitPrice)}</td>
                    <td>$ {formatMoney(r.total)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {type === 'lot' && (
        <div className="card space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="font-semibold">Reporte de Lote</h2>
            {lotId && <a className="btn-secondary" href={`/api/reports/lot/${lotId}/pdf`} target="_blank">Exportar PDF</a>}
          </div>
          {lot.data && (
            <>
              <div className="text-sm">Lote: <b>{lot.data.batchCode}</b> | Fecha: {new Date(lot.data.date).toLocaleString('es-AR')} | Proveedor: {lot.data.supplier || '-'}</div>
              <div className="grid md:grid-cols-3 gap-2">
                <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">
                  <div className="text-xs uppercase tracking-wide text-slate-600">Items</div>
                  <div className="text-2xl font-semibold">{formatInt(lotRows.length)}</div>
                </div>
                <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">
                  <div className="text-xs uppercase tracking-wide text-slate-600">Cantidad total</div>
                  <div className="text-2xl font-semibold">{formatQty(lotQty)}</div>
                </div>
                <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">
                  <div className="text-xs uppercase tracking-wide text-slate-600">Cotización lote</div>
                  <div className="text-2xl font-semibold">$ {formatMoney(lot.data.exchangeRateArs)}</div>
                </div>
              </div>
              <div className="overflow-auto border rounded">
                <table className="table">
                  <thead><tr><th>Producto</th><th>Categoría</th><th>Cantidad</th><th>Costo Final USD</th><th>Costo Final ARS</th></tr></thead>
                  <tbody>
                    {lot.data.items?.map((r: any, i: number) => (
                      <tr key={i}><td>{r.product}</td><td>{r.category}</td><td>{formatQty(r.qty)}</td><td>{formatMoney(r.finalUnitCostUsd)}</td><td>{formatMoney(r.finalUnitCostArs)}</td></tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </div>
      )}

      {type === 'annulments' && (
        <div className="card space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="font-semibold">Reporte de Anulaciones</h2>
            <a className="btn-secondary" href={`/api/reports/annulments/pdf?${dateSuffix}`} target="_blank">Exportar PDF</a>
          </div>
          <div className="rounded-xl border border-amber-200 bg-amber-50 p-3">
            <div className="text-xs uppercase tracking-wide text-amber-800">Total anulaciones</div>
            <div className="text-2xl font-semibold text-amber-900">{formatInt(annulments.data?.count)}</div>
          </div>
          <div className="overflow-auto border rounded max-h-[520px]">
            <table className="table"><thead><tr><th>Fecha</th><th>Tipo</th><th>Referencia</th><th>Motivo</th></tr></thead><tbody>
              {annulments.data?.data?.map((r: any, i: number) => <tr key={i}><td>{new Date(r.date).toLocaleString('es-AR')}</td><td>{r.type}</td><td>{r.reference}</td><td>{r.reason}</td></tr>)}
            </tbody></table>
          </div>
        </div>
      )}

      {type === 'utilities-monthly' && (
        <div className="card space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="font-semibold">Reporte de Utilidades</h2>
            <a className="btn-secondary" href={`/api/reports/utilities-monthly/pdf?year=${year}&month=${month}`} target="_blank">Exportar PDF</a>
          </div>
          <div className="grid md:grid-cols-4 gap-2">
            <div className="rounded-xl border border-violet-200 bg-violet-50 p-3">
              <div className="text-xs uppercase tracking-wide text-violet-800">Capital recuperado</div>
              <div className="text-2xl font-semibold text-violet-900">$ {formatMoney(utilities.data?.capitalRecovered)}</div>
            </div>
            <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-3">
              <div className="text-xs uppercase tracking-wide text-emerald-800">Utilidad bruta</div>
              <div className="text-2xl font-semibold text-emerald-900">$ {formatMoney(utilities.data?.grossProfit)}</div>
            </div>
            <div className="rounded-xl border border-rose-200 bg-rose-50 p-3">
              <div className="text-xs uppercase tracking-wide text-rose-800">Egresos</div>
              <div className="text-2xl font-semibold text-rose-900">$ {formatMoney(utilities.data?.expenses)}</div>
            </div>
            <div className="rounded-xl border border-sky-200 bg-sky-50 p-3">
              <div className="text-xs uppercase tracking-wide text-sky-800">Utilidad neta</div>
              <div className="text-2xl font-semibold text-sky-900">$ {formatMoney(utilities.data?.netProfit)}</div>
            </div>
          </div>
          <div className="text-xs text-slate-500">
            Mes en curso: el cálculo toma datos acumulados hasta este momento.
          </div>
        </div>
      )}
    </div>
  );
}
