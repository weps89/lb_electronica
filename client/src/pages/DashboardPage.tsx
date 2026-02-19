import { useQuery } from '@tanstack/react-query';
import { http } from '../api/http';
import { formatMoney, formatQty } from '../lib/numberFormat';
import { useAuth } from '../lib/auth';

export function DashboardPage() {
  const { user } = useAuth();
  const { data, isLoading } = useQuery({
    queryKey: ['dashboard'],
    queryFn: () => http<any>('/api/system/dashboard'),
  });

  if (isLoading) return <div>Cargando...</div>;

  const isAdmin = data?.role === 'Admin';
  const adminCards = [
    { key: 'todaySales', label: 'Ventas de hoy', kind: 'money' as const },
    { key: 'grossProfit', label: 'Utilidad bruta', kind: 'money' as const },
    { key: 'expenses', label: 'Egresos de hoy', kind: 'money' as const },
    { key: 'netProfit', label: 'Utilidad neta', kind: 'money' as const },
    { key: 'lowStockCount', label: 'Productos con stock bajo', kind: 'qty' as const },
  ];
  const cashierCards = [
    { key: 'todaySales', label: 'Mis ventas de hoy', kind: 'money' as const },
    { key: 'expectedCash', label: 'Caja esperada', kind: 'money' as const },
    { key: 'hasOpenCashSession', label: 'Caja abierta', kind: 'text' as const },
  ];
  const cards = isAdmin ? adminCards : cashierCards;
  const greetingName = user?.username || 'usuario';

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Panel</h1>

      <div className="card border border-sky-200 bg-gradient-to-r from-sky-50 to-cyan-50">
        <div className="text-lg font-semibold text-slate-800">Hola, {greetingName}</div>
        <div className="text-sm text-slate-600">
          Sonríe y escucha: en el comercio, una buena relación vale tanto como una buena venta.
        </div>
      </div>

      <div className="grid md:grid-cols-3 lg:grid-cols-5 gap-3">
        {cards.map((c) => {
          const raw = data?.[c.key];
          const value = c.kind === 'money'
            ? `$ ${formatMoney(Number(raw ?? 0))}`
            : c.kind === 'qty'
              ? formatQty(Number(raw ?? 0))
              : (raw ? 'Sí' : 'No');

          return (
            <div key={c.key} className="card border border-slate-200">
              <div className="text-xs uppercase tracking-wide text-slate-500">{c.label}</div>
              <div className="text-2xl font-semibold text-slate-900 mt-1">{value}</div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
