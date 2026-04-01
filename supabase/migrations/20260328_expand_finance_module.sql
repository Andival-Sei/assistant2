create table if not exists public.finance_categories (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references auth.users (id) on delete cascade,
    parent_id uuid null references public.finance_categories (id) on delete cascade,
    direction text not null check (direction in ('income', 'expense')),
    code text not null,
    name text not null,
    icon text null,
    color text null,
    display_order integer not null default 0,
    is_archived boolean not null default false,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

alter table public.finance_user_settings
    add column if not exists overview_cards jsonb not null default
    '["total_balance","card_balance","cash_balance","month_income","month_expense","month_result","recent_transactions"]'::jsonb;

alter table public.finance_accounts
    add column if not exists provider_code text;

update public.finance_accounts
set provider_code = case
    when kind = 'cash' then 'cash'
    when coalesce(bank_name, name) ilike '%т%' then 'tbank'
    when coalesce(bank_name, name) ilike '%sber%' or coalesce(bank_name, name) ilike '%сбер%' then 'sber'
    when coalesce(bank_name, name) ilike '%alfa%' or coalesce(bank_name, name) ilike '%альфа%' then 'alfa'
    when coalesce(bank_name, name) ilike '%втб%' or coalesce(bank_name, name) ilike '%vtb%' then 'vtb'
    when coalesce(bank_name, name) ilike '%yandex%' or coalesce(bank_name, name) ilike '%янд%' then 'yandex'
    when coalesce(bank_name, name) ilike '%ozon%' then 'ozon'
    else 'other_bank'
end
where provider_code is null;

alter table public.finance_accounts
    alter column provider_code set default 'other_bank';

alter table public.finance_accounts
    alter column provider_code set not null;

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'finance_accounts_provider_code_check'
    ) then
        alter table public.finance_accounts
            add constraint finance_accounts_provider_code_check
            check (
                provider_code = any (
                    array[
                        'cash',
                        'tbank',
                        'sber',
                        'alfa',
                        'vtb',
                        'gazprombank',
                        'yandex',
                        'ozon',
                        'raiffeisen',
                        'rosselkhoz',
                        'other_bank'
                    ]
                )
            );
    end if;
end $$;

alter table public.finance_transactions
    add column if not exists destination_account_id uuid null references public.finance_accounts (id) on delete cascade;

alter table public.finance_transactions
    add column if not exists transaction_kind text not null default 'single'
    check (transaction_kind in ('single', 'split', 'transfer'));

alter table public.finance_transactions
    add column if not exists item_count integer not null default 1;

create index if not exists finance_categories_user_idx
    on public.finance_categories (user_id, direction, display_order, created_at desc)
    where is_archived = false;

create unique index if not exists finance_categories_user_code_uidx
    on public.finance_categories (user_id, code)
    where is_archived = false;

create index if not exists finance_transactions_destination_happened_idx
    on public.finance_transactions (destination_account_id, happened_at desc)
    where destination_account_id is not null;

drop trigger if exists finance_categories_set_updated_at on public.finance_categories;
create trigger finance_categories_set_updated_at
before update on public.finance_categories
for each row
execute function public.finance_set_updated_at();

alter table public.finance_categories enable row level security;

drop policy if exists "finance_categories_select_own" on public.finance_categories;
create policy "finance_categories_select_own"
on public.finance_categories
for select
to authenticated
using ((select auth.uid()) = user_id);

drop policy if exists "finance_categories_insert_own" on public.finance_categories;
create policy "finance_categories_insert_own"
on public.finance_categories
for insert
to authenticated
with check ((select auth.uid()) = user_id);

drop policy if exists "finance_categories_update_own" on public.finance_categories;
create policy "finance_categories_update_own"
on public.finance_categories
for update
to authenticated
using ((select auth.uid()) = user_id)
with check ((select auth.uid()) = user_id);

drop policy if exists "finance_categories_delete_own" on public.finance_categories;
create policy "finance_categories_delete_own"
on public.finance_categories
for delete
to authenticated
using ((select auth.uid()) = user_id);

