create or replace function public.settings_unlink_google_identity()
returns jsonb
language plpgsql
security definer
set search_path = public, auth
as $$
declare
    v_user_id uuid := auth.uid();
    v_google_identity_id uuid;
    v_remaining_providers text[];
begin
    if v_user_id is null then
        raise exception 'Not authenticated';
    end if;

    if (
        select count(*)
        from auth.identities
        where user_id = v_user_id
    ) < 2 then
        raise exception 'Cannot unlink the only sign-in method';
    end if;

    select i.id
    into v_google_identity_id
    from auth.identities i
    where i.user_id = v_user_id
      and i.provider = 'google'
    order by i.created_at desc
    limit 1;

    if v_google_identity_id is null then
        raise exception 'Google identity not found';
    end if;

    delete from auth.identities
    where user_id = v_user_id
      and id = v_google_identity_id;

    select coalesce(
        array_agg(provider order by first_created_at),
        '{}'::text[]
    )
    into v_remaining_providers
    from (
        select i.provider, min(i.created_at) as first_created_at
        from auth.identities i
        where i.user_id = v_user_id
        group by i.provider
    ) providers;

    update auth.users
    set raw_app_meta_data = jsonb_set(
            jsonb_set(
                coalesce(raw_app_meta_data, '{}'::jsonb),
                '{providers}',
                to_jsonb(v_remaining_providers),
                true
            ),
            '{provider}',
            to_jsonb(case when coalesce(array_length(v_remaining_providers, 1), 0) > 0 then v_remaining_providers[1] else null end),
            true
        ),
        updated_at = timezone('utc', now())
    where id = v_user_id;

    return public.settings_get_linked_identities();
end;
$$;

grant execute on function public.settings_unlink_google_identity() to authenticated;
