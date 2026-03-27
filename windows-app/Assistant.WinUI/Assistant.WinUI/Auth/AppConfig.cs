using System;

namespace Assistant.WinUI.Auth
{
    internal static class AppConfig
    {
        private const string DefaultSupabaseUrl = "https://oourhsgijmwujektcfih.supabase.co";
        private const string DefaultSupabaseAnonKey = "sb_publishable_WtQYhSsi5p3Gx6eGu2oFAw_5CyAVUtQ";

        public static string SupabaseUrl =>
            Environment.GetEnvironmentVariable("ASSISTANT_SUPABASE_URL") ?? DefaultSupabaseUrl;

        public static string SupabaseAnonKey =>
            Environment.GetEnvironmentVariable("ASSISTANT_SUPABASE_ANON_KEY") ?? DefaultSupabaseAnonKey;

        public static bool GoogleAuthEnabled =>
            !bool.TryParse(Environment.GetEnvironmentVariable("ASSISTANT_ENABLE_GOOGLE_AUTH"), out var enabled) ||
            enabled;

        public static string RedirectUri => "assistant://auth/callback";
        public static string GoogleLoopbackRedirectUri => "http://127.0.0.1:43123/auth/callback/";
    }
}
