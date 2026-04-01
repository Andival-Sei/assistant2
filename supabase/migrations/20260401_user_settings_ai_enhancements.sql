alter table public.user_settings
    add column if not exists ai_enhancements_enabled boolean not null default true;
