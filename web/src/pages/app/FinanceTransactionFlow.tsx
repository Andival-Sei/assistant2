import {
  startTransition,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import type { ReactNode } from "react";

import { supabase, supabaseAnonKey, supabaseUrl } from "../../lib/supabaseClient";
import type {
  Currency,
  FinanceCategory,
  FinanceImportResult,
  FinanceOverview,
} from "../../types";
import { parseAmountToMinor } from "./FinancePanel";

type Lang = "ru" | "en";

type FinanceTransactionFlowProps = {
  lang: Lang;
  overview: FinanceOverview;
  categories: FinanceCategory[];
  categoriesLoading: boolean;
  onOverviewChange: (overview: FinanceOverview) => void;
  onTransactionsRefresh: () => Promise<void>;
  onClose: () => void;
};

type ImportSourceType = "manual" | "photo" | "file";

type DraftItemState = {
  id: string;
  title: string;
  amount: string;
  categoryId: string | null;
};

type DraftState = {
  id: string;
  sourceType: ImportSourceType;
  documentKind: "manual" | "image" | "pdf" | "eml";
  direction: "income" | "expense" | "transfer";
  title: string;
  merchantName: string;
  note: string;
  accountId: string;
  destinationAccountId: string;
  currency: Currency;
  happenedAt: string;
  items: DraftItemState[];
};

type FinanceCategoryDirection = "income" | "expense";
type FieldPickerState =
  | { draftId: string; kind: "account" }
  | { draftId: string; kind: "destination" };
type PickerOption = {
  value: string;
  label: string;
  description?: string;
};

const UNCATEGORIZED_CATEGORY_CODE: Record<FinanceCategoryDirection, string> = {
  income: "income_uncategorized",
  expense: "expense_uncategorized",
};

const copy = {
  ru: {
    title: "Добавить транзакцию",
    chooserSubtitle: "Выберите способ добавления.",
    manualTitle: "Вручную",
    manualBody: "Собрать транзакцию вручную: счёт, дата, магазин и позиции.",
    photoTitle: "Фото",
    photoBody: "Открыть камеру, сделать фото чека и разобрать его в черновик.",
    fileTitle: "Файл",
    fileBody: "Загрузить изображение, PDF или EML и получить готовые черновики.",
    noAccounts: "Сначала добавьте хотя бы один счёт.",
    back: "Назад",
    save: "Сохранить",
    saveMany: (count: number) => `Сохранить ${count} транзакц${count === 1 ? "ию" : count < 5 ? "ии" : "ий"}`,
    addItem: "Добавить позицию",
    removeItem: "Удалить",
    analyze: "Анализируем документ…",
    importFailed: "Не удалось разобрать файл.",
    methodWarnings: "Замечания импорта",
    draftLabel: "Черновик",
    account: "Счёт",
    destinationAccount: "Куда",
    direction: "Тип",
    dateTime: "Дата и время",
    descriptionLabel: "Описание",
    merchantLabel: "Магазин / источник",
    itemTitle: "Позиция",
    itemAmount: "Сумма",
    itemCategory: "Категория",
    chooseCategory: "Выбрать категорию",
    chooseAccount: "Выбрать счёт",
    chooseDestination: "Выбрать счёт назначения",
    detailsSection: "Основное",
    itemsSection: "Позиции",
    directions: {
      expense: "Расход",
      income: "Доход",
      transfer: "Перевод",
    },
    editorHint: "Проверьте счёт, дату и позиции перед сохранением.",
    importSummary: "Распознано",
    cameraPermission:
      "Не удалось открыть камеру. Можно выбрать фото из файла.",
    capture: "Сделать фото",
    openFileInstead: "Выбрать файл",
    categoryTitle: "Категории",
    accountTitle: "Счета",
    destinationTitle: "Счёт назначения",
    emptyState: "Данные пока пусты.",
  },
  en: {
    title: "Add transaction",
    chooserSubtitle: "Choose how to add it.",
    manualTitle: "Manual",
    manualBody: "Build the transaction manually: account, date, merchant, and items.",
    photoTitle: "Photo",
    photoBody: "Open the camera, capture a receipt, and turn it into a draft.",
    fileTitle: "File",
    fileBody: "Upload an image, PDF, or EML and get ready-made drafts.",
    noAccounts: "Add at least one account first.",
    back: "Back",
    save: "Save",
    saveMany: (count: number) => `Save ${count} transaction${count === 1 ? "" : "s"}`,
    addItem: "Add item",
    removeItem: "Remove",
    analyze: "Analyzing document…",
    importFailed: "Failed to parse the file.",
    methodWarnings: "Import warnings",
    draftLabel: "Draft",
    account: "Account",
    destinationAccount: "Destination",
    direction: "Type",
    dateTime: "Date and time",
    descriptionLabel: "Description",
    merchantLabel: "Merchant / source",
    itemTitle: "Item",
    itemAmount: "Amount",
    itemCategory: "Category",
    chooseCategory: "Choose category",
    chooseAccount: "Choose account",
    chooseDestination: "Choose destination account",
    detailsSection: "Basics",
    itemsSection: "Items",
    directions: {
      expense: "Expense",
      income: "Income",
      transfer: "Transfer",
    },
    editorHint: "Review the account, date, and items before saving.",
    importSummary: "Detected",
    cameraPermission:
      "The camera could not be opened. You can choose a photo file instead.",
    capture: "Take photo",
    openFileInstead: "Choose file",
    categoryTitle: "Categories",
    accountTitle: "Accounts",
    destinationTitle: "Destination account",
    emptyState: "No data yet.",
  },
} as const;

function createId() {
  return crypto.randomUUID();
}

function toLocalDateTime(value: string | null | undefined) {
  const date = value ? new Date(value) : new Date();
  const offset = date.getTimezoneOffset() * 60_000;
  const local = new Date(date.getTime() - offset);
  return local.toISOString().slice(0, 16);
}

function fromLocalDateTime(value: string) {
  if (!value) return new Date().toISOString();
  return new Date(value).toISOString();
}

function buildCategoryTree(categories: FinanceCategory[]) {
  const byId = new Map<string, FinanceCategory & { children: FinanceCategory[] }>();
  for (const category of categories) {
    byId.set(category.id, { ...category, children: [] });
  }

  const roots: Array<FinanceCategory & { children: FinanceCategory[] }> = [];
  for (const category of byId.values()) {
    if (category.parentId && byId.has(category.parentId)) {
      byId.get(category.parentId)!.children.push(category);
      continue;
    }
    roots.push(category);
  }

  const sortNodes = (items: Array<FinanceCategory & { children: FinanceCategory[] }>) => {
    items.sort((left, right) => left.displayOrder - right.displayOrder);
    items.forEach((item) => sortNodes(item.children as Array<FinanceCategory & { children: FinanceCategory[] }>));
  };

  sortNodes(roots);
  return roots;
}

function getDefaultCategoryForDirection(
  categories: FinanceCategory[],
  direction: FinanceCategoryDirection,
) {
  return (
    categories.find(
      (category) =>
        category.direction === direction &&
        category.code === UNCATEGORIZED_CATEGORY_CODE[direction],
    ) ?? null
  );
}

async function readFunctionError(response: Response, fallback: string) {
  const rawText = await response.text().catch(() => "");
  const contentType = response.headers.get("content-type") ?? "";
  if (contentType.includes("application/json")) {
    try {
      const payload = JSON.parse(rawText || "{}") as Record<string, unknown>;
      const message =
        (typeof payload.error === "string" && payload.error.trim()) ||
        (typeof payload.message === "string" && payload.message.trim()) ||
        (typeof payload.details === "string" && payload.details.trim()) ||
        null;
      if (message) return message;
    } catch {
      // Fall back to plain text parsing.
    }
  }

  return rawText.trim() || fallback;
}

async function invokeImportFunction(file: File, sourceType: "photo" | "file") {
  if (!supabaseUrl || !supabaseAnonKey) {
    throw new Error("Supabase env is missing.");
  }

  async function getAccessToken(forceRefresh = false) {
    if (forceRefresh) {
      const { data, error } = await supabase.auth.refreshSession();
      if (error) throw error;
      if (data.session?.access_token) return data.session.access_token;
    }

    const {
      data: { session },
    } = await supabase.auth.getSession();
    if (!session?.access_token) {
      const { data, error } = await supabase.auth.refreshSession();
      if (error) throw error;
      if (!data.session?.access_token) {
        throw new Error("Сессия истекла. Войдите в аккаунт ещё раз и повторите загрузку.");
      }
      return data.session.access_token;
    }

    if (session.expires_at && session.expires_at * 1000 <= Date.now() + 60_000) {
      const { data, error } = await supabase.auth.refreshSession();
      if (error) throw error;
      if (data.session?.access_token) return data.session.access_token;
    }

    return session.access_token;
  }

  async function send(accessToken: string) {
    const formData = new FormData();
    formData.set("file", file);
    formData.set("sourceType", sourceType);

    return fetch(`${supabaseUrl}/functions/v1/process-finance-import`, {
      method: "POST",
      headers: {
        apikey: supabaseAnonKey ?? "",
        Authorization: `Bearer ${accessToken}`,
      },
      body: formData,
    });
  }

  let response = await send(await getAccessToken());
  if (response.status === 401) {
    response = await send(await getAccessToken(true));
  }

  if (!response.ok) {
    throw new Error(await readFunctionError(response, "Не удалось разобрать файл."));
  }

  return (await response.json()) as FinanceImportResult;
}

function createManualDraft(overview: FinanceOverview, categories: FinanceCategory[]): DraftState {
  const primaryAccount =
    overview.accounts.find((account) => account.isPrimary) ?? overview.accounts[0];
  const defaultCategoryId = getDefaultCategoryForDirection(categories, "expense")?.id ?? null;
  return {
    id: createId(),
    sourceType: "manual",
    documentKind: "manual",
    direction: "expense",
    title: "",
    merchantName: "",
    note: "",
    accountId: primaryAccount?.id ?? "",
    destinationAccountId: "",
    currency: (primaryAccount?.currency ?? overview.defaultCurrency ?? "RUB") as Currency,
    happenedAt: toLocalDateTime(new Date().toISOString()),
    items: [
      {
        id: createId(),
        title: "",
        amount: "",
        categoryId: defaultCategoryId,
      },
    ],
  };
}

function mapImportDrafts(
  result: FinanceImportResult,
  overview: FinanceOverview,
  categories: FinanceCategory[],
): DraftState[] {
  const primaryAccount =
    overview.accounts.find((account) => account.isPrimary) ?? overview.accounts[0];
  const defaultAccountId = primaryAccount?.id ?? "";

  return result.drafts.map((draft) => ({
    id: createId(),
    sourceType: draft.sourceType,
    documentKind: draft.documentKind,
    direction: draft.direction,
    title: draft.title,
    merchantName: draft.merchantName ?? "",
    note: draft.note ?? "",
    accountId: defaultAccountId,
    destinationAccountId: "",
    currency: draft.currency,
    happenedAt: toLocalDateTime(draft.happenedAt),
    items: draft.items.map((item) => ({
      id: createId(),
      title: item.title,
      amount: String((item.amountMinor / 100).toFixed(item.amountMinor % 100 === 0 ? 0 : 2)),
      categoryId:
        item.suggestedCategoryId ??
        getDefaultCategoryForDirection(categories, draft.direction)?.id ??
        null,
    })),
  }));
}

function CameraCapture({
  lang,
  onClose,
  onCaptured,
  onUseFilePicker,
}: {
  lang: Lang;
  onClose: () => void;
  onCaptured: (file: File) => void;
  onUseFilePicker: () => void;
}) {
  const text = copy[lang];
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;

    async function startCamera() {
      try {
        const stream = await navigator.mediaDevices.getUserMedia({
          video: { facingMode: { ideal: "environment" } },
          audio: false,
        });
        if (!active) {
          stream.getTracks().forEach((track) => track.stop());
          return;
        }
        streamRef.current = stream;
        setReady(true);
      } catch {
        setError(text.cameraPermission);
      }
    }

    void startCamera();
    return () => {
      active = false;
      streamRef.current?.getTracks().forEach((track) => track.stop());
      streamRef.current = null;
    };
  }, [lang, text.cameraPermission]);

  useEffect(() => {
    const video = videoRef.current;
    const stream = streamRef.current;
    if (!video || !stream) return;

    video.srcObject = stream;
    void video.play().catch(() => {
      setError(text.cameraPermission);
      setReady(false);
    });
  }, [ready, text.cameraPermission]);

  function capture() {
    const video = videoRef.current;
    if (!video || video.videoWidth <= 0 || video.videoHeight <= 0) return;
    const canvas = document.createElement("canvas");
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    const context = canvas.getContext("2d");
    if (!context) return;
    context.drawImage(video, 0, 0, canvas.width, canvas.height);
    canvas.toBlob((blob) => {
      if (!blob) return;
      onCaptured(new File([blob], `receipt-${Date.now()}.jpg`, { type: "image/jpeg" }));
    }, "image/jpeg", 0.92);
  }

  return (
    <div className="finance-flow-camera">
      <div className="finance-modal-head">
        <div>
          <h3>{text.photoTitle}</h3>
          <p>{text.photoBody}</p>
        </div>
        <button className="finance-icon-btn" type="button" onClick={onClose}>
          ×
        </button>
      </div>
      <div className="finance-modal-body finance-flow-screen-body">
        <div className="finance-flow-camera-stage">
          <video ref={videoRef} autoPlay playsInline muted className="finance-flow-camera-video" />
          {!ready && !error ? <div className="finance-flow-loader">{text.analyze}</div> : null}
        </div>
        {error ? <p className="finance-inline-error">{error}</p> : null}
        <div className="finance-modal-actions">
          <button className="secondary-btn" type="button" onClick={onClose}>
            {text.back}
          </button>
          {error ? (
            <button className="secondary-btn" type="button" onClick={onUseFilePicker}>
              {text.openFileInstead}
            </button>
          ) : null}
          <button className="pill-btn" type="button" onClick={capture} disabled={!ready}>
            {text.capture}
          </button>
        </div>
      </div>
    </div>
  );
}

