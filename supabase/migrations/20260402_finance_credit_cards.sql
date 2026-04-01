alter table public.finance_accounts
    add column if not exists credit_limit_minor bigint null,
    add column if not exists credit_debt_minor bigint null,
    add column if not exists credit_required_payment_minor bigint null,
    add column if not exists credit_payment_due_date date null,
    add column if not exists credit_grace_period_end_date date null;

update public.finance_accounts
set credit_limit_minor = coalesce(credit_limit_minor, greatest(balance_minor, 0)),
    credit_debt_minor = coalesce(credit_debt_minor, 0),
    credit_required_payment_minor = coalesce(credit_required_payment_minor, 0),
    balance_minor = greatest(coalesce(credit_limit_minor, greatest(balance_minor, 0)) - coalesce(credit_debt_minor, 0), 0)
where kind = 'bank_card'
  and card_type = 'credit';

update public.finance_accounts
set credit_limit_minor = null,
    credit_debt_minor = null,
    credit_required_payment_minor = null,
    credit_payment_due_date = null,
    credit_grace_period_end_date = null
where kind = 'cash'
   or coalesce(card_type, 'debit') <> 'credit';

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'finance_accounts_credit_limit_minor_check'
    ) then
        alter table public.finance_accounts
            add constraint finance_accounts_credit_limit_minor_check
            check (credit_limit_minor is null or credit_limit_minor >= 0);
    end if;

    if not exists (
        select 1
        from pg_constraint
        where conname = 'finance_accounts_credit_debt_minor_check'
    ) then
        alter table public.finance_accounts
            add constraint finance_accounts_credit_debt_minor_check
            check (credit_debt_minor is null or credit_debt_minor >= 0);
    end if;

    if not exists (
        select 1
        from pg_constraint
        where conname = 'finance_accounts_credit_required_payment_minor_check'
    ) then
        alter table public.finance_accounts
            add constraint finance_accounts_credit_required_payment_minor_check
            check (credit_required_payment_minor is null or credit_required_payment_minor >= 0);
    end if;

    if not exists (
        select 1
        from pg_constraint
        where conname = 'finance_accounts_credit_fields_consistency_check'
    ) then
        alter table public.finance_accounts
            add constraint finance_accounts_credit_fields_consistency_check
            check (
                (
                    kind = 'bank_card'
                    and card_type = 'credit'
                )
                or (
                    credit_limit_minor is null
                    and credit_debt_minor is null
                    and credit_required_payment_minor is null
                    and credit_payment_due_date is null
                    and credit_grace_period_end_date is null
                )
            );
    end if;
end $$;

update public.finance_user_settings
set overview_cards = overview_cards || '["credit_debt"]'::jsonb
where not coalesce(overview_cards, '[]'::jsonb) @> '["credit_debt"]'::jsonb;

alter table public.finance_user_settings
    alter column overview_cards set default
    '["total_balance","card_balance","cash_balance","credit_debt","month_income","month_expense","month_result","recent_transactions"]'::jsonb;

create or replace function public.finance_credit_available(
    p_credit_limit_minor bigint,
    p_credit_debt_minor bigint
)
returns bigint
language sql
immutable
as $$
    select greatest(coalesce(p_credit_limit_minor, 0) - coalesce(p_credit_debt_minor, 0), 0);
$$;

create or replace function public.finance_apply_account_activity(
    p_account_id uuid,
    p_direction text,
    p_amount_minor bigint
)
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
    v_account public.finance_accounts%rowtype;
    v_next_debt bigint;
