import { FormEvent, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { http } from '../api/http';
import { useAuth } from '../lib/auth';

export function LoginPage() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const navigate = useNavigate();
  const { refresh } = useAuth();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      await http('/api/auth/login', { method: 'POST', body: JSON.stringify({ username, password }) });
      await refresh();
      navigate('/');
    } catch {
      setError('Credenciales inválidas');
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center p-4 bg-gradient-to-br from-slate-200 via-white to-teal-100">
      <form onSubmit={submit} className="card w-full max-w-md space-y-3">
        <div className="flex justify-center overflow-hidden rounded-xl border border-slate-200 bg-white p-1">
          <img
            src="/logo_lb_login.png"
            alt="LB Electronica"
            className="h-24 w-full object-contain"
          />
        </div>
        <h1 className="text-2xl font-semibold">LB Electronica</h1>
        <p className="text-sm text-slate-500">Inventario y ventas local</p>
        {error && <div className="text-red-700 text-sm">{error}</div>}
        <input className="input" value={username} onChange={(e) => setUsername(e.target.value)} placeholder="Usuario" />
        <input className="input" value={password} onChange={(e) => setPassword(e.target.value)} placeholder="Contraseña" type="password" />
        <button className="btn-primary w-full" type="submit">Iniciar sesión</button>
      </form>
    </div>
  );
}
