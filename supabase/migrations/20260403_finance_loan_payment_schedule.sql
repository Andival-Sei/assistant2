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
    v_source_account public.finance_accounts%rowtype;
    v_destination_account public.finance_accounts%rowtype;
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

        select *
        into v_source_account
        from public.finance_accounts
        where id = v_account_id
          and user_id = v_user_id
          and is_archived = false
        for update;

        if v_source_account.id is null then
            raise exception 'Source account not found';
        end if;

        if v_source_account.kind = 'loan' then
            raise exception 'Loan cannot be used as a source account';
        end if;

        if v_currency = '' then
            v_currency := v_source_account.currency;
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

            select *
            into v_destination_account
            from public.finance_accounts
            where id = v_destination_account_id
              and user_id = v_user_id
              and is_archived = false
            for update;

            if v_destination_account.id is null then
                raise exception 'Destination account not found';
            end if;

            if v_destination_account.kind = 'loan' then
                if coalesce(v_destination_account.balance_minor, 0) <= 0 then
                    raise exception 'Loan payment exceeds outstanding debt';
                end if;

                if v_amount_minor > coalesce(v_destination_account.balance_minor, 0) then
                    raise exception 'Loan payment exceeds outstanding debt';
                end if;

                v_transaction_kind := 'loan_payment';
                v_title := coalesce(v_title, 'Платёж по кредиту');

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
                    'expense',
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

                perform public.finance_apply_account_activity(v_account_id, 'expense', v_amount_minor);
                perform public.finance_apply_account_activity(v_destination_account_id, 'income', v_amount_minor);

                update public.finance_accounts
                set loan_remaining_payments_count = case
                        when coalesce(balance_minor, 0) = 0 then 0
                        when loan_remaining_payments_count is null then null
                        else greatest(loan_remaining_payments_count - 1, 0)
                    end,
                    loan_payment_due_date = case
                        when coalesce(balance_minor, 0) = 0 then null
                        when loan_payment_due_date is null then null
                        else (loan_payment_due_date + interval '1 month')::date
                    end,
                    updated_at = now()
                where id = v_destination_account_id;
            else
                v_transaction_kind := 'transfer';
                v_title := coalesce(
                    v_title,
                    case
                        when v_source_account.card_type = 'credit'
                             and coalesce(v_destination_account.card_type, 'debit') <> 'credit'
                            then 'Перевод с кредитки'
                        when coalesce(v_source_account.card_type, 'debit') <> 'credit'
                             and v_destination_account.card_type = 'credit'
                            then 'Погашение кредитки'
                        else 'Перевод'
                    end
                );

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

                perform public.finance_apply_account_activity(v_account_id, 'expense', v_amount_minor);
                perform public.finance_apply_account_activity(v_destination_account_id, 'income', v_amount_minor);
            end if;
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

            perform public.finance_apply_account_activity(
                v_account_id,
                case when v_direction = 'income' then 'income' else 'expense' end,
                v_total_minor
            );
        end if;

        v_created_count := v_created_count + 1;
    end loop;

    return jsonb_build_object(
        'createdCount', v_created_count
    );
end;
$$;

grant execute on function public.finance_create_transactions(jsonb) to authenticated;
