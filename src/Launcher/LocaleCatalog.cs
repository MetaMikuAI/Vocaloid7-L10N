using System.Globalization;
using System.Text.Json;

internal sealed record Locale(string Code, string Name);

internal static class LocaleCatalog
{
    public static List<Locale> Read(string folder)
    {
        if (!Directory.Exists(folder)) return [];
        var result = new List<Locale>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.OrdinalIgnoreCase))
        {
            var code = Path.GetFileNameWithoutExtension(file);
            var culture = CultureInfo.GetCultureInfo(code);
            var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
            if (translations is null || translations.Any(x => x.Value is null))
                throw new InvalidDataException($"Invalid language pack: {Path.GetFileName(file)}");
            result.Add(new Locale(code, $"{culture.NativeName} ({code})"));
        }
        return result;
    }
}
