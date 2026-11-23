using System;
using UnityEngine;

namespace FireFlower
{
    // Adds a type dropdown to a [SerializeReference] field so a concrete subclass can be picked in the Inspector
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SubclassSelectorAttribute : PropertyAttribute { }
}
