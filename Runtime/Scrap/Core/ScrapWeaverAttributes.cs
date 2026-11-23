using System;
using FireFlower;

// For ScrapWeaver editor display only. No effect on runtime behavior
namespace ScrapWeaver
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, Inherited = false)]
    public sealed class DisplayAttribute : Attribute
    {
        public string[] Parts { get; }
        public DisplayAttribute(params string[] parts) => Parts = parts ?? Array.Empty<string>();
    }
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class CategoryAttribute : Attribute
    {
        public string Name { get; }
        public CategoryAttribute(string name) => Name = name;
    }

    // Per-type versions of the same block (number / Vector2 / Vector3 math, etc.). Only the primary shows in the palette;
    // when plugged into a socket, the editor creates the version matching that socket's type
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class FamilyAttribute : Attribute
    {
        public string Name { get; }
        public bool Primary { get; }
        public FamilyAttribute(string name, bool primary = false)
        {
            Name = name;
            Primary = primary;
        }
    }

    // Game-specific block hidden from the shared palette of other editors (mob, etc.)
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class NotCommonAttribute : Attribute { }

    // This class fills all of its own runtime (non-serialized) fields itself when enabled (OnEnabled / Run).
    // With it, reusing a copy skips reverting those fields (fast). Without it, they are reset from the template (safe)
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ReusableAttribute : Attribute { }

    // Cap block: the stack ends here (forever, end state). No bottom notch; nothing can attach after it
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class CapAttribute : Attribute { }

    // Module block. Shown in the palette only when the edited data imports this module
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ModuleAttribute : Attribute
    {
        public string Name { get; }
        public ModuleAttribute(string name) => Name = name;
    }
    // Draws a string field as a variable picker. scopeField is the sibling field that stores the scope; type is the selectable variable kind
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class VariableKeyAttribute : UnityEngine.PropertyAttribute
    {
        public string ScopeField { get; }
        public VariableType Type { get; }
        // Component variables: sibling field that stores the component name on pick
        public string ComponentField { get; set; }
        public VariableKeyAttribute(string scopeField, VariableType type = VariableType.Number)
        {
            ScopeField = scopeField;
            Type = type;
        }
    }
    // Draws a string field as a member path (Component.member…) editor. The picker menu starts from the members of rootField's type (a sibling ObjectSource field)
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MemberPathAttribute : UnityEngine.PropertyAttribute
    {
        public readonly string RootField;
        public MemberPathAttribute(string rootField) => RootField = rootField;
    }
    // Draws a string field as a component name picker
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ComponentTypeAttribute : UnityEngine.PropertyAttribute { }
    // Draws a string field as an enum value picker ("Type.Value")
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class EnumNameAttribute : UnityEngine.PropertyAttribute { }
    // Draws a LocalizedString field as a speaker picker (Speaker/ entries in the Dialogue table)
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SpeakerAttribute : Attribute { }
    // Array elements are the parent block's mouths (branches). Not shown in the palette; added and removed with + / − on the parent block
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class BranchesAttribute : Attribute { }
}
