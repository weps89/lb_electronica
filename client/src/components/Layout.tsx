import { Link, NavLink, Outlet } from 'react-router-dom';
import { useAuth } from '../lib/auth';

const item = 'block px-3 py-2 rounded-md text-sm';

export function Layout() {
  const { user, logout } = useAuth();
  const isAdmin = user?.role === 'Admin';
  const rolLabel = user?.role === 'Admin' ? 'Administrador' : 'Cajero';

  return (
    <div className="min-h-screen flex">
      <aside className="w-64 bg-slate-900 text-slate-100 p-4 hidden md:block">
        <Link to="/" className="block mb-6">
          <div className="h-24 w-[calc(100%+2rem)] -mx-4 mb-3 overflow-hidden">
            <img
              src="/logo_lb.png"
              alt="LB Electronica"
              className="h-full w-full object-cover object-center invert brightness-200 contrast-200 opacity-95"
            />
          </div>
          <span className="text-2xl font-semibold tracking-tight">LB Electronica</span>
        </Link>
        <nav className="space-y-1">
          <NavLink to="/" className={item}>Panel</NavLink>
          <NavLink to="/pos" className={item}>POS</NavLink>
          <NavLink to="/cash" className={item}>Caja</NavLink>
          {isAdmin && <NavLink to="/products" className={item}>Productos</NavLink>}
          {isAdmin && <NavLink to="/stock" className={item}>Ingresos de Stock</NavLink>}
          {isAdmin && <NavLink to="/users" className={item}>Usuarios</NavLink>}
          {isAdmin && <NavLink to="/reports" className={item}>Reportes</NavLink>}
          {isAdmin && <NavLink to="/settings" className={item}>Configuraciones</NavLink>}
        </nav>
      </aside>
      <main className="flex-1 p-4 md:p-6">
        <div className="flex justify-between items-center mb-4 card">
          <div>
            <div className="font-semibold">{user?.username}</div>
            <div className="text-xs text-slate-500">{rolLabel}</div>
          </div>
          <button onClick={() => void logout()} className="btn-secondary">Salir</button>
        </div>
        <Outlet />
      </main>
    </div>
  );
}
