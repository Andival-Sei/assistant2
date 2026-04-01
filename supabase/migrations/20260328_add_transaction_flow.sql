create table if not exists public.finance_transaction_items (
    id uuid primary key default gen_random_uuid(),
    transaction_id uuid not null references public.finance_transactions (id) on delete cascade,
    user_id uuid not null references auth.users (id) on delete cascade,
    category_id uuid null references public.finance_categories (id) on delete set null,
    title text not null,
    amount_minor bigint not null check (amount_minor > 0),
    display_order integer not null default 0,
    created_at timestamptz not null default now()
);

alter table public.finance_transactions
    add column if not exists category_id uuid null references public.finance_categories (id) on delete set null;

alter table public.finance_transactions
    add column if not exists source_type text not null default 'manual'
    check (source_type in ('manual', 'photo', 'file'));

alter table public.finance_transactions
    add column if not exists merchant_name text null;

create index if not exists finance_transaction_items_transaction_idx
    on public.finance_transaction_items (transaction_id, display_order, created_at);

create index if not exists finance_transaction_items_user_category_idx
    on public.finance_transaction_items (user_id, category_id)
    where category_id is not null;

alter table public.finance_transaction_items enable row level security;

drop policy if exists "finance_transaction_items_select_own" on public.finance_transaction_items;
create policy "finance_transaction_items_select_own"
on public.finance_transaction_items
for select
to authenticated
using ((select auth.uid()) = user_id);

drop policy if exists "finance_transaction_items_insert_own" on public.finance_transaction_items;
create policy "finance_transaction_items_insert_own"
on public.finance_transaction_items
for insert
to authenticated
with check ((select auth.uid()) = user_id);

drop policy if exists "finance_transaction_items_update_own" on public.finance_transaction_items;
create policy "finance_transaction_items_update_own"
on public.finance_transaction_items
for update
to authenticated
using ((select auth.uid()) = user_id)
with check ((select auth.uid()) = user_id);

drop policy if exists "finance_transaction_items_delete_own" on public.finance_transaction_items;
create policy "finance_transaction_items_delete_own"
on public.finance_transaction_items
for delete
to authenticated
using ((select auth.uid()) = user_id);

create or replace function public.finance_seed_default_categories()
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user_id uuid := auth.uid();
    v_seed record;
    v_parent_id uuid;
