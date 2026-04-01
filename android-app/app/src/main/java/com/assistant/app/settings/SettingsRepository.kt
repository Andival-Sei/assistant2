package com.assistant.app.settings

import com.assistant.app.auth.SupabaseProvider
import io.github.jan.supabase.auth.auth
import io.github.jan.supabase.auth.providers.Google
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONArray
import org.json.JSONObject
import java.io.BufferedReader
import java.io.OutputStreamWriter
import java.net.HttpURLConnection
import java.net.URLEncoder
import java.net.URL
import java.nio.charset.StandardCharsets

data class LinkedIdentity(
    val identityId: String?,
    val provider: String
)

data class SettingsSnapshot(
    val userId: String,
    val email: String,
    val displayName: String,
    val geminiApiKey: String,
    val identities: List<LinkedIdentity>
)

class SettingsRepository {
    suspend fun load(): SettingsSnapshot = withContext(Dispatchers.IO) {
        val user = SupabaseProvider.client.auth.currentUserOrNull()
            ?: error("No active Supabase user.")

        val userId = user.id
        val email = user.email.orEmpty()
        val profileResponse = executeRestRequest(
            method = "GET",
            path = "/rest/v1/profiles?select=display_name,email&id=eq.${encode(userId)}"
        )
        val settingsResponse = executeRestRequest(
            method = "GET",
            path = "/rest/v1/user_settings?select=gemini_api_key&user_id=eq.${encode(userId)}"
        )

        val profileArray = JSONArray(profileResponse)
        val settingsArray = JSONArray(settingsResponse)
        val profile = profileArray.optJSONObject(0)
        val settings = settingsArray.optJSONObject(0)
        val identities = SupabaseProvider.client.auth.currentIdentitiesOrNull()
            ?.map { LinkedIdentity(identityId = it.identityId, provider = it.provider) }
            ?: emptyList()

        SettingsSnapshot(
            userId = userId,
            email = email,
            displayName = profile?.optString("display_name").orEmpty(),
            geminiApiKey = settings?.optString("gemini_api_key").orEmpty(),
            identities = identities
        )
    }

    suspend fun saveDisplayName(displayName: String) = withContext(Dispatchers.IO) {
        val user = SupabaseProvider.client.auth.currentUserOrNull()
            ?: error("No active Supabase user.")

        val payload = JSONArray().put(
            JSONObject()
                .put("id", user.id)
                .put("email", user.email ?: JSONObject.NULL)
                .put("display_name", displayName)
        )

        executeRestRequest(
            method = "POST",
            path = "/rest/v1/profiles?on_conflict=id",
            body = payload,
            prefer = "resolution=merge-duplicates"
        )
    }

    suspend fun saveGeminiApiKey(value: String) = withContext(Dispatchers.IO) {
        val user = SupabaseProvider.client.auth.currentUserOrNull()
            ?: error("No active Supabase user.")

        val payload = JSONArray().put(
            JSONObject()
                .put("user_id", user.id)
                .put("gemini_api_key", value)
        )

        executeRestRequest(
            method = "POST",
            path = "/rest/v1/user_settings?on_conflict=user_id",
            body = payload,
            prefer = "resolution=merge-duplicates"
        )
    }

    suspend fun changeEmail(value: String) = withContext(Dispatchers.IO) {
        SupabaseProvider.client.auth.updateUser {
            email = value
        }
    }

    suspend fun changePassword(value: String) = withContext(Dispatchers.IO) {
        SupabaseProvider.client.auth.updateUser {
            password = value
        }
    }

    suspend fun linkGoogle() = withContext(Dispatchers.IO) {
        SupabaseProvider.client.auth.linkIdentity(Google)
    }

    suspend fun unlinkIdentity(identityId: String) = withContext(Dispatchers.IO) {
        SupabaseProvider.client.auth.unlinkIdentity(identityId)
    }

    suspend fun deleteAccount() = withContext(Dispatchers.IO) {
        executeFunctionRequest("delete-account", JSONObject())
    }

    private suspend fun executeRestRequest(
        method: String,
        path: String,
        body: JSONArray? = null,
        prefer: String? = null
    ): String = withContext(Dispatchers.IO) {
        val session = SupabaseProvider.client.auth.currentSessionOrNull()
            ?: error("No active Supabase session.")
        val connection = URL("${SupabaseProvider.supabaseUrl}$path")
            .openConnection() as HttpURLConnection

        try {
            connection.requestMethod = method
            connection.doInput = true
            connection.setRequestProperty("apikey", SupabaseProvider.supabaseAnonKey)
            connection.setRequestProperty("Authorization", "Bearer ${session.accessToken}")
            connection.setRequestProperty("Content-Type", "application/json")
            connection.setRequestProperty("Accept", "application/json")
            if (!prefer.isNullOrBlank()) {
                connection.setRequestProperty("Prefer", prefer)
            }

            if (body != null) {
                connection.doOutput = true
                OutputStreamWriter(connection.outputStream).use { writer ->
                    writer.write(body.toString())
                }
            }

            val stream = if (connection.responseCode in 200..299) {
                connection.inputStream
            } else {
                connection.errorStream
            }

            val payload = stream.bufferedReader().use(BufferedReader::readText)
            if (connection.responseCode !in 200..299) {
                error(payload.ifBlank { "Settings request failed." })
            }

            payload.ifBlank { "[]" }
        } finally {
            connection.disconnect()
        }
    }

    private suspend fun executeFunctionRequest(
        functionName: String,
        body: JSONObject
    ): String = withContext(Dispatchers.IO) {
        val session = SupabaseProvider.client.auth.currentSessionOrNull()
            ?: error("No active Supabase session.")
        val connection = URL("${SupabaseProvider.supabaseUrl}/functions/v1/$functionName")
            .openConnection() as HttpURLConnection

        try {
            connection.requestMethod = "POST"
            connection.doOutput = true
            connection.setRequestProperty("apikey", SupabaseProvider.supabaseAnonKey)
            connection.setRequestProperty("Authorization", "Bearer ${session.accessToken}")
            connection.setRequestProperty("Content-Type", "application/json")
            connection.setRequestProperty("Accept", "application/json")

            OutputStreamWriter(connection.outputStream).use { writer ->
                writer.write(body.toString())
            }

            val stream = if (connection.responseCode in 200..299) {
                connection.inputStream
            } else {
                connection.errorStream
            }

            val payload = stream.bufferedReader().use(BufferedReader::readText)
            if (connection.responseCode !in 200..299) {
                error(payload.ifBlank { "Function request failed." })
            }

            payload
        } finally {
            connection.disconnect()
        }
    }

    private fun encode(value: String): String =
        URLEncoder.encode(value, StandardCharsets.UTF_8.toString())
}
