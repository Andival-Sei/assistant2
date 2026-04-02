create or replace function public.finance_reset_all()
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

    delete from public.finance_transactions
    where user_id = v_user_id;

    delete from public.finance_accounts
    where user_id = v_user_id;

    delete from public.finance_categories
    where user_id = v_user_id;

    delete from public.finance_user_settings
    where user_id = v_user_id;

    return jsonb_build_object('ok', true);
end;
$$;

grant execute on function public.finance_reset_all() to authenticated;
