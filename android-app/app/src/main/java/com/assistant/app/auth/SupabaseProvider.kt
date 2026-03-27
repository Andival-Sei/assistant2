package com.assistant.app.auth

import com.assistant.app.BuildConfig
import io.github.jan.supabase.SupabaseClient
import io.github.jan.supabase.auth.Auth
import io.github.jan.supabase.auth.ExternalAuthAction
import io.github.jan.supabase.auth.FlowType
import io.github.jan.supabase.createSupabaseClient

object SupabaseProvider {
    private const val defaultSupabaseUrl = "https://oourhsgijmwujektcfih.supabase.co"
    private const val defaultSupabaseAnonKey = "sb_publishable_WtQYhSsi5p3Gx6eGu2oFAw_5CyAVUtQ"

    const val redirectUrl = "assistant://auth/callback"
    val isGoogleAuthEnabled: Boolean = BuildConfig.ENABLE_GOOGLE_AUTH

    val supabaseUrl: String = BuildConfig.SUPABASE_URL.ifBlank { defaultSupabaseUrl }
    val supabaseAnonKey: String = BuildConfig.SUPABASE_ANON_KEY.ifBlank { defaultSupabaseAnonKey }
    val isConfigured: Boolean = supabaseUrl.isNotBlank() && supabaseAnonKey.isNotBlank()

    val client: SupabaseClient by lazy {
        check(isConfigured) {
            "Supabase config is missing."
        }

        createSupabaseClient(
            supabaseUrl = supabaseUrl,
            supabaseKey = supabaseAnonKey
        ) {
            install(Auth) {
                flowType = FlowType.PKCE
                scheme = "assistant"
                host = "auth"
                defaultExternalAuthAction = ExternalAuthAction.CustomTabs()
            }
        }
    }
}
