using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FireFlower
{
    // Global, persistent and object variable lists. Only used for picking in the editor (runtime looks up by name)
    [FilePath("ProjectSettings/ScrapWeaverVariables.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class ScrapWeaverVariables : ScriptableSingleton<ScrapWeaverVariables>
    {
        [SerializeField] List<string> names = new();
        [SerializeField] List<string> persistent = new();
        // Non-numeric global variables
        [SerializeField] List<VariableDecl> typed = new();
        [SerializeField] List<VariableDecl> self = new();

        public static IEnumerable<(string key, VariableScope scope, VariableType type, string component)> All
        {
            get
            {
                foreach (string n in instance.names) yield return (n, VariableScope.Global, VariableType.Number, null);
                foreach (VariableDecl d in instance.typed) yield return (d.name, VariableScope.Global, d.type, d.component);
                foreach (VariableDecl d in instance.self) yield return (d.name, VariableScope.Self, d.type, d.component);
                foreach (string n in instance.persistent) yield return (n, VariableScope.Persistent, VariableType.Number, null);
            }
        }

        public static void Add(string name, VariableScope scope, VariableType type, string component = null)
        {
            if (scope == VariableScope.Self) instance.self.Add(new VariableDecl(name, type, component));
            else if (scope == VariableScope.Persistent) instance.persistent.Add(name);
            else if (type != VariableType.Number) instance.typed.Add(new VariableDecl(name, type, component));
            else instance.names.Add(name);
            instance.Save(true);
        }

        public static void Remove(string name, VariableScope scope)
        {
            if (scope == VariableScope.Self) instance.self.RemoveAll(d => d.name == name);
            else if (scope == VariableScope.Persistent) instance.persistent.Remove(name);
            else
            {
                instance.names.Remove(name);
                instance.typed.RemoveAll(d => d.name == name);
            }
            instance.Save(true);
        }
    }
}