create or replace function public.finance_provider_label(p_provider_code text)
returns text
language sql
immutable
as $$
    select case p_provider_code
        when 'cash' then 'Наличные'
        when 'tbank' then 'Т-Банк'
        when 'sber' then 'Сбер'
        when 'alfa' then 'Альфа'
        when 'vtb' then 'ВТБ'
        when 'gazprombank' then 'Газпромбанк'
        when 'yandex' then 'Яндекс'
        when 'ozon' then 'Ozon'
        when 'raiffeisen' then 'Райффайзен'
        when 'rosselkhoz' then 'Россельхозбанк'
        else 'Другой счёт'
    end;
$$;

create or replace function public.finance_provider_kind(p_provider_code text)
returns text
language sql
immutable
as $$
    select case
        when p_provider_code = 'cash' then 'cash'
        else 'bank_card'
    end;
$$;

create or replace function public.finance_provider_code_from_input(p_value text)
returns text
language plpgsql
immutable
as $$
declare
    v_value text := lower(trim(coalesce(p_value, '')));
begin
    if v_value in ('cash', 'наличные', 'наличные деньги') then
        return 'cash';
    elsif v_value in ('tbank', 't-bank', 'т-банк', 'тинькофф', 'tinkoff') then
        return 'tbank';
    elsif v_value in ('sber', 'сбер', 'сбербанк') then
        return 'sber';
    elsif v_value in ('alfa', 'альфа', 'альфа-банк', 'alfa-bank') then
        return 'alfa';
    elsif v_value in ('vtb', 'втб') then
        return 'vtb';
    elsif v_value in ('gazprombank', 'газпромбанк') then
        return 'gazprombank';
    elsif v_value in ('yandex', 'яндекс', 'яндекс банк') then
        return 'yandex';
    elsif v_value in ('ozon', 'озон', 'озон банк') then
        return 'ozon';
    elsif v_value in ('raiffeisen', 'райффайзен') then
        return 'raiffeisen';
    elsif v_value in ('rosselkhoz', 'россельхоз', 'россельхозбанк') then
        return 'rosselkhoz';
    elsif v_value = '' then
        return null;
    else
        return 'other_bank';
    end if;
end;
$$;

