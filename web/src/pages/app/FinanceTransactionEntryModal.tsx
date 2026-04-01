import { FinanceTransactionFlow } from "./FinanceTransactionFlow";
import type { FinanceCategory, FinanceOverview } from "../../types";

export function FinanceTransactionEntryModal({
  lang,
  overview,
  categories,
  onClose,
  onSaved,
}: {
  lang: "ru" | "en";
  overview: FinanceOverview;
  categories: FinanceCategory[];
  onClose: () => void;
  onSaved: (overview: FinanceOverview) => void;
}) {
  return (
    <FinanceTransactionFlow
      lang={lang}
      overview={overview}
      categories={categories}
      categoriesLoading={false}
      onOverviewChange={onSaved}
      onTransactionsRefresh={async () => {}}
      onClose={onClose}
    />
  );
}
