// CSharp4Downgrader - text edit bookkeeping.
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace CSharp4Downgrader
{
    /// <summary>One replacement (Length &gt; 0) or insertion (Length == 0) in the original text.</summary>
    internal sealed class Edit
    {
        public int Start;
        public int Length;
        public string Text;
        public int Seq;
        public int End { get { return Start + Length; } }
    }

    /// <summary>A set of edits that is applied completely or not at all.</summary>
    internal sealed class EditGroup
    {
        public readonly string Stat;
        public readonly List<Edit> Edits = new List<Edit>();
        public int StatCount = 1;
        public List<Action> OnCommit;

        public EditGroup(string stat) { Stat = stat; }

        public void Replace(TextSpan span, string text)
        {
            Edits.Add(new Edit { Start = span.Start, Length = span.Length, Text = text });
        }
        public void Replace(int start, int end, string text)
        {
            Edits.Add(new Edit { Start = start, Length = end - start, Text = text });
        }
        public void Insert(int position, string text)
        {
            Edits.Add(new Edit { Start = position, Length = 0, Text = text });
        }
        public void Delete(TextSpan span) { Replace(span, ""); }
        public void AfterCommit(Action a)
        {
            if (OnCommit == null) OnCommit = new List<Action>();
            OnCommit.Add(a);
        }
    }

    /// <summary>The accepted edits of one file in one pass.</summary>
    internal sealed class FileEdits
    {
        private readonly List<Edit> _edits = new List<Edit>();
        private int _seq;

        public bool IsEmpty { get { return _edits.Count == 0; } }
        public int Count { get { return _edits.Count; } }

        private static bool Conflict(Edit a, Edit b)
        {
            if (a.Length == 0 && b.Length == 0) return false;
            if (a.Length == 0) return b.Start < a.Start && a.Start < b.End;
            if (b.Length == 0) return a.Start < b.Start && b.Start < a.End;
            return a.Start < b.End && b.Start < a.End;
        }

        /// <summary>Accepts the group unless one of its edits overlaps an edit accepted earlier in this pass.</summary>
        public bool TryAdd(EditGroup g, bool checkConflicts = true)
        {
            if (g.Edits.Count == 0) return false;
            if (checkConflicts) {
                foreach (Edit e in g.Edits) {
                    foreach (Edit x in _edits) {
                        if (Conflict(e, x)) return false;
                    }
                }
            }
            foreach (Edit e in g.Edits) {
                e.Seq = _seq++;
                _edits.Add(e);
            }
            return true;
        }

        public string Apply(string text)
        {
            List<Edit> sorted = new List<Edit>(_edits);
            sorted.Sort(delegate(Edit a, Edit b) {
                int c = a.Start.CompareTo(b.Start);
                if (c != 0) return c;
                int ka = a.Length == 0 ? 0 : 1;
                int kb = b.Length == 0 ? 0 : 1;
                c = ka.CompareTo(kb);
                if (c != 0) return c;
                return a.Seq.CompareTo(b.Seq);
            });
            StringBuilder sb = new StringBuilder(text.Length + 1024);
            int pos = 0;
            foreach (Edit e in sorted) {
                if (e.Start < pos) throw new InvalidOperationException("overlapping edits at " + e.Start);
                sb.Append(text, pos, e.Start - pos);
                sb.Append(e.Text);
                pos = e.End;
            }
            sb.Append(text, pos, text.Length - pos);
            return sb.ToString();
        }
    }
}
