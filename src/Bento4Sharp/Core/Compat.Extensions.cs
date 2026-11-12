// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Not ported from upstream: support code for the older target frameworks.
// Licensed under the GNU General Public License v2 or later. See LICENSE.

// PORT NOTE: the second part of Compat.cs (see there), compiled into the
// library only:
//  - extension methods standing in for instance members that the old BCL
//    lacks (so that the call sites are identical for every target);
//  - for net20 only: the attribute that lets the C# compiler accept extension
//    methods on .NET Framework 2.0.
// The tools do not get this file. They declare no extension method of their
// own, so on net20 they need no ExtensionAttribute; a second definition next
// to the one in Bento4Sharp.dll would make the Visual Studio 2010 compiler
// report CS1685 for every tool.
// Nothing in this file is compiled for net8.0.

#if !NET6_0_OR_GREATER

using System;
using System.Collections.Generic;

namespace Bento4
{

    internal static partial class Compat
    {
        /// <summary>List&lt;T&gt;.EnsureCapacity (.NET 6+).</summary>
        public static int EnsureCapacity<T>(this List<T> list, int capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException("capacity");
            if (list.Capacity < capacity) list.Capacity = capacity;
            return list.Capacity;
        }

        /// <summary>Dictionary&lt;K,V&gt;.TryAdd (.NET Core 2.0+).</summary>
        public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (dictionary.ContainsKey(key)) return false;
            dictionary.Add(key, value);
            return true;
        }
    }

}

#if NET20
namespace System.Runtime.CompilerServices
{
    // PORT NOTE: the C# compiler marks extension methods (and the classes and
    // the assembly that hold them) with this attribute, which is in System.Core
    // (.NET Framework 3.5). .NET Framework 2.0 has no System.Core; the compiler
    // accepts any type of this name, so the net20 library brings its own.
    // It is internal on purpose: a project for a newer framework that
    // references the net20 library must not see a second ExtensionAttribute.
    // (Compilers older than Roslyn, such as that of Visual Studio 2010, still
    // notice it when such a project declares extension methods of its own and
    // also references System.Core: they report warning CS1685 and go on.)
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
    internal sealed class ExtensionAttribute : Attribute
    {
    }
}
#endif
#endif
