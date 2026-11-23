using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine.UIElements;

namespace FireFlower
{
    // Editor text in the picked language (ScrapterLanguage asset). English is the source; missing entries stay English
    public static class ScrapterText
    {
        public const string English = "English";
        const string PrefKey = "FireFlower.Scrapter.Language";
        static Dictionary<string, string> map;

        public static event Action Changed;
        // Bumped on every change so caches of translated text know to rebuild
        public static int Version { get; private set; }

        public static string Current
        {
            // A language whose asset is gone (or a broken value) falls back to English
            get
            {
                string saved = EditorPrefs.GetString(PrefKey, English);
                return Languages.Contains(saved) ? saved : English;
            }
            set
            {
                EditorPrefs.SetString(PrefKey, value);
                Refresh();
            }
        }

        public static List<string> Languages =>
            Assets().Select(l => l.name).Prepend(English).Distinct().ToList();

        static IEnumerable<ScrapterLanguage> Assets() => AssetDatabase.FindAssets($"t:{nameof(ScrapterLanguage)}")
            .Select(g => AssetDatabase.LoadAssetAtPath<ScrapterLanguage>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(l => l != null);

        static Dictionary<string, string> Map
        {
            get
            {
                if (map != null) return map;
                var result = new Dictionary<string, string>();
                ScrapterLanguage language = Current == English ? null : Assets().FirstOrDefault(l => l.name == Current);
                // Right after a reload the asset can still be loading (entries null): don't cache, try again next time
                if (language != null && language.entries == null) return result;
                if (language != null)
                    foreach (ScrapterLanguage.Entry e in language.entries)
                        if (!string.IsNullOrEmpty(e.english) && !string.IsNullOrEmpty(e.text)) result[e.english] = e.text;
                return map = result;
            }
        }

        public static void Refresh()
        {
            map = null;
            Version++;
            Changed?.Invoke();
        }

        public static string T(string english) =>
            english != null && Map.TryGetValue(english, out string text) ? text : english;

        public static string T(string english, params object[] args) => string.Format(T(english), args);

        // Block title parts. The first part naming a field becomes {name} in the key (later ones are text, as in the title);
        // the translation is split back the same way
        public static string[] Display(string[] parts, Func<string, bool> isField)
        {
            if (Map.Count == 0) return parts;
            return Map.TryGetValue(DisplayKey(parts, isField), out string text) ? Regex.Split(text, @"\{(\w+)\}") : parts;
        }

        public static string DisplayKey(string[] parts, Func<string, bool> isField)
        {
            var used = new HashSet<string>();
            return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => isField(p) && used.Add(p) ? $"{{{p}}}" : p.Trim()));
        }

        // Text and tooltips written in UXML
        public static void Translate(VisualElement root)
        {
            root.Query<VisualElement>().ForEach(e =>
            {
                if (e is TextElement t && !string.IsNullOrEmpty(t.text)) t.text = T(t.text);
                if (!string.IsNullOrEmpty(e.tooltip)) e.tooltip = T(e.tooltip);
            });
        }
    }
}
