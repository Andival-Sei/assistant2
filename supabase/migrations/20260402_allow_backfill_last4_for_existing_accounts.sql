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
            or (
                coalesce(v_existing.last_four_digits, '') <> coalesce(v_last_four_digits, '')
                and not (
                    nullif(v_existing.last_four_digits, '') is null
                    and v_last_four_digits is not null
                )
            )
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
