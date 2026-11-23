using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FireFlower
{
    // A sensing block's shape is its kind, decided by the check method's return type: bool = hexagon, number = rectangle, anything else (event) = tag
    public enum BlockShape { Part, Event, Condition, Value }

    public sealed class BlockType
    {
        public Type Type;
        public Type Base;
        public string Name => ScrapWeaverTypes.DisplayOf(Type);
        public string Category;
        public BlockShape Shape;
    }

    // One piece of the title line: text or a field (a slot for sockets, a value input for plain fields)
    public readonly struct TitlePart
    {
        public readonly string Text;
        public readonly FieldInfo Field;
        public TitlePart(string text, FieldInfo field) { Text = text; Field = field; }
    }

    // Type info used by the block editor. All reflection, so it is computed once per type (rebuilt on domain reload).
    // Starting from ScrapPart, it follows [SerializeReference] fields to collect every block type that can be plugged in —
    // so new effect or condition classes need no editor changes
    public static class ScrapWeaverTypes
    {
        // Follows Scratch's category order: Motion (effect parts) · Looks (presentation) · Events (triggers) · Control (gates) · Sensing (events, conditions, game values) · Operators (math, comparison, logic) · Variables (user variables) · Buffs · My Blocks (functions)
        public const string ActionCategory = "Action";
        public const string VisualCategory = "Looks";
        public const string EventCategory = "Events";
        public const string ControlCategory = "Control";
        public const string SensingCategory = "Sensing";
        public const string OperatorCategory = "Operators";
        public const string VariableCategory = "Variables";
        public const string MyBlockCategory = "My Blocks";
        static readonly string[] CategoryOrder = { ActionCategory, VisualCategory, EventCategory, ControlCategory, SensingCategory, OperatorCategory, VariableCategory, MyBlockCategory };
        static readonly Color[] CategoryColors =
        {
            new(0.2f, 0.38f, 0.68f),
            new(0.72f, 0.2f, 0.2f),
            new(0.6f, 0.47f, 0.1f),
            new(0.62f, 0.36f, 0.1f),
            new(0.14f, 0.45f, 0.56f),
            new(0.22f, 0.5f, 0.22f),
            new(0.6f, 0.28f, 0.1f),
            new(0.72f, 0.22f, 0.36f),
        };

        // Hats (on enter, while the AI runs, etc.) use the Events color, as in Scratch
        public static readonly Color RootColor = new(0.6f, 0.47f, 0.1f);
        public static readonly Color BodyColor = new(0.2f, 0.2f, 0.2f);
        static readonly Color[] OtherColors =
        {
            new(0.56f, 0.18f, 0.23f),
            new(0.48f, 0.35f, 0.11f),
            new(0.35f, 0.38f, 0.42f),
            new(0.12f, 0.49f, 0.33f),
        };

        public static readonly List<BlockType> All = new();
        public static readonly List<string> Categories = new();
        static readonly Dictionary<Type, BlockType> byType = new();
        static readonly Dictionary<Type, FieldInfo[]> fields = new();
        static readonly Dictionary<Type, FieldInfo[]> partFields = new();
        static readonly Dictionary<Type, FieldInfo[]> socketFields = new();
        static readonly Dictionary<Type, BlockShape> shapes = new();
        static readonly Dictionary<Type, List<TitlePart>> titles = new();
        static readonly Dictionary<Type, string> displays = new();
        static int titlesVersion = -1;

        static ScrapWeaverTypes()
        {
            var queue = new Queue<Type>();
            var visited = new HashSet<Type> { typeof(ScrapPart) };
            queue.Enqueue(typeof(ScrapPart));
            while (queue.Count > 0)
            {
                Type baseType = queue.Dequeue();
                foreach (Type type in TypeCache.GetTypesDerivedFrom(baseType).Prepend(baseType))
                {
                    if (type.IsAbstract || type.IsGenericType || type.GetConstructor(Type.EmptyTypes) == null || byType.ContainsKey(type)
                        || typeof(ScrapScript).IsAssignableFrom(type)) continue;
                    BlockShape shape = ShapeOf(baseType);
                    var blockType = new BlockType { Type = type, Base = baseType, Category = CategoryOf(type, shape), Shape = shape };
                    All.Add(blockType);
                    byType[type] = blockType;
                    foreach (FieldInfo field in PartFields(type).Concat(SocketFields(type)))
                        if (visited.Add(ElementType(field.FieldType))) queue.Enqueue(ElementType(field.FieldType));
                }
            }
            // Even if first found through a broad socket (any value), the palette subheading uses the narrowest kind
            foreach (BlockType b in All) b.Base = visited.Where(v => v.IsAssignableFrom(b.Type)).OrderByDescending(Depth).First();
            Categories.AddRange(All.Select(b => b.Category).Distinct().OrderBy(OrderOf).ThenBy(c => c, StringComparer.Ordinal));
        }

        // The version that fits the socket (accepts): itself if it fits, otherwise a fitting one from the same [Family]. null if none
        public static Type FitFor(Type type, Type accepts)
        {
            if (accepts.IsAssignableFrom(type)) return type;
            if (type.GetCustomAttribute<ScrapWeaver.FamilyAttribute>() is not { } family) return null;
            return All.Select(b => b.Type).FirstOrDefault(t => accepts.IsAssignableFrom(t) && t.GetCustomAttribute<ScrapWeaver.FamilyAttribute>()?.Name == family.Name);
        }

        // Whether this is a shared block other editors also use. Only blocks marked [NotCommon] are excluded
        public static bool IsCommon(BlockType block) => !block.Type.IsDefined(typeof(ScrapWeaver.NotCommonAttribute), false);

        public static bool IsCap(Type type) => type.IsDefined(typeof(ScrapWeaver.CapAttribute), false);

        // Non-primary versions of a family are not shown separately in the palette
        public static bool IsFamilyVariant(Type type) => type.GetCustomAttribute<ScrapWeaver.FamilyAttribute>() is { Primary: false };

        static int Depth(Type type)
        {
            int depth = 0;
            for (Type t = type; t != null; t = t.BaseType) depth++;
            return depth;
        }

        // Categories created with [Category] come after the built-in ones
        static int OrderOf(string category)
        {
            int index = Array.IndexOf(CategoryOrder, category);
            return index < 0 ? CategoryOrder.Length : index;
        }

        // ── Fields ──

        public static FieldInfo[] Fields(Type type)
        {
            if (fields.TryGetValue(type, out var cached)) return cached;
            var hierarchy = new Stack<Type>();
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType) hierarchy.Push(t);
            return fields[type] = hierarchy
                .SelectMany(t => t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                .Where(f => !f.IsNotSerialized && !f.IsDefined(typeof(HideInInspector))
                            && (f.IsPublic || f.IsDefined(typeof(SerializeField)) || IsReference(f)))
                .ToArray();
        }

        public static bool IsReference(FieldInfo field) => field.IsDefined(typeof(SerializeReference));
        public static bool IsBranchList(FieldInfo field) => field.IsDefined(typeof(ScrapWeaver.BranchesAttribute));
        // Element type of a branch slot. The parent block creates it, so it is not shown in the palette
        public static bool IsBranch(Type type) =>
            (branchTypes ??= new HashSet<Type>(All.SelectMany(b => PartFields(b.Type)).Where(IsBranchList).Select(f => ElementType(f.FieldType)))).Contains(type);
        static HashSet<Type> branchTypes;
        static bool HoldsPart(FieldInfo field) => typeof(ScrapPart).IsAssignableFrom(ElementType(field.FieldType));

        // An effect node's mouth — a [SerializeReference] field holding ScrapParts
        public static FieldInfo[] PartFields(Type type) =>
            partFields.TryGetValue(type, out var cached) ? cached : partFields[type] = Fields(type).Where(f => IsReference(f) && HoldsPart(f)).ToArray();

        // A socket for sensing blocks — a [SerializeReference] field that is not a part
        public static FieldInfo[] SocketFields(Type type) =>
            socketFields.TryGetValue(type, out var cached) ? cached : socketFields[type] = Fields(type).Where(f => IsReference(f) && !HoldsPart(f)).ToArray();

        public static bool IsCollection(Type type) => type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>));
        public static Type ElementType(Type fieldType) =>
            fieldType.IsArray ? fieldType.GetElementType() : IsCollection(fieldType) ? fieldType.GetGenericArguments()[0] : fieldType;

        // ── Shape · Category · Color ──

        // Shape is decided by base class: part · condition · value · other (trigger event)
        public static BlockShape ShapeOf(Type baseType)
        {
            if (shapes.TryGetValue(baseType, out var cached)) return cached;
            return shapes[baseType] =
                typeof(ScrapPart).IsAssignableFrom(baseType) ? BlockShape.Part :
                typeof(ScrapCondition).IsAssignableFrom(baseType) ? BlockShape.Condition :
                typeof(ValueSource).IsAssignableFrom(baseType) || typeof(DataSource).IsAssignableFrom(baseType) ? BlockShape.Value :
                BlockShape.Event;
        }

        public static BlockShape ShapeOfBlock(Type type) => byType.TryGetValue(type, out var b) ? b.Shape : BlockShape.Part;

        // Parts inherit the [Category] on their base (ActionBlock, FireBlock, ControlBlock)
        static string CategoryOf(Type type, BlockShape shape)
        {
            string declared = type.GetCustomAttribute<ScrapWeaver.CategoryAttribute>(true)?.Name;
            if (!string.IsNullOrEmpty(declared)) return declared;
            return shape switch
            {
                BlockShape.Part => ActionCategory,
                _ => SensingCategory,
            };
        }

        // Modules (bridges to game C#): Module-category blocks and module/script sockets
        public static readonly Color ModuleColor = new(0.8f, 0.32f, 0.62f);
        static HashSet<string> moduleNames;

        public static Color CategoryColor(string category)
        {
            int index = Array.IndexOf(CategoryOrder, category);
            if (index >= 0) return CategoryColors[index];
            moduleNames ??= new HashSet<string>(TypeCache.GetTypesWithAttribute<ScrapWeaver.ModuleAttribute>().Select(t => t.GetCustomAttribute<ScrapWeaver.ModuleAttribute>().Name));
            if (moduleNames.Contains(category)) return ModuleColor;
            int other = Categories.Where(c => OrderOf(c) == CategoryOrder.Length).ToList().IndexOf(category);
            return OtherColors[Mathf.Max(0, other) % OtherColors.Length];
        }

        // Empty socket outline color: the category color of the blocks it accepts
        public static Color ShapeColor(BlockShape shape) => CategoryColor(shape switch
        {
            BlockShape.Part => ActionCategory,
            _ => SensingCategory,
        });

        public static Color ColorOf(Type type) => byType.TryGetValue(type, out var b) ? CategoryColor(b.Category) : CategoryColor(ActionCategory);

        // ── Names ──

        // Reads [ScrapWeaver.Display] and lays out the title line as pieces.
        // Order: class Display → sockets with a field Display → remaining sockets. With no Display at all: class name + sockets
        public static List<TitlePart> TitleParts(Type type)
        {
            if (titlesVersion != ScrapterText.Version)
            {
                titles.Clear();
                displays.Clear();
                titlesVersion = ScrapterText.Version;
            }
            if (titles.TryGetValue(type, out var cached)) return cached;
            var parts = new List<TitlePart>();
            FieldInfo[] sockets = SocketFields(type);
            // A class Display may name plain value fields as well as sockets — those fields appear in the title line instead of the body
            FieldInfo[] inline = Fields(type).Where(f => !IsReference(f) || !HoldsPart(f)).ToArray();
            var placed = new HashSet<FieldInfo>();

            void Add(IEnumerable<string> texts, FieldInfo own)
            {
                int start = parts.Count;
                foreach (string text in texts)
                {
                    FieldInfo field = inline.FirstOrDefault(f => f.Name == text);
                    if (field != null && placed.Add(field)) parts.Add(new TitlePart(null, field));
                    else if (!string.IsNullOrWhiteSpace(text)) parts.Add(new TitlePart(text.Trim(), null));
                }
                // On a field Display that doesn't name its own field, the socket goes before the text
                if (own != null && placed.Add(own)) parts.Insert(start, new TitlePart(null, own));
            }

            bool IsField(string text) => inline.Any(f => f.Name == text);

            string[] classHeader = type.GetCustomAttribute<ScrapWeaver.DisplayAttribute>(false)?.Parts;
            if (classHeader != null) Add(ScrapterText.Display(classHeader, IsField), null);
            else if (!sockets.Any(f => f.IsDefined(typeof(ScrapWeaver.DisplayAttribute))))
                parts.Add(new TitlePart(ScrapterText.T(ObjectNames.NicifyVariableName(type.Name)), null));

            foreach (FieldInfo field in sockets.Where(f => !placed.Contains(f)))
            {
                string[] fieldHeader = field.GetCustomAttribute<ScrapWeaver.DisplayAttribute>()?.Parts;
                if (fieldHeader != null) Add(ScrapterText.Display(fieldHeader, IsField), field);
                else if (placed.Add(field)) parts.Add(new TitlePart(null, field));
            }
            return titles[type] = parts;
        }

        // One-line name for the palette, search, and empty sockets. Sockets show as [ ]
        public static string DisplayOf(Type type)
        {
            List<TitlePart> parts = TitleParts(type);
            return displays.TryGetValue(type, out string cached) ? cached
                : displays[type] = string.Join(" ", parts.Select(p => p.Field != null ? "[ ]" : p.Text));
        }

        // Plain value fields moved up to the title line — excluded from the body
        public static bool InTitle(Type type, FieldInfo field) => TitleParts(type).Any(p => p.Field == field);

        // Label for a value field or child slot: the first text of [ScrapWeaver.Display], else the field name
        public static string FieldLabel(FieldInfo field) => ScrapterText.T(
            field.GetCustomAttribute<ScrapWeaver.DisplayAttribute>()?.Parts.FirstOrDefault(p => p != field.Name && !string.IsNullOrWhiteSpace(p))
            ?? ObjectNames.NicifyVariableName(field.Name));
    }
}
