# LB Electronica (Local LAN Inventory + POS)

Lightweight offline LAN system for inventory, sales/POS, and cash register operations.

## Stack
- Backend: ASP.NET Core 8 Web API + EF Core + SQLite (WAL enabled)
- Frontend: React + TypeScript + Vite + TailwindCSS
- Auth: JWT in HttpOnly cookie
- PDF: QuestPDF

## Project Structure
- `server/` ASP.NET Core API + SQLite logic
- `client/` React app
- `samples/pdfs/` sample PDF files
- `dev.ps1` run server + client for development
- `build.ps1` build client and publish server
- `run-prod.ps1` run published production build

## Default Seed
On first run, DB is created and seeded with:
- Admin user: `admin`
- Password: `admin123!`
- `ForcePasswordChange = true`
- Sample products

## Development Run (Windows PowerShell)
1. Install prerequisites:
- .NET SDK 8
- Node.js 20+

2. From repository root:
```powershell
.\dev.ps1
```

3. Open:
- Client: `http://localhost:5173`
- API docs: `http://localhost:5080/swagger`

## Production Run (Windows PowerShell)
1. Build and publish:
```powershell
.\build.ps1
```

2. Run production:
```powershell
.\run-prod.ps1 -Port 5080
```

3. Open from Admin PC:
- `http://localhost:5080`

## LAN Usage (2 PCs)
- PC1 (Admin) hosts server using `run-prod.ps1`.
- Find PC1 LAN IP (example `192.168.1.10`) with `ipconfig`.
- Allow inbound firewall on chosen port (default `5080`).
- From PC2 (Cashier), open:
  - `http://192.168.1.10:5080`

## Backup Instructions
- Recommended: stop server, then copy SQLite file (`publish/lb_electronica.db` or server output db path).
- Built-in backup endpoint (Admin only):
  - `POST /api/system/backup`
  - Creates timestamped copy under `backups/`.

## Security Notes
- Password hashing via BCrypt.
- Role-based endpoint protections:
  - Admin: full access.
  - Cashier: POS + Cash + My Day report.
- Cost/margin/capital-sensitive fields are hidden from cashier product responses.
- Audit logs for login, user changes, stock entries/adjustments, cost/margin changes.

## Features Implemented
- Auth, roles, login/logout/me/change-password
- User management (Admin)
- Products CRUD with auto internal code `P-000001` when barcode missing
- Stock entries + kardex ledger
- POS sales, stock deduction, ticket generation
- 80mm receipt HTML + print CSS
- WhatsApp link generation (`wa.me` with encoded receipt text)
- Cash open/movements/close + cashier My Day report
- Admin reports + PDF export:
  - Income/Expense Summary
  - Income/Expense Detail
  - Profit/Margin
  - Sales
  - Inventory
- Dashboard cards for Admin and Cashier
- Logo support in UI (`client/public/logo_lb.png`)
- Excel extraction/import scripts for initial data (`tools/`)

## Excel Data Extraction / Import
From `FEBRERO.xlsx`:

1. Extract useful datasets:
```bash
python3 tools/extract_febrero.py
```
Generates:
- `data/import/from_febrero_prices.json`
- `data/import/from_febrero_stock.json`
- `data/import/from_febrero_expenses.json`
- `data/import/seed_products_from_stock.json`
- `data/import/seed_stock_entry_items.json`
- `data/import/from_febrero_summary.json`

2. Import to API (products + stock + some expenses):
```bash
python3 tools/import_febrero_to_api.py --base http://127.0.0.1:5080 --apply
```
Optional:
- `--max-expenses 3` to control how many fixed expenses are imported.

## DB Initialization & Migrations
- SQL migrations in `server/Migrations/*.sql`
- Applied automatically at startup via `SqlMigrationService`
- EF `EnsureCreated` + seeding run on startup
- WAL enabled with `PRAGMA journal_mode=WAL`

## Local Test Checklist
1. Auth
- Login with `admin/admin123!`
- Verify forced password change flow via `/api/auth/change-password`

2. Users (Admin)
- Create cashier user
- Reset password
- Deactivate/reactivate user

3. Products (Admin)
- Create product with and without barcode
- Confirm internal code auto-generation (`P-xxxxxx`)
- Verify cost/margin hidden when logged in as cashier

4. Stock Entries (Admin)
- Add stock entry item
- Confirm stock increases
- Confirm ledger IN movement

5. POS (Admin/Cashier)
- Search/scan product and add cart item
- Finalize sale with each payment type
- Verify stock deduction
- Open receipt and print
- Open WhatsApp link

6. Cash Module (Admin/Cashier)
- Open session
- Add income and expense movements
- Close with counted cash
- Validate difference and My Day report

7. Reports (Admin)
- Run each report (today/month/custom)
- Export each PDF endpoint and open files

8. LAN test from second PC
- Run production on PC1
- Access `http://<PC1-IP>:5080` from PC2
- Login as cashier and perform sale + cash movement
