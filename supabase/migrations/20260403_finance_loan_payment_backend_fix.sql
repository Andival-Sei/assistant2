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
    elsif v_account.kind = 'loan' then
        v_next_debt := case
            when p_direction = 'income' then greatest(coalesce(v_account.balance_minor, 0) - p_amount_minor, 0)
            else coalesce(v_account.balance_minor, 0) + p_amount_minor
        end;

        update public.finance_accounts
        set balance_minor = v_next_debt,
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

grant execute on function public.finance_apply_account_activity(uuid, text, bigint) to authenticated;
