using System;
using UnityEngine;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Drives an Inspector dropdown (see the Editor-only property drawer) for a string field
    /// that stores the AssemblyQualifiedName of a concrete type derived from <see cref="BaseType"/>.
    /// </summary>
    public class HandlerTypeReferenceAttribute : PropertyAttribute
    {
        public readonly Type BaseType;

        public HandlerTypeReferenceAttribute(Type baseType)
        {
            BaseType = baseType;
        }
    }
}
