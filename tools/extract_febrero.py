#!/usr/bin/env python3
import json
import re
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

NS = {
    "a": "http://schemas.openxmlformats.org/spreadsheetml/2006/main",
    "r": "http://schemas.openxmlformats.org/officeDocument/2006/relationships",
}


def col_to_idx(col: str) -> int:
    n = 0
    for c in col:
        if c.isalpha():
            n = n * 26 + (ord(c.upper()) - 64)
    return n


def to_num(v):
    if v is None:
        return None
    s = str(v).strip().replace(",", ".")
    if not s:
        return None
    try:
        return float(s)
    except ValueError:
        return None


def load_sheets(xlsx_path: Path):
    with zipfile.ZipFile(xlsx_path) as z:
        shared = []
        if "xl/sharedStrings.xml" in z.namelist():
            root = ET.fromstring(z.read("xl/sharedStrings.xml"))
            for si in root.findall("a:si", NS):
                txt = "".join(t.text or "" for t in si.findall(".//a:t", NS))
                shared.append(txt)

        wb = ET.fromstring(z.read("xl/workbook.xml"))
        rels = ET.fromstring(z.read("xl/_rels/workbook.xml.rels"))
        rel_map = {
            r.attrib["Id"]: r.attrib["Target"]
            for r in rels.findall("{http://schemas.openxmlformats.org/package/2006/relationships}Relationship")
        }

        sheets = {}
        for s in wb.findall("a:sheets/a:sheet", NS):
            name = s.attrib["name"]
            rid = s.attrib["{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id"]
            target = rel_map[rid]
            if not target.startswith("xl/"):
                target = "xl/" + target

            root = ET.fromstring(z.read(target))
            rows = []
            for row in root.findall("a:sheetData/a:row", NS):
                vals = {}
                for c in row.findall("a:c", NS):
                    ref = c.attrib.get("r", "A1")
                    col = "".join(ch for ch in ref if ch.isalpha())
                    idx = col_to_idx(col)
                    t = c.attrib.get("t")
                    v = c.find("a:v", NS)
                    value = ""
                    if v is not None:
                        raw = v.text or ""
                        if t == "s":
                            try:
                                value = shared[int(raw)]
                            except Exception:
                                value = raw
                        else:
                            value = raw
                    else:
                        isel = c.find("a:is", NS)
                        if isel is not None:
                            value = "".join(tn.text or "" for tn in isel.findall(".//a:t", NS))
                    vals[idx] = value

                if vals:
                    maxc = max(vals)
                    rows.append([vals.get(i, "") for i in range(1, maxc + 1)])
            sheets[name] = rows
        return sheets


def extract_price_sheet(rows):
    out = []
    detected_rate = None
    if not rows:
        return out, detected_rate
    for r in rows[1:]:
        if len(r) < 2:
            continue
        category = str(r[0]).strip()
        name = str(r[1]).strip()
        if not name or name.upper() == "PRODUCTO":
            continue

        sale_price = to_num(r[2] if len(r) > 2 else None)
        cost_price = to_num(r[9] if len(r) > 9 else None) or to_num(r[7] if len(r) > 7 else None)
        if sale_price is None and cost_price is None:
            continue

        if len(r) > 14 and detected_rate is None:
            maybe_rate = to_num(r[14])
            if maybe_rate and maybe_rate > 100:
                detected_rate = maybe_rate

        out.append(
            {
                "category": category or "General",
                "name": name,
                "sale_price_ars": sale_price,
                "cost_price": cost_price,
            }
        )
    return out, detected_rate


def extract_stock_sheet(rows):
    out = []
    for r in rows:
        if len(r) < 4:
            continue
        category = str(r[0]).strip()
        name = str(r[1]).strip()
        if not name or name.upper() in ("PRODUCTO", "STOCK"):
            continue

        qty = to_num(r[2])
        cost = to_num(r[3])
        if qty is None or qty <= 0:
            continue

        out.append(
            {
                "category": category or "General",
                "name": name,
                "qty": qty,
                "cost_price": cost or 0,
            }
        )
    return out


def extract_expenses_sheet(rows):
    out = []
    for r in rows:
        label = str(r[0]).strip() if len(r) > 0 else ""
        amount = to_num(r[4] if len(r) > 4 else None)
        if not label or amount is None or amount <= 0:
            continue

        upper = label.upper()
        if upper.startswith("TOTAL"):
            continue
        if upper in ("FUNCIONARIOS", "GASTOS Y COSTOS FIJOS"):
            continue

        out.append({"reason": label, "amount": amount, "type": "expense"})
    return out


