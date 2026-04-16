do $$
declare
    v_user_id uuid;
    v_home_id uuid;
    v_communication_id uuid;
    v_snacks_id uuid;
    v_drinks_id uuid;
begin
    for v_user_id in
        select distinct user_id
        from public.finance_categories
        where user_id is not null
    loop
        update public.finance_categories
        set
            parent_id = null,
            direction = 'expense',
            name = 'Связь',
            icon = 'smartphone',
            color = '#54A1FF',
            display_order = 65,
            is_archived = false,
            updated_at = now()
        where user_id = v_user_id
          and code = 'expense_communication';

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
            values
                (v_user_id, null, 'expense', 'expense_communication', 'Связь', 'smartphone', '#54A1FF', 65);
        end if;

        select id
        into v_home_id
        from public.finance_categories
        where user_id = v_user_id
          and code = 'expense_home';

        select id
        into v_communication_id
        from public.finance_categories
        where user_id = v_user_id
          and code = 'expense_communication';

        select id
        into v_snacks_id
        from public.finance_categories
        where user_id = v_user_id
          and code = 'expense_food_snacks';

        select id
        into v_drinks_id
        from public.finance_categories
        where user_id = v_user_id
          and code = 'expense_food_drinks';

        if v_home_id is not null then
            update public.finance_categories
            set
                parent_id = v_home_id,
                direction = 'expense',
                name = 'Аренда',
                icon = 'key-round',
                color = null,
                display_order = 26,
                is_archived = false,
                updated_at = now()
            where user_id = v_user_id
              and code = 'expense_home_rent';

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
                values
                    (v_user_id, v_home_id, 'expense', 'expense_home_rent', 'Аренда', 'key-round', null, 26);
            end if;

            update public.finance_categories
            set
                parent_id = v_home_id,
                direction = 'expense',
                name = 'Ипотека',
                icon = 'building',
                color = null,
                display_order = 27,
                is_archived = false,
                updated_at = now()
            where user_id = v_user_id
              and code = 'expense_home_mortgage';

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
                values
                    (v_user_id, v_home_id, 'expense', 'expense_home_mortgage', 'Ипотека', 'building', null, 27);
            end if;
        end if;

        if v_communication_id is not null then
            update public.finance_categories
            set
                parent_id = v_communication_id,
                direction = 'expense',
                name = 'Мобильная связь',
                icon = 'smartphone',
                color = null,
                display_order = 651,
                is_archived = false,
                updated_at = now()
            where user_id = v_user_id
              and code = 'expense_communication_mobile';

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
                values
                    (v_user_id, v_communication_id, 'expense', 'expense_communication_mobile', 'Мобильная связь', 'smartphone', null, 651);
            end if;

            update public.finance_categories
            set
                parent_id = v_communication_id,
                direction = 'expense',
                name = 'Интернет',
                icon = 'wifi',
                color = null,
                display_order = 652,
                is_archived = false,
                updated_at = now()
            where user_id = v_user_id
              and code = 'expense_communication_internet';

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
                values
                    (v_user_id, v_communication_id, 'expense', 'expense_communication_internet', 'Интернет', 'wifi', null, 652);
            end if;

            update public.finance_categories
            set
                parent_id = v_communication_id,
                direction = 'expense',
                name = 'Телевидение',
                icon = 'monitor-play',
                color = null,
                display_order = 653,
                is_archived = false,
                updated_at = now()
            where user_id = v_user_id
              and code = 'expense_communication_tv';

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
                values
                    (v_user_id, v_communication_id, 'expense', 'expense_communication_tv', 'Телевидение', 'monitor-play', null, 653);
            end if;

            update public.finance_categories
            set
                parent_id = v_communication_id,
                direction = 'expense',
                name = 'VPN',
                icon = 'shield',
                color = null,
                display_order = 654,
                is_archived = false,
                updated_at = now()
            where user_id = v_user_id
              and code = 'expense_communication_vpn';

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
                values
                    (v_user_id, v_communication_id, 'expense', 'expense_communication_vpn', 'VPN', 'shield', null, 654);
            end if;
        end if;

        if v_drinks_id is not null then
            update public.finance_categories
            set
                parent_id = v_drinks_id,
                direction = 'expense',
                name = 'Вода',
                icon = 'glass-water',
                color = null,
                display_order = 151,
                is_archived = false,
                updated_at = now()
            where user_id = v_user_id
              and code = 'expense_food_drinks_water';

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
                values
                    (v_user_id, v_drinks_id, 'expense', 'expense_food_drinks_water', 'Вода', 'glass-water', null, 151);
            end if;

            update public.finance_categories
            set
                parent_id = v_drinks_id,
                direction = 'expense',
                name = 'Газированные напитки',
                icon = 'cup-soda',
                color = null,
                display_order = 152,
                is_archived = false,
                updated_at = now()
            where user_id = v_user_id
              and code = 'expense_food_drinks_soda';

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
                values
                    (v_user_id, v_drinks_id, 'expense', 'expense_food_drinks_soda', 'Газированные напитки', 'cup-soda', null, 152);
            end if;

            update public.finance_categories
            set
                parent_id = v_drinks_id,
                direction = 'expense',
                name = 'Соки и морсы',
                icon = 'citrus',
                color = null,
                display_order = 153,
                is_archived = false,
                updated_at = now()
            where user_id = v_user_id
              and code = 'expense_food_drinks_juice';

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
                values
                    (v_user_id, v_drinks_id, 'expense', 'expense_food_drinks_juice', 'Соки и морсы', 'citrus', null, 153);
            end if;

            update public.finance_categories
            set
                parent_id = v_drinks_id,
                direction = 'expense',
                name = 'Кофе и чай',
                icon = 'coffee',
                color = null,
                display_order = 154,
                is_archived = false,
                updated_at = now()
            where user_id = v_user_id
              and code = 'expense_food_drinks_coffee_tea';

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
                values
                    (v_user_id, v_drinks_id, 'expense', 'expense_food_drinks_coffee_tea', 'Кофе и чай', 'coffee', null, 154);
            end if;

            update public.finance_categories
            set
                parent_id = v_drinks_id,
                direction = 'expense',
                name = 'Энергетики',
                icon = 'zap',
                color = null,
                display_order = 155,
                is_archived = false,
                updated_at = now()
            where user_id = v_user_id
              and code = 'expense_food_drinks_energy';

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
                values
                    (v_user_id, v_drinks_id, 'expense', 'expense_food_drinks_energy', 'Энергетики', 'zap', null, 155);
            end if;
        end if;

        if v_snacks_id is not null then
            update public.finance_categories
            set
                parent_id = v_snacks_id,
                direction = 'expense',
                name = 'Чипсы',
                icon = 'package-open',
                color = null,
                display_order = 161,
                is_archived = false,
                updated_at = now()
            where user_id = v_user_id
              and code = 'expense_food_snacks_chips';

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
                values
                    (v_user_id, v_snacks_id, 'expense', 'expense_food_snacks_chips', 'Чипсы', 'package-open', null, 161);
            end if;

            update public.finance_categories
            set
                parent_id = v_snacks_id,
                direction = 'expense',
                name = 'Шоколад и батончики',
                icon = 'candy',
                color = null,
                display_order = 162,
                is_archived = false,
                updated_at = now()
            where user_id = v_user_id
              and code = 'expense_food_snacks_chocolate';

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
                values
                    (v_user_id, v_snacks_id, 'expense', 'expense_food_snacks_chocolate', 'Шоколад и батончики', 'candy', null, 162);
            end if;

            update public.finance_categories
            set
                parent_id = v_snacks_id,
                direction = 'expense',
                name = 'Печенье и вафли',
                icon = 'cookie',
                color = null,
                display_order = 163,
                is_archived = false,
                updated_at = now()
            where user_id = v_user_id
              and code = 'expense_food_snacks_cookies';

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
                values
                    (v_user_id, v_snacks_id, 'expense', 'expense_food_snacks_cookies', 'Печенье и вафли', 'cookie', null, 163);
            end if;

            update public.finance_categories
            set
                parent_id = v_snacks_id,
                direction = 'expense',
                name = 'Орехи и сухофрукты',
                icon = 'nut',
                color = null,
                display_order = 164,
                is_archived = false,
                updated_at = now()
            where user_id = v_user_id
              and code = 'expense_food_snacks_nuts';

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
                values
                    (v_user_id, v_snacks_id, 'expense', 'expense_food_snacks_nuts', 'Орехи и сухофрукты', 'nut', null, 164);
            end if;
        end if;
    end loop;
end $$;
