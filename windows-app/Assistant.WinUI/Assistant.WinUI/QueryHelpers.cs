using System;
using System.Collections.Generic;

namespace Assistant.WinUI;

internal static class QueryHelpers
{
    public static Dictionary<string, string> Parse(Uri uri)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddPairs(result, uri.Query?.TrimStart('?'));
        AddPairs(result, uri.Fragment?.TrimStart('#'));
        return result;
    }

    private static void AddPairs(Dictionary<string, string> target, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        foreach (var pair in raw.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            target[key] = value;
        }
    }
}
