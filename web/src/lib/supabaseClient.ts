import { createClient } from "@supabase/supabase-js";
import { authStorage, clearStoredAuthSession } from "./authStorage";

export const supabaseUrl = import.meta.env.VITE_SUPABASE_URL as string | undefined;
export const supabaseAnonKey = import.meta.env.VITE_SUPABASE_ANON_KEY as
  | string
  | undefined;
export const googleAuthEnabled =
  import.meta.env.VITE_ENABLE_GOOGLE_AUTH === "true";

if (!supabaseUrl || !supabaseAnonKey) {
  throw new Error(
    "Supabase env is missing. Set VITE_SUPABASE_URL and VITE_SUPABASE_ANON_KEY in web/.env.local",
  );
}

export const supabase = createClient(supabaseUrl, supabaseAnonKey, {
  auth: {
    detectSessionInUrl: true,
    flowType: "pkce",
    storage: authStorage,
  },
});

export async function checkEmailAvailability(email: string) {
  const { data, error } = await supabase.functions.invoke<{
    valid: boolean;
    available: boolean;
    reason?: "invalid" | "taken" | "unknown";
  }>("auth-email-availability", {
    body: { email },
  });

  if (error) throw error;
  return data;
}

export async function signOutEverywhere() {
  await supabase.auth.signOut();
  clearStoredAuthSession();
}