create or replace function public.finance_seed_default_categories()
returns void
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

    if exists (
        select 1
        from public.finance_categories
        where user_id = v_user_id
          and is_archived = false
    ) then
        return;
    end if;

    with seeds as (
        select *
        from (
            values
                ('expense_food', null, 'expense', 'Еда', 'utensils-crossed', '#2AE88B', 10),
                ('expense_food_ready', 'expense_food', 'expense', 'Готовая еда', 'sandwich', '#41D38A', 11),
                ('expense_food_groceries', 'expense_food', 'expense', 'Продукты', 'shopping-basket', '#5BCB82', 12),
                ('expense_food_groceries_meat', 'expense_food_groceries', 'expense', 'Мясо и птица', 'beef', '#7BC96F', 13),
                ('expense_food_groceries_dairy', 'expense_food_groceries', 'expense', 'Молочные продукты', 'milk', '#8FCD68', 14),
                ('expense_food_groceries_snacks', 'expense_food_groceries', 'expense', 'Снеки и сладкое', 'cookie', '#A4D45F', 15),
                ('expense_home', null, 'expense', 'Дом', 'house', '#5DA6FF', 20),
                ('expense_home_utilities', 'expense_home', 'expense', 'Коммунальные услуги', 'lamp-desk', '#6A95FF', 21),
                ('expense_home_supplies', 'expense_home', 'expense', 'Хозтовары', 'package-open', '#7A85FF', 22),
                ('expense_transport', null, 'expense', 'Транспорт', 'car-front', '#F6A52C', 30),
                ('expense_transport_fuel', 'expense_transport', 'expense', 'Топливо', 'fuel', '#F3B44D', 31),
                ('expense_transport_public', 'expense_transport', 'expense', 'Общественный транспорт', 'tram-front', '#F0C36A', 32),
                ('expense_health', null, 'expense', 'Здоровье', 'heart-pulse', '#FF6A6A', 40),
                ('expense_health_pharmacy', 'expense_health', 'expense', 'Аптека', 'pill', '#FF7E7E', 41),
                ('expense_health_doctor', 'expense_health', 'expense', 'Врачи', 'stethoscope', '#FF9494', 42),
                ('expense_leisure', null, 'expense', 'Досуг', 'gamepad-2', '#B57BFF', 50),
                ('expense_leisure_subscription', 'expense_leisure', 'expense', 'Подписки', 'sparkles', '#C28CFF', 51),
                ('expense_other', null, 'expense', 'Прочее', 'folder', '#8D96A8', 60),
                ('income_salary', null, 'income', 'Зарплата', 'badge-russian-ruble', '#2AE88B', 10),
                ('income_gifts', null, 'income', 'Подарки', 'gift', '#5DA6FF', 20),
                ('income_refunds', null, 'income', 'Возвраты', 'rotate-ccw', '#F6A52C', 30),
                ('income_other', null, 'income', 'Прочие поступления', 'plus-circle', '#8D96A8', 40)
        ) as t(code, parent_code, direction, name, icon, color, display_order)
    ),
    inserted_roots as (
        insert into public.finance_categories (
            user_id,
            parent_id,
            direction,
            code,
            name,
            icon,
            color,
            display_order
        )
        select
            v_user_id,
            null,
            s.direction,
            s.code,
            s.name,
            s.icon,
            s.color,
            s.display_order
        from seeds s
        where s.parent_code is null
        on conflict do nothing
        returning id, code
    )
    insert into public.finance_categories (
        user_id,
        parent_id,
        direction,
        code,
        name,
        icon,
        color,
        display_order
    )
    select
        v_user_id,
        parent.id,
        s.direction,
        s.code,
        s.name,
        s.icon,
        s.color,
        s.display_order
    from seeds s
    join public.finance_categories parent
      on parent.user_id = v_user_id
     and parent.code = s.parent_code
    where s.parent_code is not null
    on conflict do nothing;
end;
$$;

create or replace function public.finance_normalize_month_start(p_month text)
returns date
language plpgsql
immutable
as $$
declare
    v_month text := nullif(trim(coalesce(p_month, '')), '');
    v_date date;
begin
    if v_month is null then
        return date_trunc('month', current_date)::date;
    end if;

    begin
        v_date := to_date(v_month || '-01', 'YYYY-MM-DD');
    exception
        when others then
            raise exception 'Invalid month format. Use YYYY-MM';
    end;

    return date_trunc('month', v_date)::date;
end;
$$;

create or replace function public.finance_get_overview()
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user_id uuid := auth.uid();
    v_month_start date := date_trunc('month', current_date)::date;
    v_month_end date := (date_trunc('month', current_date) + interval '1 month')::date;
