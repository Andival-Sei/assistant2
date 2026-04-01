import { createClient } from "https://esm.sh/@supabase/supabase-js@2.57.4";
import {
  GoogleGenAI,
  createPartFromUri,
} from "npm:@google/genai@1.25.0";

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers":
    "authorization, x-client-info, apikey, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

const MAX_FILE_BYTES = 15 * 1024 * 1024;
const SUPPORTED_MIME_TYPES = new Set([
  "image/jpeg",
  "image/png",
  "image/webp",
  "image/heic",
  "image/heif",
  "application/pdf",
]);

const responseSchema = {
  type: "object",
  properties: {
    transactions: {
      type: "array",
      items: {
        type: "object",
        properties: {
          title: { type: "string" },
          note: { type: ["string", "null"] },
          merchantName: { type: ["string", "null"] },
          direction: { type: "string", enum: ["expense", "income"] },
          happenedAt: { type: ["string", "null"], format: "date-time" },
          amountMinor: { type: "integer", minimum: 1 },
          accountKindSuggestion: {
            type: ["string", "null"],
            enum: ["cash", "bank_card", null],
          },
          categoryCode: { type: ["string", "null"] },
          items: {
            type: "array",
            items: {
              type: "object",
              properties: {
                title: { type: "string" },
                amountMinor: { type: "integer", minimum: 1 },
                categoryCode: { type: ["string", "null"] },
                confidence: { type: ["number", "null"], minimum: 0, maximum: 1 },
              },
              required: ["title", "amountMinor", "categoryCode", "confidence"],
              additionalProperties: false,
            },
          },
        },
        required: [
          "title",
          "note",
          "merchantName",
          "direction",
          "happenedAt",
          "amountMinor",
          "accountKindSuggestion",
          "categoryCode",
          "items",
        ],
        additionalProperties: false,
      },
    },
  },
  required: ["transactions"],
  additionalProperties: false,
} as const;

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      ...corsHeaders,
      "Content-Type": "application/json",
    },
  });
}

function normalizeMimeType(file: File) {
  const direct = file.type?.trim().toLowerCase();
  if (direct) {
    return direct;
  }

  const name = file.name.toLowerCase();
  if (name.endsWith(".pdf")) return "application/pdf";
  if (name.endsWith(".png")) return "image/png";
  if (name.endsWith(".webp")) return "image/webp";
  if (name.endsWith(".heic")) return "image/heic";
  if (name.endsWith(".heif")) return "image/heif";
  return "image/jpeg";
}

function sanitizeFilename(name: string) {
  return name.replace(/[^a-zA-Z0-9._-]+/g, "-").replace(/-+/g, "-").slice(0, 120);
}

