using System.Diagnostics;
using System.Globalization;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            var folder = AppContext.BaseDirectory;
            var locales = LocaleCatalog.Read(Path.Combine(folder, "locales"));
            if (args is ["--list-languages"])
            {
                foreach (var locale in locales) Console.WriteLine($"{locale.Code}\t{locale.Name}");
                return 0;
            }
            var editor = Path.Combine(folder, "VOCALOID7.exe");
            var hook = Path.Combine(folder, "Vocaloid7.L10N.StartupHook.dll");
            if (!File.Exists(editor) || !File.Exists(Path.Combine(folder, "VOCALOID7.dll")))
                throw new FileNotFoundException("Extract this package into the VOCALOID7 Editor folder, beside VOCALOID7.exe.");
            if (!File.Exists(hook)) throw new FileNotFoundException("Startup component missing. Extract the complete package again.");
            if (locales.Count == 0) throw new FileNotFoundException("No language packs found in the locales folder.");

            var userFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vocaloid7-L10N");
            var preference = Path.Combine(userFolder, "language.txt");
            var preferred = CultureInfo.CurrentUICulture.Name;
            try { if (File.Exists(preference)) preferred = File.ReadAllText(preference).Trim(); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            using var form = new Form
            {
                Text = $"VOCALOID7 L10N {typeof(Program).Assembly.GetName().Version!.ToString(3)}", ClientSize = new Size(380, 145),
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false,
                MinimizeBox = false, StartPosition = FormStartPosition.CenterScreen,
                AutoScaleMode = AutoScaleMode.Dpi
            };
            var label = new Label { Text = "Language / 语言", AutoSize = true, Location = new Point(20, 18) };
            var languages = new ComboBox
            {
                Location = new Point(20, 45), Width = 340, DropDownStyle = ComboBoxStyle.DropDownList,
                DisplayMember = nameof(Locale.Name)
            };
            languages.Items.AddRange(locales.Cast<object>().ToArray());
            languages.SelectedIndex = Math.Max(0, locales.FindIndex(x => x.Code == preferred));
            var launch = new Button { Text = "Start / 启动", Location = new Point(240, 95), Size = new Size(120, 30), DialogResult = DialogResult.OK };
            form.Controls.AddRange([label, languages, launch]);
            form.AcceptButton = launch;
            if (form.ShowDialog() != DialogResult.OK) return 0;

            var language = ((Locale)languages.SelectedItem!).Code;
            Directory.CreateDirectory(userFolder);
            File.WriteAllText(preference, language);
            var logFolder = Path.Combine(userFolder, "logs");
            Directory.CreateDirectory(logFolder);
            var start = new ProcessStartInfo(editor) { UseShellExecute = false, WorkingDirectory = folder };
            start.Environment.TryGetValue("DOTNET_STARTUP_HOOKS", out var existing);
            start.Environment["DOTNET_STARTUP_HOOKS"] = string.IsNullOrEmpty(existing) ? hook : existing + Path.PathSeparator + hook;
            start.Environment["VOCALOID_L10N_LANGUAGE"] = language;
            start.Environment["VOCALOID_L10N_LOG"] = Path.Combine(logFolder, $"startup-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");
            foreach (var argument in args) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "VOCALOID7 L10N", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}

