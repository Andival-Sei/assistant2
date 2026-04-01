import { createClient } from "https://esm.sh/@supabase/supabase-js@2.57.4";

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers":
    "authorization, x-client-info, apikey, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

const MAX_FILE_BYTES = 12 * 1024 * 1024;
const GOOGLE_API_BASE = "https://generativelanguage.googleapis.com/v1beta";

type CategoryRecord = {
  id: string;
  parentId: string | null;
  direction: "income" | "expense";
  code: string;
  name: string;
  icon: string | null;
  color: string | null;
  displayOrder: number;
};

type ImportDraftItem = {
  title: string;
  amountMinor: number;
  suggestedCategoryCode: string | null;
  suggestedCategoryId: string | null;
  suggestedCategoryName: string | null;
  suggestedCategoryPath: string | null;
};

type ImportDraftTransaction = {
  title: string;
  merchantName: string | null;
  note: string | null;
  direction: "income" | "expense";
  transactionKind: "single" | "split";
  amountMinor: number;
  currency: "RUB" | "USD" | "EUR";
  happenedAt: string | null;
  sourceType: "photo" | "file";
  documentKind: "image" | "pdf" | "eml";
  items: ImportDraftItem[];
};

type AttachmentPart = {
  mimeType: string;
  filename: string;
  bytes: Uint8Array;
};

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      ...corsHeaders,
      "Content-Type": "application/json",
    },
  });
}

function normalizeMimeType(file: File) {
  const fileName = file.name.toLowerCase();
  const mimeType = (file.type || "").toLowerCase();

  if (mimeType.startsWith("image/")) return mimeType;
  if (mimeType === "application/pdf" || fileName.endsWith(".pdf")) {
    return "application/pdf";
  }
  if (
    mimeType === "message/rfc822" ||
    fileName.endsWith(".eml") ||
    fileName.endsWith(".email")
  ) {
    return "message/rfc822";
  }

  return mimeType || "application/octet-stream";
}

function inferDocumentKind(mimeType: string): "image" | "pdf" | "eml" | null {
  if (mimeType.startsWith("image/")) return "image";
  if (mimeType === "application/pdf") return "pdf";
  if (mimeType === "message/rfc822") return "eml";
  return null;
}

function bytesToBase64(bytes: Uint8Array) {
  let binary = "";
  const chunkSize = 0x8000;
  for (let index = 0; index < bytes.length; index += chunkSize) {
    const chunk = bytes.subarray(index, index + chunkSize);
    binary += String.fromCharCode(...chunk);
  }
  return btoa(binary);
}

function base64ToBytes(value: string) {
  const normalized = value.replace(/\s+/g, "");
  const binary = atob(normalized);
  const result = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index += 1) {
    result[index] = binary.charCodeAt(index);
  }
  return result;
}

function stripHtml(value: string) {
  return value
    .replace(/<style[\s\S]*?<\/style>/gi, " ")
    .replace(/<script[\s\S]*?<\/script>/gi, " ")
    .replace(/<[^>]+>/g, " ")
    .replace(/&nbsp;/g, " ")
    .replace(/&amp;/g, "&")
    .replace(/\s+/g, " ")
    .trim();
}

function decodeText(bytes: Uint8Array) {
  try {
    return new TextDecoder("utf-8", { fatal: false }).decode(bytes);
  } catch {
    return new TextDecoder().decode(bytes);
  }
}

function parseHeaderLines(rawHeaders: string) {
  const headers = new Map<string, string>();
  const lines = rawHeaders.split(/\r?\n/);
  let currentKey: string | null = null;
  let currentValue = "";

  for (const line of lines) {
    if (/^\s/.test(line) && currentKey) {
      currentValue += ` ${line.trim()}`;
      continue;
    }

    if (currentKey) {
      headers.set(currentKey, currentValue.trim());
    }

    const separator = line.indexOf(":");
    if (separator === -1) {
      currentKey = null;
      currentValue = "";
      continue;
    }

    currentKey = line.slice(0, separator).trim().toLowerCase();
    currentValue = line.slice(separator + 1).trim();
  }

  if (currentKey) {
    headers.set(currentKey, currentValue.trim());
  }

  return headers;
}

