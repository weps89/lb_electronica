import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '../api/http';

type CollectMethod = 'Cash' | 'Card' | 'Transfer';

type PendingInvoice = {
  id: number;
  ticketNumber: string;
  date: string;
  total: number;
  subtotal: number;
  discountTotal: number;
  suggestedPaymentMethod: CollectMethod;
  seller: string;
  itemsCount: number;
};

const EXPENSE_CATEGORIES = [
  'PAGO A PROVEEDORES',
  'SUELDO',
  'SERVICIOS BASICOS',
  'HONORARIOS PROFESIONALES',
  'IMPUESTOS (A.R.C.A)',
  'OBRA SOCIAL',
  'SERVICIO DE INTERNET',
  'MANTENIMIENTO DE MOVILES',
  'GASTOS DE OFICINA',
  'INSUMOS PARA LIMPIEZA',
  'COMISIONES PAGO T.C.',
  'DEVOLUCIONES',
] as const;

const moneyFormat = new Intl.NumberFormat('es-AR', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const intFormat = new Intl.NumberFormat('es-AR', {
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

function formatMoney(value: number) {
  return moneyFormat.format(Number.isFinite(value) ? value : 0);
}

function parseArNumber(value: string) {
  const cleaned = value
    .trim()
    .replace(/\s/g, '')
    .replace(/\./g, '')
    .replace(',', '.')
    .replace(/[^\d.-]/g, '');
  const num = Number(cleaned);
  return Number.isFinite(num) ? num : 0;
}

function normalizeMoneyInput(raw: string) {
  return raw.replace(/[^\d,.-]/g, '');
}

export function CashPage() {
  const qc = useQueryClient();
  const [openingInput, setOpeningInput] = useState('0,00');
  const [showOpenModal, setShowOpenModal] = useState(false);
  const [exchangeRateInput, setExchangeRateInput] = useState('0,00');
  const [amountInput, setAmountInput] = useState('0,00');
  const [reason, setReason] = useState('');
  const [expenseCategory, setExpenseCategory] = useState<string>(EXPENSE_CATEGORIES[0]);
  const [countedInput, setCountedInput] = useState('0,00');

  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [collectMethod, setCollectMethod] = useState<CollectMethod>('Cash');
  const [receivedInput, setReceivedInput] = useState('0,00');
  const [operationNumber, setOperationNumber] = useState('');
  const [verifyTransfer, setVerifyTransfer] = useState(true);
  const [cashError, setCashError] = useState('');
  const [cashInfo, setCashInfo] = useState('');

  const [annulTarget, setAnnulTarget] = useState<PendingInvoice | null>(null);
  const [annulReason, setAnnulReason] = useState('');

  const { data: current } = useQuery({
    queryKey: ['cash-current'],
    queryFn: () => http<any>('/api/cash/current'),
    refetchInterval: 5000,
  });

  const { data: myDay } = useQuery({
    queryKey: ['cash-my-day'],
    queryFn: () => http<any>('/api/cash/my-day'),
    refetchInterval: 7000,
  });

  const { data: pending } = useQuery({
    queryKey: ['cash-pending'],
    queryFn: () => http<PendingInvoice[]>('/api/cash/pending-invoices'),
    refetchInterval: 4000,
  });

  const refreshAll = async () => {
    await qc.invalidateQueries({ queryKey: ['cash-current'] });
    await qc.invalidateQueries({ queryKey: ['cash-my-day'] });
    await qc.invalidateQueries({ queryKey: ['cash-pending'] });
    await qc.invalidateQueries({ queryKey: ['products'] });
    await qc.invalidateQueries({ queryKey: ['sales'] });
  };

  const selectedInvoice = useMemo(
    () => pending?.find((x) => x.id === selectedId) ?? null,
    [pending, selectedId],
  );

  const openingAmount = parseArNumber(openingInput);
  const movementAmount = parseArNumber(amountInput);
  const countedAmount = parseArNumber(countedInput);
  const receivedAmount = parseArNumber(receivedInput);

  const expectedChange = useMemo(() => {
    if (!selectedInvoice || collectMethod !== 'Cash') return 0;
    return Math.max(0, receivedAmount - Number(selectedInvoice.total || 0));
  }, [selectedInvoice, collectMethod, receivedAmount]);

  const open = async () => {
    setCashError('');
    setCashInfo('');
    try {
      const rate = parseArNumber(exchangeRateInput);
      if (rate > 0) {
        await http('/api/system/exchange-rate', {
          method: 'POST',
          body: JSON.stringify({ arsPerUsd: rate }),
        });
      }
      await http('/api/cash/open', {
        method: 'POST',
        body: JSON.stringify({ openingAmount }),
      });
      setCashInfo('Caja abierta correctamente.');
      setShowOpenModal(false);
      await refreshAll();
    } catch (e: any) {
      setCashError(String(e?.message || 'No se pudo abrir caja'));
    }
  };

  const move = async (type: 'Ingreso' | 'Gasto') => {
    setCashError('');
    setCashInfo('');

    if (!reason.trim()) {
      setCashError('Debes ingresar un motivo o detalle.');
      return;
    }

    try {
      const typeValue = type === 'Ingreso' ? 1 : 2;
      await http('/api/cash/movement', {
        method: 'POST',
        body: JSON.stringify({
          type: typeValue,
          amount: movementAmount,
          reason,
          category: type === 'Gasto' ? expenseCategory : 'INGRESO_MANUAL',
        }),
      });
      setAmountInput('0,00');
      setReason('');
      setCashInfo(`${type} registrado.`);
      await refreshAll();
    } catch (e: any) {
      setCashError(String(e?.message || `No se pudo registrar ${type.toLowerCase()}`));
    }
  };

  const close = async () => {
    setCashError('');
    setCashInfo('');
    try {
      await http('/api/cash/close', {
        method: 'POST',
        body: JSON.stringify({ countedCash: countedAmount }),
      });
      setCashInfo('Caja cerrada.');
      await refreshAll();
    } catch (e: any) {
      setCashError(String(e?.message || 'No se pudo cerrar caja'));
    }
  };

  const prepareCollect = (invoice: PendingInvoice) => {
    setSelectedId(invoice.id);
    setCollectMethod(invoice.suggestedPaymentMethod || 'Cash');
    setReceivedInput(formatMoney(Number(invoice.total ?? 0)));
    setOperationNumber('');
    setVerifyTransfer(true);
    setCashError('');
    setCashInfo('');
  };

  const collect = async () => {
    if (!selectedInvoice) return;

    setCashError('');
    setCashInfo('');

    if (!current) {
      setCashError('Debes abrir caja para cobrar facturas pendientes.');
      return;
    }

    if (collectMethod === 'Cash' && receivedAmount < Number(selectedInvoice.total)) {
      setCashError('El monto recibido es insuficiente.');
      return;
    }

    if (collectMethod !== 'Cash' && !operationNumber.trim()) {
      setCashError('Debes ingresar número de operación.');
      return;
    }

    try {
      const methodMap: Record<CollectMethod, number> = { Cash: 1, Transfer: 2, Card: 3 };
      await http('/api/cash/collect-invoice', {
        method: 'POST',
        body: JSON.stringify({
          saleId: selectedInvoice.id,
          paymentMethod: methodMap[collectMethod],
          receivedAmount: collectMethod === 'Cash' ? receivedAmount : null,
          operationNumber: collectMethod === 'Cash' ? null : operationNumber.trim(),
          verified: collectMethod === 'Transfer' ? verifyTransfer : false,
          customer: null,
        }),
      });

      setCashInfo(`Factura ${selectedInvoice.ticketNumber} cobrada.`);
      setSelectedId(null);
      setOperationNumber('');
      await refreshAll();
    } catch (e: any) {
      setCashError(String(e?.message || 'No se pudo cobrar factura'));
    }
  };

  const openAnnulModal = (invoice: PendingInvoice) => {
    setAnnulTarget(invoice);
    setAnnulReason('');
    setCashError('');
    setCashInfo('');
  };

  const confirmAnnul = async () => {
    if (!annulTarget) return;
    if (!annulReason.trim()) {
      setCashError('Debes ingresar un motivo de anulación.');
      return;
    }

    setCashError('');
    setCashInfo('');

    try {
      await http('/api/cash/annul-invoice', {
        method: 'POST',
        body: JSON.stringify({ saleId: annulTarget.id, reason: annulReason.trim() }),
      });
      setCashInfo(`Factura ${annulTarget.ticketNumber} anulada y stock restituido.`);
      if (selectedId === annulTarget.id) setSelectedId(null);
      setAnnulTarget(null);
      setAnnulReason('');
      await refreshAll();
    } catch (e: any) {
      setCashError(String(e?.message || 'No se pudo anular la factura'));
    }
  };

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Módulo de Caja</h1>

      {cashError && <div className="card border-red-300 bg-red-50 text-red-700 text-sm">{cashError}</div>}
      {cashInfo && <div className="card border-emerald-300 bg-emerald-50 text-emerald-700 text-sm">{cashInfo}</div>}

      {!current ? (
        <div className="card flex gap-2 items-center flex-wrap">
          <button className="btn-primary" onClick={() => setShowOpenModal(true)}>Abrir caja</button>
          <div className="text-sm text-slate-500">Al abrir caja, ingresa apertura y cotización del día.</div>
        </div>
      ) : (
        <div className="card space-y-2">
          <div>Abierta en: {new Date(current.openedAt).toLocaleString('es-AR')}</div>
          <div className="grid md:grid-cols-4 gap-2 items-end">
            <div>
              <label className="text-xs text-slate-600">Monto</label>
              <input
                className="input"
                inputMode="decimal"
                value={amountInput}
                onChange={(e) => setAmountInput(normalizeMoneyInput(e.target.value))}
                onBlur={() => setAmountInput(formatMoney(movementAmount))}
                placeholder="0,00"
              />
            </div>
            <div>
              <label className="text-xs text-slate-600">Detalle / Motivo</label>
              <input className="input" value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Detalle del movimiento" />
            </div>
            <div>
              <label className="text-xs text-slate-600">Categoría de egreso</label>
              <select className="input" value={expenseCategory} onChange={(e) => setExpenseCategory(e.target.value)}>
                {EXPENSE_CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
              </select>
            </div>
            <div className="flex gap-2">
              <button className="btn-secondary" onClick={() => void move('Ingreso')}>Registrar ingreso</button>
              <button className="btn-secondary" onClick={() => void move('Gasto')}>Registrar gasto</button>
            </div>
          </div>
          <div className="grid md:grid-cols-3 gap-2 items-end">
            <div>
              <label className="text-xs text-slate-600">Efectivo contado</label>
              <input
                className="input"
                inputMode="decimal"
                value={countedInput}
                onChange={(e) => setCountedInput(normalizeMoneyInput(e.target.value))}
                onBlur={() => setCountedInput(formatMoney(countedAmount))}
                placeholder="0,00"
              />
            </div>
            <button className="btn-primary" onClick={() => void close()}>Cerrar caja</button>
          </div>
        </div>
      )}

      <div className="card space-y-3">
        <div className="flex items-center justify-between gap-2">
          <h2 className="font-semibold">Facturas pendientes / ventas por cobrar</h2>
          <button className="btn-secondary" onClick={() => void refreshAll()}>Actualizar</button>
        </div>

        {!current && (
          <div className="text-sm text-amber-700">Abre caja para poder registrar cobros.</div>
        )}

        <div className="overflow-auto">
          <table className="table">
            <thead>
              <tr>
                <th>Ticket</th>
                <th>Fecha</th>
                <th>Vendedor</th>
                <th>Items</th>
                <th>Total</th>
                <th>Método sugerido</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {(pending ?? []).map((p) => (
                <tr key={p.id} className={selectedId === p.id ? 'bg-sky-50' : ''}>
                  <td>{p.ticketNumber}</td>
                  <td>{new Date(p.date).toLocaleString('es-AR')}</td>
                  <td>{p.seller}</td>
                  <td>{intFormat.format(Number(p.itemsCount ?? 0))}</td>
                  <td>$ {formatMoney(Number(p.total ?? 0))}</td>
                  <td>{p.suggestedPaymentMethod === 'Cash' ? 'Efectivo' : p.suggestedPaymentMethod === 'Card' ? 'Tarjeta' : 'Transferencia'}</td>
                  <td className="flex gap-2">
                    <button className="btn-primary" onClick={() => prepareCollect(p)}>Cobrar</button>
                    <button className="btn-secondary !bg-red-100 !text-red-700 hover:!bg-red-200" onClick={() => openAnnulModal(p)}>Anular</button>
                  </td>
                </tr>
              ))}
              {!pending?.length && (
                <tr><td colSpan={7} className="text-slate-500">No hay facturas pendientes.</td></tr>
              )}
            </tbody>
          </table>
        </div>

        {selectedInvoice && (
          <div className="rounded-lg border border-sky-200 bg-sky-50 p-3 space-y-3">
            <div className="font-medium">
              Cobrar factura {selectedInvoice.ticketNumber} por $ {formatMoney(Number(selectedInvoice.total ?? 0))}
            </div>
            <div className="grid md:grid-cols-4 gap-2 items-end">
              <div>
                <label className="text-xs text-slate-600">Método de pago</label>
                <select className="input" value={collectMethod} onChange={(e) => setCollectMethod(e.target.value as CollectMethod)}>
                  <option value="Cash">Efectivo</option>
                  <option value="Card">Tarjeta</option>
                  <option value="Transfer">Transferencia</option>
                </select>
              </div>

              {collectMethod === 'Cash' ? (
                <>
                  <div>
                    <label className="text-xs text-slate-600">Monto recibido</label>
                    <input
                      className="input"
                      inputMode="decimal"
                      value={receivedInput}
                      onChange={(e) => setReceivedInput(normalizeMoneyInput(e.target.value))}
                      onBlur={() => setReceivedInput(formatMoney(receivedAmount))}
                      placeholder="0,00"
                    />
                  </div>
                  <div>
                    <label className="text-xs text-slate-600">Vuelto</label>
                    <input className="input" value={`$ ${formatMoney(expectedChange)}`} readOnly />
                  </div>
                </>
              ) : (
                <div>
                  <label className="text-xs text-slate-600">Nro de operación</label>
                  <input className="input" value={operationNumber} onChange={(e) => setOperationNumber(e.target.value)} placeholder="Ej: OP-12345" />
                </div>
              )}

              {collectMethod === 'Transfer' && (
                <label className="flex items-center gap-2 text-sm">
                  <input type="checkbox" checked={verifyTransfer} onChange={(e) => setVerifyTransfer(e.target.checked)} />
                  Transferencia verificada
                </label>
              )}
            </div>

            <div className="flex gap-2">
              <button className="btn-primary" onClick={() => void collect()}>Confirmar cobro</button>
              <button className="btn-secondary" onClick={() => setSelectedId(null)}>Cancelar</button>
            </div>
          </div>
        )}
      </div>

      <div className="card">
        <h2 className="font-semibold mb-2">Mi reporte del día</h2>
        <div className="grid md:grid-cols-4 gap-2">
          <div className="rounded-lg border border-slate-200 bg-slate-50 p-3">
            <div className="text-xs text-slate-500">Ventas cobradas</div>
            <div className="text-xl font-semibold">{intFormat.format(Number(myDay?.salesCount ?? 0))}</div>
          </div>
          <div className="rounded-lg border border-slate-200 bg-slate-50 p-3">
            <div className="text-xs text-slate-500">Total ventas cobradas</div>
            <div className="text-xl font-semibold">$ {formatMoney(Number(myDay?.salesTotal ?? 0))}</div>
          </div>
          <div className="rounded-lg border border-slate-200 bg-slate-50 p-3">
            <div className="text-xs text-slate-500">Ingresos no venta</div>
            <div className="text-xl font-semibold">$ {formatMoney(Number(myDay?.incomes ?? 0))}</div>
          </div>
          <div className="rounded-lg border border-slate-200 bg-slate-50 p-3">
            <div className="text-xs text-slate-500">Egresos</div>
            <div className="text-xl font-semibold">$ {formatMoney(Number(myDay?.expenses ?? 0))}</div>
          </div>
        </div>
        <div className="text-xs text-slate-500 mt-3">
          El detalle por ventas y egresos está disponible en el módulo Reportes.
        </div>
      </div>

      {annulTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
          <div className="w-full max-w-md rounded-xl bg-white p-4 shadow-xl space-y-3">
            <h3 className="text-lg font-semibold">Anular factura {annulTarget.ticketNumber}</h3>
            <p className="text-sm text-slate-600">
              Esta acción devuelve productos al stock y cancela la factura pendiente.
            </p>
            <div>
              <label className="text-xs text-slate-600">Motivo de anulación</label>
              <textarea
                className="input min-h-24"
                value={annulReason}
                onChange={(e) => setAnnulReason(e.target.value)}
                placeholder="Ej: error de carga, cliente canceló compra, etc."
              />
            </div>
            <div className="flex justify-end gap-2">
              <button className="btn-secondary" onClick={() => setAnnulTarget(null)}>Cancelar</button>
              <button className="btn-primary !bg-red-600 hover:!bg-red-700" onClick={() => void confirmAnnul()}>Confirmar anulación</button>
            </div>
          </div>
        </div>
      )}

      {showOpenModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
          <div className="w-full max-w-md rounded-xl bg-white p-4 shadow-xl space-y-3">
            <h3 className="text-lg font-semibold">Apertura de caja</h3>
            <div>
              <label className="text-xs text-slate-600">Monto de apertura</label>
              <input
                className="input"
                inputMode="decimal"
                value={openingInput}
                onChange={(e) => setOpeningInput(normalizeMoneyInput(e.target.value))}
                onBlur={() => setOpeningInput(formatMoney(openingAmount))}
                placeholder="0,00"
              />
            </div>
            <div>
              <label className="text-xs text-slate-600">Cotización del día (ARS/USD)</label>
              <input
                className="input"
                inputMode="decimal"
                value={exchangeRateInput}
                onChange={(e) => setExchangeRateInput(normalizeMoneyInput(e.target.value))}
                placeholder="0,00"
              />
            </div>
            <div className="flex justify-end gap-2">
              <button className="btn-secondary" onClick={() => setShowOpenModal(false)}>Cancelar</button>
              <button className="btn-primary" onClick={() => void open()}>Confirmar apertura</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