begin
    if v_user_id is null then
        raise exception 'Not authenticated';
    end if;

    for v_seed in
        select *
        from (
            values
                (1, 'expense_food', null, 'expense', 'Еда', 'utensils-crossed', '#2AE88B', 10),
                (1, 'expense_home', null, 'expense', 'Дом', 'house', '#5DA6FF', 20),
                (1, 'expense_transport', null, 'expense', 'Транспорт', 'car-front', '#F6A52C', 30),
                (1, 'expense_health', null, 'expense', 'Здоровье', 'heart-pulse', '#FF6A6A', 40),
                (1, 'expense_personal', null, 'expense', 'Личное', 'sparkles', '#D881FF', 50),
                (1, 'expense_leisure', null, 'expense', 'Досуг', 'gamepad-2', '#B57BFF', 60),
                (1, 'expense_family', null, 'expense', 'Семья и близкие', 'heart-handshake', '#FF8A65', 70),
                (1, 'expense_finance', null, 'expense', 'Финансы и обязательства', 'badge-russian-ruble', '#7E8A9C', 80),
                (1, 'expense_other', null, 'expense', 'Прочее', 'folder', '#8D96A8', 90),
                (1, 'income_salary', null, 'income', 'Зарплата', 'badge-russian-ruble', '#2AE88B', 10),
                (1, 'income_bonus', null, 'income', 'Премии и бонусы', 'sparkles', '#47DB8E', 20),
                (1, 'income_freelance', null, 'income', 'Подработка и фриланс', 'briefcase-business', '#63D69A', 30),
                (1, 'income_refunds', null, 'income', 'Возвраты', 'rotate-ccw', '#5DA6FF', 40),
                (1, 'income_gifts', null, 'income', 'Подарки', 'gift', '#F6A52C', 50),
                (1, 'income_investments', null, 'income', 'Инвестиции', 'chart-column-big', '#7FCE7A', 60),
                (1, 'income_sales', null, 'income', 'Продажа вещей', 'package-open', '#A6C75E', 70),
                (1, 'income_other', null, 'income', 'Прочие поступления', 'plus-circle', '#8D96A8', 80),
                (2, 'expense_uncategorized', 'expense_other', 'expense', 'Без категории', 'circle-help', null, 91),
                (2, 'income_uncategorized', 'income_other', 'income', 'Без категории', 'circle-help', null, 81),

                (2, 'expense_food_ready', 'expense_food', 'expense', 'Готовая еда', 'sandwich', null, 11),
                (2, 'expense_food_groceries', 'expense_food', 'expense', 'Продукты', 'shopping-basket', null, 12),
                (2, 'expense_food_cafe', 'expense_food', 'expense', 'Кафе и рестораны', 'utensils', null, 13),
                (2, 'expense_food_delivery', 'expense_food', 'expense', 'Доставка еды', 'bike', null, 14),
                (2, 'expense_food_drinks', 'expense_food', 'expense', 'Напитки', 'cup-soda', null, 15),
                (2, 'expense_food_snacks', 'expense_food', 'expense', 'Снеки и сладкое', 'cookie', null, 16),

                (2, 'expense_home_utilities', 'expense_home', 'expense', 'Коммунальные услуги', 'lamp-desk', null, 21),
                (2, 'expense_home_supplies', 'expense_home', 'expense', 'Хозтовары', 'package-open', null, 22),
                (2, 'expense_home_cleaning', 'expense_home', 'expense', 'Уборка и стирка', 'spray-can', null, 23),
                (2, 'expense_home_furniture', 'expense_home', 'expense', 'Мебель и интерьер', 'armchair', null, 24),
                (2, 'expense_home_appliances', 'expense_home', 'expense', 'Техника для дома', 'microwave', null, 25),

                (2, 'expense_transport_fuel', 'expense_transport', 'expense', 'Топливо', 'fuel', null, 31),
                (2, 'expense_transport_public', 'expense_transport', 'expense', 'Общественный транспорт', 'tram-front', null, 32),
                (2, 'expense_transport_taxi', 'expense_transport', 'expense', 'Такси', 'car-taxi-front', null, 33),
                (2, 'expense_transport_parking', 'expense_transport', 'expense', 'Парковка и платные дороги', 'circle-parking', null, 34),
                (2, 'expense_transport_service', 'expense_transport', 'expense', 'Обслуживание авто', 'wrench', null, 35),

                (2, 'expense_health_pharmacy', 'expense_health', 'expense', 'Аптека', 'pill', null, 41),
                (2, 'expense_health_doctor', 'expense_health', 'expense', 'Врачи', 'stethoscope', null, 42),
                (2, 'expense_health_dentist', 'expense_health', 'expense', 'Стоматология', 'scan-tooth', null, 43),
                (2, 'expense_health_diagnostics', 'expense_health', 'expense', 'Анализы и диагностика', 'activity', null, 44),

                (2, 'expense_personal_beauty', 'expense_personal', 'expense', 'Красота и уход', 'sparkles', null, 51),
                (2, 'expense_personal_clothes', 'expense_personal', 'expense', 'Одежда', 'shirt', null, 52),
                (2, 'expense_personal_shoes', 'expense_personal', 'expense', 'Обувь', 'footprints', null, 53),
                (2, 'expense_personal_accessories', 'expense_personal', 'expense', 'Аксессуары', 'watch', null, 54),
                (2, 'expense_personal_sport', 'expense_personal', 'expense', 'Спорт и фитнес', 'dumbbell', null, 55),

                (2, 'expense_leisure_subscriptions', 'expense_leisure', 'expense', 'Подписки', 'sparkles', null, 61),
                (2, 'expense_leisure_books', 'expense_leisure', 'expense', 'Книги и обучение', 'book-open', null, 62),
                (2, 'expense_leisure_cinema', 'expense_leisure', 'expense', 'Кино и события', 'film', null, 63),
                (2, 'expense_leisure_games', 'expense_leisure', 'expense', 'Игры и хобби', 'gamepad-2', null, 64),
                (2, 'expense_leisure_travel', 'expense_leisure', 'expense', 'Путешествия', 'plane', null, 65),

                (2, 'expense_family_kids', 'expense_family', 'expense', 'Дети', 'baby', null, 71),
                (2, 'expense_family_pets', 'expense_family', 'expense', 'Животные', 'paw-print', null, 72),
                (2, 'expense_family_gifts', 'expense_family', 'expense', 'Подарки и помощь', 'gift', null, 73),

                (2, 'expense_finance_credit', 'expense_finance', 'expense', 'Кредиты и долги', 'landmark', null, 81),
                (2, 'expense_finance_taxes', 'expense_finance', 'expense', 'Налоги', 'receipt-text', null, 82),
                (2, 'expense_finance_fees', 'expense_finance', 'expense', 'Комиссии', 'percent-circle', null, 83),
                (2, 'expense_finance_savings', 'expense_finance', 'expense', 'Сбережения и инвестиции', 'piggy-bank', null, 84),

                (2, 'income_investments_dividends', 'income_investments', 'income', 'Дивиденды и купоны', 'wallet-cards', null, 61),
                (2, 'income_investments_interest', 'income_investments', 'income', 'Проценты по вкладам', 'badge-percent', null, 62),
                (2, 'income_investments_sales', 'income_investments', 'income', 'Продажа активов', 'arrow-up-right', null, 63),

                (3, 'expense_food_groceries_meat', 'expense_food_groceries', 'expense', 'Мясо', 'beef', null, 121),
                (3, 'expense_food_groceries_fish', 'expense_food_groceries', 'expense', 'Рыба и морепродукты', 'fish', null, 122),
                (3, 'expense_food_groceries_dairy', 'expense_food_groceries', 'expense', 'Молочные продукты', 'milk', null, 123),
                (3, 'expense_food_groceries_bread', 'expense_food_groceries', 'expense', 'Хлеб и выпечка', 'croissant', null, 124),
                (3, 'expense_food_groceries_cereals', 'expense_food_groceries', 'expense', 'Крупы и макароны', 'wheat', null, 125),
                (3, 'expense_food_groceries_vegetables', 'expense_food_groceries', 'expense', 'Овощи', 'carrot', null, 126),
                (3, 'expense_food_groceries_fruits', 'expense_food_groceries', 'expense', 'Фрукты и ягоды', 'apple', null, 127),
                (3, 'expense_food_groceries_frozen', 'expense_food_groceries', 'expense', 'Заморозка и полуфабрикаты', 'snowflake', null, 128),
                (3, 'expense_food_groceries_household_misc', 'expense_food_groceries', 'expense', 'Сопутствующие товары', 'package', null, 129),

                (3, 'expense_home_supplies_dishes', 'expense_home_supplies', 'expense', 'Для посуды', 'glass-water', null, 221),
                (3, 'expense_home_supplies_bathroom', 'expense_home_supplies', 'expense', 'Для ванной и туалета', 'bath', null, 222),
                (3, 'expense_home_supplies_kitchen', 'expense_home_supplies', 'expense', 'Для кухни', 'chef-hat', null, 223),
                (3, 'expense_home_supplies_consumables', 'expense_home_supplies', 'expense', 'Расходники для дома', 'package-2', null, 224),

                (3, 'expense_health_doctor_clinic', 'expense_health_doctor', 'expense', 'Клиника и приём', 'hospital', null, 421),
                (3, 'expense_health_doctor_online', 'expense_health_doctor', 'expense', 'Онлайн-консультации', 'monitor-smartphone', null, 422),

                (3, 'expense_family_kids_food', 'expense_family_kids', 'expense', 'Детское питание', 'baby', null, 711),
                (3, 'expense_family_kids_clothes', 'expense_family_kids', 'expense', 'Детская одежда', 'shirt', null, 712),
                (3, 'expense_family_kids_education', 'expense_family_kids', 'expense', 'Кружки и обучение', 'graduation-cap', null, 713),
                (3, 'expense_family_pets_food', 'expense_family_pets', 'expense', 'Корм', 'paw-print', null, 721),
                (3, 'expense_family_pets_vet', 'expense_family_pets', 'expense', 'Ветклиника', 'stethoscope', null, 722),

                (4, 'expense_food_groceries_meat_poultry', 'expense_food_groceries_meat', 'expense', 'Птица', 'drumstick', null, 1211),
                (4, 'expense_food_groceries_meat_beef', 'expense_food_groceries_meat', 'expense', 'Говядина', 'beef', null, 1212),
                (4, 'expense_food_groceries_meat_pork', 'expense_food_groceries_meat', 'expense', 'Свинина', 'beef', null, 1213),
                (4, 'expense_food_groceries_dairy_milk', 'expense_food_groceries_dairy', 'expense', 'Молоко', 'milk', null, 1231),
                (4, 'expense_food_groceries_dairy_cheese', 'expense_food_groceries_dairy', 'expense', 'Сыр', 'sandwich', null, 1232),
                (4, 'expense_food_groceries_dairy_yogurt', 'expense_food_groceries_dairy', 'expense', 'Йогурты и десерты', 'ice-cream-bowl', null, 1233),

                (5, 'expense_food_groceries_meat_poultry_chicken', 'expense_food_groceries_meat_poultry', 'expense', 'Курица', 'drumstick', null, 12111),
                (5, 'expense_food_groceries_meat_poultry_turkey', 'expense_food_groceries_meat_poultry', 'expense', 'Индейка', 'bird', null, 12112),
                (5, 'expense_food_groceries_meat_poultry_other', 'expense_food_groceries_meat_poultry', 'expense', 'Другая птица', 'egg', null, 12113)
        ) as seeds(depth, code, parent_code, direction, name, icon, color, display_order)
        order by depth, display_order, code
    loop
        v_parent_id := null;

        if v_seed.parent_code is not null then
            select id
            into v_parent_id
            from public.finance_categories
            where user_id = v_user_id
              and code = v_seed.parent_code
              and is_archived = false
            limit 1;
        end if;

        update public.finance_categories
        set parent_id = v_parent_id,
            direction = v_seed.direction,
            name = v_seed.name,
            icon = v_seed.icon,
            color = case
                when v_seed.depth = 1 then v_seed.color
                else null
            end,
            display_order = v_seed.display_order,
            is_archived = false,
            updated_at = now()
        where user_id = v_user_id
          and code = v_seed.code;

        if not found then
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
            values (
                v_user_id,
                v_parent_id,
                v_seed.direction,
                v_seed.code,
                v_seed.name,
                v_seed.icon,
                case
                    when v_seed.depth = 1 then v_seed.color
                    else null
                end,
                v_seed.display_order
            );
        end if;
    end loop;
