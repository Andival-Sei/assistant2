package com.assistant.app.auth

import android.content.Context

object AuthPreferences {
    private const val prefsName = "assistant_auth_prefs"
    private const val rememberKey = "remember_device"

    fun loadRememberDevice(context: Context): Boolean {
        return context.getSharedPreferences(prefsName, Context.MODE_PRIVATE)
            .getBoolean(rememberKey, false)
    }

    fun saveRememberDevice(context: Context, enabled: Boolean) {
        context.getSharedPreferences(prefsName, Context.MODE_PRIVATE)
            .edit()
            .putBoolean(rememberKey, enabled)
            .apply()
    }
}