function getHeaderParam(value: string | undefined, key: string) {
  if (!value) return null;
  const match = value.match(new RegExp(`${key}="?([^";]+)"?`, "i"));
  return match?.[1] ?? null;
}

function parseEml(bytes: Uint8Array) {
  const raw = decodeText(bytes);
  const [, headerPart = "", bodyPart = ""] = raw.match(/^([\s\S]*?)\r?\n\r?\n([\s\S]*)$/) ?? [];
  const headers = parseHeaderLines(headerPart);
  const contentType = headers.get("content-type") ?? "";
  const boundary = getHeaderParam(contentType, "boundary");

  const textSegments: string[] = [];
  const attachments: AttachmentPart[] = [];

  const pushTextPart = (mimeType: string, content: string) => {
    if (!content.trim()) return;
    if (mimeType.includes("html")) {
      textSegments.push(stripHtml(content));
      return;
    }
    textSegments.push(content.replace(/\s+/g, " ").trim());
  };

  if (!boundary) {
    pushTextPart(contentType, bodyPart);
    return {
      plainText: textSegments.join("\n\n").trim(),
      attachments,
    };
  }

  const boundaryMarker = `--${boundary}`;
  const rawParts = bodyPart.split(boundaryMarker);

  for (const rawPart of rawParts) {
    const part = rawPart.trim();
    if (!part || part === "--") continue;

    const normalizedPart = part.replace(/^--/, "").trim();
    const match = normalizedPart.match(/^([\s\S]*?)\r?\n\r?\n([\s\S]*)$/);
    if (!match) continue;

    const partHeaders = parseHeaderLines(match[1]);
    const partBody = match[2].replace(/\r?\n--$/, "").trim();
    const partContentType = (partHeaders.get("content-type") ?? "text/plain").toLowerCase();
    const disposition = partHeaders.get("content-disposition") ?? "";
    const transferEncoding = (partHeaders.get("content-transfer-encoding") ?? "").toLowerCase();
    const filename =
      getHeaderParam(disposition, "filename") ??
      getHeaderParam(partHeaders.get("content-type"), "name") ??
      "attachment";

    if (
      (partContentType.startsWith("image/") || partContentType === "application/pdf") &&
      transferEncoding.includes("base64")
    ) {
      try {
        attachments.push({
          mimeType: partContentType,
          filename,
          bytes: base64ToBytes(partBody),
        });
      } catch {
        // Ignore malformed attachments and continue with text fallback.
      }
      continue;
    }

    if (partContentType.startsWith("text/")) {
      pushTextPart(partContentType, partBody);
    }
  }

  return {
    plainText: textSegments.join("\n\n").trim(),
    attachments,
  };
}

function buildCategoryCatalog(categories: CategoryRecord[]) {
  const byId = new Map(categories.map((category) => [category.id, category]));

  const resolvePath = (category: CategoryRecord) => {
    const chain: string[] = [];
    let current: CategoryRecord | undefined = category;

    while (current) {
      chain.unshift(current.name);
      current = current.parentId ? byId.get(current.parentId) : undefined;
    }

    return chain.join(" > ");
  };

  return categories.map((category) => ({
    ...category,
    path: resolvePath(category),
  }));
}

function extractJsonEnvelope(payload: unknown) {
  const text =
    typeof payload === "string"
      ? payload
      : JSON.stringify(payload);
  const match = text.match(/\{[\s\S]*\}/);
  if (!match) {
    throw new Error("Gemini returned no JSON payload.");
  }
  return JSON.parse(match[0]);
}