end;
$$;

create or replace function public.finance_get_transaction_items_json(p_transaction_id uuid)
returns jsonb
language sql
stable
set search_path = public
as $$
    select coalesce(
        (
            select jsonb_agg(
                jsonb_build_object(
                    'id', item.id,
                    'title', item.title,
                    'amountMinor', item.amount_minor,
                    'categoryId', item.category_id,
                    'categoryName', category.name,
                    'categoryCode', category.code,
                    'displayOrder', item.display_order
                )
                order by item.display_order, item.created_at
            )
            from public.finance_transaction_items item
            left join public.finance_categories category on category.id = item.category_id
            where item.transaction_id = p_transaction_id
        ),
        '[]'::jsonb
    );
$$;

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
                        'merchantName', t.merchant_name,
                        'note', t.note,
                        'amountMinor', t.amount_minor,
                        'currency', t.currency,
                        'happenedAt', t.happened_at,
                        'destinationAccountId', t.destination_account_id,
                        'destinationAccountName', destination_account.name,
                        'categoryId', t.category_id,
                        'categoryName', category.name,
                        'itemCount', t.item_count,
                        'sourceType', t.source_type,
                        'items', public.finance_get_transaction_items_json(t.id)
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
                        'merchantName', t.merchant_name,
                        'note', t.note,
                        'amountMinor', t.amount_minor,
                        'currency', t.currency,
                        'happenedAt', t.happened_at,
                        'destinationAccountId', t.destination_account_id,
                        'destinationAccountName', destination_account.name,
                        'categoryId', t.category_id,
                        'categoryName', category.name,
                        'itemCount', t.item_count,
                        'sourceType', t.source_type,
                        'items', public.finance_get_transaction_items_json(t.id)
                    )
                    order by t.happened_at desc
                )
                from public.finance_transactions t
                join public.finance_accounts source_account on source_account.id = t.account_id
                left join public.finance_accounts destination_account on destination_account.id = t.destination_account_id
                left join public.finance_categories category on category.id = t.category_id
                where t.user_id = v_user_id
                  and t.happened_at >= v_month_start
                  and t.happened_at < v_month_end
            ),
            '[]'::jsonb
        )
    );
