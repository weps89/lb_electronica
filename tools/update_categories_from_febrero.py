#!/usr/bin/env python3
import re
import sqlite3
from pathlib import Path

from extract_febrero import load_sheets, extract_price_sheet

ROOT = Path('/var/www/html/lb_electronica')
DB = ROOT / 'server' / 'lb_electronica.db'
XLSX_CANDIDATES = [
    Path('/home/walter/descargas/FEBRERO.xlsx'),
    Path('/home/walter/Descargas/FEBRERO.xlsx'),
]


def norm(s: str) -> str:
    s = (s or '').strip().upper()
    s = re.sub(r'\s+', ' ', s)
    return s


def main():
    xlsx = next((p for p in XLSX_CANDIDATES if p.exists()), None)
    if xlsx is None:
        raise SystemExit(f'No se encontró FEBRERO.xlsx en: {XLSX_CANDIDATES}')
    if not DB.exists():
        raise SystemExit(f'No se encontró DB: {DB}')

    sheets = load_sheets(xlsx)
    price_name = next((n for n in sheets if 'LISTA' in n.upper()), None)
    if not price_name:
        raise SystemExit('No se encontró hoja de lista de precios')

    prices, _ = extract_price_sheet(sheets[price_name])
    mapping = {}
    for p in prices:
        name = norm(p.get('name', ''))
        cat = (p.get('category') or 'General').strip()
        if name:
            mapping[name] = cat

    con = sqlite3.connect(DB)
    cur = con.cursor()

    # ensure ProductCategories table exists for new ABM
    cur.execute(
        """
        CREATE TABLE IF NOT EXISTS ProductCategories (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL UNIQUE,
            Active INTEGER NOT NULL DEFAULT 1,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        )
        """
    )

    cur.execute('SELECT Id, Name, Category FROM Products')
    rows = cur.fetchall()
    updated = 0
    inserted_categories = 0

    for pid, pname, old_cat in rows:
        key = norm(pname or '')
        if key not in mapping:
            continue
        new_cat = mapping[key]
        if (old_cat or '').strip() == new_cat:
            continue

        cur.execute('UPDATE Products SET Category = ?, UpdatedAt = datetime(\'now\') WHERE Id = ?', (new_cat, pid))
        updated += 1

    # sync categories catalog from final product categories
    cur.execute('SELECT DISTINCT Category FROM Products WHERE Category IS NOT NULL AND trim(Category) <> ""')
    for (cat,) in cur.fetchall():
        cur.execute('SELECT 1 FROM ProductCategories WHERE Name = ?', (cat,))
        if cur.fetchone() is None:
            cur.execute(
                'INSERT INTO ProductCategories(Name, Active, CreatedAt, UpdatedAt) VALUES(?, 1, datetime(\'now\'), datetime(\'now\'))',
                (cat,),
            )
            inserted_categories += 1

    con.commit()
    con.close()

    print(f'XLSX: {xlsx}')
    print(f'Productos con categoría actualizada: {updated}')
    print(f'Categorías insertadas en catálogo: {inserted_categories}')


if __name__ == '__main__':
    main()
