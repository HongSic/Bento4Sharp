// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4List.h (Operator/Finder helpers only)
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    // PORT NOTE: AP4_List<T> itself is replaced by System.Collections.Generic.List<T>
    // (see docs/PORTING.md). Only the Operator/Finder visitor idiom is kept, as
    // extension methods on List<T>, so that code such as
    //     m_Children.Apply(AP4_AtomListWriter(stream));
    // ports mechanically to
    //     _children.Apply(new AtomListWriter(stream));

    /// <summary>AP4_List&lt;T&gt;::Item::Operator</summary>
    public interface IListOperator<T>
    {
        Ap4Result Action(T data);
    }

    /// <summary>AP4_List&lt;T&gt;::Item::Finder</summary>
    public interface IListFinder<T>
    {
        Ap4Result Test(T data);
    }

    public static class ListExtensions
    {
        /*----------------------------------------------------------------------
        |   AP4_List<T>::Apply
        +---------------------------------------------------------------------*/
        /// <summary>Calls Action on every item. Like upstream, the results of Action are IGNORED: always returns SUCCESS.</summary>
        public static Ap4Result Apply<T>(this List<T> list, IListOperator<T> op)
        {
            // PORT NOTE: indexed loop (not foreach) so that an Action that
            // modifies the list does not throw; upstream walks the linked list.
            for (int i = 0; i < list.Count; i++) {
                op.Action(list[i]);
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_List<T>::ApplyUntilFailure
        +---------------------------------------------------------------------*/
        public static Ap4Result ApplyUntilFailure<T>(this List<T> list, IListOperator<T> op)
        {
            for (int i = 0; i < list.Count; i++) {
                Ap4Result result;
                result = op.Action(list[i]);
                if (result != Ap4Result.SUCCESS) return result;
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_List<T>::ApplyUntilSuccess
        +---------------------------------------------------------------------*/
        public static Ap4Result ApplyUntilSuccess<T>(this List<T> list, IListOperator<T> op)
        {
            for (int i = 0; i < list.Count; i++) {
                Ap4Result result;
                result = op.Action(list[i]);
                if (result == Ap4Result.SUCCESS) return Ap4Result.SUCCESS;
            }

            return Ap4Result.FAILURE;
        }

        /*----------------------------------------------------------------------
        |   AP4_List<T>::ReverseApply
        +---------------------------------------------------------------------*/
        public static Ap4Result ReverseApply<T>(this List<T> list, IListOperator<T> op)
        {
            for (int i = list.Count-1; i >= 0; i--) {
                if (op.Action(list[i]) != Ap4Result.SUCCESS) {
                    return Ap4Result.ERROR_LIST_OPERATION_ABORTED;
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_List<T>::Find
        +---------------------------------------------------------------------*/
        public static Ap4Result Find<T>(this List<T> list, IListFinder<T> finder, out T data) where T : class
        {
            for (int i = 0; i < list.Count; i++) {
                if (finder.Test(list[i]) == Ap4Result.SUCCESS) {
                    data = list[i];
                    return Ap4Result.SUCCESS;
                }
            }

            data = null;
            return Ap4Result.ERROR_NO_SUCH_ITEM;
        }

        /*----------------------------------------------------------------------
        |   AP4_List<T>::ReverseFind
        +---------------------------------------------------------------------*/
        public static Ap4Result ReverseFind<T>(this List<T> list, IListFinder<T> finder, out T data) where T : class
        {
            for (int i = list.Count-1; i >= 0; i--) {
                if (finder.Test(list[i]) == Ap4Result.SUCCESS) {
                    data = list[i];
                    return Ap4Result.SUCCESS;
                }
            }

            data = null;
            return Ap4Result.ERROR_NO_SUCH_ITEM;
        }
    }
}
