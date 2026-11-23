using System;
using System.Collections.Generic;
using System.Linq;

namespace FireFlower
{
    public class DevCommand
    {
        public string Name { get; }
        public string Usage { get; }
        public string Help { get; }
        public readonly Action<DevArgs> Run;
        readonly Dictionary<int, Func<IEnumerable<string>>> suggestions = new();

        public DevCommand(string name, string usage, string help, Action<DevArgs> run)
        {
            Name = name;
            Usage = usage;
            Help = help;
            Run = run;
        }

        // Tab completions while typing argument #index
        public DevCommand Suggest(int index, Func<IEnumerable<string>> source)
        {
            suggestions[index] = source;
            return this;
        }

        public IEnumerable<string> Suggestions(int index)
            => suggestions.TryGetValue(index, out var source) ? source() : Enumerable.Empty<string>();
    }

    // Failures are thrown to abort the command. The console shows a red line and keeps the window open
    public class DevCommandException : Exception
    {
        public DevCommandException(string message) : base(message) { }
    }

    public class DevArgs
    {
        readonly DevCommand command;
        readonly List<string> words;
        readonly DeveloperConsole console;

        public DevArgs(DevCommand command, List<string> words, DeveloperConsole console)
        {
            this.command = command;
            this.words = words;
            this.console = console;
        }

        public int Count => words.Count;
        public bool IsInt(int i) => i < words.Count && int.TryParse(words[i], out _);

        public string Str(int i) => i < words.Count ? words[i] : throw Usage();
        public string Str(int i, string fallback) => i < words.Count ? words[i] : fallback;
        public int Int(int i) => int.TryParse(Str(i), out int v) ? v : throw Usage();
        public int Int(int i, int fallback) => i < words.Count ? Int(i) : fallback;

        public void Log(string message) => console.Write(ConsoleLineKind.Info, message);
        public void Fail(string message) => throw new DevCommandException(message);

        Exception Usage() => new DevCommandException($"Usage: /{command.Name} {command.Usage}");
    }
}