async function resolveGeminiModel(apiKey: string) {
  const response = await fetch(`${GOOGLE_API_BASE}/models`, {
    headers: { "x-goog-api-key": apiKey },
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(errorText || "Failed to list Gemini models.");
  }

  const payload = (await response.json()) as {
    models?: Array<{
      name: string;
      supportedGenerationMethods?: string[];
      supportedActions?: string[];
    }>;
  };

  const models = (payload.models ?? []).filter((model) => {
    const methods = model.supportedGenerationMethods ?? [];
    const actions = model.supportedActions ?? [];
    return methods.includes("generateContent") || actions.includes("generateContent");
  });

  const preferred = [
    "models/gemini-2.5-flash",
    "models/gemini-2.5-flash-lite",
  ];

  for (const candidate of preferred) {
    if (models.some((model) => model.name === candidate)) {
      return candidate;
    }
  }

  if (!models.length) {
    throw new Error("No Gemini model with generateContent is available for this API key.");
  }

  return models[0].name;
}

async function uploadGeminiFile(apiKey: string, bytes: Uint8Array, mimeType: string, filename: string) {
  const startResponse = await fetch(`${GOOGLE_API_BASE}/upload/files?key=${apiKey}`, {
    method: "POST",
    headers: {
      "X-Goog-Upload-Protocol": "resumable",
      "X-Goog-Upload-Command": "start",
      "X-Goog-Upload-Header-Content-Length": String(bytes.byteLength),
      "X-Goog-Upload-Header-Content-Type": mimeType,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      file: {
        display_name: filename,
      },
    }),
  });

  if (!startResponse.ok) {
    const errorText = await startResponse.text();
    throw new Error(errorText || "Failed to initialize Gemini file upload.");
  }

  const uploadUrl = startResponse.headers.get("x-goog-upload-url");
  if (!uploadUrl) {
    throw new Error("Gemini file upload URL is missing.");
  }

  const uploadResponse = await fetch(uploadUrl, {
    method: "POST",
    headers: {
      "Content-Length": String(bytes.byteLength),
      "X-Goog-Upload-Offset": "0",
      "X-Goog-Upload-Command": "upload, finalize",
    },
    body: bytes,
  });

  if (!uploadResponse.ok) {
    const errorText = await uploadResponse.text();
    throw new Error(errorText || "Failed to upload file to Gemini.");
  }

  const uploaded = await uploadResponse.json() as {
    file?: {
      name: string;
      uri: string;
      mime_type?: string;
      mimeType?: string;
      state?: string;
    };
  };

  const fileName = uploaded.file?.name;
  if (!fileName) {
    throw new Error("Gemini upload did not return a file identifier.");
  }

  let currentFile = uploaded.file;
  for (let attempt = 0; attempt < 6; attempt += 1) {
    if (!currentFile?.state || currentFile.state === "ACTIVE") break;
    if (currentFile.state === "FAILED") {
      throw new Error("Gemini file processing failed.");
    }

    await new Promise((resolve) => setTimeout(resolve, 1500));
    const statusResponse = await fetch(`${GOOGLE_API_BASE}/${fileName}?key=${apiKey}`);
    if (!statusResponse.ok) break;
    const statusPayload = await statusResponse.json() as { name?: string; uri?: string; mimeType?: string; mime_type?: string; state?: string };
    currentFile = {
      name: statusPayload.name ?? fileName,
      uri: statusPayload.uri ?? currentFile?.uri ?? "",
      mimeType: statusPayload.mimeType ?? statusPayload.mime_type,
      state: statusPayload.state,
    };
  }

  if (!currentFile?.uri) {
    throw new Error("Gemini file URI is missing after upload.");
  }

  return {
    fileUri: currentFile.uri,
    mimeType: currentFile.mimeType ?? currentFile.mime_type ?? mimeType,
  };
}

