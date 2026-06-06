namespace PeFix.Meta;

internal static class AsmNameParse
{
    public static bool TryParse(
        string literal,
        bool requireComma,
        out string assemblyName)
    {
        assemblyName = string.Empty;
        string[] parts = literal.Split(',');
        if (requireComma && parts.Length < 2)
            return false;

        string? candidate = Candidate(parts);
        if (candidate is null)
            return false;

        return TryNormalize(candidate, out assemblyName);
    }

    private static string? Candidate(string[] parts)
    {
        string? candidate = null;
        foreach (string part in parts)
        {
            string value = part.Trim();
            if (value.Length == 0)
                continue;

            if (IsKeyValue(value))
                break;

            candidate = value;
        }

        return candidate;
    }

    private static bool TryNormalize(string candidate, out string assemblyName)
    {
        assemblyName = string.Empty;
        if (candidate.IndexOfAny(['/', '\\']) >= 0)
            return false;

        string value = StripExtension(candidate);
        if (value.Length == 0 || value.Contains('='))
            return false;

        assemblyName = value;
        return true;
    }

    private static string StripExtension(string value)
    {
        return value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(value)
            : value;
    }

    private static bool IsKeyValue(string value)
    {
        int equals = value.IndexOf('=');
        return equals > 0;
    }
}
