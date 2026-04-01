create table if not exists public.finance_transaction_items (
    id uuid primary key default gen_random_uuid(),
    transaction_id uuid not null references public.finance_transactions (id) on delete cascade,
    category_id uuid null references public.finance_categories (id) on delete set null,
    title text not null,
    amount_minor bigint not null check (amount_minor > 0),
    display_order integer not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

alter table public.finance_transactions
    add column if not exists category_id uuid null references public.finance_categories (id) on delete set null;

alter table public.finance_transactions
    add column if not exists merchant_name text null;

alter table public.finance_transactions
    add column if not exists source_kind text not null default 'manual'
    check (source_kind in ('manual', 'camera', 'file'));

alter table public.finance_transactions
    add column if not exists receipt_storage_path text null;

alter table public.finance_transactions
    add column if not exists raw_payload jsonb null;

create index if not exists finance_transactions_category_idx
    on public.finance_transactions (category_id, happened_at desc)
    where category_id is not null;

create index if not exists finance_transaction_items_transaction_idx
    on public.finance_transaction_items (transaction_id, display_order, created_at);

create index if not exists finance_transaction_items_category_idx
    on public.finance_transaction_items (category_id)
    where category_id is not null;

drop trigger if exists finance_transaction_items_set_updated_at on public.finance_transaction_items;
create trigger finance_transaction_items_set_updated_at
before update on public.finance_transaction_items
for each row
execute function public.finance_set_updated_at();

alter table public.finance_transaction_items enable row level security;

drop policy if exists "finance_transaction_items_select_own" on public.finance_transaction_items;
create policy "finance_transaction_items_select_own"
on public.finance_transaction_items
for select
to authenticated
using (
    exists (
        select 1
        from public.finance_transactions t
        where t.id = finance_transaction_items.transaction_id
          and t.user_id = (select auth.uid())
    )
);

drop policy if exists "finance_transaction_items_insert_own" on public.finance_transaction_items;
create policy "finance_transaction_items_insert_own"
on public.finance_transaction_items
for insert
to authenticated
with check (
    exists (
        select 1
        from public.finance_transactions t
        where t.id = finance_transaction_items.transaction_id
          and t.user_id = (select auth.uid())
    )
);

drop policy if exists "finance_transaction_items_update_own" on public.finance_transaction_items;
create policy "finance_transaction_items_update_own"
on public.finance_transaction_items
for update
to authenticated
using (
    exists (
        select 1
        from public.finance_transactions t
        where t.id = finance_transaction_items.transaction_id
          and t.user_id = (select auth.uid())
    )
)
with check (
    exists (
        select 1
        from public.finance_transactions t
        where t.id = finance_transaction_items.transaction_id
          and t.user_id = (select auth.uid())
    )
);

drop policy if exists "finance_transaction_items_delete_own" on public.finance_transaction_items;
create policy "finance_transaction_items_delete_own"
on public.finance_transaction_items
for delete
to authenticated
using (
    exists (
        select 1
        from public.finance_transactions t
        where t.id = finance_transaction_items.transaction_id
          and t.user_id = (select auth.uid())
    )
);

insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values (
    'finance-receipts',
    'finance-receipts',
    false,
    15728640,
    array[
        'image/jpeg',
        'image/png',
        'image/webp',
        'image/heic',
        'image/heif',
        'application/pdf'
    ]
)
on conflict (id) do update
set public = excluded.public,
    file_size_limit = excluded.file_size_limit,
    allowed_mime_types = excluded.allowed_mime_types;

create or replace function public.finance_seed_default_categories()
returns void
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

    with seeds as (
        select *
        from (
            values
                ('expense_food', null, 'expense', 'Еда', 'utensils-crossed', '#E25E4B', 10),
                ('expense_food_groceries', 'expense_food', 'expense', 'Продукты', 'shopping-basket', null, 11),
                ('expense_food_groceries_meat', 'expense_food_groceries', 'expense', 'Мясо', 'beef', null, 12),
                ('expense_food_groceries_meat_poultry', 'expense_food_groceries_meat', 'expense', 'Птица', 'drumstick', null, 13),
                ('expense_food_groceries_meat_poultry_chicken', 'expense_food_groceries_meat_poultry', 'expense', 'Курица', 'egg', null, 14),
                ('expense_food_groceries_meat_poultry_turkey', 'expense_food_groceries_meat_poultry', 'expense', 'Индейка', 'egg-fried', null, 15),
                ('expense_food_groceries_meat_beef', 'expense_food_groceries_meat', 'expense', 'Говядина', 'beef', null, 16),
                ('expense_food_groceries_meat_pork', 'expense_food_groceries_meat', 'expense', 'Свинина', 'beef', null, 17),
                ('expense_food_groceries_fish', 'expense_food_groceries', 'expense', 'Рыба и морепродукты', 'fish', null, 18),
                ('expense_food_groceries_dairy', 'expense_food_groceries', 'expense', 'Молочные продукты', 'milk', null, 19),
                ('expense_food_groceries_vegetables', 'expense_food_groceries', 'expense', 'Овощи', 'carrot', null, 20),
                ('expense_food_groceries_fruits', 'expense_food_groceries', 'expense', 'Фрукты и ягоды', 'apple', null, 21),
                ('expense_food_groceries_bakery', 'expense_food_groceries', 'expense', 'Хлеб и выпечка', 'sandwich', null, 22),
                ('expense_food_groceries_grains', 'expense_food_groceries', 'expense', 'Крупы и макароны', 'wheat', null, 23),
                ('expense_food_groceries_beverages', 'expense_food_groceries', 'expense', 'Напитки', 'cup-soda', null, 24),
                ('expense_food_groceries_sweets', 'expense_food_groceries', 'expense', 'Сладости и десерты', 'cookie', null, 25),
                ('expense_food_groceries_frozen', 'expense_food_groceries', 'expense', 'Замороженные продукты', 'snowflake', null, 26),
                ('expense_food_ready', 'expense_food', 'expense', 'Готовая еда', 'chef-hat', null, 27),
                ('expense_food_delivery', 'expense_food', 'expense', 'Доставка еды', 'bike', null, 28),
                ('expense_food_cafe', 'expense_food', 'expense', 'Кафе и рестораны', 'utensils', null, 29),
                ('expense_food_alcohol', 'expense_food', 'expense', 'Алкоголь', 'wine', null, 30),
                ('expense_home', null, 'expense', 'Дом', 'house', '#7C70FF', 40),
                ('expense_home_rent', 'expense_home', 'expense', 'Аренда и ипотека', 'house-plus', null, 41),
                ('expense_home_utilities', 'expense_home', 'expense', 'Коммунальные услуги', 'receipt-text', null, 42),
                ('expense_home_repair', 'expense_home', 'expense', 'Ремонт и обслуживание', 'hammer', null, 43),
                ('expense_home_furniture', 'expense_home', 'expense', 'Мебель и интерьер', 'sofa', null, 44),
                ('expense_home_appliances', 'expense_home', 'expense', 'Бытовая техника', 'refrigerator', null, 45),
                ('expense_home_supplies', 'expense_home', 'expense', 'Хозтовары', 'package-open', null, 46),
                ('expense_transport', null, 'expense', 'Транспорт', 'car-front', '#4A8BFF', 60),
                ('expense_transport_fuel', 'expense_transport', 'expense', 'Топливо', 'fuel', null, 61),
                ('expense_transport_public', 'expense_transport', 'expense', 'Общественный транспорт', 'bus-front', null, 62),
                ('expense_transport_taxi', 'expense_transport', 'expense', 'Такси', 'car-taxi-front', null, 63),
                ('expense_transport_parking', 'expense_transport', 'expense', 'Парковка', 'parking-circle', null, 64),
                ('expense_transport_maintenance', 'expense_transport', 'expense', 'Обслуживание автомобиля', 'wrench', null, 65),
                ('expense_transport_insurance', 'expense_transport', 'expense', 'Страховка автомобиля', 'shield', null, 66),
                ('expense_health', null, 'expense', 'Здоровье', 'heart-pulse', '#EF5D71', 80),
                ('expense_health_pharmacy', 'expense_health', 'expense', 'Аптека', 'pill', null, 81),
                ('expense_health_doctor', 'expense_health', 'expense', 'Врачи', 'stethoscope', null, 82),
                ('expense_health_dentist', 'expense_health', 'expense', 'Стоматология', 'badge-plus', null, 83),
                ('expense_health_optics', 'expense_health', 'expense', 'Оптика', 'glasses', null, 84),
                ('expense_health_insurance', 'expense_health', 'expense', 'Медицинская страховка', 'shield-check', null, 85),
                ('expense_leisure', null, 'expense', 'Досуг', 'gamepad-2', '#C973FF', 100),
                ('expense_leisure_movies', 'expense_leisure', 'expense', 'Кино и театры', 'film', null, 101),
                ('expense_leisure_games', 'expense_leisure', 'expense', 'Игры и развлечения', 'gamepad-2', null, 102),
                ('expense_leisure_hobby', 'expense_leisure', 'expense', 'Хобби', 'palette', null, 103),
                ('expense_leisure_subscription', 'expense_leisure', 'expense', 'Подписки', 'sparkles', null, 104),
                ('expense_leisure_events', 'expense_leisure', 'expense', 'Концерты и мероприятия', 'ticket', null, 105),
                ('expense_education', null, 'expense', 'Образование', 'graduation-cap', '#6D7CF7', 120),
                ('expense_education_courses', 'expense_education', 'expense', 'Курсы и обучение', 'school', null, 121),
                ('expense_education_books', 'expense_education', 'expense', 'Книги и учебники', 'book-open', null, 122),
                ('expense_education_online', 'expense_education', 'expense', 'Онлайн-обучение', 'laptop', null, 123),
                ('expense_education_stationery', 'expense_education', 'expense', 'Канцелярия', 'notebook-pen', null, 124),
                ('expense_shopping', null, 'expense', 'Покупки', 'shopping-bag', '#F59B38', 140),
                ('expense_shopping_electronics', 'expense_shopping', 'expense', 'Электроника', 'smartphone', null, 141),
                ('expense_shopping_books', 'expense_shopping', 'expense', 'Книги и журналы', 'book', null, 142),
                ('expense_shopping_gifts', 'expense_shopping', 'expense', 'Подарки', 'gift', null, 143),
                ('expense_clothes', null, 'expense', 'Одежда и обувь', 'shirt', '#E56FA4', 160),
                ('expense_clothes_apparel', 'expense_clothes', 'expense', 'Одежда', 'shirt', null, 161),
                ('expense_clothes_shoes', 'expense_clothes', 'expense', 'Обувь', 'footprints', null, 162),
                ('expense_clothes_accessories', 'expense_clothes', 'expense', 'Аксессуары', 'watch', null, 163),
                ('expense_clothes_cleaning', 'expense_clothes', 'expense', 'Химчистка и ремонт', 'scissors', null, 164),
                ('expense_communication', null, 'expense', 'Связь', 'smartphone', '#54A1FF', 180),
                ('expense_communication_mobile', 'expense_communication', 'expense', 'Мобильная связь', 'smartphone', null, 181),
                ('expense_communication_internet', 'expense_communication', 'expense', 'Интернет', 'wifi', null, 182),
                ('expense_communication_tv', 'expense_communication', 'expense', 'Телевидение', 'monitor-play', null, 183),
                ('expense_communication_mail', 'expense_communication', 'expense', 'Почтовые услуги', 'mail', null, 184),
                ('expense_finance', null, 'expense', 'Финансы', 'wallet', '#6B73D6', 200),
                ('expense_finance_loans', 'expense_finance', 'expense', 'Кредиты', 'credit-card', null, 201),
                ('expense_finance_interest', 'expense_finance', 'expense', 'Проценты по кредитам', 'percent', null, 202),
                ('expense_finance_bank_fee', 'expense_finance', 'expense', 'Банковские комиссии', 'receipt', null, 203),
                ('expense_finance_insurance', 'expense_finance', 'expense', 'Страхование', 'shield', null, 204),
                ('expense_finance_investment', 'expense_finance', 'expense', 'Инвестиции', 'chart-candlestick', null, 205),
                ('expense_finance_savings', 'expense_finance', 'expense', 'Сбережения', 'piggy-bank', null, 206),
                ('expense_beauty', null, 'expense', 'Красота и уход', 'sparkles', '#D86BC5', 220),
                ('expense_beauty_cosmetics', 'expense_beauty', 'expense', 'Косметика', 'sparkles', null, 221),
                ('expense_beauty_hair', 'expense_beauty', 'expense', 'Парикмахерская', 'scissors', null, 222),
                ('expense_beauty_spa', 'expense_beauty', 'expense', 'Спа и массаж', 'flower-2', null, 223),
                ('expense_beauty_care', 'expense_beauty', 'expense', 'Косметология', 'sparkle', null, 224),
                ('expense_sport', null, 'expense', 'Спорт и фитнес', 'dumbbell', '#3DBA7C', 240),
                ('expense_sport_gym', 'expense_sport', 'expense', 'Спортзал', 'dumbbell', null, 241),
                ('expense_sport_apparel', 'expense_sport', 'expense', 'Спортивная одежда', 'shirt', null, 242),
                ('expense_sport_food', 'expense_sport', 'expense', 'Спортивное питание', 'apple', null, 243),
                ('expense_sport_trainer', 'expense_sport', 'expense', 'Тренер', 'medal', null, 244),
                ('expense_travel', null, 'expense', 'Путешествия', 'plane', '#4F8AF0', 260),
                ('expense_travel_flights', 'expense_travel', 'expense', 'Авиабилеты', 'plane', null, 261),
                ('expense_travel_hotels', 'expense_travel', 'expense', 'Отели', 'hotel', null, 262),
                ('expense_travel_food', 'expense_travel', 'expense', 'Питание в поездках', 'utensils', null, 263),
                ('expense_travel_events', 'expense_travel', 'expense', 'Экскурсии и развлечения', 'camera', null, 264),
                ('expense_travel_souvenirs', 'expense_travel', 'expense', 'Сувениры', 'gift', null, 265),
                ('expense_travel_docs', 'expense_travel', 'expense', 'Виза и документы', 'file-text', null, 266),
                ('expense_children', null, 'expense', 'Дети', 'baby', '#F29B53', 280),
                ('expense_children_clothes', 'expense_children', 'expense', 'Детская одежда', 'shirt', null, 281),
                ('expense_children_food', 'expense_children', 'expense', 'Детское питание', 'baby', null, 282),
                ('expense_children_toys', 'expense_children', 'expense', 'Игрушки', 'blocks', null, 283),
                ('expense_children_education', 'expense_children', 'expense', 'Образование детей', 'school', null, 284),
                ('expense_children_clubs', 'expense_children', 'expense', 'Кружки и секции', 'music-4', null, 285),
                ('expense_children_health', 'expense_children', 'expense', 'Детское здоровье', 'heart', null, 286),
                ('expense_pets', null, 'expense', 'Домашние животные', 'paw-print', '#F08D4F', 300),
                ('expense_pets_food', 'expense_pets', 'expense', 'Корм для животных', 'paw-print', null, 301),
                ('expense_pets_vet', 'expense_pets', 'expense', 'Ветеринар', 'stethoscope', null, 302),
                ('expense_pets_accessories', 'expense_pets', 'expense', 'Аксессуары', 'bone', null, 303),
                ('expense_pets_grooming', 'expense_pets', 'expense', 'Груминг', 'scissors', null, 304),
                ('expense_business', null, 'expense', 'Бизнес', 'briefcase-business', '#6D7CF7', 320),
                ('expense_business_office', 'expense_business', 'expense', 'Офис', 'building-2', null, 321),
                ('expense_business_ads', 'expense_business', 'expense', 'Реклама', 'megaphone', null, 322),
                ('expense_business_equipment', 'expense_business', 'expense', 'Оборудование', 'monitor-cog', null, 323),
                ('expense_business_stationery', 'expense_business', 'expense', 'Канцелярия', 'notebook-pen', null, 324),
                ('expense_taxes', null, 'expense', 'Налоги', 'file-text', '#D35A5A', 340),
                ('expense_taxes_income', 'expense_taxes', 'expense', 'Подоходный налог', 'badge-percent', null, 341),
                ('expense_taxes_property', 'expense_taxes', 'expense', 'Имущественный налог', 'house', null, 342),
                ('expense_taxes_transport', 'expense_taxes', 'expense', 'Транспортный налог', 'car-front', null, 343),
                ('expense_taxes_other', 'expense_taxes', 'expense', 'Прочие налоги', 'file-text', null, 344),
                ('expense_charity', null, 'expense', 'Подарки и благотворительность', 'gift', '#8B7CFF', 360),
                ('expense_charity_gifts', 'expense_charity', 'expense', 'Подарки', 'gift', null, 361),
                ('expense_charity_donations', 'expense_charity', 'expense', 'Благотворительность', 'heart-handshake', null, 362),
                ('expense_other', null, 'expense', 'Прочее', 'folder', '#8D96A8', 380),
                ('income_salary', null, 'income', 'Зарплата', 'badge-russian-ruble', '#2AE88B', 10),
                ('income_salary_bonus', 'income_salary', 'income', 'Премии', 'gift', null, 11),
                ('income_investments', null, 'income', 'Инвестиции', 'chart-candlestick', '#18C17E', 20),
                ('income_investments_dividends', 'income_investments', 'income', 'Дивиденды', 'landmark', null, 21),
                ('income_investments_deposits', 'income_investments', 'income', 'Проценты по вкладам', 'banknote-arrow-down', null, 22),
                ('income_investments_assets', 'income_investments', 'income', 'Продажа активов', 'trending-up', null, 23),
                ('income_rent', null, 'income', 'Аренда недвижимости', 'house', '#16B36F', 30),
                ('income_rent_home', 'income_rent', 'income', 'Аренда квартиры', 'house', null, 31),
                ('income_rent_commercial', 'income_rent', 'income', 'Коммерческая аренда', 'building-2', null, 32),
                ('income_gifts', null, 'income', 'Подарки', 'gift', '#5DA6FF', 40),
                ('income_refunds', null, 'income', 'Возвраты', 'rotate-ccw', '#F6A52C', 50),
                ('income_other', null, 'income', 'Прочие поступления', 'plus-circle', '#8D96A8', 60)
        ) as t(code, parent_code, direction, name, icon, color, display_order)
    ),
    inserted_roots as (
        insert into public.finance_categories (
            user_id,
            parent_id,
            direction,
            code,
            name,
            icon,
            color,
            display_order
        )
        select
            v_user_id,
            null,
            s.direction,
            s.code,
            s.name,
            s.icon,
            s.color,
            s.display_order
        from seeds s
        where s.parent_code is null
        on conflict (user_id, code) where is_archived = false do update
        set direction = excluded.direction,
            name = excluded.name,
            icon = excluded.icon,
            color = excluded.color,
            display_order = excluded.display_order,
            updated_at = now()
        returning id, code
    )
    insert into public.finance_categories (
        user_id,
        parent_id,
        direction,
        code,
        name,
        icon,
        color,
        display_order
    )
    select
        v_user_id,
        parent.id,
        s.direction,
        s.code,
        s.name,
        s.icon,
        s.color,
        s.display_order
    from seeds s
    join public.finance_categories parent
      on parent.user_id = v_user_id
     and parent.code = s.parent_code
     and parent.is_archived = false
    where s.parent_code is not null
    on conflict (user_id, code) where is_archived = false do update
    set parent_id = excluded.parent_id,
        direction = excluded.direction,
        name = excluded.name,
        icon = excluded.icon,
        color = excluded.color,
        display_order = excluded.display_order,
        updated_at = now();
end;
$$;

create or replace function public.finance_create_transaction(
    p_account_id uuid,
    p_direction text,
    p_title text default null,
    p_note text default null,
    p_amount_minor bigint default null,
    p_currency text default null,
    p_happened_at timestamptz default null,
    p_category_id uuid default null,
    p_items jsonb default '[]'::jsonb,
    p_destination_account_id uuid default null,
    p_source_kind text default 'manual',
    p_receipt_storage_path text default null,
    p_merchant_name text default null,
    p_raw_payload jsonb default null
)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user_id uuid := auth.uid();
    v_account public.finance_accounts%rowtype;
    v_destination_account public.finance_accounts%rowtype;
    v_direction text := lower(trim(coalesce(p_direction, '')));
    v_source_kind text := lower(trim(coalesce(p_source_kind, 'manual')));
    v_currency text;
    v_happened_at timestamptz := coalesce(p_happened_at, now());
    v_items jsonb := coalesce(p_items, '[]'::jsonb);
    v_items_count integer := 0;
    v_total_minor bigint := 0;
    v_transaction_kind text := 'single';
    v_title text;
    v_transaction_id uuid;
    v_first_item_title text;
    v_first_item_category_id uuid;
    v_item jsonb;
    v_item_index integer := 0;
    v_item_title text;
    v_item_amount bigint;
    v_item_category_id uuid;
begin
    if v_user_id is null then
        raise exception 'Not authenticated';
    end if;

    if v_direction not in ('income', 'expense', 'transfer') then
        raise exception 'Unsupported direction';
    end if;

    if v_source_kind not in ('manual', 'camera', 'file') then
        raise exception 'Unsupported source kind';
    end if;

    select *
    into v_account
    from public.finance_accounts
    where id = p_account_id
      and user_id = v_user_id
      and is_archived = false
    for update;

    if v_account.id is null then
        raise exception 'Account not found';
    end if;

    if p_category_id is not null and not exists (
        select 1
        from public.finance_categories
        where id = p_category_id
          and user_id = v_user_id
          and is_archived = false
    ) then
        raise exception 'Category not found';
    end if;

    if jsonb_typeof(v_items) <> 'array' then
        raise exception 'Items must be an array';
    end if;

    v_items_count := jsonb_array_length(v_items);

    if v_direction = 'transfer' then
        if p_destination_account_id is null then
            raise exception 'Destination account is required';
        end if;

        if p_destination_account_id = p_account_id then
            raise exception 'Destination account must differ from source account';
        end if;

        select *
        into v_destination_account
        from public.finance_accounts
        where id = p_destination_account_id
          and user_id = v_user_id
          and is_archived = false
        for update;

        if v_destination_account.id is null then
            raise exception 'Destination account not found';
        end if;

        v_total_minor := coalesce(p_amount_minor, 0);
        if v_total_minor <= 0 then
            raise exception 'Amount must be positive';
        end if;

        v_transaction_kind := 'transfer';
    else
        if v_items_count > 0 then
            for v_item in
                select value
                from jsonb_array_elements(v_items)
            loop
                v_item_index := v_item_index + 1;
                v_item_title := nullif(trim(coalesce(v_item ->> 'title', '')), '');
                v_item_amount := nullif(v_item ->> 'amountMinor', '')::bigint;
                v_item_category_id := nullif(v_item ->> 'categoryId', '')::uuid;

                if v_item_title is null then
                    raise exception 'Each item must have a title';
                end if;

                if v_item_amount is null or v_item_amount <= 0 then
                    raise exception 'Each item amount must be positive';
                end if;

                if v_item_category_id is not null and not exists (
                    select 1
                    from public.finance_categories
                    where id = v_item_category_id
                      and user_id = v_user_id
                      and is_archived = false
                ) then
                    raise exception 'Item category not found';
                end if;

                if v_item_index = 1 then
                    v_first_item_title := v_item_title;
                    v_first_item_category_id := v_item_category_id;
                end if;

                v_total_minor := v_total_minor + v_item_amount;
            end loop;

            if p_amount_minor is not null and p_amount_minor <> v_total_minor then
                raise exception 'Items total must match transaction amount';
            end if;
        else
            v_total_minor := coalesce(p_amount_minor, 0);
        end if;

        if v_total_minor <= 0 then
            raise exception 'Amount must be positive';
        end if;

        v_transaction_kind := case when v_items_count > 1 then 'split' else 'single' end;
    end if;

    v_currency := coalesce(p_currency, v_account.currency, 'RUB');
    if v_currency not in ('RUB', 'USD', 'EUR') then
        raise exception 'Unsupported currency';
    end if;

    v_title := nullif(trim(coalesce(p_title, '')), '');
    if v_title is null then
        if v_direction = 'transfer' then
            v_title := coalesce(nullif(trim(coalesce(p_merchant_name, '')), ''), 'Перевод между счетами');
        elsif v_transaction_kind = 'single' then
            v_title := coalesce(v_first_item_title, nullif(trim(coalesce(p_merchant_name, '')), ''), 'Транзакция');
        else
            v_title := coalesce(nullif(trim(coalesce(p_merchant_name, '')), ''), 'Покупка');
        end if;
    end if;

    insert into public.finance_transactions (
        user_id,
        account_id,
        destination_account_id,
        direction,
        transaction_kind,
        title,
        note,
        amount_minor,
        currency,
        happened_at,
        category_id,
        item_count,
        merchant_name,
        source_kind,
        receipt_storage_path,
        raw_payload
    )
    values (
        v_user_id,
        p_account_id,
        case when v_direction = 'transfer' then p_destination_account_id else null end,
        v_direction,
        v_transaction_kind,
        v_title,
        nullif(trim(coalesce(p_note, '')), ''),
        v_total_minor,
        v_currency,
        v_happened_at,
        case
            when v_direction = 'transfer' then null
            when v_transaction_kind = 'single' then coalesce(p_category_id, v_first_item_category_id)
            else null
        end,
        greatest(1, case when v_direction = 'transfer' then 1 else v_items_count end),
        nullif(trim(coalesce(p_merchant_name, '')), ''),
        v_source_kind,
        nullif(trim(coalesce(p_receipt_storage_path, '')), ''),
        p_raw_payload
    )
    returning id into v_transaction_id;

    if v_direction <> 'transfer' and v_items_count > 1 then
        insert into public.finance_transaction_items (
            transaction_id,
            category_id,
            title,
            amount_minor,
            display_order
        )
        select
            v_transaction_id,
            nullif(value ->> 'categoryId', '')::uuid,
            trim(value ->> 'title'),
            (value ->> 'amountMinor')::bigint,
            row_number() over ()
        from jsonb_array_elements(v_items);
    end if;

    if v_direction = 'income' then
        update public.finance_accounts
        set balance_minor = balance_minor + v_total_minor,
            updated_at = now()
        where id = p_account_id;
    elsif v_direction = 'expense' then
        update public.finance_accounts
        set balance_minor = balance_minor - v_total_minor,
            updated_at = now()
        where id = p_account_id;
    else
        update public.finance_accounts
        set balance_minor = balance_minor - v_total_minor,
            updated_at = now()
        where id = p_account_id;

        update public.finance_accounts
        set balance_minor = balance_minor + v_total_minor,
            updated_at = now()
        where id = p_destination_account_id;
    end if;

    perform public.finance_seed_default_categories();

    return public.finance_get_overview();
end;
$$;

grant execute on function public.finance_create_transaction(
    uuid,
    text,
    text,
    text,
    bigint,
    text,
    timestamptz,
    uuid,
    jsonb,
    uuid,
    text,
    text,
    text,
    jsonb
) to authenticated;

create or replace function public.finance_get_overview()
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user_id uuid := auth.uid();
    v_month_start date := date_trunc('month', current_date)::date;
    v_month_end date := (date_trunc('month', current_date) + interval '1 month')::date;
begin
    if v_user_id is null then
        raise exception 'Not authenticated';
    end if;

    perform public.finance_seed_default_categories();

    return jsonb_build_object(
        'onboardingCompleted',
        exists(
            select 1
            from public.finance_user_settings
            where user_id = v_user_id
              and onboarding_completed_at is not null
        ),
        'defaultCurrency',
        (
            select coalesce(default_currency, 'RUB')
            from public.finance_user_settings
            where user_id = v_user_id
        ),
        'overviewCards',
        coalesce(
            (
                select overview_cards
                from public.finance_user_settings
                where user_id = v_user_id
            ),
            '["total_balance","card_balance","cash_balance","month_income","month_expense","month_result","recent_transactions"]'::jsonb
        ),
        'totalBalanceMinor',
        coalesce(
            (
                select sum(balance_minor)
                from public.finance_accounts
                where user_id = v_user_id
                  and is_archived = false
            ),
            0
        ),
        'cardBalanceMinor',
        coalesce(
            (
                select sum(balance_minor)
                from public.finance_accounts
                where user_id = v_user_id
                  and kind = 'bank_card'
                  and is_archived = false
            ),
            0
        ),
        'cashBalanceMinor',
        coalesce(
            (
                select sum(balance_minor)
                from public.finance_accounts
                where user_id = v_user_id
                  and kind = 'cash'
                  and is_archived = false
            ),
            0
        ),
        'monthIncomeMinor',
        coalesce(
            (
                select sum(amount_minor)
                from public.finance_transactions
                where user_id = v_user_id
                  and direction = 'income'
                  and happened_at >= v_month_start
                  and happened_at < v_month_end
            ),
            0
        ),
        'monthExpenseMinor',
        coalesce(
            (
                select sum(amount_minor)
                from public.finance_transactions
                where user_id = v_user_id
                  and direction = 'expense'
                  and happened_at >= v_month_start
                  and happened_at < v_month_end
            ),
            0
        ),
        'monthNetMinor',
        coalesce(
            (
                select sum(
                    case
                        when direction = 'income' then amount_minor
                        when direction = 'expense' then -amount_minor
                        else 0
                    end
                )
                from public.finance_transactions
                where user_id = v_user_id
                  and happened_at >= v_month_start
                  and happened_at < v_month_end
            ),
            0
        ),
        'accounts',
        coalesce(
            (
                select jsonb_agg(
                    jsonb_build_object(
                        'id', a.id,
                        'kind', a.kind,
                        'name', a.name,
                        'bankName', a.bank_name,
                        'providerCode', a.provider_code,
                        'currency', a.currency,
                        'balanceMinor', a.balance_minor,
                        'isPrimary', a.is_primary,
                        'transactionCount', coalesce(tx.transaction_count, 0),
                        'balanceEditable', coalesce(tx.transaction_count, 0) = 0
                    )
                    order by a.kind = 'cash', a.display_order, a.created_at desc
                )
                from public.finance_accounts a
                left join (
                    select
                        account_id,
                        count(*)::int as transaction_count
                    from public.finance_transactions
                    where user_id = v_user_id
                    group by account_id
                ) tx on tx.account_id = a.id
                where a.user_id = v_user_id
                  and a.is_archived = false
            ),
            '[]'::jsonb
        ),
        'recentTransactions',
        coalesce(
            (
                select jsonb_agg(
                    jsonb_build_object(
                        'id', t.id,
                        'accountId', t.account_id,
                        'accountName', source_account.name,
                        'direction', t.direction,
                        'transactionKind', t.transaction_kind,
                        'title', t.title,
                        'note', t.note,
                        'amountMinor', t.amount_minor,
                        'currency', t.currency,
                        'happenedAt', t.happened_at,
                        'destinationAccountId', t.destination_account_id,
                        'destinationAccountName', destination_account.name,
                        'itemCount', t.item_count,
                        'categoryId', t.category_id,
                        'categoryName', category.name,
                        'merchantName', t.merchant_name,
                        'sourceKind', t.source_kind,
                        'receiptStoragePath', t.receipt_storage_path,
                        'items', coalesce(item_rows.items, '[]'::jsonb)
                    )
                    order by t.happened_at desc
                )
                from (
                    select *
                    from public.finance_transactions
                    where user_id = v_user_id
                    order by happened_at desc
                    limit 8
                ) t
                join public.finance_accounts source_account on source_account.id = t.account_id
                left join public.finance_accounts destination_account on destination_account.id = t.destination_account_id
                left join public.finance_categories category on category.id = t.category_id
                left join lateral (
                    select jsonb_agg(
                        jsonb_build_object(
                            'id', item.id,
                            'categoryId', item.category_id,
                            'categoryName', item_category.name,
                            'title', item.title,
                            'amountMinor', item.amount_minor,
                            'displayOrder', item.display_order
                        )
                        order by item.display_order, item.created_at
                    ) as items
                    from public.finance_transaction_items item
                    left join public.finance_categories item_category on item_category.id = item.category_id
                    where item.transaction_id = t.id
                ) item_rows on true
            ),
            '[]'::jsonb
        ),
        'categoriesCount',
        coalesce(
            (
                select count(*)::int
                from public.finance_categories
                where user_id = v_user_id
                  and is_archived = false
            ),
            0
        )
    );
end;
$$;

create or replace function public.finance_get_transactions(p_month text default null)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user_id uuid := auth.uid();
    v_month_start date := public.finance_normalize_month_start(p_month);
    v_month_end date := (v_month_start + interval '1 month')::date;
begin
    if v_user_id is null then
        raise exception 'Not authenticated';
    end if;

    return jsonb_build_object(
        'month', to_char(v_month_start, 'YYYY-MM'),
        'availableMonths',
        coalesce(
            (
                select jsonb_agg(month_key order by month_key desc)
                from (
                    select distinct to_char(date_trunc('month', happened_at), 'YYYY-MM') as month_key
                    from public.finance_transactions
                    where user_id = v_user_id
                ) months
            ),
            jsonb_build_array(to_char(v_month_start, 'YYYY-MM'))
        ),
        'transactions',
        coalesce(
            (
                select jsonb_agg(
                    jsonb_build_object(
                        'id', t.id,
                        'accountId', t.account_id,
                        'accountName', source_account.name,
                        'direction', t.direction,
                        'transactionKind', t.transaction_kind,
                        'title', t.title,
                        'note', t.note,
                        'amountMinor', t.amount_minor,
                        'currency', t.currency,
                        'happenedAt', t.happened_at,
                        'destinationAccountId', t.destination_account_id,
                        'destinationAccountName', destination_account.name,
                        'itemCount', t.item_count,
                        'categoryId', t.category_id,
                        'categoryName', category.name,
                        'merchantName', t.merchant_name,
                        'sourceKind', t.source_kind,
                        'receiptStoragePath', t.receipt_storage_path,
                        'items', coalesce(item_rows.items, '[]'::jsonb)
                    )
                    order by t.happened_at desc
                )
                from public.finance_transactions t
                join public.finance_accounts source_account on source_account.id = t.account_id
                left join public.finance_accounts destination_account on destination_account.id = t.destination_account_id
                left join public.finance_categories category on category.id = t.category_id
                left join lateral (
                    select jsonb_agg(
                        jsonb_build_object(
                            'id', item.id,
                            'categoryId', item.category_id,
                            'categoryName', item_category.name,
                            'title', item.title,
                            'amountMinor', item.amount_minor,
                            'displayOrder', item.display_order
                        )
                        order by item.display_order, item.created_at
                    ) as items
                    from public.finance_transaction_items item
                    left join public.finance_categories item_category on item_category.id = item.category_id
                    where item.transaction_id = t.id
                ) item_rows on true
                where t.user_id = v_user_id
                  and t.happened_at >= v_month_start
                  and t.happened_at < v_month_end
            ),
            '[]'::jsonb
        )
    );
end;
$$;
