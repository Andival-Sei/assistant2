import { createBrowserRouter, Navigate } from "react-router-dom";

import { AppHomePage } from "../pages/app/AppHomePage";
import { LandingPage } from "../pages/LandingPage";
import { AuthCallbackPage } from "../pages/auth/AuthCallbackPage";
import { ForgotPasswordPage } from "../pages/auth/ForgotPasswordPage";
import { LoginPage } from "../pages/auth/LoginPage";
import { RegisterPage } from "../pages/auth/RegisterPage";
import { ResetPasswordPage } from "../pages/auth/ResetPasswordPage";
import { LegalPage } from "../pages/LegalPage";
import { RequireAuth } from "../providers/RequireAuth";

export const router = createBrowserRouter([
  { path: "/", element: <LandingPage /> },

  { path: "/auth/login", element: <LoginPage /> },
  { path: "/auth/register", element: <RegisterPage /> },
  { path: "/auth/forgot", element: <ForgotPasswordPage /> },
  { path: "/auth/reset", element: <ResetPasswordPage /> },
  { path: "/auth/callback", element: <AuthCallbackPage /> },

  { path: "/privacy", element: <LegalPage type="privacy" /> },
  { path: "/terms", element: <LegalPage type="terms" /> },

  {
    path: "/app",
    element: (
      <RequireAuth>
        <AppHomePage />
      </RequireAuth>
    ),
  },

  { path: "*", element: <Navigate to="/auth/login" replace /> },
]);