begin
    if v_user_id is null then
        raise exception 'Not authenticated';
    end if;

    perform public.finance_seed_default_categories();

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
            select coalesce(default_currency, 'RUB')
            from public.finance_user_settings
            where user_id = v_user_id
        ),
        'overviewCards',
        coalesce(
            (
                select overview_cards
                from public.finance_user_settings
                where user_id = v_user_id
            ),
            '["total_balance","card_balance","cash_balance","month_income","month_expense","month_result","recent_transactions"]'::jsonb
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
        'cardBalanceMinor',
        coalesce(
            (
                select sum(balance_minor)
                from public.finance_accounts
                where user_id = v_user_id
                  and kind = 'bank_card'
                  and is_archived = false
            ),
            0
        ),
        'cashBalanceMinor',
        coalesce(
            (
                select sum(balance_minor)
                from public.finance_accounts
                where user_id = v_user_id
                  and kind = 'cash'
                  and is_archived = false
            ),
            0
        ),
        'monthIncomeMinor',
        coalesce(
            (
                select sum(amount_minor)
                from public.finance_transactions
                where user_id = v_user_id
                  and direction = 'income'
                  and happened_at >= v_month_start
                  and happened_at < v_month_end
            ),
            0
        ),
        'monthExpenseMinor',
        coalesce(
            (
                select sum(amount_minor)
                from public.finance_transactions
                where user_id = v_user_id
                  and direction = 'expense'
                  and happened_at >= v_month_start
                  and happened_at < v_month_end
            ),
            0
        ),
        'monthNetMinor',
        coalesce(
            (
                select sum(
                    case
                        when direction = 'income' then amount_minor
                        when direction = 'expense' then -amount_minor
                        else 0
                    end
                )
                from public.finance_transactions
                where user_id = v_user_id
                  and happened_at >= v_month_start
                  and happened_at < v_month_end
            ),
            0
        ),
        'accounts',
        coalesce(
            (
                select jsonb_agg(
                    jsonb_build_object(
                        'id', a.id,
                        'kind', a.kind,
                        'name', a.name,
                        'bankName', a.bank_name,
                        'providerCode', a.provider_code,
                        'currency', a.currency,
                        'balanceMinor', a.balance_minor,
                        'isPrimary', a.is_primary,
                        'transactionCount', coalesce(tx.transaction_count, 0),
                        'balanceEditable', coalesce(tx.transaction_count, 0) = 0
                    )
                    order by a.kind = 'cash', a.display_order, a.created_at desc
                )
                from public.finance_accounts a
                left join (
                    select
                        account_id,
                        count(*)::int as transaction_count
                    from public.finance_transactions
                    where user_id = v_user_id
                    group by account_id
                ) tx on tx.account_id = a.id
                where a.user_id = v_user_id
                  and a.is_archived = false
            ),
            '[]'::jsonb
        ),
        'recentTransactions',
        coalesce(
            (
                select jsonb_agg(
                    jsonb_build_object(
                        'id', t.id,
                        'accountId', t.account_id,
                        'accountName', source_account.name,
                        'direction', t.direction,
                        'transactionKind', t.transaction_kind,
                        'title', t.title,
                        'note', t.note,
                        'amountMinor', t.amount_minor,
                        'currency', t.currency,
                        'happenedAt', t.happened_at,
                        'destinationAccountId', t.destination_account_id,
                        'destinationAccountName', destination_account.name,
                        'itemCount', t.item_count
                    )
                    order by t.happened_at desc
                )
                from (
                    select *
                    from public.finance_transactions
                    where user_id = v_user_id
                    order by happened_at desc
                    limit 8
                ) t
                join public.finance_accounts source_account on source_account.id = t.account_id
                left join public.finance_accounts destination_account on destination_account.id = t.destination_account_id
            ),
            '[]'::jsonb
        ),
        'categoriesCount',
        coalesce(
            (
                select count(*)::int
                from public.finance_categories
                where user_id = v_user_id
                  and is_archived = false
            ),
            0
        )
    );
end;
$$;

create or replace function public.finance_get_accounts()
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

    return coalesce(
        (
            select jsonb_agg(
                jsonb_build_object(
                    'id', a.id,
                    'kind', a.kind,
                    'name', a.name,
                    'bankName', a.bank_name,
                    'providerCode', a.provider_code,
                    'currency', a.currency,
                    'balanceMinor', a.balance_minor,
                    'isPrimary', a.is_primary,
                    'transactionCount', coalesce(tx.transaction_count, 0),
                    'balanceEditable', coalesce(tx.transaction_count, 0) = 0
                )
                order by a.kind = 'cash', a.display_order, a.created_at desc
            )
            from public.finance_accounts a
            left join (
                select
                    account_id,
                    count(*)::int as transaction_count
                from public.finance_transactions
                where user_id = v_user_id
                group by account_id
            ) tx on tx.account_id = a.id
            where a.user_id = v_user_id
              and a.is_archived = false
        ),
        '[]'::jsonb
    );
end;
$$;

create or replace function public.finance_get_transactions(p_month text default null)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user_id uuid := auth.uid();
    v_month_start date := public.finance_normalize_month_start(p_month);
    v_month_end date := (v_month_start + interval '1 month')::date;