begin
    if p_direction not in ('income', 'expense') then
        raise exception 'Unsupported account activity direction';
    end if;

    if coalesce(p_amount_minor, 0) <= 0 then
        raise exception 'Account activity amount must be positive';
    end if;

    select *
    into v_account
    from public.finance_accounts
    where id = p_account_id
    for update;

    if v_account.id is null then
        raise exception 'Account not found';
    end if;

    if v_account.kind = 'bank_card' and v_account.card_type = 'credit' then
        v_next_debt := case
            when p_direction = 'income' then greatest(coalesce(v_account.credit_debt_minor, 0) - p_amount_minor, 0)
            else coalesce(v_account.credit_debt_minor, 0) + p_amount_minor
        end;

        update public.finance_accounts
        set credit_debt_minor = v_next_debt,
            balance_minor = public.finance_credit_available(credit_limit_minor, v_next_debt),
            updated_at = now()
        where id = p_account_id;
    else
        update public.finance_accounts
        set balance_minor = case
                when p_direction = 'income' then balance_minor + p_amount_minor
                else balance_minor - p_amount_minor
            end,
            updated_at = now()
        where id = p_account_id;
    end if;
end;
$$;

drop function if exists public.finance_upsert_account(uuid, text, bigint, text, text, boolean);
drop function if exists public.finance_upsert_account(uuid, text, text, text, bigint, text, boolean);

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
            '["total_balance","card_balance","cash_balance","credit_debt","month_income","month_expense","month_result","recent_transactions"]'::jsonb
        ),
        'totalBalanceMinor',
        coalesce(
            (
                select sum(balance_minor)
                from public.finance_accounts
                where user_id = v_user_id
                  and is_archived = false
                  and (
                      kind = 'cash'
                      or (kind = 'bank_card' and coalesce(card_type, 'debit') <> 'credit')
                  )
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
                  and coalesce(card_type, 'debit') <> 'credit'
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
        'creditDebtMinor',
        coalesce(
            (
                select sum(coalesce(credit_debt_minor, 0))
                from public.finance_accounts
                where user_id = v_user_id
                  and kind = 'bank_card'
                  and card_type = 'credit'
                  and is_archived = false
            ),
            0
        ),
        'creditAvailableMinor',
        coalesce(
            (
                select sum(public.finance_credit_available(credit_limit_minor, credit_debt_minor))
                from public.finance_accounts
                where user_id = v_user_id
                  and kind = 'bank_card'
                  and card_type = 'credit'
                  and is_archived = false
            ),
            0
        ),
        'creditLimitMinor',
        coalesce(
            (
                select sum(coalesce(credit_limit_minor, 0))
                from public.finance_accounts
                where user_id = v_user_id
                  and kind = 'bank_card'
                  and card_type = 'credit'
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
                        'cardType', a.card_type,
                        'lastFourDigits', a.last_four_digits,
                        'currency', a.currency,
                        'balanceMinor', a.balance_minor,
                        'creditLimitMinor', a.credit_limit_minor,
                        'creditDebtMinor', a.credit_debt_minor,
                        'creditAvailableMinor', case
                            when a.kind = 'bank_card' and a.card_type = 'credit'
                                then public.finance_credit_available(a.credit_limit_minor, a.credit_debt_minor)
                            else null
                        end,
                        'creditRequiredPaymentMinor', a.credit_required_payment_minor,
                        'creditPaymentDueDate', a.credit_payment_due_date,
                        'creditGracePeriodEndDate', a.credit_grace_period_end_date,
                        'isPrimary', a.is_primary,
                        'transactionCount', coalesce(tx.transaction_count, 0),
                        'balanceEditable', case
                            when a.kind = 'bank_card' and a.card_type = 'credit' then true
                            else coalesce(tx.transaction_count, 0) = 0
                        end
                    )
                    order by a.kind = 'cash', a.display_order, a.created_at desc
                )
                from public.finance_accounts a
                left join (
                    select
                        account_ref.account_id,
                        count(*)::int as transaction_count
                    from (
                        select account_id
                        from public.finance_transactions
                        where user_id = v_user_id

                        union all

                        select destination_account_id as account_id
                        from public.finance_transactions
                        where user_id = v_user_id
                          and destination_account_id is not null
                    ) account_ref
                    group by account_ref.account_id
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
                        'itemCount', t.item_count,
                        'categoryId', t.category_id,
                        'categoryName', category.name,
                        'merchantName', t.merchant_name,
                        'sourceType', t.source_type,
                        'receiptStoragePath', t.receipt_storage_path,
                        'items', coalesce(item_rows.items, '[]'::jsonb)
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
                left join public.finance_categories category on category.id = t.category_id
                left join lateral (
                    select jsonb_agg(
                        jsonb_build_object(
                            'id', item.id,
                            'categoryId', item.category_id,
                            'categoryName', item_category.name,
                            'title', item.title,
                            'amountMinor', item.amount_minor,
                            'displayOrder', item.display_order
                        )
                        order by item.display_order, item.created_at
                    ) as items
                    from public.finance_transaction_items item
                    left join public.finance_categories item_category on item_category.id = item.category_id
                    where item.transaction_id = t.id
                ) item_rows on true
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

create or replace function public.finance_upsert_account(
    p_id uuid default null,
    p_provider_code text default null,
    p_card_type text default null,
    p_last_four_digits text default null,
    p_balance_minor bigint default 0,
    p_currency text default null,
    p_make_primary boolean default false,
    p_credit_limit_minor bigint default null,
    p_credit_debt_minor bigint default null,
    p_credit_required_payment_minor bigint default null,
    p_credit_payment_due_date date default null,
    p_credit_grace_period_end_date date default null
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
    v_card_type text;
    v_last_four_digits text;
    v_name text;
    v_existing public.finance_accounts%rowtype;
    v_transaction_count integer := 0;
    v_next_order integer;
    v_credit_limit_minor bigint;
    v_credit_debt_minor bigint;
    v_credit_required_payment_minor bigint;
    v_effective_balance_minor bigint;
begin
    if v_user_id is null then
        raise exception 'Not authenticated';
    end if;

    if v_provider_code is null then
        raise exception 'Provider is required';
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

    v_card_type := case
        when v_kind = 'cash' then null
        else lower(nullif(trim(coalesce(p_card_type, '')), ''))
    end;

    if v_kind = 'bank_card' and v_card_type not in ('debit', 'credit') then
        raise exception 'Card type must be debit or credit';
    end if;

    v_last_four_digits := case
        when v_kind = 'cash' then null
        else nullif(regexp_replace(coalesce(p_last_four_digits, ''), '\D', '', 'g'), '')
    end;

    if v_kind = 'bank_card' and (v_last_four_digits is null or length(v_last_four_digits) <> 4) then
        raise exception 'Last four digits are required';
    end if;

    if v_kind = 'bank_card' and v_card_type = 'credit' then
        v_credit_limit_minor := coalesce(p_credit_limit_minor, nullif(p_balance_minor, 0), 0);
        v_credit_debt_minor := coalesce(p_credit_debt_minor, 0);
        v_credit_required_payment_minor := nullif(coalesce(p_credit_required_payment_minor, 0), 0);

        if v_credit_limit_minor <= 0 then
            raise exception 'Credit limit must be positive';
        end if;

        if v_credit_debt_minor < 0 then
            raise exception 'Credit debt must be zero or positive';
        end if;

        v_effective_balance_minor := public.finance_credit_available(v_credit_limit_minor, v_credit_debt_minor);
    else
        if p_balance_minor < 0 then
            raise exception 'Balance must be zero or positive';
        end if;

        v_credit_limit_minor := null;
        v_credit_debt_minor := null;
        v_credit_required_payment_minor := null;
        v_effective_balance_minor := p_balance_minor;
    end if;

    v_name := case
        when v_kind = 'cash' then 'Наличные'
        else trim(public.finance_provider_label(v_provider_code) || ' •••• ' || v_last_four_digits)
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
            card_type,
            last_four_digits,
            currency,
            balance_minor,
            credit_limit_minor,
            credit_debt_minor,
            credit_required_payment_minor,
            credit_payment_due_date,
            credit_grace_period_end_date,
            is_primary,
            display_order
        )
        values (
            v_user_id,
            v_kind,
            v_name,
            case when v_kind = 'cash' then null else public.finance_provider_label(v_provider_code) end,
            v_provider_code,
            v_card_type,
            v_last_four_digits,
            v_currency,
            v_effective_balance_minor,
            v_credit_limit_minor,
            v_credit_debt_minor,
            v_credit_required_payment_minor,
            case when v_card_type = 'credit' then p_credit_payment_due_date else null end,
            case when v_card_type = 'credit' then p_credit_grace_period_end_date else null end,
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
          and (account_id = p_id or destination_account_id = p_id);

        if v_transaction_count > 0
           and v_existing.kind <> 'bank_card'
           and v_existing.balance_minor <> v_effective_balance_minor then
            raise exception 'Balance cannot be changed after first transaction';
        end if;

        if v_transaction_count > 0 and (
            v_existing.provider_code <> v_provider_code
            or v_existing.currency <> v_currency
            or v_existing.kind <> v_kind
            or coalesce(v_existing.card_type, '') <> coalesce(v_card_type, '')
            or coalesce(v_existing.last_four_digits, '') <> coalesce(v_last_four_digits, '')
        ) then
            raise exception 'Account type and card identity cannot be changed after first transaction';
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
            card_type = v_card_type,
            last_four_digits = v_last_four_digits,
            currency = v_currency,
            balance_minor = case
                when v_transaction_count > 0 and v_existing.kind <> 'bank_card' then balance_minor
                else v_effective_balance_minor
            end,
            credit_limit_minor = v_credit_limit_minor,
            credit_debt_minor = v_credit_debt_minor,
            credit_required_payment_minor = v_credit_required_payment_minor,
            credit_payment_due_date = case when v_card_type = 'credit' then p_credit_payment_due_date else null end,
            credit_grace_period_end_date = case when v_card_type = 'credit' then p_credit_grace_period_end_date else null end,
            is_primary = case when v_kind = 'bank_card' then p_make_primary else false end,
            updated_at = now()
        where id = p_id
          and user_id = v_user_id;
    end if;

    perform public.finance_seed_default_categories();

    return public.finance_get_overview();
end;
$$;

create or replace function public.finance_create_transactions(p_transactions jsonb)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user_id uuid := auth.uid();
    v_transaction jsonb;
    v_item jsonb;
    v_account_id uuid;
    v_destination_account_id uuid;
    v_category_id uuid;
    v_item_category_id uuid;
    v_transaction_id uuid;
    v_direction text;
    v_transaction_kind text;
    v_currency text;
    v_source_type text;
    v_title text;
    v_merchant_name text;
    v_note text;
    v_happened_at timestamptz;
    v_amount_minor bigint;
    v_total_minor bigint;
    v_item_title text;
    v_item_count integer;
    v_index integer;
    v_created_count integer := 0;
    v_uncategorized_expense_id uuid;
    v_uncategorized_income_id uuid;
    v_source_account public.finance_accounts%rowtype;
    v_destination_account public.finance_accounts%rowtype;
begin
    if v_user_id is null then
        raise exception 'Not authenticated';
    end if;

    if jsonb_typeof(p_transactions) <> 'array' or jsonb_array_length(p_transactions) = 0 then
        raise exception 'Transactions payload must be a non-empty array';
    end if;

    for v_transaction in
        select value
        from jsonb_array_elements(p_transactions)
    loop
        v_direction := lower(trim(coalesce(v_transaction->>'direction', '')));
        v_source_type := lower(trim(coalesce(v_transaction->>'sourceType', 'manual')));
        v_account_id := nullif(v_transaction->>'accountId', '')::uuid;
        v_destination_account_id := nullif(v_transaction->>'destinationAccountId', '')::uuid;
        v_currency := upper(trim(coalesce(v_transaction->>'currency', '')));
        v_note := nullif(trim(coalesce(v_transaction->>'note', '')), '');
        v_merchant_name := nullif(trim(coalesce(v_transaction->>'merchantName', '')), '');
        v_title := nullif(trim(coalesce(v_transaction->>'title', '')), '');
        v_happened_at := coalesce(nullif(v_transaction->>'happenedAt', '')::timestamptz, now());
        v_total_minor := 0;
        v_item_count := 0;
        v_category_id := nullif(v_transaction->>'categoryId', '')::uuid;

        if v_direction not in ('income', 'expense', 'transfer') then
            raise exception 'Unsupported direction';
        end if;

        if v_source_type not in ('manual', 'photo', 'file') then
            raise exception 'Unsupported source type';
        end if;

        if v_account_id is null then
            raise exception 'Account is required';
        end if;

        select *
        into v_source_account
        from public.finance_accounts
        where id = v_account_id
          and user_id = v_user_id
          and is_archived = false
        for update;

        if v_source_account.id is null then
            raise exception 'Source account not found';
        end if;

        if v_currency = '' then
            v_currency := v_source_account.currency;
        end if;

        if v_currency not in ('RUB', 'USD', 'EUR') then
            raise exception 'Unsupported currency';
        end if;

        if v_category_id is not null and not exists (
            select 1
            from public.finance_categories
            where id = v_category_id
              and user_id = v_user_id
              and is_archived = false
        ) then
            raise exception 'Category not found';
        end if;

        if v_direction = 'transfer' then
            v_amount_minor := coalesce((v_transaction->>'amountMinor')::bigint, 0);

            if v_amount_minor <= 0 then
                raise exception 'Transfer amount must be positive';
            end if;

            if v_destination_account_id is null then
                raise exception 'Destination account is required for transfer';
            end if;

            if v_destination_account_id = v_account_id then
                raise exception 'Destination account must be different';
            end if;

            select *
            into v_destination_account
            from public.finance_accounts
            where id = v_destination_account_id
              and user_id = v_user_id
              and is_archived = false
            for update;

            if v_destination_account.id is null then
                raise exception 'Destination account not found';
            end if;

            v_transaction_kind := 'transfer';
            v_title := coalesce(
                v_title,
                case
                    when v_source_account.card_type = 'credit'
                         and coalesce(v_destination_account.card_type, 'debit') <> 'credit'
                        then 'Перевод с кредитки'
                    when coalesce(v_source_account.card_type, 'debit') <> 'credit'
                         and v_destination_account.card_type = 'credit'
                        then 'Погашение кредитки'
                    else 'Перевод'
                end
            );

            insert into public.finance_transactions (
                user_id,
                account_id,
                destination_account_id,
                direction,
                transaction_kind,
                title,
                merchant_name,
                note,
                amount_minor,
                currency,
                happened_at,
                item_count,
                source_type
            )
            values (
                v_user_id,
                v_account_id,
                v_destination_account_id,
                v_direction,
                v_transaction_kind,
                v_title,
                v_merchant_name,
                v_note,
                v_amount_minor,
                v_currency,
                v_happened_at,
                0,
                v_source_type
            );

            perform public.finance_apply_account_activity(v_account_id, 'expense', v_amount_minor);
            perform public.finance_apply_account_activity(v_destination_account_id, 'income', v_amount_minor);
        else
            select id
            into v_uncategorized_expense_id
            from public.finance_categories
            where user_id = v_user_id
              and code = 'expense_uncategorized'
              and is_archived = false
            limit 1;

            select id
            into v_uncategorized_income_id
            from public.finance_categories
            where user_id = v_user_id
              and code = 'income_uncategorized'
              and is_archived = false
            limit 1;

            if coalesce(jsonb_typeof(v_transaction->'items'), 'null') <> 'array'
                or jsonb_array_length(coalesce(v_transaction->'items', '[]'::jsonb)) = 0 then
                v_transaction := jsonb_set(
                    v_transaction,
                    '{items}',
                    jsonb_build_array(
                        jsonb_build_object(
                            'title', coalesce(v_title, v_merchant_name, case when v_direction = 'income' then 'Поступление' else 'Трата' end),
                            'amountMinor', coalesce((v_transaction->>'amountMinor')::bigint, 0),
                            'categoryId', v_transaction->>'categoryId'
                        )
                    )
                );
            end if;

            for v_item in
                select value
                from jsonb_array_elements(v_transaction->'items')
            loop
                v_amount_minor := coalesce((v_item->>'amountMinor')::bigint, 0);
                v_item_category_id := coalesce(
                    nullif(v_item->>'categoryId', '')::uuid,
                    v_category_id,
                    case
                        when v_direction = 'income' then v_uncategorized_income_id
                        else v_uncategorized_expense_id
                    end
                );

                if v_amount_minor <= 0 then
                    raise exception 'Transaction item amount must be positive';
                end if;

                if v_item_category_id is not null and not exists (
                    select 1
                    from public.finance_categories
                    where id = v_item_category_id
                      and user_id = v_user_id
                      and is_archived = false
                ) then
                    raise exception 'Transaction item category not found';
                end if;

                v_total_minor := v_total_minor + v_amount_minor;
                v_item_count := v_item_count + 1;
            end loop;

            if v_total_minor <= 0 then
                raise exception 'Transaction amount must be positive';
            end if;

            v_transaction_kind := case
                when v_item_count > 1 then 'split'
                else 'single'
            end;

            if v_item_count = 1 then
                v_category_id := coalesce(
                    nullif((v_transaction->'items'->0->>'categoryId'), '')::uuid,
                    case
                        when v_direction = 'income' then v_uncategorized_income_id
                        else v_uncategorized_expense_id
                    end
                );
                v_title := coalesce(
                    v_title,
                    nullif(trim(coalesce(v_transaction->'items'->0->>'title', '')), ''),
                    v_merchant_name,
                    case when v_direction = 'income' then 'Поступление' else 'Трата' end
                );
            else
                v_category_id := null;
                v_title := coalesce(v_title, v_merchant_name, case when v_direction = 'income' then 'Поступление' else 'Чек' end);
            end if;

            insert into public.finance_transactions (
                user_id,
                account_id,
                direction,
                transaction_kind,
                title,
                merchant_name,
                note,
                amount_minor,
                currency,
                happened_at,
                category_id,
                item_count,
                source_type
            )
            values (
                v_user_id,
                v_account_id,
                v_direction,
                v_transaction_kind,
                v_title,
                v_merchant_name,
                v_note,
                v_total_minor,
                v_currency,
                v_happened_at,
                v_category_id,
                v_item_count,
                v_source_type
            )
            returning id into v_transaction_id;

            v_index := 0;
            for v_item in
                select value
                from jsonb_array_elements(v_transaction->'items')
            loop
                v_amount_minor := coalesce((v_item->>'amountMinor')::bigint, 0);
                v_item_title := nullif(trim(coalesce(v_item->>'title', '')), '');
                v_item_category_id := coalesce(
                    nullif(v_item->>'categoryId', '')::uuid,
                    v_category_id,
                    case
                        when v_direction = 'income' then v_uncategorized_income_id
                        else v_uncategorized_expense_id
                    end
                );

                insert into public.finance_transaction_items (
                    transaction_id,
                    user_id,
                    category_id,
                    title,
                    amount_minor,
                    display_order
                )
                values (
                    v_transaction_id,
                    v_user_id,
                    v_item_category_id,
                    coalesce(v_item_title, case when v_direction = 'income' then 'Поступление' else 'Позиция' end),
                    v_amount_minor,
                    v_index
                );

                v_index := v_index + 1;
            end loop;

            perform public.finance_apply_account_activity(
                v_account_id,
                case when v_direction = 'income' then 'income' else 'expense' end,
                v_total_minor
            );
        end if;

        v_created_count := v_created_count + 1;
    end loop;

    return jsonb_build_object(
        'createdCount', v_created_count
    );
end;
$$;

grant execute on function public.finance_credit_available(bigint, bigint) to authenticated;
grant execute on function public.finance_apply_account_activity(uuid, text, bigint) to authenticated;
grant execute on function public.finance_upsert_account(
    uuid,
    text,
    text,
    text,
    bigint,
    text,
    boolean,
    bigint,
    bigint,
    bigint,
    date,
    date
) to authenticated;
