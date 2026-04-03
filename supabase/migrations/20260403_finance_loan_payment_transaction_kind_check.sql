alter table public.finance_transactions
    drop constraint if exists finance_transactions_transaction_kind_check;

alter table public.finance_transactions
    add constraint finance_transactions_transaction_kind_check
    check (transaction_kind in ('single', 'split', 'transfer', 'loan_payment'));
