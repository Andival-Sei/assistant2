export const en = {
  translation: {
    brand: "Assistant",
    nav: {
      login: "Sign in",
      register: "Get started",
    },
    hero: {
      kicker: "Personal ecosystem",
      title: "Assistant for finance, health, and tasks.",
      subtitle:
        "A unified command center: sync across devices, smart insights, and privacy by default.",
      ctaPrimary: "Start free",
      ctaSecondary: "Sign in",
      tiles: [
        { label: "Finance", icon: "F", color: "#23e0a5" },
        { label: "Health", icon: "H", color: "#ff6b6b" },
        { label: "Tasks", icon: "T", color: "#1bb3ff" },
        { label: "Family", icon: "S", color: "#9b6bff" },
      ],
    },
    features: {
      title: "Capabilities",
      subtitle: "Everything you need to stay in control every day.",
      items: [
        {
          title: "Money clarity",
          text: "Budgets, categories, quick input, and clean analytics.",
          icon: "F",
          color: "#23e0a5",
        },
        {
          title: "Health overview",
          text: "Activity, sleep, and reminders in one place.",
          icon: "H",
          color: "#ff6b6b",
        },
        {
          title: "Smart tasks",
          text: "Contextual lists, templates, and focus mode.",
          icon: "T",
          color: "#1bb3ff",
        },
        {
          title: "Family modes",
          text: "Shared budgets and tasks with no noise.",
          icon: "S",
          color: "#9b6bff",
        },
      ],
    },
    capabilities: {
      title: "Why it works",
      subtitle: "Intentional UX that saves time and mental energy.",
      items: [
        {
          title: "Local-first",
          text: "Sensitive data stays on device, sync on demand.",
        },
        {
          title: "Multi‑platform",
          text: "Windows, Android, and Web without compromises.",
        },
        {
          title: "AI hints",
          text: "Summaries and suggestions based on your habits.",
        },
        { title: "Modular", text: "Enable what you need, hide the rest." },
      ],
    },
    how: {
      title: "How it works",
      subtitle: "Three steps to calm daily operations.",
      items: [
        {
          step: "1",
          title: "Create a profile",
          text: "Pick modules and set priorities.",
        },
        {
          step: "2",
          title: "Add your data",
          text: "Finance, health, and tasks stay in sync.",
        },
        {
          step: "3",
          title: "Move with confidence",
          text: "Act on insights and track progress.",
        },
      ],
    },
    trust: {
      items: [
        {
          title: "Security",
          text: "Encryption with clear storage rules.",
          icon: "SEC",
          color: "#23e0a5",
        },
        {
          title: "Speed",
          text: "Instant navigation and fast input.",
          icon: "SPD",
          color: "#1bb3ff",
        },
        {
          title: "Privacy",
          text: "You control what goes to the cloud.",
          icon: "PRV",
          color: "#9b6bff",
        },
      ],
    },
    cta: {
      title: "Ready to bring everything together?",
      subtitle: "Launch Assistant and get your first summary today.",
      primary: "Start free",
      secondary: "Sign in",
    },
    footer: {
      tagline: "Finance, health, and tasks in one app.",
      linksTitle: "Links",
      privacy: "Privacy policy",
      terms: "Terms of use",
      contactTitle: "Contact",
      contact: "support@assistant.app",
      note: "© 2026 Assistant. All rights reserved.",
    },
    auth: {
      titleLogin: "Welcome back",
      subtitleLogin: "Sign in to continue with Assistant.",
      titleRegister: "Create your account",
      subtitleRegister: "One account for Web, Android, and Windows.",
      cardBadge: "Secure access",
      shellTitle: "Secure access to your workspace",
      shellSubtitle:
        "One screen for sign-in, registration, and recovery across all platforms.",
      shellHint:
        "Sessions sync through Supabase Auth, and devices can be remembered without storing the password.",
      google: "Continue with Google",
      googlePending: "Opening Google…",
      googleUnavailable: "Google sign-in will become available after Google Cloud and Supabase OAuth are configured.",
      or: "or",
      email: "Email",
      password: "Password",
      confirmPassword: "Confirm password",
      remember: "Remember this device",
      rememberHint: "Only the secure session is remembered. The password is never stored.",
      forgot: "Forgot password?",
      showPassword: "Show password",
      hidePassword: "Hide password",
      ctaLogin: "Sign in",
      ctaRegister: "Create account",
      ctaSendReset: "Send link",
      ctaUpdatePassword: "Update password",
      checkingEmail: "Checking email…",
      emailAvailable: "Email is available.",
      signOut: "Sign out",
      linkToRegister: "Don't have an account? Sign up",
      linkToLogin: "Already have an account? Sign in",
      forgotTitle: "Password recovery",
      forgotSubtitle: "We will email you a reset link.",
      resetTitle: "New password",
      resetSubtitle: "Choose a new password for your account.",
      callbackTitle: "Finishing sign‑in…",
      success: {
        verifyEmail: "Check your email to confirm, then sign in.",
        resetEmail:
          "Email sent. If the address exists, you will receive a reset link.",
        passwordUpdated: "Password updated. You can sign in now.",
      },
      errors: {
        generic: "Something went wrong. Please try again.",
        passwordMismatch: "Passwords do not match.",
        emailRequired: "Enter your email.",
        emailInvalid: "Enter a valid email address.",
        emailTaken: "This email is already in use.",
        emailNetwork:
          "We could not verify this email right now. Try again or continue later.",
        invalidLogin: "Incorrect email or password.",
        emailNotConfirmed: "Confirm your email first, then sign in.",
        rateLimited: "Too many attempts. Wait a bit and try again.",
        signUpDisabled: "Sign-up is currently unavailable.",
        network: "Network issue. Check your connection and try again.",
        googleRetry:
          "Google sign-in could not be completed. Start the sign-in again in the same browser tab.",
        googleCancelled: "Google sign-in was cancelled.",
        passwordRequired: "Enter your password.",
        passwordLength: "Use at least 8 characters.",
        passwordUppercase: "Add at least one uppercase letter.",
        passwordLowercase: "Add at least one lowercase letter.",
        passwordDigit: "Add at least one number.",
        passwordCommon: "This password is too common. Pick a stronger one.",
        passwordEmail: "Your password should not include your email name.",
        confirmRequired: "Confirm your password.",
        forgotContext: "Open this page from the password recovery email.",
      },
    },
    legal: {
      updated: "Last updated: March 17, 2026",
      privacy: {
        title: "Privacy policy",
        sections: [
          {
            title: "Data we process",
            text: [
              "Email and basic profile data for authentication.",
              "Technical sign-in events for security and recovery.",
            ],
          },
          {
            title: "How we use data",
            text: [
              "Only to deliver the app features and account support.",
              "We do not sell or share data without a valid basis.",
            ],
          },
          {
            title: "Storage and security",
            text: [
              "Data is protected with modern encryption methods.",
              "You can request account and data deletion at any time.",
            ],
          },
        ],
      },
      terms: {
        title: "Terms of use",
        sections: [
          {
            title: "Using the service",
            text: [
              "The service is provided “as is” for personal use.",
              "You are responsible for keeping your account secure.",
            ],
          },
          {
            title: "Restrictions",
            text: [
              "Do not use the service for unlawful activity.",
              "We may suspend access if rules are violated.",
            ],
          },
          {
            title: "Changes",
            text: [
              "We may update the terms and will notify you of major changes.",
            ],
          },
        ],
      },
    },
    common: {
      themeSystem: "Theme: System",
      themeLight: "Theme: Light",
      themeDark: "Theme: Dark",
      langRu: "RU",
      langEn: "EN",
      backToLanding: "Back to landing",
      loading: "Loading…",
    },
  },
} as const;