begin
    if v_user_id is null then
        raise exception 'Not authenticated';
    end if;

    return jsonb_build_object(
        'month', to_char(v_month_start, 'YYYY-MM'),
        'availableMonths',
        coalesce(
            (
                select jsonb_agg(month_key order by month_key desc)
                from (
                    select distinct to_char(date_trunc('month', happened_at), 'YYYY-MM') as month_key
                    from public.finance_transactions
                    where user_id = v_user_id
                ) months
            ),
            jsonb_build_array(to_char(v_month_start, 'YYYY-MM'))
        ),
        'transactions',
        coalesce(
            (
                select jsonb_agg(
                    jsonb_build_object(
                        'id', t.id,
                        'accountId', t.account_id,
                        'accountName', source_account.name,
                        'direction', t.direction,
                        'transactionKind', t.transaction_kind,
                        'title', t.title,
                        'note', t.note,
                        'amountMinor', t.amount_minor,
                        'currency', t.currency,
                        'happenedAt', t.happened_at,
                        'destinationAccountId', t.destination_account_id,
                        'destinationAccountName', destination_account.name,
                        'itemCount', t.item_count
                    )
                    order by t.happened_at desc
                )
                from public.finance_transactions t
                join public.finance_accounts source_account on source_account.id = t.account_id
                left join public.finance_accounts destination_account on destination_account.id = t.destination_account_id
                where t.user_id = v_user_id
                  and t.happened_at >= v_month_start
                  and t.happened_at < v_month_end
            ),
            '[]'::jsonb
        )
    );
end;
$$;

create or replace function public.finance_get_categories()
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

    perform public.finance_seed_default_categories();

    return coalesce(
        (
            select jsonb_agg(
                jsonb_build_object(
                    'id', c.id,
                    'parentId', c.parent_id,
                    'direction', c.direction,
                    'code', c.code,
                    'name', c.name,
                    'icon', c.icon,
                    'color', c.color,
                    'displayOrder', c.display_order
                )
                order by c.direction, c.display_order, c.created_at
            )
            from public.finance_categories c
            where c.user_id = v_user_id
              and c.is_archived = false
        ),
        '[]'::jsonb
    );
end;
$$;

create or replace function public.finance_update_overview_cards(p_cards jsonb)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user_id uuid := auth.uid();
    v_invalid_count integer;
begin
    if v_user_id is null then
        raise exception 'Not authenticated';
    end if;

    if jsonb_typeof(p_cards) <> 'array' then
        raise exception 'Overview cards must be an array';
    end if;

    select count(*)
    into v_invalid_count
    from jsonb_array_elements_text(p_cards) as item(card_id)
    where item.card_id <> all (
        array[
            'total_balance',
            'card_balance',
            'cash_balance',
            'month_income',
            'month_expense',
            'month_result',
            'recent_transactions'
        ]
    );

    if v_invalid_count > 0 then
        raise exception 'Overview cards list contains unsupported items';
    end if;

    insert into public.finance_user_settings (user_id, overview_cards)
    values (v_user_id, p_cards)
    on conflict (user_id) do update
    set overview_cards = excluded.overview_cards,
        updated_at = now();

    return public.finance_get_overview();
end;
$$;

create or replace function public.finance_upsert_account(
    p_id uuid default null,
    p_provider_code text default null,
    p_balance_minor bigint default 0,
    p_currency text default null,
    p_name text default null,
    p_make_primary boolean default false
)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user_id uuid := auth.uid();
    v_provider_code text := public.finance_provider_code_from_input(p_provider_code);
    v_kind text;
    v_currency text;
    v_name text;
    v_existing public.finance_accounts%rowtype;
    v_transaction_count integer := 0;
    v_next_order integer;
