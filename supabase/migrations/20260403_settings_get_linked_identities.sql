create or replace function public.settings_get_linked_identities()
returns jsonb
language plpgsql
security definer
set search_path = public, auth
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
                    'identity_id', i.provider_id,
                    'id', i.id,
                    'provider', i.provider
                )
                order by i.created_at
            )
            from auth.identities i
            where i.user_id = v_user_id
        ),
        '[]'::jsonb
    );
end;
$$;

grant execute on function public.settings_get_linked_identities() to authenticated;
