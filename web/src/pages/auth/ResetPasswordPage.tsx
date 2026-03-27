import { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { AuthScreen } from "../../components/auth/AuthScreen";
import { supabase } from "../../lib/supabaseClient";

export function ResetPasswordPage() {
  const navigate = useNavigate();

  useEffect(() => {
    const run = async () => {
      try {
        const params = new URLSearchParams(window.location.search);
        const code = params.get("code");
        if (code) {
          const { error: exchangeError } = await supabase.auth.exchangeCodeForSession(code);
          if (exchangeError) throw exchangeError;
          window.history.replaceState({}, "", "/auth/reset");
        }
      } catch {
        navigate("/auth/login", { replace: true });
      } finally {
      }
    };

    void run();
  }, [navigate]);

  return <AuthScreen initialMode="reset" />;
}
