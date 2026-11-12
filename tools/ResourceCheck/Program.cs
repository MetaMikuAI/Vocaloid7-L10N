using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text.Json;
using System.Text.RegularExpressions;

var assembly = Assembly.LoadFrom(args[0]);
var type = assembly.GetType("Yamaha.VOCALOID.Properties.Resources", true)!;
var property = type.GetProperty("ResourceManager")!;
var original = (ResourceManager)property.GetValue(null)!;
var texts = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(args[2]))!;
var source = original.GetResourceSet(CultureInfo.InvariantCulture, true, false)!;
var count = source.Cast<System.Collections.DictionaryEntry>().Count(e => e.Value is string);
if (count != texts.Count) throw new Exception($"Resource count mismatch: {count} / {texts.Count}");
foreach (var (key, text) in texts)
{
    var value = original.GetString(key, CultureInfo.InvariantCulture) ?? throw new Exception("Invalid resource key: " + key);
    _ = System.Text.CompositeFormat.Parse(text);
    foreach (var pattern in new[] { @"\{[^{}]*\}", @"\(_[A-Za-z0-9]\)", @"\r\n|\\n" })
        if (!Regex.Matches(value, pattern).Select(m => m.Value).SequenceEqual(Regex.Matches(text, pattern).Select(m => m.Value)))
            throw new Exception("Format mismatch: " + key);
    if (key.EndsWith("_Filter") && !value.Split('|').Where((_, i) => i % 2 == 1).SequenceEqual(text.Split('|').Where((_, i) => i % 2 == 1)))
        throw new Exception("File filter mismatch: " + key);
}
Environment.SetEnvironmentVariable("VOCALOID_L10N_LANGUAGE", "zh-CN");
Assembly.LoadFrom(args[1]).GetType("StartupHook", true)!.GetMethod("Initialize")!.Invoke(null, null);
var localized = (ResourceManager)property.GetValue(null)!;
if (ReferenceEquals(original, localized)) throw new Exception("Resource adapter was not installed");
foreach (var (key, text) in texts)
    if (localized.GetString(key, CultureInfo.InvariantCulture) != text) throw new Exception("Translation mismatch: " + key);
if (localized.GetString("__missing_resource_check__") != original.GetString("__missing_resource_check__"))
    throw new Exception("Missing-resource fallback mismatch");
Console.WriteLine($"Passed: {count} resources, translations, format checks and missing-resource fallback.");
