create or replace function public.handle_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.updated_at = now();
  return new;
end;
$$;

create table if not exists public.user_settings (
  user_id uuid primary key references auth.users(id) on delete cascade,
  gemini_api_key text,
  created_at timestamptz not null default timezone('utc'::text, now()),
  updated_at timestamptz not null default timezone('utc'::text, now())
);

alter table public.user_settings enable row level security;

drop policy if exists "Users can view their own settings" on public.user_settings;
create policy "Users can view their own settings"
  on public.user_settings for select
  using (auth.uid() = user_id);

drop policy if exists "Users can insert their own settings" on public.user_settings;
create policy "Users can insert their own settings"
  on public.user_settings for insert
  with check (auth.uid() = user_id);

drop policy if exists "Users can update their own settings" on public.user_settings;
create policy "Users can update their own settings"
  on public.user_settings for update
  using (auth.uid() = user_id);

drop trigger if exists user_settings_set_updated_at on public.user_settings;
create trigger user_settings_set_updated_at
before update on public.user_settings
for each row execute function public.handle_updated_at();

grant all on table public.user_settings to authenticated;
grant all on table public.user_settings to service_role;