async function buildGeminiParts(
  apiKey: string,
  file: File,
  mimeType: string,
  documentKind: "image" | "pdf" | "eml",
) {
  const bytes = new Uint8Array(await file.arrayBuffer());
  const parts: Array<Record<string, unknown>> = [];

  if (documentKind === "image") {
    parts.push({
      inline_data: {
        mime_type: mimeType,
        data: bytesToBase64(bytes),
      },
    });
    return parts;
  }

  if (documentKind === "pdf") {
    const { fileUri, mimeType: uploadedMimeType } = await uploadGeminiFile(
      apiKey,
      bytes,
      mimeType,
      file.name || "receipt.pdf",
    );
    parts.push({
      file_data: {
        mime_type: uploadedMimeType,
        file_uri: fileUri,
      },
    });
    return parts;
  }

  const eml = parseEml(bytes);
  const summaryText = eml.plainText || `EML file: ${file.name}`;
  parts.push({
    text: `Email source:\n${summaryText}`,
  });

  for (const attachment of eml.attachments.slice(0, 3)) {
    if (attachment.mimeType === "application/pdf") {
      const { fileUri, mimeType: uploadedMimeType } = await uploadGeminiFile(
        apiKey,
        attachment.bytes,
        attachment.mimeType,
        attachment.filename,
      );
      parts.push({
        file_data: {
          mime_type: uploadedMimeType,
          file_uri: fileUri,
        },
      });
      continue;
    }

    if (attachment.mimeType.startsWith("image/")) {
      parts.push({
        inline_data: {
          mime_type: attachment.mimeType,
          data: bytesToBase64(attachment.bytes),
        },
      });
    }
  }

  return parts;
}

function normalizeMinor(value: unknown) {
  const numberValue = typeof value === "number" ? value : Number(value);
  if (!Number.isFinite(numberValue) || numberValue <= 0) return 0;
  return Math.round(numberValue);
}

function normalizeCurrency(value: unknown): "RUB" | "USD" | "EUR" {
  const normalized = String(value ?? "RUB").trim().toUpperCase();
  if (normalized === "USD" || normalized === "EUR") return normalized;
  return "RUB";
}

function mapDrafts(
  raw: unknown,
  categories: ReturnType<typeof buildCategoryCatalog>,
  sourceType: "photo" | "file",
  documentKind: "image" | "pdf" | "eml",
) {
  const payload = typeof raw === "object" && raw !== null ? raw as Record<string, unknown> : {};
  const rawTransactions = Array.isArray(payload.transactions) ? payload.transactions : [];

  const categoryByCode = new Map(categories.map((category) => [category.code, category]));

  return rawTransactions.map((transaction) => {
    const tx = transaction as Record<string, unknown>;
    const items = Array.isArray(tx.items) ? tx.items : [];

    const mappedItems = items
      .map((item) => {
        const current = item as Record<string, unknown>;
        const categoryCode =
          typeof current.suggestedCategoryCode === "string"
            ? current.suggestedCategoryCode
            : null;
        const category = categoryCode ? categoryByCode.get(categoryCode) : undefined;
        const amountMinor = normalizeMinor(current.amountMinor);
        if (!amountMinor) return null;

        return {
          title: String(current.title ?? "Позиция").trim() || "Позиция",
          amountMinor,
          suggestedCategoryCode: category?.code ?? categoryCode,
          suggestedCategoryId: category?.id ?? null,
          suggestedCategoryName: category?.name ?? null,
          suggestedCategoryPath: category?.path ?? null,
        } satisfies ImportDraftItem;
      })
      .filter((item): item is ImportDraftItem => Boolean(item));

    const amountMinor =
      normalizeMinor(tx.amountMinor) ||
      mappedItems.reduce((total, item) => total + item.amountMinor, 0);

    return {
      title: String(tx.title ?? tx.merchantName ?? "Новая транзакция").trim() || "Новая транзакция",
      merchantName: typeof tx.merchantName === "string" ? tx.merchantName.trim() || null : null,
      note: typeof tx.note === "string" ? tx.note.trim() || null : null,
      direction: tx.direction === "income" ? "income" : "expense",
      transactionKind: mappedItems.length > 1 ? "split" : "single",
      amountMinor,
      currency: normalizeCurrency(tx.currency),
      happenedAt: typeof tx.happenedAt === "string" ? tx.happenedAt : null,
      sourceType,
      documentKind,
      items: mappedItems.length
        ? mappedItems
        : [
            {
              title: String(tx.title ?? tx.merchantName ?? "Позиция").trim() || "Позиция",
              amountMinor,
              suggestedCategoryCode: null,
              suggestedCategoryId: null,
              suggestedCategoryName: null,
              suggestedCategoryPath: null,
            },
          ],
    } satisfies ImportDraftTransaction;
  }).filter((draft) => draft.amountMinor > 0);
}

