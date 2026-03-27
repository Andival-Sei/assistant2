const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
  "Content-Type": "application/json",
};

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/i;

type AvailabilityResponse = {
  valid: boolean;
  available: boolean;
  reason?: "invalid" | "taken" | "unknown";
};

function json(body: AvailabilityResponse, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: corsHeaders,
  });
}

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders });
  }

  if (req.method !== "POST") {
    return json({ valid: false, available: false, reason: "unknown" }, 405);
  }

  const supabaseUrl = Deno.env.get("SUPABASE_URL");
  const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");

  if (!supabaseUrl || !serviceRoleKey) {
    return json({ valid: false, available: false, reason: "unknown" }, 500);
  }

  let email = "";
  try {
    const payload = await req.json();
    email = String(payload?.email ?? "").trim().toLowerCase();
  } catch {
    return json({ valid: false, available: false, reason: "invalid" }, 400);
  }

  if (!emailPattern.test(email)) {
    return json({ valid: false, available: false, reason: "invalid" });
  }

  try {
    let page = 1;
    const perPage = 200;

    while (page <= 10) {
      const response = await fetch(
        `${supabaseUrl}/auth/v1/admin/users?page=${page}&per_page=${perPage}`,
        {
          headers: {
            apikey: serviceRoleKey,
            Authorization: `Bearer ${serviceRoleKey}`,
          },
        },
      );

      if (!response.ok) {
        return json({ valid: true, available: false, reason: "unknown" }, 502);
      }

      const data = await response.json();
      const users = Array.isArray(data?.users) ? data.users : [];
      const exists = users.some((user) => String(user?.email ?? "").toLowerCase() === email);
      if (exists) {
        return json({ valid: true, available: false, reason: "taken" });
      }

      if (users.length < perPage) {
        return json({ valid: true, available: true });
      }

      page += 1;
    }

    return json({ valid: true, available: false, reason: "unknown" }, 429);
  } catch {
    return json({ valid: true, available: false, reason: "unknown" }, 500);
  }
});