function buildPrompt(categoryCatalog: string) {
  return [
    "Ты извлекаешь финансовые транзакции из чеков и PDF-документов.",
    "Файл может содержать одну или несколько транзакций.",
    "Если видишь обычный кассовый чек, верни одну транзакцию с items.",
    "Если видишь выписку или несколько независимых операций, верни несколько transactions.",
    "Суммы верни в minor units: копейки/центы как integer.",
    "Используй только категории из каталога ниже и только leaf category code. Если не уверен, categoryCode = null.",
    "direction выбирай expense или income.",
    "accountKindSuggestion: cash если явно наличные, bank_card если оплата картой/безналом, иначе null.",
    "Для split-транзакции items должны содержать все позиции. Для single допускается один item.",
    "Если дата не читается точно, happenedAt = null.",
    "Каталог категорий:",
    categoryCatalog,
  ].join("\n");
}

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders });
  }

  if (req.method !== "POST") {
    return json(405, { error: "Method not allowed" });
  }

  try {
    const supabaseUrl = Deno.env.get("SUPABASE_URL");
    const supabaseAnonKey = Deno.env.get("SUPABASE_ANON_KEY");
    const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");

    if (!supabaseUrl || !supabaseAnonKey || !serviceRoleKey) {
      return json(500, { error: "Missing Supabase environment" });
    }

    const authHeader =
      req.headers.get("Authorization") ?? req.headers.get("authorization");
    if (!authHeader) {
      return json(401, { error: "Missing Authorization header" });
    }

    const token = authHeader.startsWith("Bearer ")
      ? authHeader.slice("Bearer ".length).trim()
      : authHeader.trim();

    const userClient = createClient(supabaseUrl, supabaseAnonKey, {
      global: {
        headers: {
          Authorization: `Bearer ${token}`,
        },
      },
      auth: {
        autoRefreshToken: false,
        persistSession: false,
      },
    });

    const adminClient = createClient(supabaseUrl, serviceRoleKey, {
      auth: {
        autoRefreshToken: false,
        persistSession: false,
      },
    });

    const {
      data: { user },
      error: userError,
    } = await userClient.auth.getUser(token);

    if (userError || !user) {
      return json(401, {
        error: "Unauthorized",
        details: userError?.message,
      });
    }

    const formData = await req.formData();
    const fileValue = formData.get("file");
    const sourceKindValue = formData.get("sourceKind");

    if (!(fileValue instanceof File)) {
      return json(400, { error: "Missing file" });
    }

    const file = fileValue;
    const mimeType = normalizeMimeType(file);
    if (!SUPPORTED_MIME_TYPES.has(mimeType)) {
      if (file.name.toLowerCase().endsWith(".eml")) {
        return json(422, {
          error: "EML parsing is planned but not enabled yet.",
          code: "eml_not_supported_yet",
        });
      }
      return json(422, { error: "Unsupported file type" });
    }

    if (file.size <= 0 || file.size > MAX_FILE_BYTES) {
      return json(422, {
        error: "File size must be between 1 byte and 15 MiB",
      });
    }

    const { data: settingsRow, error: settingsError } = await adminClient
      .from("user_settings")
      .select("gemini_api_key")
      .eq("user_id", user.id)
      .maybeSingle();

    if (settingsError) {
      return json(500, {
        error: "Failed to load user settings",
        details: settingsError.message,
      });
    }

    const geminiApiKey = settingsRow?.gemini_api_key?.trim();
    if (!geminiApiKey) {
      return json(400, {
        error: "Gemini API key is missing in settings",
        code: "missing_gemini_api_key",
      });
    }

    const { data: categories, error: categoriesError } = await adminClient
      .from("finance_categories")
      .select("id, parent_id, direction, code, name")
      .eq("user_id", user.id)
      .eq("is_archived", false);

    if (categoriesError) {
      return json(500, {
        error: "Failed to load categories",
        details: categoriesError.message,
      });
    }

    const parentIds = new Set(
      (categories ?? [])
        .map((item) => item.parent_id as string | null)
        .filter((value): value is string => Boolean(value)),
    );
    const leafCategories = (categories ?? []).filter(
      (item) => !parentIds.has(item.id),
    );
    const categoryByCode = new Map(
      (categories ?? []).map((item) => [item.code, item.id] as const),
    );
    const categoryCatalog = leafCategories
      .map((item) => `${item.direction}:${item.code}:${item.name}`)
      .join("\n");

    const storagePath =
      `${user.id}/${new Date().toISOString().slice(0, 10)}/` +
      `${crypto.randomUUID()}-${sanitizeFilename(file.name || "receipt")}`;

    const { error: uploadError } = await adminClient.storage
      .from("finance-receipts")
      .upload(storagePath, file, {
        contentType: mimeType,
        upsert: false,
      });

    if (uploadError) {
      return json(500, {
        error: "Failed to upload source file",
        details: uploadError.message,
      });
    }

    const ai = new GoogleGenAI({ apiKey: geminiApiKey });
    const uploadedFile = await ai.files.upload({
      file,
      config: {
        mimeType,
        displayName: file.name || "receipt",
      },
    });

    const response = await ai.models.generateContent({
      model: "gemini-2.5-flash",
      contents: [
        {
          role: "user",
          parts: [
            { text: buildPrompt(categoryCatalog) },
            createPartFromUri(uploadedFile.uri!, uploadedFile.mimeType!),
          ],
        },
      ],
      config: {
        responseMimeType: "application/json",
        responseJsonSchema: responseSchema,
      },
    });

    const parsed = JSON.parse(response.text ?? "{\"transactions\":[]}") as {
      transactions?: Array<{
        title?: string;
        note?: string | null;
        merchantName?: string | null;
        direction?: "expense" | "income";
        happenedAt?: string | null;
        amountMinor?: number;
        accountKindSuggestion?: "cash" | "bank_card" | null;
        categoryCode?: string | null;
        items?: Array<{
          title?: string;
          amountMinor?: number;
          categoryCode?: string | null;
          confidence?: number | null;
        }>;
      }>;
    };

    const drafts = (parsed.transactions ?? [])
      .map((transaction) => {
        const normalizedItems = (transaction.items ?? [])
          .filter((item) => item.title && Number(item.amountMinor) > 0)
          .map((item, index) => ({
            title: item.title!.trim(),
            amountMinor: Math.round(Number(item.amountMinor)),
            suggestedCategoryCode: item.categoryCode ?? null,
            suggestedCategoryId: item.categoryCode
              ? (categoryByCode.get(item.categoryCode) ?? null)
              : null,
            suggestedCategoryName: item.categoryCode
              ? (leafCategories.find((category) => category.code === item.categoryCode)?.name ?? null)
              : null,
            suggestedCategoryPath: null,
          }));

        const total =
          normalizedItems.reduce((sum, item) => sum + item.amountMinor, 0) ||
          Math.round(Number(transaction.amountMinor ?? 0));

        if (!transaction.title || total <= 0) {
          return null;
        }

        return {
          title: transaction.title.trim(),
          note: transaction.note?.trim() || null,
          merchantName: transaction.merchantName?.trim() || null,
          direction: transaction.direction === "income" ? "income" : "expense",
          happenedAt: transaction.happenedAt ?? null,
          amountMinor: total,
          transactionKind: normalizedItems.length > 1 ? "split" : "single",
          currency: "RUB",
          sourceType:
            sourceKindValue === "camera" ? "photo" : "file",
          documentKind: mimeType === "application/pdf" ? "pdf" : "image",
          items:
            normalizedItems.length > 0
              ? normalizedItems
              : [
                  {
                    title: transaction.title.trim(),
                    amountMinor: total,
                    suggestedCategoryCode: transaction.categoryCode ?? null,
                    suggestedCategoryId: transaction.categoryCode
                      ? (categoryByCode.get(transaction.categoryCode) ?? null)
                      : null,
                    suggestedCategoryName: transaction.categoryCode
                      ? (leafCategories.find((category) => category.code === transaction.categoryCode)?.name ?? null)
                      : null,
                    suggestedCategoryPath: null,
                  },
                ],
        };
      })
      .filter((item): item is NonNullable<typeof item> => item !== null);

    return json(200, {
      sourceType:
        sourceKindValue === "camera" ? "photo" : "file",
      fileName: file.name,
      documentKind: mimeType === "application/pdf" ? "pdf" : "image",
      warnings: [],
      storagePath,
      drafts,
    });
  } catch (error) {
    return json(500, {
      error: error instanceof Error ? error.message : "Unknown error",
    });
  }
});
