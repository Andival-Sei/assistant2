create extension if not exists pgcrypto;

create table if not exists public.finance_user_settings (
    user_id uuid primary key references auth.users (id) on delete cascade,
    default_currency text null check (default_currency in ('RUB', 'USD', 'EUR')),
    onboarding_completed_at timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists public.finance_accounts (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references auth.users (id) on delete cascade,
    kind text not null check (kind in ('bank_card', 'cash')),
    name text not null,
    bank_name text null,
    currency text not null check (currency in ('RUB', 'USD', 'EUR')),
    balance_minor bigint not null default 0,
    is_primary boolean not null default false,
    is_archived boolean not null default false,
    display_order integer not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists public.finance_transactions (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references auth.users (id) on delete cascade,
    account_id uuid not null references public.finance_accounts (id) on delete cascade,
    direction text not null check (direction in ('income', 'expense', 'transfer')),
    title text not null,
    note text null,
    amount_minor bigint not null check (amount_minor > 0),
    currency text not null check (currency in ('RUB', 'USD', 'EUR')),
    happened_at timestamptz not null default now(),
    created_at timestamptz not null default now()
);

create index if not exists finance_accounts_user_idx
    on public.finance_accounts (user_id, display_order, created_at desc);

create index if not exists finance_accounts_user_kind_idx
    on public.finance_accounts (user_id, kind)
    where is_archived = false;

create unique index if not exists finance_accounts_one_cash_idx
    on public.finance_accounts (user_id)
    where kind = 'cash' and is_archived = false;

create unique index if not exists finance_accounts_one_primary_idx
    on public.finance_accounts (user_id)
    where kind = 'bank_card' and is_primary = true and is_archived = false;

create index if not exists finance_transactions_user_happened_idx
    on public.finance_transactions (user_id, happened_at desc);

create index if not exists finance_transactions_account_happened_idx
    on public.finance_transactions (account_id, happened_at desc);

create or replace function public.finance_set_updated_at()
returns trigger
language plpgsql
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

drop trigger if exists finance_user_settings_set_updated_at on public.finance_user_settings;
create trigger finance_user_settings_set_updated_at
before update on public.finance_user_settings
for each row
execute function public.finance_set_updated_at();

drop trigger if exists finance_accounts_set_updated_at on public.finance_accounts;
create trigger finance_accounts_set_updated_at
before update on public.finance_accounts
for each row
execute function public.finance_set_updated_at();

alter table public.finance_user_settings enable row level security;
alter table public.finance_accounts enable row level security;
alter table public.finance_transactions enable row level security;

drop policy if exists "finance_user_settings_select_own" on public.finance_user_settings;
create policy "finance_user_settings_select_own"
on public.finance_user_settings
for select
to authenticated
using ((select auth.uid()) = user_id);

drop policy if exists "finance_user_settings_insert_own" on public.finance_user_settings;
create policy "finance_user_settings_insert_own"
on public.finance_user_settings
for insert
to authenticated
with check ((select auth.uid()) = user_id);

drop policy if exists "finance_user_settings_update_own" on public.finance_user_settings;
create policy "finance_user_settings_update_own"
on public.finance_user_settings
for update
to authenticated
using ((select auth.uid()) = user_id)
with check ((select auth.uid()) = user_id);

drop policy if exists "finance_accounts_select_own" on public.finance_accounts;
create policy "finance_accounts_select_own"
on public.finance_accounts
for select
to authenticated
using ((select auth.uid()) = user_id);

drop policy if exists "finance_accounts_insert_own" on public.finance_accounts;
create policy "finance_accounts_insert_own"
on public.finance_accounts
for insert
to authenticated
with check ((select auth.uid()) = user_id);

drop policy if exists "finance_accounts_update_own" on public.finance_accounts;
create policy "finance_accounts_update_own"
on public.finance_accounts
for update
to authenticated
using ((select auth.uid()) = user_id)
with check ((select auth.uid()) = user_id);

drop policy if exists "finance_transactions_select_own" on public.finance_transactions;
create policy "finance_transactions_select_own"
on public.finance_transactions
for select
to authenticated
using ((select auth.uid()) = user_id);

drop policy if exists "finance_transactions_insert_own" on public.finance_transactions;
create policy "finance_transactions_insert_own"
on public.finance_transactions
for insert
to authenticated
with check ((select auth.uid()) = user_id);

create or replace function public.finance_get_overview()
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user_id uuid := auth.uid();
begin
    if v_user_id is null then
        raise exception 'Not authenticated';
    end if;

    return jsonb_build_object(
        'onboardingCompleted',
        exists(
            select 1
            from public.finance_user_settings
            where user_id = v_user_id
              and onboarding_completed_at is not null
        ),
        'defaultCurrency',
        (
            select default_currency
            from public.finance_user_settings
            where user_id = v_user_id
        ),
        'totalBalanceMinor',
        coalesce(
            (
                select sum(balance_minor)
                from public.finance_accounts
                where user_id = v_user_id
                  and is_archived = false
            ),
            0
        ),
        'accounts',
        coalesce(
            (
                select jsonb_agg(
                    jsonb_build_object(
                        'id', id,
                        'kind', kind,
                        'name', name,
                        'bankName', bank_name,
                        'currency', currency,
                        'balanceMinor', balance_minor,
                        'isPrimary', is_primary
                    )
                    order by display_order, created_at desc
                )
                from public.finance_accounts
                where user_id = v_user_id
                  and is_archived = false
            ),
            '[]'::jsonb
        ),
        'recentTransactions',
        coalesce(
            (
                select jsonb_agg(
                    jsonb_build_object(
                        'id', id,
                        'accountId', account_id,
                        'direction', direction,
                        'title', title,
                        'note', note,
                        'amountMinor', amount_minor,
                        'currency', currency,
                        'happenedAt', happened_at
                    )
                    order by happened_at desc
                )
                from (
                    select *
                    from public.finance_transactions
                    where user_id = v_user_id
                    order by happened_at desc
                    limit 12
                ) recent_transactions
            ),
            '[]'::jsonb
        )
    );
end;
$$;

create or replace function public.finance_complete_onboarding(
    p_currency text default null,
    p_bank text default null,
    p_cash_minor bigint default null,
    p_primary_account_balance_minor bigint default null
)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user_id uuid := auth.uid();
    v_currency text;
    v_primary_account_id uuid;
    v_cash_account_id uuid;
begin
    if v_user_id is null then
        raise exception 'Not authenticated';
    end if;

    if p_currency is not null and p_currency not in ('RUB', 'USD', 'EUR') then
        raise exception 'Unsupported currency';
    end if;

    if p_cash_minor is not null and p_cash_minor < 0 then
        raise exception 'Cash balance must be positive';
    end if;

    if p_primary_account_balance_minor is not null and p_primary_account_balance_minor < 0 then
        raise exception 'Primary account balance must be positive';
    end if;

    insert into public.finance_user_settings (
        user_id,
        default_currency,
        onboarding_completed_at
    )
    values (
        v_user_id,
        p_currency,
        now()
    )
    on conflict (user_id) do update
    set default_currency = coalesce(excluded.default_currency, public.finance_user_settings.default_currency),
        onboarding_completed_at = now(),
        updated_at = now();

    select coalesce(
        p_currency,
        (select default_currency from public.finance_user_settings where user_id = v_user_id),
        'RUB'
    )
    into v_currency;

    if p_bank is not null and btrim(p_bank) <> '' then
        select id
        into v_primary_account_id
        from public.finance_accounts
        where user_id = v_user_id
          and kind = 'bank_card'
          and is_primary = true
          and is_archived = false
        limit 1;

        if v_primary_account_id is null then
            insert into public.finance_accounts (
                user_id,
                kind,
                name,
                bank_name,
                currency,
                balance_minor,
                is_primary,
                display_order
            )
            values (
                v_user_id,
                'bank_card',
                btrim(p_bank),
                btrim(p_bank),
                v_currency,
                coalesce(p_primary_account_balance_minor, 0),
                true,
                0
            );
        else
            update public.finance_accounts
            set name = btrim(p_bank),
                bank_name = btrim(p_bank),
                currency = v_currency,
                balance_minor = coalesce(p_primary_account_balance_minor, balance_minor),
                updated_at = now()
            where id = v_primary_account_id;
        end if;
    end if;

    if p_cash_minor is not null then
        select id
        into v_cash_account_id
        from public.finance_accounts
        where user_id = v_user_id
          and kind = 'cash'
          and is_archived = false
        limit 1;

        if v_cash_account_id is null then
            insert into public.finance_accounts (
                user_id,
                kind,
                name,
                currency,
                balance_minor,
                display_order
            )
            values (
                v_user_id,
                'cash',
                'Cash',
                v_currency,
                p_cash_minor,
                1
            );
        else
            update public.finance_accounts
            set currency = v_currency,
                balance_minor = p_cash_minor,
                updated_at = now()
            where id = v_cash_account_id;
        end if;
    end if;

    return public.finance_get_overview();
end;
$$;

grant execute on function public.finance_get_overview() to authenticated;
grant execute on function public.finance_complete_onboarding(text, text, bigint, bigint) to authenticated;
