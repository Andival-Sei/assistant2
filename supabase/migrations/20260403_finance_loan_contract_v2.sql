alter table public.finance_accounts
    add column if not exists loan_total_payable_minor bigint null,
    add column if not exists loan_total_payments_count integer null,
    add column if not exists loan_final_payment_minor bigint null;

update public.finance_accounts
set balance_minor = 31768588,
    loan_payment_amount_minor = coalesce(loan_payment_amount_minor, 1189000),
    loan_total_payable_minor = 97990954,
    loan_total_payments_count = 83,
    loan_final_payment_minor = 492954,
    loan_remaining_payments_count = 42,
    loan_payment_due_date = coalesce(loan_payment_due_date, date '2026-05-15'),
    updated_at = now()
where id = '997c2ef6-709f-4f01-9105-f63814b85baa'::uuid
  and kind = 'loan';

alter table public.finance_accounts
    drop constraint if exists finance_accounts_loan_contract_v2_check;

alter table public.finance_accounts
    add constraint finance_accounts_loan_contract_v2_check
    check (
        (
            kind = 'loan'
            and loan_principal_minor is not null
            and loan_interest_percent is not null
            and loan_payment_amount_minor is not null
            and loan_payment_due_date is not null
            and loan_remaining_payments_count is not null
            and loan_total_payable_minor is not null
            and loan_total_payments_count is not null
            and loan_final_payment_minor is not null
        )
        or (
            kind <> 'loan'
            and loan_total_payable_minor is null
            and loan_total_payments_count is null
            and loan_final_payment_minor is null
        )
    );

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
            '["total_balance","card_balance","cash_balance","credit_debt","loan_debt","credit_spend","month_income","month_expense","month_result","recent_transactions"]'::jsonb
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
        'loanDebtMinor',
        coalesce(
            (
                select sum(balance_minor)
                from public.finance_accounts
                where user_id = v_user_id
                  and kind = 'loan'
                  and is_archived = false
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
                      or a.kind = 'loan'
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
                      or a.kind = 'loan'
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
                        'loanPrincipalMinor', a.loan_principal_minor,
                        'loanCurrentDebtMinor', case when a.kind = 'loan' then a.balance_minor else null end,
                        'loanInterestPercent', a.loan_interest_percent,
                        'loanPaymentAmountMinor', a.loan_payment_amount_minor,
                        'loanPaymentDueDate', a.loan_payment_due_date,
                        'loanRemainingPaymentsCount', a.loan_remaining_payments_count,
                        'loanTotalPayableMinor', a.loan_total_payable_minor,
                        'loanTotalPaymentsCount', a.loan_total_payments_count,
                        'loanFinalPaymentMinor', a.loan_final_payment_minor,
                        'isPrimary', a.is_primary,
                        'transactionCount', coalesce(tx.transaction_count, 0),
                        'balanceEditable', case
                            when a.kind = 'bank_card' and a.card_type = 'credit' then true
                            when a.kind = 'loan' then true
                            else coalesce(tx.transaction_count, 0) = 0
                        end
                    )
                    order by
                        case
                            when a.kind = 'bank_card' then 0
                            when a.kind = 'loan' then 1
                            else 2
                        end,
                        a.display_order,
                        a.created_at desc
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
                    limit 12
                ) t
                left join public.finance_accounts source_account on source_account.id = t.account_id
                left join public.finance_accounts destination_account on destination_account.id = t.destination_account_id
                left join public.finance_categories category on category.id = t.category_id
                left join lateral (
                    select jsonb_agg(
                        jsonb_build_object(
                            'id', item.id,
                            'title', item.title,
                            'amountMinor', item.amount_minor,
                            'categoryId', item.category_id,
                            'categoryName', item_category.name,
                            'categoryCode', item_category.code,
                            'displayOrder', item.display_order
                        )
                        order by item.display_order
                    ) as items
                    from public.finance_transaction_items item
                    left join public.finance_categories item_category on item_category.id = item.category_id
                    where item.transaction_id = t.id
                ) item_rows on true
            ),
            '[]'::jsonb
        ),
        'categoriesCount',
        (
            select count(*)::int
            from public.finance_categories
            where user_id = v_user_id
              and is_archived = false
        )
    );
