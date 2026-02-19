const base = import.meta.env.VITE_API_BASE || '';

export async function http<T>(path: string, init?: RequestInit): Promise<T> {
  let res: Response;
  try {
    res = await fetch(`${base}${path}`, {
      ...init,
      headers: {
        'Content-Type': 'application/json',
        ...(init?.headers || {}),
      },
      credentials: 'include',
    });
  } catch {
    throw new Error('No se pudo conectar con el servidor. Verifica que API (5080) esté activa.');
  }

  if (!res.ok) {
    const ctype = res.headers.get('content-type') || '';
    if (ctype.includes('application/json')) {
      const data = await res.json().catch(() => null);
      if (data?.message) throw new Error(`${data.message} (HTTP ${res.status})`);
    }

    const msg = await res.text();
    throw new Error((msg || `Error de solicitud (HTTP ${res.status})`).trim());
  }

  if (res.status === 204) return undefined as T;
  const ctype = res.headers.get('content-type') || '';
  if (!ctype.includes('application/json')) return (await res.text()) as T;
  return res.json();
}
