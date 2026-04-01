alter table public.finance_transactions
    add column if not exists receipt_storage_path text null;