end;
$$;

create or replace function public.finance_upsert_account(
    p_id uuid default null,
    p_kind text default null,
    p_provider_code text default null,
    p_name text default null,
    p_card_type text default null,
    p_last_four_digits text default null,
    p_balance_minor bigint default 0,
    p_currency text default null,
    p_make_primary boolean default false,
    p_credit_limit_minor bigint default null,
    p_credit_debt_minor bigint default null,
    p_credit_required_payment_minor bigint default null,
    p_credit_payment_due_date date default null,
    p_credit_grace_period_end_date date default null,
    p_loan_principal_minor bigint default null,
    p_loan_current_debt_minor bigint default null,
    p_loan_interest_percent numeric default null,
    p_loan_payment_amount_minor bigint default null,
    p_loan_payment_due_date date default null,
    p_loan_remaining_payments_count integer default null,
    p_loan_total_payable_minor bigint default null,
    p_loan_total_payments_count integer default null,
    p_loan_final_payment_minor bigint default null
)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user_id uuid := auth.uid();
    v_provider_code text := public.finance_provider_code_from_input(p_provider_code);
    v_kind text;
    v_currency text;
    v_card_type text;
    v_last_four_digits text;
    v_name text;
    v_existing public.finance_accounts%rowtype;
    v_transaction_count integer := 0;
    v_next_order integer;
    v_credit_limit_minor bigint;
    v_credit_debt_minor bigint;
    v_credit_required_payment_minor bigint;
    v_effective_balance_minor bigint;
    v_loan_principal_minor bigint;
    v_loan_current_debt_minor bigint;
    v_loan_payment_amount_minor bigint;
    v_loan_remaining_payments_count integer;
    v_loan_interest_percent numeric;
    v_loan_total_payable_minor bigint;
    v_loan_total_payments_count integer;
    v_loan_final_payment_minor bigint;
