using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

if (args is ["--sanitize-apphost", var hostPath])
{
    var bytes = File.ReadAllBytes(hostPath);
    using var pe = new PEReader(new MemoryStream(bytes));
    foreach (var entry in pe.ReadDebugDirectory())
    {
        if (entry.Type != DebugDirectoryEntryType.CodeView) continue;
        // Keep the CodeView identity while removing the SDK build-machine path.
        var pathOffset = entry.DataPointer + 24;
        Array.Clear(bytes, pathOffset, entry.DataSize - 24);
        Encoding.UTF8.GetBytes("apphost.pdb").CopyTo(bytes, pathOffset);
    }
    File.WriteAllBytes(hostPath, bytes);
    return;
}

var version = typeof(Program).Assembly.GetName().Version!;
var zip = Path.Combine(args[0], $"Vocaloid7-L10N-{version.Major}.{version.Minor}.{version.Build}-win-x64.zip");
using var archive = ZipFile.OpenRead(zip);
var required = new HashSet<string>(StringComparer.Ordinal)
{
    "Vocaloid7.L10N.Launcher.exe", "Vocaloid7.L10N.Launcher.dll",
    "Vocaloid7.L10N.Launcher.deps.json", "Vocaloid7.L10N.Launcher.runtimeconfig.json",
    "Vocaloid7.L10N.StartupHook.dll", "README.md", "locales/zh-CN.json"
};
foreach (var entry in archive.Entries)
{
    if (entry.FullName.EndsWith('/')) continue;
    bool locale = Regex.IsMatch(entry.FullName, @"^locales/[A-Za-z0-9-]+\.json$");
    if (!required.Remove(entry.FullName) && !locale)
        throw new Exception("Unexpected package entry: " + entry.FullName);
    using var data = new MemoryStream();
    using (var stream = entry.Open()) stream.CopyTo(data);
    var bytes = data.ToArray();
    foreach (var encoding in new[] { Encoding.UTF8, Encoding.Unicode })
        if (Regex.IsMatch(encoding.GetString(bytes), @"(?<![A-Za-z0-9])[A-Za-z]:[\\/]"))
            throw new Exception("Local path found in: " + entry.FullName);
    if (entry.FullName.EndsWith(".dll"))
    {
        data.Position = 0;
        using var pe = new PEReader(data);
        if (pe.GetMetadataReader().GetAssemblyDefinition().Version != version)
            throw new Exception("Assembly version mismatch: " + entry.FullName);
    }
    if (locale)
    {
        var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(bytes)!;
        if (strings.Count == 0 || strings.Any(x => x.Value is null))
            throw new Exception("Invalid language pack: " + entry.FullName);
        foreach (var text in strings.Values) _ = System.Text.CompositeFormat.Parse(text);
    }
}
if (required.Count != 0) throw new Exception("Missing package entries: " + string.Join(", ", required));
Console.WriteLine("Package validation passed: layout, versions, language packs and local-path exclusion.");
