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
            'credit_debt',
            'credit_spend',
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

grant execute on function public.finance_update_overview_cards(jsonb) to authenticated;