begin
    if v_user_id is null then
        raise exception 'Not authenticated';
    end if;

    v_kind := lower(nullif(trim(coalesce(p_kind, '')), ''));
    if v_kind is null then
        v_kind := public.finance_provider_kind(v_provider_code);
    end if;

    if v_kind not in ('cash', 'bank_card', 'loan') then
        raise exception 'Unsupported account kind';
    end if;

    if v_kind = 'cash' then
        v_provider_code := 'cash';
    elsif v_provider_code is null then
        raise exception 'Provider is required';
    end if;

    select coalesce(
        p_currency,
        (
            select default_currency
            from public.finance_user_settings
            where user_id = v_user_id
        ),
        'RUB'
    )
    into v_currency;

    if v_currency not in ('RUB', 'USD', 'EUR') then
        raise exception 'Unsupported currency';
    end if;

    v_card_type := case
        when v_kind <> 'bank_card' then null
        else lower(nullif(trim(coalesce(p_card_type, '')), ''))
    end;

    if v_kind = 'bank_card' and v_card_type not in ('debit', 'credit') then
        raise exception 'Card type must be debit or credit';
    end if;

    v_last_four_digits := case
        when v_kind <> 'bank_card' then null
        else nullif(regexp_replace(coalesce(p_last_four_digits, ''), '\D', '', 'g'), '')
    end;

    if v_kind = 'bank_card' and (v_last_four_digits is null or length(v_last_four_digits) <> 4) then
        raise exception 'Last four digits are required';
    end if;

    if v_kind = 'bank_card' and v_card_type = 'credit' then
        v_credit_limit_minor := coalesce(p_credit_limit_minor, nullif(p_balance_minor, 0), 0);
        v_credit_debt_minor := coalesce(p_credit_debt_minor, 0);
        v_credit_required_payment_minor := nullif(coalesce(p_credit_required_payment_minor, 0), 0);

        if v_credit_limit_minor <= 0 then
            raise exception 'Credit limit must be positive';
        end if;

        if v_credit_debt_minor < 0 then
            raise exception 'Credit debt must be zero or positive';
        end if;

        v_effective_balance_minor := public.finance_credit_available(v_credit_limit_minor, v_credit_debt_minor);
        v_loan_principal_minor := null;
        v_loan_current_debt_minor := null;
        v_loan_interest_percent := null;
        v_loan_payment_amount_minor := null;
        v_loan_remaining_payments_count := null;
        v_loan_total_payable_minor := null;
        v_loan_total_payments_count := null;
        v_loan_final_payment_minor := null;
    elsif v_kind = 'loan' then
        v_loan_principal_minor := p_loan_principal_minor;
        v_loan_current_debt_minor := p_loan_current_debt_minor;
        v_loan_interest_percent := p_loan_interest_percent;
        v_loan_payment_amount_minor := p_loan_payment_amount_minor;
        v_loan_remaining_payments_count := p_loan_remaining_payments_count;
        v_loan_total_payable_minor := p_loan_total_payable_minor;
        v_loan_total_payments_count := p_loan_total_payments_count;
        v_loan_final_payment_minor := p_loan_final_payment_minor;

        if nullif(trim(coalesce(p_name, '')), '') is null then
            raise exception 'Loan name is required';
        end if;

        if v_loan_principal_minor is null or v_loan_principal_minor <= 0 then
            raise exception 'Loan principal must be positive';
        end if;

        if v_loan_current_debt_minor is null or v_loan_current_debt_minor < 0 then
            raise exception 'Loan current debt must be zero or positive';
        end if;

        if v_loan_interest_percent is null or v_loan_interest_percent < 0 then
            raise exception 'Loan interest percent must be zero or positive';
        end if;

        if v_loan_payment_amount_minor is null or v_loan_payment_amount_minor <= 0 then
            raise exception 'Loan payment amount must be positive';
        end if;

        if p_loan_payment_due_date is null then
            raise exception 'Loan payment due date is required';
        end if;

        if v_loan_remaining_payments_count is null or v_loan_remaining_payments_count < 0 then
            raise exception 'Loan remaining payments count must be zero or positive';
        end if;

        if v_loan_total_payable_minor is null or v_loan_total_payable_minor <= 0 then
            raise exception 'Loan total payable must be positive';
        end if;

        if v_loan_total_payments_count is null or v_loan_total_payments_count <= 0 then
            raise exception 'Loan total payments count must be positive';
        end if;

        if v_loan_final_payment_minor is null or v_loan_final_payment_minor <= 0 then
            raise exception 'Loan final payment must be positive';
        end if;

        if v_loan_remaining_payments_count > v_loan_total_payments_count then
            raise exception 'Loan remaining payments count cannot exceed total payments count';
        end if;

        v_credit_limit_minor := null;
        v_credit_debt_minor := null;
        v_credit_required_payment_minor := null;
        v_effective_balance_minor := v_loan_current_debt_minor;
    else
        if p_balance_minor < 0 then
            raise exception 'Balance must be zero or positive';
        end if;

        v_credit_limit_minor := null;
        v_credit_debt_minor := null;
        v_credit_required_payment_minor := null;
        v_loan_principal_minor := null;
        v_loan_current_debt_minor := null;
        v_loan_interest_percent := null;
        v_loan_payment_amount_minor := null;
        v_loan_remaining_payments_count := null;
        v_loan_total_payable_minor := null;
        v_loan_total_payments_count := null;
        v_loan_final_payment_minor := null;
        v_effective_balance_minor := p_balance_minor;
    end if;

    v_name := case
        when v_kind = 'cash' then 'Наличные'
        when v_kind = 'loan' then trim(p_name)
        else trim(public.finance_provider_label(v_provider_code) || ' •••• ' || v_last_four_digits)
    end;

    if p_id is null then
        if v_kind = 'cash' and exists (
            select 1
            from public.finance_accounts
            where user_id = v_user_id
              and kind = 'cash'
              and is_archived = false
        ) then
            raise exception 'Cash account already exists';
        end if;

        if p_make_primary and v_kind = 'bank_card' then
            update public.finance_accounts
            set is_primary = false
            where user_id = v_user_id
              and kind = 'bank_card'
              and is_archived = false;
        end if;

        select coalesce(max(display_order), -1) + 1
        into v_next_order
        from public.finance_accounts
        where user_id = v_user_id;

        insert into public.finance_accounts (
            user_id, kind, name, bank_name, provider_code, card_type, last_four_digits, currency, balance_minor,
            credit_limit_minor, credit_debt_minor, credit_required_payment_minor, credit_payment_due_date, credit_grace_period_end_date,
            loan_principal_minor, loan_interest_percent, loan_payment_amount_minor, loan_payment_due_date, loan_remaining_payments_count,
            loan_total_payable_minor, loan_total_payments_count, loan_final_payment_minor, is_primary, display_order
        )
        values (
            v_user_id, v_kind, v_name, case when v_kind = 'cash' then null else public.finance_provider_label(v_provider_code) end,
            v_provider_code, v_card_type, v_last_four_digits, v_currency, v_effective_balance_minor,
            v_credit_limit_minor, v_credit_debt_minor, v_credit_required_payment_minor,
            case when v_card_type = 'credit' then p_credit_payment_due_date else null end,
            case when v_card_type = 'credit' then p_credit_grace_period_end_date else null end,
            v_loan_principal_minor, v_loan_interest_percent, v_loan_payment_amount_minor,
            case when v_kind = 'loan' then p_loan_payment_due_date else null end,
            v_loan_remaining_payments_count, v_loan_total_payable_minor, v_loan_total_payments_count, v_loan_final_payment_minor,
            case when v_kind = 'bank_card' then p_make_primary else false end, v_next_order
        );
    else
        select *
        into v_existing
        from public.finance_accounts
        where id = p_id
          and user_id = v_user_id
        for update;

        if v_existing.id is null then
            raise exception 'Account not found';
        end if;

        select count(*)
        into v_transaction_count
        from public.finance_transactions
        where user_id = v_user_id
          and (account_id = p_id or destination_account_id = p_id);

        if v_transaction_count > 0
           and v_existing.kind = 'cash'
           and v_existing.balance_minor <> v_effective_balance_minor then
            raise exception 'Balance cannot be changed after first transaction';
        end if;

        if v_transaction_count > 0 and (
            v_existing.provider_code <> v_provider_code
            or v_existing.currency <> v_currency
            or v_existing.kind <> v_kind
            or coalesce(v_existing.card_type, '') <> coalesce(v_card_type, '')
            or coalesce(v_existing.last_four_digits, '') <> coalesce(v_last_four_digits, '')
        ) then
            raise exception 'Account type and identity cannot be changed after first transaction';
        end if;

        if p_make_primary and v_kind = 'bank_card' then
            update public.finance_accounts
            set is_primary = false
            where user_id = v_user_id
              and kind = 'bank_card'
              and is_archived = false
              and id <> p_id;
        end if;

        update public.finance_accounts
        set name = v_name,
            bank_name = case when v_kind = 'cash' then null else public.finance_provider_label(v_provider_code) end,
            provider_code = v_provider_code,
            card_type = v_card_type,
            last_four_digits = v_last_four_digits,
            currency = v_currency,
            balance_minor = case
                when v_transaction_count > 0 and v_existing.kind = 'cash' then balance_minor
                else v_effective_balance_minor
            end,
            credit_limit_minor = v_credit_limit_minor,
            credit_debt_minor = v_credit_debt_minor,
            credit_required_payment_minor = v_credit_required_payment_minor,
            credit_payment_due_date = case when v_card_type = 'credit' then p_credit_payment_due_date else null end,
            credit_grace_period_end_date = case when v_card_type = 'credit' then p_credit_grace_period_end_date else null end,
            loan_principal_minor = v_loan_principal_minor,
            loan_interest_percent = v_loan_interest_percent,
            loan_payment_amount_minor = v_loan_payment_amount_minor,
            loan_payment_due_date = case when v_kind = 'loan' then p_loan_payment_due_date else null end,
            loan_remaining_payments_count = v_loan_remaining_payments_count,
            loan_total_payable_minor = v_loan_total_payable_minor,
            loan_total_payments_count = v_loan_total_payments_count,
            loan_final_payment_minor = v_loan_final_payment_minor,
            is_primary = case when v_kind = 'bank_card' then p_make_primary else false end,
            updated_at = now()
        where id = p_id
          and user_id = v_user_id;
    end if;

    perform public.finance_seed_default_categories();
    return public.finance_get_overview();
