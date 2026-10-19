using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Media;

namespace SolutionColor
{
    /// <summary>
    /// Per-solution color settings. Stored either per-user in the solution's .vs folder
    /// or next to the solution file (so it can be committed and shared with the team).
    /// </summary>
    [DataContract]
    internal sealed class SolutionColorSettings
    {
        [DataMember(Name = "color", Order = 0)]
        public string Color { get; set; } = "#3B82F6";

        [DataMember(Name = "colorTitleBar", Order = 1)]
        public bool ColorTitleBar { get; set; } = true;

        [DataMember(Name = "tintIcon", Order = 2)]
        public bool TintIcon { get; set; } = true;

        [DataMember(Name = "taskbarBadge", Order = 3)]
        public bool TaskbarBadge { get; set; } = true;

        [DataMember(Name = "colorWindowBorder", Order = 4)]
        public bool ColorWindowBorder { get; set; } = true;

        /// <summary>Not serialized: where the settings were loaded from / should be saved to.</summary>
        public bool Shared { get; set; }

        public Color GetColor() => TryParseColor(Color, out var c) ? c : Colors.SteelBlue;

        public static bool TryParseColor(string text, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();
            if (!text.StartsWith("#", StringComparison.Ordinal))
                text = "#" + text;

            try
            {
                var converted = ColorConverter.ConvertFromString(text);
                if (converted is Color c)
                {
                    color = System.Windows.Media.Color.FromRgb(c.R, c.G, c.B);
                    return true;
                }
            }
            catch (FormatException)
            {
            }

            return false;
        }

        public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    /// <summary>
    /// Resolves the settings file locations for a solution (or an opened folder).
    /// </summary>
    internal sealed class SolutionColorStore
    {
        private const string FileName = "SolutionColor.json";

        public SolutionColorStore(string solutionDirectory, string solutionFile)
        {
            SolutionDirectory = solutionDirectory;
            SolutionName = !string.IsNullOrEmpty(solutionFile)
                ? Path.GetFileNameWithoutExtension(solutionFile)
                : new DirectoryInfo(solutionDirectory).Name;

            // .vs\<SolutionName>\SolutionColor.json  (per user, normally git-ignored)
            UserFilePath = Path.Combine(solutionDirectory, ".vs", SolutionName, FileName);

            // <SolutionName>.slncolor.json next to the .sln (shared, can be committed)
            SharedFilePath = Path.Combine(solutionDirectory, SolutionName + ".slncolor.json");
        }

        public string SolutionDirectory { get; }
        public string SolutionName { get; }
        public string UserFilePath { get; }
        public string SharedFilePath { get; }

        /// <summary>Per-user settings win over shared settings. Returns null when the solution has no color.</summary>
        public SolutionColorSettings Load()
        {
            var user = Read(UserFilePath);
            if (user != null)
                return user;

            var shared = Read(SharedFilePath);
            if (shared != null)
                shared.Shared = true;
            return shared;
        }

        public void Save(SolutionColorSettings settings)
        {
            var target = settings.Shared ? SharedFilePath : UserFilePath;
            var other = settings.Shared ? UserFilePath : SharedFilePath;

            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, Serialize(settings), new UTF8Encoding(false));

            // Only one location is authoritative; remove the per-user override when switching to shared.
            if (settings.Shared && File.Exists(other))
                File.Delete(other);
        }

        public void Delete()
        {
            if (File.Exists(UserFilePath))
                File.Delete(UserFilePath);
            if (File.Exists(SharedFilePath))
                File.Delete(SharedFilePath);
        }

        private static SolutionColorSettings Read(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                using (var stream = File.OpenRead(path))
                {
                    var serializer = new DataContractJsonSerializer(typeof(SolutionColorSettings));
                    return serializer.ReadObject(stream) as SolutionColorSettings;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Serialize(SolutionColorSettings settings)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, ownsStream: false, indent: true))
                {
                    new DataContractJsonSerializer(typeof(SolutionColorSettings)).WriteObject(writer, settings);
                }
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
