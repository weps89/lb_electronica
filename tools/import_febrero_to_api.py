#!/usr/bin/env python3
import argparse
import json
import re
from http.cookiejar import CookieJar
from pathlib import Path
from urllib import request, parse, error

BASE = "http://127.0.0.1:5081"


def norm(s: str) -> str:
    return re.sub(r"\s+", " ", (s or "").strip().upper())


class Api:
    def __init__(self, base: str):
        self.base = base.rstrip("/")
        self.jar = CookieJar()
        self.opener = request.build_opener(request.HTTPCookieProcessor(self.jar))

    def _call(self, method: str, path: str, data=None):
        url = self.base + path
        payload = None
        headers = {"Content-Type": "application/json"}
        if data is not None:
            payload = json.dumps(data).encode("utf-8")
        req = request.Request(url, data=payload, headers=headers, method=method)
        try:
            with self.opener.open(req, timeout=30) as res:
                body = res.read().decode("utf-8")
                ctype = res.headers.get("Content-Type", "")
                if "application/json" in ctype and body:
                    return res.status, json.loads(body)
                return res.status, body
        except error.HTTPError as ex:
            body = ex.read().decode("utf-8", errors="ignore")
            raise RuntimeError(f"{method} {path} -> HTTP {ex.code}: {body}")

    def login(self, username: str, password: str):
        self._call("POST", "/api/auth/login", {"username": username, "password": password})

    def get_products(self, q: str):
        status, data = self._call("GET", "/api/products?q=" + parse.quote(q))
        return data if status == 200 else []

    def create_product(self, payload):
        return self._call("POST", "/api/products", payload)[1]

    def create_stock_entry(self, payload):
        return self._call("POST", "/api/stock/entries", payload)[1]

    def cash_current(self):
        return self._call("GET", "/api/cash/current")[1]

    def cash_open(self, amount: float):
        return self._call("POST", "/api/cash/open", {"openingAmount": amount})[1]

    def cash_movement(self, movement_type: int, amount: float, reason: str):
        return self._call(
            "POST",
            "/api/cash/movement",
            {"type": movement_type, "amount": amount, "reason": reason, "category": "GASTO FIJO"},
        )[1]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", default=BASE)
    ap.add_argument("--username", default="admin")
    ap.add_argument("--password", default="admin123!")
    ap.add_argument("--data-dir", default="data/import")
    ap.add_argument("--max-expenses", type=int, default=3)
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    data_dir = Path(args.data_dir)
    products_seed = json.loads((data_dir / "seed_products_from_stock.json").read_text(encoding="utf-8"))
    products_price_list = json.loads((data_dir / "seed_products_from_price_list.json").read_text(encoding="utf-8"))
    stock_seed = json.loads((data_dir / "seed_stock_entry_items.json").read_text(encoding="utf-8"))
    expenses_seed = json.loads((data_dir / "from_febrero_expenses.json").read_text(encoding="utf-8"))

    api = Api(args.base)
    api.login(args.username, args.password)

    # 1) Ensure full price list exists (stock defaults to 0)
    created_from_price_list = 0
    for p in products_price_list:
        matches = api.get_products(p["name"]) or []
        found = next((x for x in matches if norm(x.get("name", "")) == norm(p["name"])), None)
        if found:
            mapped[norm(p["name"])] = found["id"]
            continue

        payload = {
            "barcode": None,
            "name": p["name"],
            "category": p["category"],
            "brand": None,
            "model": None,
            "imeiOrSerial": None,
            "costPrice": p["cost_price_usd"],
            "marginPercent": p["margin_percent"],
            "salePrice": None,
            "stockQuantity": 0,
            "stockMinimum": p["stock_minimum"],
            "active": True,
        }
        if args.apply:
            created_product = api.create_product(payload)
            mapped[norm(p["name"])] = created_product["id"]
        created_from_price_list += 1

    # 2) Ensure stock sheet products exist too
    created = 0

    for p in products_seed:
        matches = api.get_products(p["name"]) or []
        found = next((x for x in matches if norm(x.get("name", "")) == norm(p["name"])), None)
        if found:
            mapped[norm(p["name"])] = found["id"]
            continue

        payload = {
            "barcode": None,
            "name": p["name"],
            "category": p["category"],
            "brand": None,
            "model": None,
            "imeiOrSerial": None,
            "costPrice": p["cost_price"],
            "marginPercent": p["margin_percent"],
            "salePrice": p["sale_price"],
            "stockQuantity": 0,
            "stockMinimum": p["stock_minimum"],
            "active": True,
        }
        if args.apply:
            created_product = api.create_product(payload)
            mapped[norm(p["name"])] = created_product["id"]
        created += 1

    stock_items = []
    missing = []
    for s in stock_seed:
        pid = mapped.get(norm(s["product_name"]))
        if not pid:
            matches = api.get_products(s["product_name"]) or []
            found = next((x for x in matches if norm(x.get("name", "")) == norm(s["product_name"])), None)
            pid = found["id"] if found else None
        if not pid:
            missing.append(s["product_name"])
            continue
        stock_items.append(
            {
                "productId": int(pid),
                "qty": float(s["qty"]),
                "purchaseUnitCostUsd": float(s["cost_price"]),
                "marginPercent": float(s["margin_percent"]),
                "productName": None,
                "category": None,
            }
        )

    stock_result = None
    if args.apply and stock_items:
        stock_result = api.create_stock_entry(
            {
                "date": "2026-02-18T00:00:00Z",
                "supplier": "CARGA INICIAL FEBRERO",
                "documentNumber": "FEBRERO.xlsx",
                "notes": "Importacion inicial desde planilla",
                "logisticsUsd": 0,
                "exchangeRateArs": 1450,
                "items": stock_items,
            }
        )

    expenses_applied = 0
    expenses_to_apply = expenses_seed[: max(args.max_expenses, 0)]
    if args.apply and expenses_to_apply:
        current = api.cash_current()
        if not current:
            api.cash_open(0)
        for e in expenses_to_apply:
            api.cash_movement(2, float(e["amount"]), f"{e['reason']} (importado)")
            expenses_applied += 1

    result = {
        "base": args.base,
        "dry_run": not args.apply,
        "products_price_list_seed": len(products_price_list),
        "products_price_list_new_detected": created_from_price_list,
        "products_total_seed": len(products_seed),
        "products_new_detected": created,
        "stock_items_ready": len(stock_items),
        "stock_items_missing_product": missing,
        "stock_entry_result": stock_result,
        "expenses_selected": len(expenses_to_apply),
        "expenses_applied": expenses_applied,
    }

    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
