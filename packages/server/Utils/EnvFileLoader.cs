namespace SchoolRecommender.Api.Utils;

public static class EnvFileLoader
{
    public static void Load(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..separatorIndex].Trim();
            var rawValue = trimmed[(separatorIndex + 1)..].Trim();
            // Strip inline comments: handle both quoted values and bare values
            var value = StripInlineComment(rawValue);

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static string StripInlineComment(string rawValue)
    {
        // Quoted value: "some value" optionally followed by # comment
        if (rawValue.StartsWith('"'))
        {
            var closingQuote = rawValue.IndexOf('"', 1);
            if (closingQuote > 0)
            {
                return rawValue[1..closingQuote];
            }
            return rawValue.Trim('"');
        }

        // Bare value: strip trailing # comment (must be preceded by whitespace)
        var commentIdx = rawValue.IndexOf(" #", StringComparison.Ordinal);
        if (commentIdx >= 0)
        {
            return rawValue[..commentIdx].TrimEnd();
        }
        return rawValue;
    }
}