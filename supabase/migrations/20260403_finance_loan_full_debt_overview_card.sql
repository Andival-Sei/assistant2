update public.finance_user_settings
set overview_cards = case
        when coalesce(overview_cards, '[]'::jsonb) @> '["loan_full_debt"]'::jsonb then overview_cards
        when coalesce(overview_cards, '[]'::jsonb) @> '["loan_debt"]'::jsonb then
            (
                select jsonb_agg(
                    to_jsonb(card_rows.value)
                    order by card_rows.sort_order
                )
                from (
                    select cards.value, cards.ordinality * 2 - 1 as sort_order
                    from jsonb_array_elements_text(overview_cards) with ordinality as cards(value, ordinality)

                    union all

                    select 'loan_full_debt', cards.ordinality * 2
                    from jsonb_array_elements_text(overview_cards) with ordinality as cards(value, ordinality)
                    where cards.value = 'loan_debt'
                ) as card_rows
            )
        else overview_cards || '["loan_full_debt"]'::jsonb
    end,
    updated_at = now()
where exists (
    select 1
    from public.finance_accounts
    where user_id = finance_user_settings.user_id
      and kind = 'loan'
      and is_archived = false
)
  and not coalesce(overview_cards, '[]'::jsonb) @> '["loan_full_debt"]'::jsonb;

alter table public.finance_user_settings
    alter column overview_cards set default
    '["total_balance","card_balance","cash_balance","credit_debt","loan_debt","loan_full_debt","credit_spend","month_income","month_expense","month_result","recent_transactions"]'::jsonb;

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
            'loan_debt',
            'loan_full_debt',
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
