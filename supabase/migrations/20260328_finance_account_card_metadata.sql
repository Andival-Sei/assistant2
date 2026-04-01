alter table public.finance_accounts
    add column if not exists card_type text null;

alter table public.finance_accounts
    add column if not exists last_four_digits text null;

update public.finance_accounts
set card_type = case
        when kind = 'bank_card' then coalesce(card_type, 'debit')
        else null
    end,
    last_four_digits = case
        when kind = 'cash' then null
        when nullif(regexp_replace(coalesce(last_four_digits, ''), '\D', '', 'g'), '') is not null
            then right(regexp_replace(coalesce(last_four_digits, ''), '\D', '', 'g'), 4)
        when nullif(regexp_replace(coalesce(name, ''), '\D', '', 'g'), '') is not null
            then right(regexp_replace(coalesce(name, ''), '\D', '', 'g'), 4)
        else last_four_digits
    end
where kind in ('bank_card', 'cash');

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'finance_accounts_card_type_check'
    ) then
        alter table public.finance_accounts
            add constraint finance_accounts_card_type_check
            check (
                card_type is null
                or card_type = any (array['debit', 'credit'])
            );
    end if;

    if not exists (
        select 1
        from pg_constraint
        where conname = 'finance_accounts_last_four_digits_check'
    ) then
        alter table public.finance_accounts
            add constraint finance_accounts_last_four_digits_check
            check (
                last_four_digits is null
                or last_four_digits ~ '^[0-9]{4}$'
            );
    end if;
end $$;

create or replace function public.finance_get_overview()
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user_id uuid := auth.uid();
    v_month_start timestamptz := date_trunc('month', now());
    v_month_end timestamptz := v_month_start + interval '1 month';
begin
    if v_user_id is null then
        raise exception 'Not authenticated';
    end if;

    perform public.finance_seed_default_categories();

    return jsonb_build_object(
        'onboardingCompleted',
        exists (
            select 1
            from public.finance_user_settings
            where user_id = v_user_id
              and onboarding_completed_at is not null
        ),
        'defaultCurrency',
        coalesce(
            (
                select default_currency
                from public.finance_user_settings
                where user_id = v_user_id
            ),
            'RUB'
        ),
        'overviewCards',
        coalesce(
            (
                select array_agg(value order by ordinality)
                from public.finance_user_settings,
                jsonb_array_elements_text(overview_cards) with ordinality as cards(value, ordinality)
                where user_id = v_user_id
            ),
            array['total_balance','card_balance','cash_balance','month_income','month_expense','month_result','recent_transactions']
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
                select
                    coalesce(sum(case when direction = 'income' then amount_minor end), 0) -
                    coalesce(sum(case when direction = 'expense' then amount_minor end), 0)
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
                        'merchantName', t.merchant_name,
                        'amountMinor', t.amount_minor,
                        'currency', t.currency,
                        'happenedAt', t.happened_at,
                        'destinationAccountId', t.destination_account_id,
                        'destinationAccountName', destination_account.name,
                        'categoryId', t.category_id,
                        'categoryName', categories.name,
                        'itemCount', t.item_count,
                        'sourceType', t.source_type,
                        'receiptStoragePath', t.receipt_storage_path,
                        'items', coalesce(items.items, '[]'::jsonb)
                    )
                    order by t.happened_at desc, t.created_at desc
                )
                from (
                    select *
                    from public.finance_transactions
                    where user_id = v_user_id
                    order by happened_at desc, created_at desc
                    limit 5
                ) t
                join public.finance_accounts source_account on source_account.id = t.account_id
                left join public.finance_accounts destination_account on destination_account.id = t.destination_account_id
                left join public.finance_categories categories on categories.id = t.category_id
                left join lateral (
                    select jsonb_agg(
                        jsonb_build_object(
                            'id', i.id,
                            'title', i.title,
                            'amountMinor', i.amount_minor,
                            'categoryId', i.category_id,
                            'categoryName', item_categories.name,
                            'categoryCode', item_categories.code,
                            'displayOrder', i.display_order
                        )
                        order by i.display_order, i.created_at
                    ) as items
                    from public.finance_transaction_items i
                    left join public.finance_categories item_categories on item_categories.id = i.category_id
                    where i.transaction_id = t.id
                ) items on true
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
    v_card_type text;
    v_last_four_digits text;
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
