using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text.Json;

internal class StartupHook
{
    private static readonly object Gate = new();
    private static readonly HashSet<Assembly> Installed = new();
    private static Dictionary<string, string> translations = new();
    private static string? logPath;

    public static void Initialize()
    {
        try
        {
            var folder = Path.GetDirectoryName(typeof(StartupHook).Assembly.Location)!;
            logPath = Environment.GetEnvironmentVariable("VOCALOID_L10N_LOG");
            var language = Environment.GetEnvironmentVariable("VOCALOID_L10N_LANGUAGE") ?? "zh-CN";
            if (language.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || language.Contains('/') || language.Contains('\\'))
                throw new ArgumentException("Invalid language name.");
            translations = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(Path.Combine(folder, "locales", language + ".json")))!;
            AppDomain.CurrentDomain.AssemblyLoad += (_, e) => Install(e.LoadedAssembly);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) Install(assembly);
            Log("Startup hook ready: " + language);
        }
        catch (Exception ex) { Log("Initialization failed; original resources retained: " + ex.Message); }
    }

    private static void Install(Assembly assembly)
    {
        if (assembly.GetName().Name != "VOCALOID7") return;
        lock (Gate)
        {
            if (!Installed.Add(assembly)) return;
            try
            {
                var type = assembly.GetType("Yamaha.VOCALOID.Properties.Resources", true)!;
                var field = type.GetField("resourceMan", BindingFlags.Static | BindingFlags.NonPublic)
                    ?? throw new MissingFieldException(type.FullName, "resourceMan");
                var original = (ResourceManager)type.GetProperty("ResourceManager")!.GetValue(null)!;
                var valid = new Dictionary<string, string>();
                foreach (var (key, text) in translations)
                {
                    var source = original.GetString(key, CultureInfo.InvariantCulture);
                    if (source is null) { Log("Unknown key: " + key); continue; }
                    if (System.Text.CompositeFormat.Parse(source).MinimumArgumentCount !=
                        System.Text.CompositeFormat.Parse(text).MinimumArgumentCount)
                    { Log("Placeholder mismatch: " + key); continue; }
                    valid.Add(key, text);
                }
                field.SetValue(null, new TranslationResources(original, valid));
                Log($"Installed for {assembly.GetName().Version}: {valid.Count} translations");
            }
            catch (Exception ex) { Log("Installation failed; original resources retained: " + ex.Message); }
        }
    }

    internal static void Log(string message)
    {
        try { if (logPath is not null) File.AppendAllText(logPath, $"{DateTime.Now:O} {message}\n"); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed class TranslationResources(ResourceManager original, Dictionary<string, string> texts) : ResourceManager
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> seen = new();
        public override string? GetString(string name) => GetString(name, null);
        public override string? GetString(string name, CultureInfo? culture)
        {
            if (!texts.TryGetValue(name, out var text)) return original.GetString(name, culture);
            if (seen.TryAdd(name, 0)) Log("Translated: " + name);
            return text;
        }
        public override object? GetObject(string name) => original.GetObject(name);
        public override object? GetObject(string name, CultureInfo? culture) => original.GetObject(name, culture);
        public override ResourceSet? GetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents)
            => original.GetResourceSet(culture, createIfNotExists, tryParents);
    }
}