end;
$$;

drop function if exists public.finance_upsert_account(
    uuid,
    text,
    text,
    text,
    text,
    text,
    bigint,
    text,
    boolean,
    bigint,
    bigint,
    bigint,
    date,
    date,
    bigint,
    bigint,
    numeric,
    bigint,
    date,
    integer
);

create or replace function public.finance_record_loan_payment(
    p_source_account_id uuid,
    p_loan_account_id uuid,
    p_amount_minor bigint,
    p_new_current_debt_minor bigint,
    p_happened_at timestamptz default now(),
    p_title text default null,
    p_note text default null,
    p_source_type text default 'manual'
)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user_id uuid := auth.uid();
    v_source_account public.finance_accounts%rowtype;
    v_loan_account public.finance_accounts%rowtype;
    v_title text := nullif(trim(coalesce(p_title, '')), '');
    v_note text := nullif(trim(coalesce(p_note, '')), '');
    v_source_type text := lower(trim(coalesce(p_source_type, 'manual')));
    v_new_remaining integer;
begin
    if v_user_id is null then
        raise exception 'Not authenticated';
    end if;

    if coalesce(p_amount_minor, 0) <= 0 then
        raise exception 'Loan payment amount must be positive';
    end if;

    if p_new_current_debt_minor is null or p_new_current_debt_minor < 0 then
        raise exception 'Loan debt after payment must be zero or positive';
    end if;

    if v_source_type not in ('manual', 'photo', 'file') then
        raise exception 'Unsupported source type';
    end if;

    select *
    into v_source_account
    from public.finance_accounts
    where id = p_source_account_id
      and user_id = v_user_id
      and is_archived = false
    for update;

    if v_source_account.id is null then
        raise exception 'Source account not found';
    end if;

    if not (
        v_source_account.kind = 'cash'
        or (v_source_account.kind = 'bank_card' and coalesce(v_source_account.card_type, 'debit') <> 'credit')
    ) then
        raise exception 'Loan payment source must be a cash or debit account';
    end if;

    if coalesce(v_source_account.balance_minor, 0) < p_amount_minor then
        raise exception 'Insufficient available funds';
    end if;

    select *
    into v_loan_account
    from public.finance_accounts
    where id = p_loan_account_id
      and user_id = v_user_id
      and is_archived = false
    for update;

    if v_loan_account.id is null or v_loan_account.kind <> 'loan' then
        raise exception 'Loan account not found';
    end if;

    if p_new_current_debt_minor > coalesce(v_loan_account.balance_minor, 0) then
        raise exception 'Loan debt after payment cannot exceed current debt';
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
        item_count,
        source_type
    )
    values (
        v_user_id,
        p_source_account_id,
        p_loan_account_id,
        'expense',
        'loan_payment',
        coalesce(v_title, 'Платёж по кредиту'),
        v_note,
        p_amount_minor,
        v_source_account.currency,
        coalesce(p_happened_at, now()),
        0,
        v_source_type
    );

    perform public.finance_apply_account_activity(p_source_account_id, 'expense', p_amount_minor);

    v_new_remaining := greatest(coalesce(v_loan_account.loan_remaining_payments_count, 0) - 1, 0);

    update public.finance_accounts
    set balance_minor = p_new_current_debt_minor,
        loan_remaining_payments_count = v_new_remaining,
        loan_payment_due_date = case
            when v_new_remaining = 0 then null
            when loan_payment_due_date is null then null
            else (loan_payment_due_date + interval '1 month')::date
        end,
        updated_at = now()
    where id = p_loan_account_id;

    return public.finance_get_overview();
end;
$$;

grant execute on function public.finance_upsert_account(
    uuid,
    text,
    text,
    text,
    text,
    text,
    bigint,
    text,
    boolean,
    bigint,
    bigint,
    bigint,
    date,
    date,
    bigint,
    bigint,
    numeric,
    bigint,
    date,
    integer,
    bigint,
    integer,
    bigint
) to authenticated;

grant execute on function public.finance_record_loan_payment(uuid, uuid, bigint, bigint, timestamptz, text, text, text) to authenticated;