begin
    if v_user_id is null then
        raise exception 'Not authenticated';
    end if;

    if v_provider_code is null then
        raise exception 'Provider is required';
    end if;

    if p_balance_minor < 0 then
        raise exception 'Balance must be zero or positive';
    end if;

    v_kind := public.finance_provider_kind(v_provider_code);

    select coalesce(
        p_currency,
        (
            select default_currency
            from public.finance_user_settings
            where user_id = v_user_id
        ),
        'RUB'
    )
    into v_currency;

    if v_currency not in ('RUB', 'USD', 'EUR') then
        raise exception 'Unsupported currency';
    end if;

    v_name := case
        when v_kind = 'cash' then 'Наличные'
        when nullif(trim(coalesce(p_name, '')), '') is not null then trim(p_name)
        else public.finance_provider_label(v_provider_code)
    end;

    if p_id is null then
        if v_kind = 'cash' and exists (
            select 1
            from public.finance_accounts
            where user_id = v_user_id
              and kind = 'cash'
              and is_archived = false
        ) then
            raise exception 'Cash account already exists';
        end if;

        if p_make_primary and v_kind = 'bank_card' then
            update public.finance_accounts
            set is_primary = false
            where user_id = v_user_id
              and kind = 'bank_card'
              and is_archived = false;
        end if;

        select coalesce(max(display_order), -1) + 1
        into v_next_order
        from public.finance_accounts
        where user_id = v_user_id;

        insert into public.finance_accounts (
            user_id,
            kind,
            name,
            bank_name,
            provider_code,
            currency,
            balance_minor,
            is_primary,
            display_order
        )
        values (
            v_user_id,
            v_kind,
            v_name,
            case when v_kind = 'cash' then null else public.finance_provider_label(v_provider_code) end,
            v_provider_code,
            v_currency,
            p_balance_minor,
            case when v_kind = 'bank_card' then p_make_primary else false end,
            v_next_order
        );
    else
        select *
        into v_existing
        from public.finance_accounts
        where id = p_id
          and user_id = v_user_id
        for update;

        if v_existing.id is null then
            raise exception 'Account not found';
        end if;

        select count(*)
        into v_transaction_count
        from public.finance_transactions
        where user_id = v_user_id
          and account_id = p_id;

        if v_transaction_count > 0 and v_existing.balance_minor <> p_balance_minor then
            raise exception 'Balance cannot be changed after first transaction';
        end if;

        if v_transaction_count > 0 and (
            v_existing.provider_code <> v_provider_code
            or v_existing.currency <> v_currency
            or v_existing.kind <> v_kind
        ) then
            raise exception 'Account type and currency cannot be changed after first transaction';
        end if;

        if p_make_primary and v_kind = 'bank_card' then
            update public.finance_accounts
            set is_primary = false
            where user_id = v_user_id
              and kind = 'bank_card'
              and is_archived = false
              and id <> p_id;
        end if;

        update public.finance_accounts
        set name = v_name,
            bank_name = case when v_kind = 'cash' then null else public.finance_provider_label(v_provider_code) end,
            provider_code = v_provider_code,
            currency = v_currency,
            balance_minor = case
                when v_transaction_count > 0 then balance_minor
                else p_balance_minor
            end,
            is_primary = case when v_kind = 'bank_card' then p_make_primary else false end,
            updated_at = now()
        where id = p_id
          and user_id = v_user_id;
    end if;

    perform public.finance_seed_default_categories();

    return public.finance_get_overview();
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
    v_provider_code text;
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
        (
            select default_currency
            from public.finance_user_settings
            where user_id = v_user_id
        ),
        'RUB'
    )
    into v_currency;

    v_provider_code := public.finance_provider_code_from_input(p_bank);

    if v_provider_code is not null and v_provider_code <> 'cash' and not exists (
        select 1
        from public.finance_accounts
        where user_id = v_user_id
          and kind = 'bank_card'
          and is_archived = false
    ) then
        perform public.finance_upsert_account(
            null,
            v_provider_code,
            coalesce(p_primary_account_balance_minor, 0),
            v_currency,
            null,
            true
        );
    end if;

    if p_cash_minor is not null and not exists (
        select 1
        from public.finance_accounts
        where user_id = v_user_id
          and kind = 'cash'
          and is_archived = false
    ) then
        perform public.finance_upsert_account(
            null,
            'cash',
            p_cash_minor,
            v_currency,
            null,
            false
        );
    end if;

    perform public.finance_seed_default_categories();

    return public.finance_get_overview();
end;
$$;
