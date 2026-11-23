using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FireFlower
{
    // Reads serialized fields by "a.b[0].c" paths and walks the serialized tree.
    // Descends into SerializeReference fields and lists, but not into other assets (UnityEngine.Object references)
    public static class FieldPath
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public readonly struct Node
        {
            public readonly object Value;
            public readonly string Path;
            public readonly FieldInfo Field;
            public Node(object value, string path, FieldInfo field) { Value = value; Path = path; Field = field; }
        }

        public static object Get(object root, string path)
        {
            object node = root;
            if (node == null || string.IsNullOrEmpty(path)) return node;
            foreach (string step in path.Split('.'))
            {
                int bracket = step.IndexOf('[');
                node = FieldOf(node.GetType(), bracket < 0 ? step : step.Substring(0, bracket))?.GetValue(node);
                if (bracket >= 0)
                {
                    if (node is not IList list || !int.TryParse(step.Substring(bracket + 1, step.Length - bracket - 2), out int index)
                        || index < 0 || index >= list.Count) return null;
                    node = list[index];
                }
                if (node == null) return null;
            }
            return node;
        }

        public static FieldInfo FieldOf(Type type, string name)
        {
            for (; type != null && type != typeof(object); type = type.BaseType)
                if (type.GetField(name, Fields) is FieldInfo field) return field;
            return null;
        }

        // Base class fields first. Fields of MonoBehaviour / ScriptableObject themselves are excluded
        public static IEnumerable<FieldInfo> SerializedFields(Type type)
        {
            var chain = new List<Type>();
            for (; type != null && type != typeof(object) && type != typeof(MonoBehaviour) && type != typeof(ScriptableObject); type = type.BaseType)
                chain.Add(type);
            for (int i = chain.Count - 1; i >= 0; i--)
                foreach (FieldInfo field in chain[i].GetFields(Fields))
                    if (IsSerialized(field)) yield return field;
        }

        public static bool IsSerialized(FieldInfo field) =>
            !field.IsNotSerialized && (field.IsPublic || field.IsDefined(typeof(SerializeField)) || field.IsDefined(typeof(SerializeReference)));

        // Every field value under root (numbers included) with its path. root itself is not yielded
        public static IEnumerable<Node> Walk(object root)
        {
            var visited = new HashSet<object>(ReferenceComparer.Instance) { root };
            return Walk(root, "", visited);
        }

        public static IEnumerable<T> FindAll<T>(object root) => Walk(root).Select(n => n.Value).OfType<T>();

        static IEnumerable<Node> Walk(object node, string prefix, HashSet<object> visited)
        {
            if (node == null) yield break;
            foreach (FieldInfo field in SerializedFields(node.GetType()))
            {
                object value = field.GetValue(node);
                string path = prefix + field.Name;
                if (value is IList list && value is not string)
                {
                    for (int i = 0; i < list.Count; i++)
                        foreach (Node child in Step(list[i], $"{path}[{i}]", field, visited)) yield return child;
                }
                else
                    foreach (Node child in Step(value, path, field, visited)) yield return child;
            }
        }

        static IEnumerable<Node> Step(object value, string path, FieldInfo field, HashSet<object> visited)
        {
            if (value == null) yield break;
            yield return new Node(value, path, field);
            Type type = value.GetType();
            if (value is UnityEngine.Object || type.IsPrimitive || type.IsEnum || value is string || !visited.Add(value)) yield break;
            foreach (Node child in Walk(value, path + ".", visited)) yield return child;
        }

        public sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
