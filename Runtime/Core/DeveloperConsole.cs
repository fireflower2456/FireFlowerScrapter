using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FireFlower
{
    public enum ConsoleLineKind { Input, Info, Error, Chat }

    public readonly struct ConsoleLine
    {
        public readonly ConsoleLineKind Kind;
        public readonly string Text;

        public ConsoleLine(ConsoleLineKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }
    }

    // Lines starting with "/" are commands; anything else is just logged as chat
    public class DeveloperConsole
    {
        public static DeveloperConsole Instance { get; } = new();

        const int MaxLines = 100;

        // Typed lines recalled with Up/Down
        public IReadOnlyList<string> History => history;
        public IReadOnlyList<ConsoleLine> Lines => lines;
        public event Action<ConsoleLine> Written;

        // Re-registering a name overwrites it (no pile-up with domain reload disabled)
        readonly Dictionary<string, DevCommand> commands = new(StringComparer.OrdinalIgnoreCase);
        readonly List<string> history = new();
        readonly List<ConsoleLine> lines = new();

        DeveloperConsole()
        {
            Add("Help", "[command]", "List commands or show one command's usage", Help)
                .Suggest(0, () => commands.Keys);
        }

        // Command names start with an uppercase letter (AddItem, Spawn)
        public DevCommand Add(string name, string usage, string help, Action<DevArgs> run)
        {
            if (string.IsNullOrEmpty(name) || !char.IsUpper(name[0]))
                Debug.LogError($"Console command '{name}': name must start with an uppercase letter");
            return commands[name] = new DevCommand(name, usage, help, run);
        }

        // true when finished (closes). false if the command failed (stays open)
        public bool Submit(string input)
        {
            input = input.Trim();
            if (input.Length == 0) return true;
            AddHistory(input);

            if (input[0] != '/')
            {
                Write(ConsoleLineKind.Chat, input);
                return true;
            }

            Write(ConsoleLineKind.Input, input);
            List<string> words = Split(input.Substring(1));
            if (words.Count == 0 || !commands.TryGetValue(words[0], out DevCommand command))
                return Error($"Unknown command: /{(words.Count > 0 ? words[0] : "")}  (/Help for a list)");

            try
            {
                command.Run(new DevArgs(command, words.GetRange(1, words.Count - 1), this));
                return true;
            }
            catch (DevCommandException e) { return Error(e.Message); }
            catch (Exception e)
            {
                Debug.LogException(e);
                return Error($"Error: {e.Message}");
            }
        }

        // Completions for the last word: command names for the first word, otherwise the command's own list
        public List<string> Complete(string input)
        {
            if (input.Length == 0 || input[0] != '/') return new List<string>();
            string body = input.Substring(1);
            List<string> words = Split(body);
            if (words.Count == 0 || body.EndsWith(" ")) words.Add("");

            string last = words[^1];
            IEnumerable<string> pool = words.Count == 1 ? commands.Keys
                : commands.TryGetValue(words[0], out DevCommand c) ? c.Suggestions(words.Count - 2)
                : Enumerable.Empty<string>();
            return pool.Where(s => s.StartsWith(last, StringComparison.OrdinalIgnoreCase))
                       .OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public void Write(ConsoleLineKind kind, string text)
        {
            var line = new ConsoleLine(kind, text);
            lines.Add(line);
            if (lines.Count > MaxLines) lines.RemoveAt(0);
            Written?.Invoke(line);
        }

        bool Error(string message)
        {
            Write(ConsoleLineKind.Error, message);
            return false;
        }

        void Help(DevArgs a)
        {
            if (a.Count > 0)
            {
                if (!commands.TryGetValue(a.Str(0), out DevCommand c)) a.Fail($"Unknown command: {a.Str(0)}");
                a.Log($"/{c.Name} {c.Usage} — {c.Help}");
                return;
            }
            foreach (DevCommand c in commands.Values.OrderBy(c => c.Name))
                a.Log($"/{c.Name} {c.Usage}");
        }

        static List<string> Split(string s) => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        // Skip a line identical to the previous one
        void AddHistory(string input)
        {
            if (history.Count > 0 && history[^1] == input) return;
            history.Add(input);
        }
    }
}
