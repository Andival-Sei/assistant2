import type { ReactNode } from "react";
import { useEffect, useState } from "react";
import { Navigate, useLocation } from "react-router-dom";

import type { Session } from "@supabase/supabase-js";

import { supabase } from "../lib/supabaseClient";
import { LoadingScreen } from "../ui/LoadingScreen";

export function RequireAuth({ children }: { children: ReactNode }) {
  const location = useLocation();
  const [session, setSession] = useState<Session | null | undefined>(undefined);

  useEffect(() => {
    let mounted = true;
    void supabase.auth.getSession().then(({ data }) => {
      if (mounted) setSession(data.session);
    });
    const { data: subscription } = supabase.auth.onAuthStateChange(
      (_event, nextSession) => {
        setSession(nextSession);
      },
    );
    return () => {
      mounted = false;
      subscription.subscription.unsubscribe();
    };
  }, []);

  if (session === undefined) return <LoadingScreen />;
  if (!session)
    return <Navigate to="/auth/login" replace state={{ from: location }} />;

  return <>{children}</>;
}