end;
$$;

create or replace function public.finance_create_transactions(p_transactions jsonb)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user_id uuid := auth.uid();
    v_transaction jsonb;
    v_item jsonb;
    v_account_id uuid;
    v_destination_account_id uuid;
    v_category_id uuid;
    v_item_category_id uuid;
    v_transaction_id uuid;
    v_direction text;
    v_transaction_kind text;
    v_currency text;
    v_source_type text;
    v_title text;
    v_merchant_name text;
    v_note text;
    v_happened_at timestamptz;
    v_amount_minor bigint;
    v_total_minor bigint;
    v_item_title text;
    v_item_count integer;
    v_index integer;
    v_created_count integer := 0;
    v_uncategorized_expense_id uuid;
    v_uncategorized_income_id uuid;
begin
    if v_user_id is null then
        raise exception 'Not authenticated';
    end if;

    if jsonb_typeof(p_transactions) <> 'array' or jsonb_array_length(p_transactions) = 0 then
        raise exception 'Transactions payload must be a non-empty array';
    end if;

    for v_transaction in
        select value
        from jsonb_array_elements(p_transactions)
    loop
        v_direction := lower(trim(coalesce(v_transaction->>'direction', '')));
        v_source_type := lower(trim(coalesce(v_transaction->>'sourceType', 'manual')));
        v_account_id := nullif(v_transaction->>'accountId', '')::uuid;
        v_destination_account_id := nullif(v_transaction->>'destinationAccountId', '')::uuid;
        v_currency := upper(trim(coalesce(v_transaction->>'currency', '')));
        v_note := nullif(trim(coalesce(v_transaction->>'note', '')), '');
        v_merchant_name := nullif(trim(coalesce(v_transaction->>'merchantName', '')), '');
        v_title := nullif(trim(coalesce(v_transaction->>'title', '')), '');
        v_happened_at := coalesce(nullif(v_transaction->>'happenedAt', '')::timestamptz, now());
        v_total_minor := 0;
        v_item_count := 0;
        v_category_id := nullif(v_transaction->>'categoryId', '')::uuid;

        if v_direction not in ('income', 'expense', 'transfer') then
            raise exception 'Unsupported direction';
        end if;

        if v_source_type not in ('manual', 'photo', 'file') then
            raise exception 'Unsupported source type';
        end if;

        if v_account_id is null then
            raise exception 'Account is required';
        end if;

        if not exists (
            select 1
            from public.finance_accounts
            where id = v_account_id
              and user_id = v_user_id
              and is_archived = false
        ) then
            raise exception 'Source account not found';
        end if;

        if v_currency = '' then
            select currency
            into v_currency
            from public.finance_accounts
            where id = v_account_id;
        end if;

        if v_currency not in ('RUB', 'USD', 'EUR') then
            raise exception 'Unsupported currency';
        end if;

        if v_category_id is not null and not exists (
            select 1
            from public.finance_categories
            where id = v_category_id
              and user_id = v_user_id
              and is_archived = false
        ) then
            raise exception 'Category not found';
        end if;

        if v_direction = 'transfer' then
            v_amount_minor := coalesce((v_transaction->>'amountMinor')::bigint, 0);

            if v_amount_minor <= 0 then
                raise exception 'Transfer amount must be positive';
            end if;

            if v_destination_account_id is null then
                raise exception 'Destination account is required for transfer';
            end if;

            if v_destination_account_id = v_account_id then
                raise exception 'Destination account must be different';
            end if;

            if not exists (
                select 1
                from public.finance_accounts
                where id = v_destination_account_id
                  and user_id = v_user_id
                  and is_archived = false
            ) then
                raise exception 'Destination account not found';
            end if;

            v_transaction_kind := 'transfer';
            v_title := coalesce(v_title, 'Перевод');

            insert into public.finance_transactions (
                user_id,
                account_id,
                destination_account_id,
                direction,
                transaction_kind,
                title,
                merchant_name,
                note,
                amount_minor,
                currency,
                happened_at,
                item_count,
                source_type
            )
            values (
                v_user_id,
                v_account_id,
                v_destination_account_id,
                v_direction,
                v_transaction_kind,
                v_title,
                v_merchant_name,
                v_note,
                v_amount_minor,
                v_currency,
                v_happened_at,
                0,
                v_source_type
            );

            update public.finance_accounts
            set balance_minor = balance_minor - v_amount_minor,
                updated_at = now()
            where id = v_account_id;

            update public.finance_accounts
            set balance_minor = balance_minor + v_amount_minor,
                updated_at = now()
            where id = v_destination_account_id;
        else
            select id
            into v_uncategorized_expense_id
            from public.finance_categories
            where user_id = v_user_id
              and code = 'expense_uncategorized'
              and is_archived = false
            limit 1;

            select id
            into v_uncategorized_income_id
            from public.finance_categories
            where user_id = v_user_id
              and code = 'income_uncategorized'
              and is_archived = false
            limit 1;

            if coalesce(jsonb_typeof(v_transaction->'items'), 'null') <> 'array'
                or jsonb_array_length(coalesce(v_transaction->'items', '[]'::jsonb)) = 0 then
                v_transaction := jsonb_set(
                    v_transaction,
                    '{items}',
                    jsonb_build_array(
                        jsonb_build_object(
                            'title', coalesce(v_title, v_merchant_name, case when v_direction = 'income' then 'Поступление' else 'Трата' end),
                            'amountMinor', coalesce((v_transaction->>'amountMinor')::bigint, 0),
                            'categoryId', v_transaction->>'categoryId'
                        )
                    )
                );
            end if;

            for v_item in
                select value
                from jsonb_array_elements(v_transaction->'items')
            loop
                v_amount_minor := coalesce((v_item->>'amountMinor')::bigint, 0);
                v_item_category_id := coalesce(
                    nullif(v_item->>'categoryId', '')::uuid,
                    v_category_id,
                    case
                        when v_direction = 'income' then v_uncategorized_income_id
                        else v_uncategorized_expense_id
                    end
                );

                if v_amount_minor <= 0 then
                    raise exception 'Transaction item amount must be positive';
                end if;

                if v_item_category_id is not null and not exists (
                    select 1
                    from public.finance_categories
                    where id = v_item_category_id
                      and user_id = v_user_id
                      and is_archived = false
                ) then
                    raise exception 'Transaction item category not found';
                end if;

                v_total_minor := v_total_minor + v_amount_minor;
                v_item_count := v_item_count + 1;
            end loop;

            if v_total_minor <= 0 then
                raise exception 'Transaction amount must be positive';
            end if;

            v_transaction_kind := case
                when v_item_count > 1 then 'split'
                else 'single'
            end;

            if v_item_count = 1 then
                v_category_id := coalesce(
                    nullif((v_transaction->'items'->0->>'categoryId'), '')::uuid,
                    case
                        when v_direction = 'income' then v_uncategorized_income_id
                        else v_uncategorized_expense_id
                    end
                );
                v_title := coalesce(
                    v_title,
                    nullif(trim(coalesce(v_transaction->'items'->0->>'title', '')), ''),
                    v_merchant_name,
                    case when v_direction = 'income' then 'Поступление' else 'Трата' end
                );
            else
                v_category_id := null;
                v_title := coalesce(v_title, v_merchant_name, case when v_direction = 'income' then 'Поступление' else 'Чек' end);
            end if;

            insert into public.finance_transactions (
                user_id,
                account_id,
                direction,
                transaction_kind,
                title,
                merchant_name,
                note,
                amount_minor,
                currency,
                happened_at,
                category_id,
                item_count,
                source_type
            )
            values (
                v_user_id,
                v_account_id,
                v_direction,
                v_transaction_kind,
                v_title,
                v_merchant_name,
                v_note,
                v_total_minor,
                v_currency,
                v_happened_at,
                v_category_id,
                v_item_count,
                v_source_type
            )
            returning id into v_transaction_id;

            v_index := 0;
            for v_item in
                select value
                from jsonb_array_elements(v_transaction->'items')
            loop
                v_amount_minor := coalesce((v_item->>'amountMinor')::bigint, 0);
                v_item_title := nullif(trim(coalesce(v_item->>'title', '')), '');
                v_item_category_id := coalesce(
                    nullif(v_item->>'categoryId', '')::uuid,
                    v_category_id,
                    case
                        when v_direction = 'income' then v_uncategorized_income_id
                        else v_uncategorized_expense_id
                    end
                );

                insert into public.finance_transaction_items (
                    transaction_id,
                    user_id,
                    category_id,
                    title,
                    amount_minor,
                    display_order
                )
                values (
                    v_transaction_id,
                    v_user_id,
                    v_item_category_id,
                    coalesce(v_item_title, case when v_direction = 'income' then 'Поступление' else 'Позиция' end),
                    v_amount_minor,
                    v_index
                );

                v_index := v_index + 1;
            end loop;

            update public.finance_accounts
            set balance_minor = case
                    when v_direction = 'income' then balance_minor + v_total_minor
                    else balance_minor - v_total_minor
                end,
                updated_at = now()
            where id = v_account_id;
        end if;

        v_created_count := v_created_count + 1;
    end loop;

    return jsonb_build_object(
        'createdCount', v_created_count
    );
end;
$$;

grant execute on function public.finance_get_transaction_items_json(uuid) to authenticated;
grant execute on function public.finance_create_transactions(jsonb) to authenticated;