function OptionPicker({
  title,
  subtitle,
  options,
  selectedValue,
  onSelect,
  onClose,
}: {
  title: string;
  subtitle: string;
  options: PickerOption[];
  selectedValue: string;
  onSelect: (value: string) => void;
  onClose: () => void;
}) {
  return (
    <div className="finance-flow-category-sheet">
      <div className="finance-modal-head">
        <div>
          <h3>{title}</h3>
          <p>{subtitle}</p>
        </div>
        <button className="finance-icon-btn" type="button" onClick={onClose}>
          ×
        </button>
      </div>
      <div className="finance-modal-body finance-flow-screen-body">
        <div className="finance-flow-category-list">
          {options.map((option) => (
            <button
              key={option.value}
              className={`finance-flow-category-btn finance-flow-option-btn ${selectedValue === option.value ? "is-selected" : ""}`}
              type="button"
              onClick={() => onSelect(option.value)}
            >
              <span>
                <strong>{option.label}</strong>
                {option.description ? <small>{option.description}</small> : null}
              </span>
              {selectedValue === option.value ? <strong>✓</strong> : null}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

function CategoryPicker({
  lang,
  categories,
  direction,
  defaultCategoryId,
  selectedId,
  onSelect,
  onClose,
}: {
  lang: Lang;
  categories: FinanceCategory[];
  direction: "income" | "expense";
  defaultCategoryId: string | null;
  selectedId: string | null;
  onSelect: (categoryId: string | null) => void;
  onClose: () => void;
}) {
  const text = copy[lang];
  const tree = useMemo(
    () => buildCategoryTree(categories.filter((category) => category.direction === direction)),
    [categories, direction],
  );

  function renderNode(
    node: FinanceCategory & { children: FinanceCategory[] },
    level = 0,
  ): ReactNode {
    return (
      <div key={node.id} className="finance-flow-category-node">
        <button
          className={`finance-flow-category-btn ${selectedId === node.id ? "is-selected" : ""}`}
          type="button"
          style={{ ["--finance-tree-level" as string]: String(level) }}
          onClick={() => onSelect(node.id)}
        >
          <span>{node.name}</span>
          {selectedId === node.id ? <strong>✓</strong> : null}
        </button>
        {node.children.length
          ? node.children.map((child) =>
              renderNode(child as FinanceCategory & { children: FinanceCategory[] }, level + 1),
            )
          : null}
      </div>
    );
  }

  return (
    <div className="finance-flow-category-sheet">
      <div className="finance-modal-head">
        <div>
          <h3>{text.categoryTitle}</h3>
          <p>{text.chooseCategory}</p>
        </div>
        <button className="finance-icon-btn" type="button" onClick={onClose}>
          ×
        </button>
      </div>
      <button
        className={`finance-flow-category-btn finance-flow-category-btn--default ${selectedId === defaultCategoryId ? "is-selected" : ""}`}
        type="button"
        onClick={() => onSelect(defaultCategoryId)}
      >
        <span>Без категории</span>
        {selectedId === defaultCategoryId ? <strong>✓</strong> : null}
      </button>
      <div className="finance-flow-category-list">
        {tree.length ? tree.map((node) => renderNode(node)) : <p className="finance-empty-copy">{text.emptyState}</p>}
      </div>
    </div>
  );
}

export function FinanceTransactionFlow({
  lang,
  overview,
  categories,
  categoriesLoading,
  onOverviewChange,
  onTransactionsRefresh,
  onClose,
}: FinanceTransactionFlowProps) {
  const text = copy[lang];
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const photoInputRef = useRef<HTMLInputElement | null>(null);
  const [step, setStep] = useState<"chooser" | "camera" | "editor">("chooser");
  const [drafts, setDrafts] = useState<DraftState[]>([]);
  const [selectedDraftId, setSelectedDraftId] = useState<string | null>(null);
  const [categoryTarget, setCategoryTarget] = useState<{ draftId: string; itemId: string } | null>(null);
  const [importWarnings, setImportWarnings] = useState<string[]>([]);
  const [busy, setBusy] = useState<"import" | "save" | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [fieldPicker, setFieldPicker] = useState<FieldPickerState | null>(null);

  const selectedDraft = drafts.find((draft) => draft.id === selectedDraftId) ?? drafts[0] ?? null;

  const categoryById = useMemo(
    () => new Map(categories.map((category) => [category.id, category])),
    [categories],
  );
  const uncategorizedByDirection = useMemo(
    () => ({
      expense: getDefaultCategoryForDirection(categories, "expense"),
      income: getDefaultCategoryForDirection(categories, "income"),
    }),
    [categories],
  );
  const targetDraft = categoryTarget
    ? drafts.find((draft) => draft.id === categoryTarget.draftId) ?? null
    : null;
  const targetDirection: FinanceCategoryDirection =
    targetDraft?.direction === "income" ? "income" : "expense";
  const targetDefaultCategoryId = uncategorizedByDirection[targetDirection]?.id ?? null;
  const targetSelectedCategoryId =
    targetDraft?.items.find((item) => item.id === categoryTarget?.itemId)?.categoryId ??
    targetDefaultCategoryId;
  const fieldPickerDraft = fieldPicker
    ? drafts.find((draft) => draft.id === fieldPicker.draftId) ?? null
    : null;
  const accountPickerOptions = useMemo<PickerOption[]>(() => (
    overview.accounts.map((account) => ({
      value: account.id,
      label: account.name,
      description: account.bankName ?? account.currency,
    }))
  ), [overview.accounts]);
  const destinationPickerOptions = useMemo<PickerOption[]>(() => (
    overview.accounts
      .filter((account) => account.id !== fieldPickerDraft?.accountId)
      .map((account) => ({
        value: account.id,
        label: account.name,
        description: account.bankName ?? account.currency,
      }))
  ), [fieldPickerDraft?.accountId, overview.accounts]);

  function updateDraft(draftId: string, updater: (draft: DraftState) => DraftState) {
    setDrafts((current) =>
      current.map((draft) => (draft.id === draftId ? updater(draft) : draft)),
    );
  }

  function openManual() {
    const next = createManualDraft(overview, categories);
    setDrafts([next]);
    setSelectedDraftId(next.id);
    setImportWarnings([]);
    setError(null);
    setStep("editor");
  }

  async function importFile(file: File, sourceType: "photo" | "file") {
    try {
      setBusy("import");
      setError(null);
      const {
        data: { session },
      } = await supabase.auth.getSession();
      if (!session?.access_token) {
        throw new Error(text.importFailed);
      }
      const data = await invokeImportFunction(file, sourceType);
      if (!data?.drafts?.length) throw new Error(text.importFailed);
      const nextDrafts = mapImportDrafts(data, overview, categories);
      setDrafts(nextDrafts);
      setSelectedDraftId(nextDrafts[0]?.id ?? null);
      setImportWarnings(data.warnings ?? []);
      setStep("editor");
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : text.importFailed);
    } finally {
      setBusy(null);
    }
  }

  async function submitDrafts() {
    try {
      setBusy("save");
      setError(null);

      const payload = drafts.map((draft) => {
        const fallbackCategoryId =
          draft.direction === "expense"
            ? uncategorizedByDirection.expense?.id ?? null
            : draft.direction === "income"
              ? uncategorizedByDirection.income?.id ?? null
              : null;
        const items = draft.items.map((item) => ({
          title: item.title.trim() || draft.title.trim() || draft.merchantName.trim() || text.itemTitle,
          amountMinor: parseAmountToMinor(item.amount) ?? 0,
          categoryId:
            draft.direction === "transfer"
              ? null
              : item.categoryId ?? fallbackCategoryId,
        }));

        return {
          accountId: draft.accountId,
          destinationAccountId: draft.direction === "transfer" ? draft.destinationAccountId || null : null,
          direction: draft.direction,
          transactionKind:
            draft.direction === "transfer"
              ? "transfer"
              : items.length > 1
                ? "split"
                : "single",
          title: draft.title.trim() || null,
          merchantName: draft.merchantName.trim() || null,
          note: draft.note.trim() || null,
          currency: draft.currency,
          happenedAt: fromLocalDateTime(draft.happenedAt),
          sourceType: draft.sourceType,
          amountMinor:
            draft.direction === "transfer"
              ? parseAmountToMinor(draft.items[0]?.amount ?? "") ?? 0
              : items.reduce((total, item) => total + item.amountMinor, 0),
          items: draft.direction === "transfer" ? [] : items,
        };
      });

      const { error: rpcError } = await supabase.rpc("finance_create_transactions", {
        p_transactions: payload,
      });
      if (rpcError) throw rpcError;

      const { data: nextOverview, error: overviewError } = await supabase.rpc("finance_get_overview");
      if (overviewError) throw overviewError;

      onOverviewChange(nextOverview as FinanceOverview);
      await onTransactionsRefresh();
      onClose();
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : text.importFailed);
    } finally {
      setBusy(null);
    }
  }

  const canSave = useMemo(() => {
    if (!drafts.length || busy === "save") return false;
    return drafts.every((draft) => {
      if (!draft.accountId) return false;
      if (draft.direction === "transfer") {
        return (
          Boolean(draft.destinationAccountId) &&
          draft.destinationAccountId !== draft.accountId &&
          Boolean(parseAmountToMinor(draft.items[0]?.amount ?? ""))
        );
      }

      return draft.items.length > 0 && draft.items.every((item) => {
        const amount = parseAmountToMinor(item.amount);
        return Boolean(amount);
      });
    });
  }, [busy, drafts]);

  function renderEditor() {
    if (!selectedDraft) return null;

    return (
      <div className="finance-flow-editor">
        <div className="finance-flow-editor-head">
          <div>
            {selectedDraft.sourceType !== "manual" ? (
              <span className="finance-surface-label">{text.importSummary}</span>
            ) : null}
            <h3>{drafts.length > 1 ? text.saveMany(drafts.length) : text.title}</h3>
            <p>{text.editorHint}</p>
          </div>
          <button className="finance-icon-btn" type="button" onClick={onClose}>
            ×
          </button>
        </div>
        <div className="finance-modal-body finance-flow-screen-body">
          {drafts.length > 1 ? (
            <div className="finance-flow-draft-strip">
              {drafts.map((draft, index) => (
                <button
                  key={draft.id}
                  className={`finance-flow-draft-pill ${selectedDraftId === draft.id ? "is-active" : ""}`}
                  type="button"
                  onClick={() => startTransition(() => setSelectedDraftId(draft.id))}
                >
                  {text.draftLabel} {index + 1}
                </button>
              ))}
            </div>
          ) : null}

          {importWarnings.length ? (
            <article className="finance-flow-warning-card">
              <strong>{text.methodWarnings}</strong>
              {importWarnings.map((warning) => (
                <p key={warning}>{warning}</p>
              ))}
            </article>
          ) : null}

          {error ? <p className="finance-inline-error">{error}</p> : null}

          <div className="finance-flow-form">
            <div className="finance-flow-layout">
              <section className="finance-flow-section finance-flow-section--meta">
                <div className="finance-flow-section-head">
                  <div>
                    <strong>{text.detailsSection}</strong>
                  </div>
                </div>

                <div className="finance-flow-grid finance-flow-grid--meta">
                  <div className="finance-field">
                    <span>{text.account}</span>
                    <button
                      className="finance-flow-category-trigger finance-flow-picker-trigger"
                      type="button"
                      onClick={() => setFieldPicker({ draftId: selectedDraft.id, kind: "account" })}
                    >
                      <span>{text.chooseAccount}</span>
                      <strong>{overview.accounts.find((account) => account.id === selectedDraft.accountId)?.name ?? text.chooseAccount}</strong>
                    </button>
                  </div>

                  <label className="finance-field">
                    <span>{text.direction}</span>
                    <div className="finance-flow-segmented">
                      {(["expense", "income", "transfer"] as const).map((direction) => (
                        <button
                          key={direction}
                          className={selectedDraft.direction === direction ? "is-active" : ""}
                          type="button"
                          onClick={() =>
                            updateDraft(selectedDraft.id, (draft) => ({
                              ...draft,
                              direction,
                              items:
                                direction === "transfer"
                                  ? [
                                      {
                                        id: draft.items[0]?.id ?? createId(),
                                        title: draft.items[0]?.title ?? "",
                                        amount: draft.items[0]?.amount ?? "",
                                        categoryId: null,
                                      },
                                    ]
                                  : draft.items.map((item) => {
                                      const fallbackCategoryId =
                                        direction === "expense"
                                          ? uncategorizedByDirection.expense?.id ?? null
                                          : direction === "income"
                                            ? uncategorizedByDirection.income?.id ?? null
                                            : null;
                                      return {
                                        ...item,
                                        categoryId: item.categoryId ?? fallbackCategoryId,
                                      };
                                    }),
                            }))
                          }
                        >
                          {text.directions[direction]}
                        </button>
                      ))}
                    </div>
                  </label>

                  <label className="finance-field">
                    <span>{text.dateTime}</span>
                    <input
                      type="datetime-local"
                      value={selectedDraft.happenedAt}
                      onChange={(event) =>
                        updateDraft(selectedDraft.id, (draft) => ({
                          ...draft,
                          happenedAt: event.target.value,
                        }))
                      }
                    />
                  </label>

                  {selectedDraft.direction === "transfer" ? (
                    <div className="finance-field">
                      <span>{text.destinationAccount}</span>
                      <button
                        className="finance-flow-category-trigger finance-flow-picker-trigger"
                        type="button"
                        onClick={() => setFieldPicker({ draftId: selectedDraft.id, kind: "destination" })}
                      >
                        <span>{text.chooseDestination}</span>
                        <strong>
                          {overview.accounts.find((account) => account.id === selectedDraft.destinationAccountId)?.name ??
                            text.chooseDestination}
                        </strong>
                      </button>
                    </div>
                  ) : null}

                  <label className="finance-field finance-field--wide">
                    <span>{text.descriptionLabel}</span>
                    <input
                      value={selectedDraft.title}
                      onChange={(event) =>
                        updateDraft(selectedDraft.id, (draft) => ({
                          ...draft,
                          title: event.target.value,
                        }))
                      }
                    />
                  </label>

                  <label className="finance-field finance-field--wide">
                    <span>{text.merchantLabel}</span>
                    <input
                      value={selectedDraft.merchantName}
                      onChange={(event) =>
                        updateDraft(selectedDraft.id, (draft) => ({
                          ...draft,
                          merchantName: event.target.value,
                        }))
                      }
                    />
                  </label>
                </div>
              </section>

              <section className="finance-flow-section finance-flow-section--items">
                <div className="finance-flow-section-head finance-flow-section-head--actions">
                  <div>
                    <strong>{text.itemsSection}</strong>
                  </div>
                  {selectedDraft.direction !== "transfer" ? (
                    <button
                      className="secondary-btn finance-flow-add-item"
                      type="button"
                      onClick={() =>
                        updateDraft(selectedDraft.id, (draft) => ({
                          ...draft,
                          items: [
                            ...draft.items,
                            {
                              id: createId(),
                              title: "",
                              amount: "",
                              categoryId:
                                draft.direction === "expense"
                                  ? uncategorizedByDirection.expense?.id ?? null
                                  : draft.direction === "income"
                                    ? uncategorizedByDirection.income?.id ?? null
                                    : null,
                            },
                          ],
                        }))
                      }
                    >
                      {text.addItem}
                    </button>
                  ) : null}
                </div>

                <div className="finance-flow-items">
                  {selectedDraft.items.map((item, index) => {
              const category =
                (item.categoryId ? categoryById.get(item.categoryId) : null) ??
                (selectedDraft.direction === "expense"
                  ? uncategorizedByDirection.expense
                  : selectedDraft.direction === "income"
                    ? uncategorizedByDirection.income
                    : null);
              return (
                <article key={item.id} className="finance-flow-item-card">
                  <div className="finance-flow-item-head">
                    <strong>
                      {text.itemTitle} {index + 1}
                    </strong>
                    {selectedDraft.direction !== "transfer" && selectedDraft.items.length > 1 ? (
                      <button
                        className="finance-text-link"
                        type="button"
                        onClick={() =>
                          updateDraft(selectedDraft.id, (draft) => ({
                            ...draft,
                            items: draft.items.filter((current) => current.id !== item.id),
                          }))
                        }
                      >
                        {text.removeItem}
                      </button>
                    ) : null}
                  </div>

                  <div className="finance-flow-grid finance-flow-grid--item">
                    <label className="finance-field finance-field--wide">
                      <span>{text.itemTitle}</span>
                      <input
                        value={item.title}
                        onChange={(event) =>
                          updateDraft(selectedDraft.id, (draft) => ({
                            ...draft,
                            items: draft.items.map((current) =>
                              current.id === item.id
                                ? { ...current, title: event.target.value }
                                : current,
                            ),
                          }))
                        }
                      />
                    </label>

                    <label className="finance-field">
                      <span>{text.itemAmount}</span>
                      <input
                        value={item.amount}
                        onChange={(event) =>
                          updateDraft(selectedDraft.id, (draft) => ({
                            ...draft,
                            items: draft.items.map((current) =>
                              current.id === item.id
                                ? { ...current, amount: event.target.value }
                                : current,
                            ),
                          }))
                        }
                      />
                    </label>

                    {selectedDraft.direction !== "transfer" ? (
                      <button
                        className="finance-flow-category-trigger"
                        type="button"
                        onClick={() =>
                          setCategoryTarget({
                            draftId: selectedDraft.id,
                            itemId: item.id,
                          })
                        }
                      >
                        <span>{text.itemCategory}</span>
                        <strong>{category?.name ?? text.chooseCategory}</strong>
                      </button>
                    ) : null}
                  </div>
                </article>
              );
                  })}
                </div>
              </section>
            </div>
          </div>

          <div className="finance-modal-actions finance-modal-footer">
            <button className="secondary-btn" type="button" onClick={() => setStep("chooser")}>
              {text.back}
            </button>
            <button className="pill-btn" type="button" disabled={!canSave || busy === "save"} onClick={() => void submitDrafts()}>
              {busy === "save" ? "…" : drafts.length > 1 ? text.saveMany(drafts.length) : text.save}
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <>
      <div className="finance-modal-backdrop is-open" role="presentation" onClick={onClose}>
        <div
          className="finance-modal finance-modal--wide is-open finance-flow-modal"
          role="dialog"
          aria-modal="true"
          aria-label={text.title}
          onClick={(event) => event.stopPropagation()}
        >
          {step === "chooser" ? (
            <>
              <div className="finance-modal-head">
                <div>
                  <h3>{text.title}</h3>
                </div>
                <button className="finance-icon-btn" type="button" onClick={onClose}>
                  ×
                </button>
              </div>
              <div className="finance-modal-body finance-flow-screen-body">
                {error || !overview.accounts.length ? (
                  <div className="finance-flow-status-stack">
                    {error ? <p className="finance-inline-error">{error}</p> : null}
                    {!overview.accounts.length ? <p className="finance-inline-error">{text.noAccounts}</p> : null}
                  </div>
                ) : null}
                <div className="finance-flow-chooser">
                  <button className="finance-flow-method-card" type="button" onClick={openManual} disabled={!overview.accounts.length}>
                    <strong>{text.manualTitle}</strong>
                    <p>{text.manualBody}</p>
                  </button>
                  <button className="finance-flow-method-card finance-flow-method-card--emphasis" type="button" onClick={() => {
                    if (typeof navigator !== "undefined" && Boolean(navigator.mediaDevices?.getUserMedia)) {
                      setStep("camera");
                      return;
                    }
                    photoInputRef.current?.click();
                  }} disabled={!overview.accounts.length}>
                    <strong>{text.photoTitle}</strong>
                    <p>{text.photoBody}</p>
                  </button>
                  <button className="finance-flow-method-card" type="button" onClick={() => fileInputRef.current?.click()} disabled={!overview.accounts.length}>
                    <strong>{text.fileTitle}</strong>
                    <p>{text.fileBody}</p>
                  </button>
                  {busy === "import" ? <div className="finance-flow-loader">{text.analyze}</div> : null}
                </div>
              </div>
            </>
          ) : null}
          {step === "camera" ? (
            <CameraCapture
              lang={lang}
              onClose={() => setStep("chooser")}
              onCaptured={(file) => void importFile(file, "photo")}
              onUseFilePicker={() => photoInputRef.current?.click()}
            />
          ) : null}
          {step === "editor" ? renderEditor() : null}
        </div>
      </div>

      {categoryTarget ? (
        <div className="finance-modal-backdrop is-open" role="presentation" onClick={() => setCategoryTarget(null)}>
          <div className="finance-modal finance-modal--wide is-open finance-flow-category-modal" role="dialog" aria-modal="true" onClick={(event) => event.stopPropagation()}>
            <CategoryPicker
              lang={lang}
              categories={categories}
              direction={targetDirection}
              defaultCategoryId={targetDefaultCategoryId}
              selectedId={targetSelectedCategoryId}
              onSelect={(categoryId) => {
                updateDraft(categoryTarget.draftId, (draft) => ({
                  ...draft,
                  items: draft.items.map((item) =>
                    item.id === categoryTarget.itemId
                      ? { ...item, categoryId }
                      : item,
                  ),
                }));
                setCategoryTarget(null);
              }}
              onClose={() => setCategoryTarget(null)}
            />
          </div>
        </div>
      ) : null}

      {fieldPicker && fieldPickerDraft ? (
        <div className="finance-modal-backdrop is-open" role="presentation" onClick={() => setFieldPicker(null)}>
          <div className="finance-modal finance-modal--wide is-open finance-flow-category-modal" role="dialog" aria-modal="true" onClick={(event) => event.stopPropagation()}>
            <OptionPicker
              title={fieldPicker.kind === "account" ? text.accountTitle : text.destinationTitle}
              subtitle={fieldPicker.kind === "account" ? text.chooseAccount : text.chooseDestination}
              options={fieldPicker.kind === "account" ? accountPickerOptions : destinationPickerOptions}
              selectedValue={fieldPicker.kind === "account" ? fieldPickerDraft.accountId : fieldPickerDraft.destinationAccountId}
              onClose={() => setFieldPicker(null)}
              onSelect={(value) => {
                updateDraft(fieldPicker.draftId, (draft) => ({
                  ...draft,
                  accountId: fieldPicker.kind === "account" ? value : draft.accountId,
                  currency:
                    fieldPicker.kind === "account"
                      ? (overview.accounts.find((account) => account.id === value)?.currency ?? draft.currency) as Currency
                      : draft.currency,
                  destinationAccountId: fieldPicker.kind === "destination" ? value : draft.destinationAccountId,
                }));
                setFieldPicker(null);
              }}
            />
          </div>
        </div>
      ) : null}

      <input
        ref={fileInputRef}
        type="file"
        accept="image/*,application/pdf,.eml,message/rfc822"
        hidden
        onChange={(event) => {
          const file = event.target.files?.[0];
          if (file) {
            void importFile(file, "file");
          }
          event.target.value = "";
        }}
      />
      <input
        ref={photoInputRef}
        type="file"
        accept="image/*"
        capture="environment"
        hidden
        onChange={(event) => {
          const file = event.target.files?.[0];
          if (file) {
            void importFile(file, "photo");
          }
          event.target.value = "";
        }}
      />
    </>
  );
}
