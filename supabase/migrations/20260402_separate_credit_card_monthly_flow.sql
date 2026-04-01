update public.finance_user_settings
set overview_cards = overview_cards || '["credit_spend"]'::jsonb
where not coalesce(overview_cards, '[]'::jsonb) @> '["credit_spend"]'::jsonb;

alter table public.finance_user_settings
    alter column overview_cards set default
    '["total_balance","card_balance","cash_balance","credit_debt","credit_spend","month_income","month_expense","month_result","recent_transactions"]'::jsonb;

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
            '["total_balance","card_balance","cash_balance","credit_debt","credit_spend","month_income","month_expense","month_result","recent_transactions"]'::jsonb
        ),
        'totalBalanceMinor',
        coalesce(
            (
                select sum(balance_minor)
                from public.finance_accounts
                where user_id = v_user_id
                  and is_archived = false
                  and (
                      kind = 'cash'
                      or (kind = 'bank_card' and coalesce(card_type, 'debit') <> 'credit')
                  )
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
                  and coalesce(card_type, 'debit') <> 'credit'
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
        'creditDebtMinor',
        coalesce(
            (
                select sum(coalesce(credit_debt_minor, 0))
                from public.finance_accounts
                where user_id = v_user_id
                  and kind = 'bank_card'
                  and card_type = 'credit'
                  and is_archived = false
            ),
            0
        ),
        'creditAvailableMinor',
        coalesce(
            (
                select sum(public.finance_credit_available(credit_limit_minor, credit_debt_minor))
                from public.finance_accounts
                where user_id = v_user_id
                  and kind = 'bank_card'
                  and card_type = 'credit'
                  and is_archived = false
            ),
            0
        ),
        'creditLimitMinor',
        coalesce(
            (
                select sum(coalesce(credit_limit_minor, 0))
                from public.finance_accounts
                where user_id = v_user_id
                  and kind = 'bank_card'
                  and card_type = 'credit'
                  and is_archived = false
            ),
            0
        ),
        'creditSpendMinor',
        coalesce(
            (
                select sum(t.amount_minor)
                from public.finance_transactions t
                join public.finance_accounts a on a.id = t.account_id
                where t.user_id = v_user_id
                  and t.direction = 'expense'
                  and t.happened_at >= v_month_start
                  and t.happened_at < v_month_end
                  and a.kind = 'bank_card'
                  and a.card_type = 'credit'
            ),
            0
        ),
        'monthIncomeMinor',
        coalesce(
            (
                select sum(t.amount_minor)
                from public.finance_transactions t
                join public.finance_accounts a on a.id = t.account_id
                where t.user_id = v_user_id
                  and t.direction = 'income'
                  and t.happened_at >= v_month_start
                  and t.happened_at < v_month_end
                  and (
                      a.kind = 'cash'
                      or (a.kind = 'bank_card' and coalesce(a.card_type, 'debit') <> 'credit')
                  )
            ),
            0
        ),
        'monthExpenseMinor',
        coalesce(
            (
                select sum(t.amount_minor)
                from public.finance_transactions t
                join public.finance_accounts a on a.id = t.account_id
                where t.user_id = v_user_id
                  and t.direction = 'expense'
                  and t.happened_at >= v_month_start
                  and t.happened_at < v_month_end
                  and (
                      a.kind = 'cash'
                      or (a.kind = 'bank_card' and coalesce(a.card_type, 'debit') <> 'credit')
                  )
            ),
            0
        ),
        'monthNetMinor',
        coalesce(
            (
                select sum(
                    case
                        when t.direction = 'income' then t.amount_minor
                        when t.direction = 'expense' then -t.amount_minor
                        else 0
                    end
                )
                from public.finance_transactions t
                join public.finance_accounts a on a.id = t.account_id
                where t.user_id = v_user_id
                  and t.happened_at >= v_month_start
                  and t.happened_at < v_month_end
                  and (
                      a.kind = 'cash'
                      or (a.kind = 'bank_card' and coalesce(a.card_type, 'debit') <> 'credit')
                  )
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
                        'cardType', a.card_type,
                        'lastFourDigits', a.last_four_digits,
                        'currency', a.currency,
                        'balanceMinor', a.balance_minor,
                        'creditLimitMinor', a.credit_limit_minor,
                        'creditDebtMinor', a.credit_debt_minor,
                        'creditAvailableMinor', case
                            when a.kind = 'bank_card' and a.card_type = 'credit'
                                then public.finance_credit_available(a.credit_limit_minor, a.credit_debt_minor)
                            else null
                        end,
                        'creditRequiredPaymentMinor', a.credit_required_payment_minor,
                        'creditPaymentDueDate', a.credit_payment_due_date,
                        'creditGracePeriodEndDate', a.credit_grace_period_end_date,
                        'isPrimary', a.is_primary,
                        'transactionCount', coalesce(tx.transaction_count, 0),
                        'balanceEditable', case
                            when a.kind = 'bank_card' and a.card_type = 'credit' then true
                            else coalesce(tx.transaction_count, 0) = 0
                        end
                    )
                    order by a.kind = 'cash', a.display_order, a.created_at desc
                )
                from public.finance_accounts a
                left join (
                    select
                        account_ref.account_id,
                        count(*)::int as transaction_count
                    from (
                        select account_id
                        from public.finance_transactions
                        where user_id = v_user_id

                        union all

                        select destination_account_id as account_id
                        from public.finance_transactions
                        where user_id = v_user_id
                          and destination_account_id is not null
                    ) account_ref
                    group by account_ref.account_id
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
                        'sourceType', t.source_type,
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
