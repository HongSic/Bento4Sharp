// CSharp4Downgrader - local functions that capture nothing become private methods.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSharp4Downgrader
{
    internal sealed partial class Rewriter
    {
        private void HandleLocalFunction(LocalFunctionStatementSyntax node)
        {
            const string what = "local function";
            if (node.ExpressionBody != null) return; // after it got a block body
            IMethodSymbol ms = Model.GetDeclaredSymbol(node) as IMethodSymbol;
            SyntaxNode member = MemberOf(node);
            if (ms == null || !(member.Parent is TypeDeclarationSyntax) || !(node.Parent is BlockSyntax) || !IsFirstOnLine(node.SpanStart)) {
                Manual(node, what, "cannot be moved automatically; turn it into a private method by hand");
                return;
            }
            if (node.Ancestors().OfType<LocalFunctionStatementSyntax>().Any() || node.Ancestors().OfType<AnonymousFunctionExpressionSyntax>().Any()) {
                return; // the enclosing function first (or a lambda: reported below when it captures)
            }
            bool needsInstance = false;
            foreach (SyntaxNode n in node.DescendantNodes()) {
                if (n is ThisExpressionSyntax || n is BaseExpressionSyntax) { needsInstance = true; continue; }
                IdentifierNameSyntax id = n as IdentifierNameSyntax;
                if (id == null) continue;
                ISymbol s = Model.GetSymbolInfo(id).Symbol;
                if (s == null) continue;
                if (s is ILocalSymbol || s is IParameterSymbol || s is IRangeVariableSymbol) {
                    SyntaxReference r = s.DeclaringSyntaxReferences.FirstOrDefault();
                    if (r == null || r.SyntaxTree != node.SyntaxTree || !node.Span.Contains(r.Span)) {
                        Manual(node, what, "captures '" + s.Name + "' of the enclosing method: turn it into a private method with extra parameters (or a Func<>/Action<> delegate) by hand");
                        return;
                    }
                } else if (s is ITypeParameterSymbol) {
                    ITypeParameterSymbol tp = (ITypeParameterSymbol)s;
                    if (tp.DeclaringMethod != null && !SymbolEqualityComparer.Default.Equals(tp.DeclaringMethod, ms)) {
                        Manual(node, what, "uses type parameter '" + s.Name + "' of the enclosing method: turn it into a private generic method by hand");
                        return;
                    }
                } else if (s is IMethodSymbol && ((IMethodSymbol)s).MethodKind == MethodKind.LocalFunction) {
                    if (!SymbolEqualityComparer.Default.Equals(s, ms)) {
                        SyntaxReference r = s.DeclaringSyntaxReferences.FirstOrDefault();
                        if (r != null && !node.Span.Contains(r.Span)) return; // wait until the sibling local function has moved
                    }
                } else if ((s is IFieldSymbol || s is IPropertySymbol || s is IMethodSymbol || s is IEventSymbol) && !s.IsStatic && !IsMemberNamePosition(id)) {
                    if (!(s is IMethodSymbol) || ((IMethodSymbol)s).MethodKind == MethodKind.Ordinary) needsInstance = true;
                }
            }
            INamedTypeSymbol ct = ms.ContainingType;
            if (ct != null && TypeHasMember(ct, node.Identifier.ValueText)) {
                Manual(node, what, "the containing type already has a member named '" + node.Identifier.ValueText + "': move and rename by hand");
                return;
            }
            string mi = IndentOf(member.SpanStart), ni = IndentOf(node.SpanStart);
            int cut = Math.Max(0, ni.Length - mi.Length);
            int firstLine = _c.Text.Lines.GetLineFromPosition(node.SpanStart).LineNumber;
            while (firstLine > 0 && _c.Text.Lines[firstLine - 1].ToString().TrimStart().StartsWith("//", StringComparison.Ordinal)) firstLine--;
            int lastLine = _c.Text.Lines.GetLineFromPosition(node.Span.End).LineNumber;
            if (!string.IsNullOrWhiteSpace(TextOf(node.Span.End, _c.Text.Lines[lastLine].End))) {
                string tail = TextOf(node.Span.End, _c.Text.Lines[lastLine].End).Trim();
                if (!tail.StartsWith("//", StringComparison.Ordinal)) { Manual(node, what, "unusual layout; move by hand"); return; }
            }
            StringBuilder sb = new StringBuilder();
            sb.Append(NL);
            SyntaxNode root = _c.Tree.GetRoot();
            for (int i = firstLine; i <= lastLine; i++) {
                TextLine line = _c.Text.Lines[i];
                string s = _c.Text.ToString(line.SpanIncludingLineBreak);
                if (i == lastLine && line.EndIncludingLineBreak == line.End) s += NL;
                bool dedent = IsIndentable(root, _c.Text, line) && s.Length >= cut && s.Substring(0, cut).Trim().Length == 0;
                if (dedent) s = s.Substring(cut);
                if (line.Start <= node.SpanStart && node.SpanStart <= line.End) {
                    // the declaration line: modifiers
                    int col = node.SpanStart - line.Start - (dedent ? cut : 0);
                    string head = s.Substring(0, col), rest = s.Substring(col);
                    foreach (SyntaxToken m in node.Modifiers) {
                        if (m.IsKind(SyntaxKind.StaticKeyword) && rest.StartsWith("static ", StringComparison.Ordinal)) rest = rest.Substring(7);
                    }
                    s = head + "private " + (needsInstance ? "" : "static ") + rest;
                }
                sb.Append(s);
            }
            EditGroup g = new EditGroup("local function (no captures) -> private method");
            int removeFrom = firstLine;
            if (firstLine > 0 && _c.Text.Lines[firstLine - 1].ToString().Trim().Length == 0 && lastLine + 1 < _c.Text.Lines.Count &&
                _c.Text.Lines[lastLine + 1].ToString().TrimStart().StartsWith("}", StringComparison.Ordinal)) removeFrom = firstLine - 1; // the blank line in front of it
            g.Replace(_c.Text.Lines[removeFrom].Start, _c.Text.Lines[lastLine].EndIncludingLineBreak, "");
            TextLine memberEnd = _c.Text.Lines.GetLineFromPosition(member.Span.End);
            if (memberEnd.EndIncludingLineBreak == memberEnd.End) { Manual(node, what, "unusual layout; move by hand"); return; }
            g.Insert(memberEnd.EndIncludingLineBreak, sb.ToString());
            Commit(g);
        }
    }
}
