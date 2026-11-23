using System;
using System.Collections.Generic;
using System.Globalization;

namespace FireFlower
{
    // Runtime-only store owned by each controller. Register defaults with Set/Require.
    public class Blackboard
    {
        readonly Dictionary<string, object> values = new();

        public int Count => values.Count;
        public bool Has(string key) => values.ContainsKey(key);

        // Can store references, enums and custom data too. Set on an existing key replaces the value.
        public void Set<T>(string key, T value)
        {
            ValidateKey(key);
            values[key] = value;
        }

        // Missing keys throw KeyNotFoundException; type mismatches throw InvalidCastException.
        public T Get<T>(string key)
        {
            if (!values.TryGetValue(key, out var value))
                throw new KeyNotFoundException($"Blackboard key '{key}' was not found.");
            if (TryConvert(value, out T result)) return result;
            throw new InvalidCastException($"Blackboard key '{key}' cannot be read as {typeof(T).Name}.");
        }

        // false on a missing key or type mismatch. A stored null can be read as a reference/nullable type.
        public bool TryGet<T>(string key, out T value)
        {
            value = default;
            return values.TryGetValue(key, out var stored) && TryConvert(stored, out value);
        }

        public T GetOrDefault<T>(string key, T fallback = default)
            => TryGet<T>(key, out var value) ? value : fallback;

        // Keeps an existing value. Throws like Get if the existing value's type differs.
        public T Require<T>(string key, T defaultValue = default)
        {
            if (Has(key)) return Get<T>(key);
            Set(key, defaultValue);
            return defaultValue;
        }

        public bool Remove(string key) => values.Remove(key);
        public void Clear() => values.Clear();

        // Runtime-only values such as references are left out
        public BlackboardValue[] Snapshot()
        {
            var saved = new List<BlackboardValue>();
            foreach (var (key, value) in values)
            {
                switch (value)
                {
                    case float f: saved.Add(new(key, BlackboardValue.Kind.Float, f.ToString(CultureInfo.InvariantCulture))); break;
                    case int i: saved.Add(new(key, BlackboardValue.Kind.Int, i.ToString(CultureInfo.InvariantCulture))); break;
                    case bool b: saved.Add(new(key, BlackboardValue.Kind.Bool, b.ToString())); break;
                    case string s: saved.Add(new(key, BlackboardValue.Kind.String, s)); break;
                }
            }
            return saved.ToArray();
        }

        public void Restore(BlackboardValue[] saved)
        {
            if (saved == null) return;
            foreach (var v in saved)
            {
                switch (v.kind)
                {
                    case BlackboardValue.Kind.Float: Set(v.key, float.Parse(v.value, CultureInfo.InvariantCulture)); break;
                    case BlackboardValue.Kind.Int: Set(v.key, int.Parse(v.value, CultureInfo.InvariantCulture)); break;
                    case BlackboardValue.Kind.Bool: Set(v.key, bool.Parse(v.value)); break;
                    case BlackboardValue.Kind.String: Set(v.key, v.value); break;
                }
            }
        }

        static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Blackboard key must not be empty.", nameof(key));
        }

        static bool TryConvert<T>(object stored, out T value)
        {
            if (stored is T typed) { value = typed; return true; }
            value = default;
            return stored == null && (!typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null);
        }
    }

    [Serializable]
    public struct BlackboardValue
    {
        public enum Kind { Float, Int, Bool, String }

        public string key;
        public Kind kind;
        public string value;

        public BlackboardValue(string key, Kind kind, string value)
        {
            this.key = key;
            this.kind = kind;
            this.value = value;
        }
    }
}
