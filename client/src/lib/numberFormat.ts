const money = new Intl.NumberFormat('es-AR', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const qty = new Intl.NumberFormat('es-AR', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const integer = new Intl.NumberFormat('es-AR', {
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

export function formatMoney(value: number | null | undefined) {
  return money.format(Number(value ?? 0));
}

export function formatQty(value: number | null | undefined) {
  return qty.format(Number(value ?? 0));
}

export function formatInt(value: number | null | undefined) {
  return integer.format(Number(value ?? 0));
}

export function formatObjectNumbers<T>(value: T): T {
  if (typeof value === 'number') return formatMoney(value) as T;
  if (Array.isArray(value)) return value.map((x) => formatObjectNumbers(x)) as T;
  if (value && typeof value === 'object') {
    const out: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(value as Record<string, unknown>)) {
      out[k] = formatObjectNumbers(v);
    }
    return out as T;
  }
  return value;
}
