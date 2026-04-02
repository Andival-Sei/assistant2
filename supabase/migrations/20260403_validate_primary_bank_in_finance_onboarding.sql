create or replace function public.finance_complete_onboarding(
    p_currency text default null,
    p_bank text default null,
    p_card_type text default null,
    p_last_four_digits text default null,
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
    v_card_type text;
    v_last_four_digits text;
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

    v_provider_code := public.finance_provider_code_from_input(p_bank);
    v_card_type := lower(nullif(trim(coalesce(p_card_type, '')), ''));
    v_last_four_digits := nullif(regexp_replace(coalesce(p_last_four_digits, ''), '\D', '', 'g'), '');

    if v_provider_code is null and (v_last_four_digits is not null or coalesce(p_primary_account_balance_minor, 0) > 0) then
        raise exception 'Primary card bank is required';
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

    if v_provider_code is not null and v_provider_code <> 'cash' and not exists (
        select 1
        from public.finance_accounts
        where user_id = v_user_id
          and kind = 'bank_card'
          and is_archived = false
    ) then
        if v_card_type not in ('debit', 'credit') then
            raise exception 'Card type must be debit or credit';
        end if;

        if v_last_four_digits is null or length(v_last_four_digits) <> 4 then
            raise exception 'Last four digits are required';
        end if;

        perform public.finance_upsert_account(
            p_provider_code := v_provider_code,
            p_card_type := v_card_type,
            p_last_four_digits := v_last_four_digits,
            p_balance_minor := coalesce(p_primary_account_balance_minor, 0),
            p_currency := v_currency,
            p_make_primary := true
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
            p_provider_code := 'cash',
            p_balance_minor := p_cash_minor,
            p_currency := v_currency,
            p_make_primary := false
        );
    end if;

    perform public.finance_seed_default_categories();

    return public.finance_get_overview();
end;
$$;

grant execute on function public.finance_complete_onboarding(text, text, text, text, bigint, bigint) to authenticated;
