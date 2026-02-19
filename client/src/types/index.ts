export type Role = 'Admin' | 'Cashier';

export type AuthUser = {
  id: number;
  username: string;
  role: Role;
  forcePasswordChange: boolean;
};

export type Product = {
  id: number;
  internalCode: string;
  barcode?: string | null;
  name: string;
  category?: string | null;
  brand?: string | null;
  model?: string | null;
  imeiOrSerial?: string | null;
  costPrice?: number;
  marginPercent?: number;
  salePrice: number;
  stockQuantity: number;
  stockMinimum: number;
  active: boolean;
};
