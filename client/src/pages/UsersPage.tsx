import { FormEvent, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '../api/http';

export function UsersPage() {
  const qc = useQueryClient();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState('Cajero');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editRole, setEditRole] = useState('Cajero');
  const [editPassword, setEditPassword] = useState('');
  const [message, setMessage] = useState('');
  const { data } = useQuery({ queryKey: ['users'], queryFn: () => http<any[]>('/api/users') });

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setMessage('');
    const roleValue = role === 'Admin' ? 1 : 2;
    await http('/api/users', { method: 'POST', body: JSON.stringify({ username, password, role: roleValue }) });
    setUsername(''); setPassword(''); setRole('Cajero');
    await qc.invalidateQueries({ queryKey: ['users'] });
    setMessage('Usuario creado correctamente.');
  };

  const startEdit = (u: any) => {
    setEditingId(u.id);
    setEditRole(u.role === 'Admin' ? 'Admin' : 'Cajero');
    setEditPassword('');
    setMessage('');
  };

  const saveEdit = async () => {
    if (!editingId) return;
    setMessage('');
    const roleValue = editRole === 'Admin' ? 1 : 2;
    await http(`/api/users/${editingId}`, {
      method: 'PUT',
      body: JSON.stringify({ role: roleValue, newPassword: editPassword.trim() || null }),
    });
    setEditingId(null);
    setEditPassword('');
    await qc.invalidateQueries({ queryKey: ['users'] });
    setMessage('Usuario actualizado correctamente.');
  };

  const toggleActive = async (u: any) => {
    setMessage('');
    await http(`/api/users/${u.id}/active?active=${!u.isActive}`, { method: 'PATCH' });
    await qc.invalidateQueries({ queryKey: ['users'] });
  };

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Usuarios</h1>
      {message && <div className="card border border-emerald-300 bg-emerald-50 text-emerald-700 text-sm">{message}</div>}
      <form onSubmit={submit} className="card grid md:grid-cols-4 gap-2">
        <input className="input" value={username} onChange={e => setUsername(e.target.value)} placeholder="Usuario" required />
        <input className="input" value={password} onChange={e => setPassword(e.target.value)} placeholder="Contraseña" required />
        <select className="input" value={role} onChange={e => setRole(e.target.value)}><option>Cajero</option><option>Admin</option></select>
        <button className="btn-primary">Crear usuario</button>
      </form>
      {editingId && (
        <div className="card grid md:grid-cols-4 gap-2 items-end">
          <div>
            <label className="text-xs text-slate-600">Rol</label>
            <select className="input" value={editRole} onChange={e => setEditRole(e.target.value)}>
              <option>Cajero</option>
              <option>Admin</option>
            </select>
          </div>
          <div className="md:col-span-2">
            <label className="text-xs text-slate-600">Nueva contraseña (opcional)</label>
            <input className="input" value={editPassword} onChange={e => setEditPassword(e.target.value)} placeholder="Dejar vacío para no cambiar" />
          </div>
          <div className="flex gap-2">
            <button className="btn-primary" type="button" onClick={() => void saveEdit()}>Guardar</button>
            <button className="btn-secondary" type="button" onClick={() => { setEditingId(null); setEditRole('Cajero'); setEditPassword(''); }}>Cancelar</button>
          </div>
        </div>
      )}
      <div className="card overflow-auto">
        <table className="table">
          <thead><tr><th>Usuario</th><th>Rol</th><th>Activo</th><th>Acciones</th></tr></thead>
          <tbody>{data?.map((u) => (
            <tr key={u.id}>
              <td>{u.username}</td>
              <td>{u.role}</td>
              <td>{u.isActive ? 'Sí' : 'No'}</td>
              <td className="flex gap-2">
                <button className="btn-secondary" onClick={() => startEdit(u)}>Editar</button>
                <button className="btn-secondary" onClick={() => void toggleActive(u)}>
                  {u.isActive ? 'Desactivar' : 'Activar'}
                </button>
              </td>
            </tr>
          ))}</tbody>
        </table>
      </div>
    </div>
  );
}