def main():
    xlsx_path = Path("/home/walter/Descargas/FEBRERO.xlsx")
    out_dir = Path("data/import")
    out_dir.mkdir(parents=True, exist_ok=True)

    sheets = load_sheets(xlsx_path)

    price_name = next((n for n in sheets if "LISTA" in n.upper()), None)
    stock_name = next((n for n in sheets if "STOCK" in n.upper()), None)
    expense_name = next((n for n in sheets if "GASTOS" in n.upper()), None)

    price_rows = sheets.get(price_name, [])
    stock_rows = sheets.get(stock_name, [])
    expense_rows = sheets.get(expense_name, [])

    prices, detected_rate = extract_price_sheet(price_rows)
    stock = extract_stock_sheet(stock_rows)
    expenses = extract_expenses_sheet(expense_rows)
    exchange_rate = detected_rate or 1450.0

    prices_by_name = {re.sub(r"\s+", " ", p["name"].strip().upper()): p for p in prices}

    initial_products = []
    stock_entry_items = []
    for s in stock:
        key = re.sub(r"\s+", " ", s["name"].strip().upper())
        p = prices_by_name.get(key)
        cost = s["cost_price"] or (p.get("cost_price") if p else 0) or 0
        sale = (p.get("sale_price_ars") if p else None)
        if sale is None:
            sale = round(cost * 1.8, 2) if cost > 0 else 0
        margin = round(((sale / cost) - 1) * 100, 2) if cost > 0 else 0

        initial_products.append(
            {
                "name": s["name"],
                "category": s["category"],
                "cost_price": round(cost, 2),
                "sale_price": round(sale, 2),
                "margin_percent": margin,
                "stock_minimum": 1,
            }
        )
        stock_entry_items.append(
            {
                "product_name": s["name"],
                "qty": s["qty"],
                "cost_price": round(cost, 2),
                "margin_percent": margin,
            }
        )

    seed_from_price_list = []
    for p in prices:
        cost_ars = p.get("cost_price") or 0
        sale_ars = p.get("sale_price_ars") or 0
        if cost_ars <= 0 and sale_ars > 0:
            cost_ars = round(sale_ars / 1.8, 2)
        cost_usd = round((cost_ars / exchange_rate), 6) if exchange_rate > 0 else 0
        margin = 80.0
        if cost_ars > 0 and sale_ars > 0:
            margin = round(((sale_ars / cost_ars) - 1) * 100, 2)

        seed_from_price_list.append(
            {
                "name": p["name"],
                "category": p["category"],
                "cost_price_usd": cost_usd,
                "stock_quantity": 0,
                "stock_minimum": 1,
                "margin_percent": margin,
                "cash_price_ars_ref": sale_ars,
            }
        )

    (out_dir / "from_febrero_prices.json").write_text(json.dumps(prices, ensure_ascii=False, indent=2), encoding="utf-8")
    (out_dir / "from_febrero_stock.json").write_text(json.dumps(stock, ensure_ascii=False, indent=2), encoding="utf-8")
    (out_dir / "from_febrero_expenses.json").write_text(json.dumps(expenses, ensure_ascii=False, indent=2), encoding="utf-8")
    (out_dir / "seed_products_from_stock.json").write_text(json.dumps(initial_products, ensure_ascii=False, indent=2), encoding="utf-8")
    (out_dir / "seed_stock_entry_items.json").write_text(json.dumps(stock_entry_items, ensure_ascii=False, indent=2), encoding="utf-8")
    (out_dir / "seed_products_from_price_list.json").write_text(json.dumps(seed_from_price_list, ensure_ascii=False, indent=2), encoding="utf-8")

    summary = {
        "sheets_detected": {
            "price": price_name,
            "stock": stock_name,
            "expenses": expense_name,
        },
        "counts": {
            "prices": len(prices),
            "stock_rows": len(stock),
            "expenses": len(expenses),
            "seed_products": len(initial_products),
            "seed_products_price_list": len(seed_from_price_list),
        },
        "exchange_rate_detected": exchange_rate,
        "examples": {
            "product": initial_products[:5],
            "expense": expenses[:10],
        },
    }
    (out_dir / "from_febrero_summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")

    print(json.dumps(summary, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
