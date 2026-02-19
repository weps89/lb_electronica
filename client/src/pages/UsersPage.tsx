import { FormEvent, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '../api/http';

export function UsersPage() {
  const qc = useQueryClient();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState('Cajero');
  const { data } = useQuery({ queryKey: ['users'], queryFn: () => http<any[]>('/api/users') });

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const roleValue = role === 'Admin' ? 1 : 2;
    await http('/api/users', { method: 'POST', body: JSON.stringify({ username, password, role: roleValue }) });
    setUsername(''); setPassword('');
    await qc.invalidateQueries({ queryKey: ['users'] });
  };

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Usuarios</h1>
      <form onSubmit={submit} className="card grid md:grid-cols-4 gap-2">
        <input className="input" value={username} onChange={e => setUsername(e.target.value)} placeholder="Usuario" required />
        <input className="input" value={password} onChange={e => setPassword(e.target.value)} placeholder="Contraseña" required />
        <select className="input" value={role} onChange={e => setRole(e.target.value)}><option>Cajero</option><option>Admin</option></select>
        <button className="btn-primary">Crear usuario</button>
      </form>
      <div className="card overflow-auto">
        <table className="table">
          <thead><tr><th>Usuario</th><th>Rol</th><th>Activo</th></tr></thead>
          <tbody>{data?.map((u) => <tr key={u.id}><td>{u.username}</td><td>{u.role}</td><td>{String(u.isActive)}</td></tr>)}</tbody>
        </table>
      </div>
    </div>
  );
}