Deno.serve(async (request) => {
  if (request.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders });
  }

  if (request.method !== "POST") {
    return jsonResponse({ error: "Method not allowed." }, 405);
  }

  try {
    const supabaseUrl = Deno.env.get("SUPABASE_URL");
    const supabaseAnonKey = Deno.env.get("SUPABASE_ANON_KEY");
    const supabaseServiceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
    const authorization = request.headers.get("Authorization");

    if (!supabaseUrl || !supabaseAnonKey || !supabaseServiceRoleKey) {
      throw new Error("Supabase function secrets are not configured.");
    }

    if (!authorization?.startsWith("Bearer ")) {
      return jsonResponse({ error: "Missing bearer token." }, 401);
    }

    const accessToken = authorization.slice("Bearer ".length).trim();
    const userClient = createClient(supabaseUrl, supabaseAnonKey, {
      global: {
        headers: {
          Authorization: `Bearer ${accessToken}`,
        },
      },
    });
    const adminClient = createClient(supabaseUrl, supabaseServiceRoleKey);

    const {
      data: { user },
      error: userError,
    } = await userClient.auth.getUser(accessToken);

    if (userError || !user) {
      return jsonResponse({ error: userError?.message || "Unauthorized." }, 401);
    }

    const formData = await request.formData();
    const fileEntry = formData.get("file");
    const sourceTypeEntry = formData.get("sourceType") ?? formData.get("sourceKind");
    const sourceType =
      sourceTypeEntry === "photo" || sourceTypeEntry === "camera" ? "photo" : "file";

    if (!(fileEntry instanceof File)) {
      return jsonResponse({ error: "File is required." }, 400);
    }

    if (fileEntry.size <= 0 || fileEntry.size > MAX_FILE_BYTES) {
      return jsonResponse(
        { error: `File size must be between 1 byte and ${MAX_FILE_BYTES} bytes.` },
        400,
      );
    }

    const mimeType = normalizeMimeType(fileEntry);
    const documentKind = inferDocumentKind(mimeType);
    if (!documentKind) {
      return jsonResponse(
        { error: "Supported formats: image, PDF, and EML." },
        400,
      );
    }

    const { data: settingsRow, error: settingsError } = await adminClient
      .from("user_settings")
      .select("gemini_api_key, ai_enhancements_enabled")
      .eq("user_id", user.id)
      .maybeSingle<{ gemini_api_key: string | null; ai_enhancements_enabled: boolean | null }>();

    if (settingsError) {
      throw new Error(settingsError.message);
    }

    const aiEnhancementsEnabled = settingsRow?.ai_enhancements_enabled ?? true;
    if (!aiEnhancementsEnabled) {
      return jsonResponse(
        {
          error:
            "AI enhancements are disabled. Enable them in Settings to extract transactions from photos and files.",
        },
        400,
      );
    }

    const geminiApiKey = settingsRow?.gemini_api_key?.trim();
    if (!geminiApiKey) {
      return jsonResponse(
        {
          error:
            "Gemini API key is missing. Add it in Settings before importing receipts.",
        },
        400,
      );
    }

    const categoriesResponse = await fetch(`${supabaseUrl}/rest/v1/rpc/finance_get_categories`, {
      method: "POST",
      headers: {
        apikey: supabaseAnonKey,
        Authorization: `Bearer ${accessToken}`,
        "Content-Type": "application/json",
      },
      body: "{}",
    });

    if (!categoriesResponse.ok) {
      throw new Error(await categoriesResponse.text());
    }

    const categories = buildCategoryCatalog((await categoriesResponse.json()) as CategoryRecord[]);
    const categoryPrompt = categories
      .map((category) => `${category.code} | ${category.direction} | ${category.path}`)
      .join("\n");

    const modelName = await resolveGeminiModel(geminiApiKey);
    const fileParts = await buildGeminiParts(
      geminiApiKey,
      fileEntry,
      mimeType,
      documentKind,
    );

    const prompt = [
      "You extract finance transactions from receipts, PDFs, and email receipts.",
      "Return strict JSON only.",
      "If the document contains multiple receipts or multiple distinct transactions, return each of them.",
      "Use amountMinor in integer minor units only.",
      "Prefer expense unless the document clearly indicates income or refund.",
      "Pick suggestedCategoryCode only from the provided catalog. Use null when nothing fits.",
      "Each item should preserve the original line title from the receipt when possible.",
      "Categories catalog:",
      categoryPrompt,
    ].join("\n");

    const response = await fetch(`${GOOGLE_API_BASE}/${modelName}:generateContent`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "x-goog-api-key": geminiApiKey,
      },
      body: JSON.stringify({
        contents: [
          {
            role: "user",
            parts: [{ text: prompt }, ...fileParts],
          },
        ],
        generationConfig: {
          responseMimeType: "application/json",
          responseSchema: {
            type: "OBJECT",
            properties: {
              transactions: {
                type: "ARRAY",
                items: {
                  type: "OBJECT",
                  properties: {
                    title: { type: "STRING" },
                    merchantName: { type: "STRING", nullable: true },
                    note: { type: "STRING", nullable: true },
                    direction: { type: "STRING" },
                    amountMinor: { type: "NUMBER" },
                    currency: { type: "STRING" },
                    happenedAt: { type: "STRING", nullable: true },
                    items: {
                      type: "ARRAY",
                      items: {
                        type: "OBJECT",
                        properties: {
                          title: { type: "STRING" },
                          amountMinor: { type: "NUMBER" },
                          suggestedCategoryCode: { type: "STRING", nullable: true },
                        },
                        required: ["title", "amountMinor", "suggestedCategoryCode"],
                      },
                    },
                  },
                  required: [
                    "title",
                    "merchantName",
                    "note",
                    "direction",
                    "amountMinor",
                    "currency",
                    "happenedAt",
                    "items",
                  ],
                },
              },
              warnings: {
                type: "ARRAY",
                items: { type: "STRING" },
              },
            },
            required: ["transactions", "warnings"],
          },
        },
      }),
    });

    if (!response.ok) {
      throw new Error(await response.text());
    }

    const payload = await response.json();
    const rawJson = payload?.candidates?.[0]?.content?.parts?.[0]?.text ?? payload;
    const parsed = extractJsonEnvelope(rawJson);
    const drafts = mapDrafts(parsed, categories, sourceType, documentKind);

    if (!drafts.length) {
      return jsonResponse(
        {
          error: "No transactions were detected in the document.",
          warnings: Array.isArray(parsed?.warnings) ? parsed.warnings : [],
        },
        422,
      );
    }

    return jsonResponse({
      drafts,
      warnings: Array.isArray(parsed?.warnings) ? parsed.warnings : [],
      documentKind,
      sourceType,
      fileName: fileEntry.name,
    });
  } catch (error) {
    console.error("process-finance-import failed", error);
    return jsonResponse(
      {
        error: error instanceof Error ? error.message : "Unexpected import error.",
      },
      500,
    );
  }
});
