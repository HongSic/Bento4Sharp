// CSharp4Downgrader - transformations that do not restructure statements:
// nullable annotations, literals, nameof, modifiers, expression bodies, default, new(), interpolation, tuples.
using System;
using System.Collections.Generic;
using System.Globalization;
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
        // ------------------------------------------------------------------ nullable reference types

        /// <summary>true: Nullable&lt;T&gt; (stays); false: annotation on a reference type (goes); null: unknown.</summary>
        private bool? IsNullableValueTypeSyntax(NullableTypeSyntax node)
        {
            ITypeSymbol t = Model.GetTypeInfo(node).Type ?? Model.GetSymbolInfo(node).Symbol as ITypeSymbol;
            if (!IsErrorOrNull(t)) return IsNullableValue(t);
            ITypeSymbol et = Model.GetTypeInfo(node.ElementType).Type ?? Model.GetSymbolInfo(node.ElementType).Symbol as ITypeSymbol;
            if (!IsErrorOrNull(et)) {
                ITypeParameterSymbol tp = et as ITypeParameterSymbol;
                if (tp != null) return tp.HasValueTypeConstraint || tp.HasUnmanagedTypeConstraint;
                return et.IsValueType;
            }
            PredefinedTypeSyntax pre = node.ElementType as PredefinedTypeSyntax;
            if (pre != null) return !(pre.Keyword.IsKind(SyntaxKind.StringKeyword) || pre.Keyword.IsKind(SyntaxKind.ObjectKeyword));
            if (node.ElementType is ArrayTypeSyntax) return false;
            return null;
        }

        /// <summary>Whether a blank should take the place of a removed `?` so that an aligned column stays aligned.</summary>
        private bool PadAfterRemoval(SyntaxToken q)
        {
            int end = q.Span.End;
            TextLine line = _c.Text.Lines.GetLineFromPosition(end);
            string s = line.ToString();
            int col = end - line.Start;
            int ws = 0;
            while (col + ws < s.Length && s[col + ws] == ' ') ws++;
            if (ws == 0 || col + ws >= s.Length) return false;
            if (ws >= 2) return true;
            int c = col + 1;
            for (int d = -1; d <= 1; d += 2) {
                int n = line.LineNumber + d;
                if (n < 0 || n >= _c.Text.Lines.Count) continue;
                string t = _c.Text.Lines[n].ToString();
                if (t.Length <= c || c < 2) continue;
                if (t[c] == ' ' || t[c - 1] != ' ') continue;
                if (t[c - 2] == '?') continue;                        // that line shifts as well
                if (t.Substring(0, c).Trim().Length == 0) continue;  // just its indentation
                if (!(char.IsLetter(t[c]) || t[c] == '_' || t[c] == '@')) continue;
                return true;
            }
            return false;
        }

        public override void VisitNullableType(NullableTypeSyntax node)
        {
            bool? value = IsNullableValueTypeSyntax(node);
            if (value == false) {
                EditGroup g = new EditGroup("nullable reference annotation `T?` removed");
                if (PadAfterRemoval(node.QuestionToken)) g.Replace(node.QuestionToken.Span, " ");
                else g.Delete(node.QuestionToken.Span);
                Commit(g);
            } else if (value == null) {
                Manual(node, "nullable type", "cannot tell whether `" + node + "` is a nullable value type or an annotated reference type (the type does not bind); remove the `?` by hand if it is a reference type");
            }
            base.VisitNullableType(node);
        }

        public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.SuppressNullableWarningExpression)) {
                EditGroup g = new EditGroup("null-forgiving `!` removed");
                g.Delete(node.OperatorToken.Span);
                Commit(g);
            }
            base.VisitPostfixUnaryExpression(node);
        }

        public override void VisitNullableDirectiveTrivia(NullableDirectiveTriviaSyntax node)
        {
            EditGroup g = new EditGroup("`#nullable` directive removed");
            TextLine line = _c.Text.Lines.GetLineFromPosition(node.SpanStart);
            g.Delete(line.SpanIncludingLineBreak);
            Commit(g);
            base.VisitNullableDirectiveTrivia(node);
        }

        /// <summary>`#pragma warning disable CS0675` -> `#pragma warning disable 0675` (compilers before C# 6 only take numbers).</summary>
        public override void VisitPragmaWarningDirectiveTrivia(PragmaWarningDirectiveTriviaSyntax node)
        {
            List<int> drop = new List<int>();
            for (int i = 0; i < node.ErrorCodes.Count; i++) {
                IdentifierNameSyntax id = node.ErrorCodes[i] as IdentifierNameSyntax;
                if (id == null) continue;
                string t = id.Identifier.ValueText;
                if (t.Length > 2 && t.StartsWith("CS", StringComparison.Ordinal) && t.Substring(2).All(char.IsDigit)) {
                    EditGroup g = new EditGroup("`#pragma warning disable CSnnnn` -> numeric id");
                    g.Replace(id.Span, t.Substring(2));
                    Commit(g);
                } else {
                    drop.Add(i);
                }
            }
            if (drop.Count > 0) {
                EditGroup g = new EditGroup("`#pragma warning` for a non-compiler id (analyzer) removed");
                if (drop.Count == node.ErrorCodes.Count) {
                    g.StatCount = drop.Count;
                    g.Delete(_c.Text.Lines.GetLineFromPosition(node.SpanStart).SpanIncludingLineBreak);
                } else {
                    RemoveFromList(g, node.ErrorCodes, drop[0]);
                }
                Commit(g);
            }
            base.VisitPragmaWarningDirectiveTrivia(node);
        }

        private static readonly HashSet<string> NullableFlowAttributes = new HashSet<string>(StringComparer.Ordinal) {
            "AllowNull", "DisallowNull", "MaybeNull", "NotNull", "MaybeNullWhen", "NotNullWhen", "NotNullIfNotNull",
            "DoesNotReturn", "DoesNotReturnIf", "MemberNotNull", "MemberNotNullWhen"
        };

        private bool IsNullableFlowAttribute(AttributeSyntax a)
        {
            string name = a.Name.ToString();
            int dot = name.LastIndexOf('.');
            if (dot >= 0) name = name.Substring(dot + 1);
            if (name.EndsWith("Attribute", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 9);
            if (!NullableFlowAttributes.Contains(name)) return false;
            ISymbol s = Model.GetSymbolInfo(a).Symbol;
            if (s == null || s.ContainingType == null) return true;
            INamespaceSymbol ns = s.ContainingType.ContainingNamespace;
            return ns != null && ns.ToDisplayString() == "System.Diagnostics.CodeAnalysis";
        }

        private void DeleteWithTrailingBlank(EditGroup g, int start, int end)
        {
            // a construct alone on its line takes the line with it; otherwise the blanks that follow it go
            TextLine line = _c.Text.Lines.GetLineFromPosition(start);
            TextLine endLine = _c.Text.Lines.GetLineFromPosition(end);
            if (string.IsNullOrWhiteSpace(TextOf(line.Start, start)) && string.IsNullOrWhiteSpace(TextOf(end, endLine.End))) {
                g.Replace(line.Start, endLine.EndIncludingLineBreak, "");
                return;
            }
            int e = end;
            string text = _c.Text.ToString();
            while (e < text.Length && (text[e] == ' ' || text[e] == '\t')) e++;
            g.Replace(start, e, "");
        }

        private void RemoveFromList<T>(EditGroup g, SeparatedSyntaxList<T> list, int index) where T : SyntaxNode
        {
            if (index > 0) g.Replace(list.GetSeparator(index - 1).SpanStart, list[index].Span.End, "");
            else g.Replace(list[0].SpanStart, list[1].SpanStart, "");
        }

        public override void VisitAttributeList(AttributeListSyntax node)
        {
            List<int> drop = new List<int>();
            for (int i = 0; i < node.Attributes.Count; i++) {
                if (IsNullableFlowAttribute(node.Attributes[i])) drop.Add(i);
            }
            if (drop.Count > 0) {
                EditGroup g = new EditGroup("nullable flow attribute ([NotNullWhen] ...) removed");
                if (drop.Count == node.Attributes.Count) {
                    g.StatCount = drop.Count;
                    DeleteWithTrailingBlank(g, node.SpanStart, node.Span.End);
                } else {
                    RemoveFromList(g, node.Attributes, drop[0]);
                }
                Commit(g);
            }
            base.VisitAttributeList(node);
        }

        public override void VisitTypeParameterConstraintClause(TypeParameterConstraintClauseSyntax node)
        {
            List<int> drop = new List<int>();
            for (int i = 0; i < node.Constraints.Count; i++) {
                TypeParameterConstraintSyntax c = node.Constraints[i];
                ClassOrStructConstraintSyntax cs = c as ClassOrStructConstraintSyntax;
                if (cs != null && !cs.QuestionToken.IsKind(SyntaxKind.None)) {
                    EditGroup g = new EditGroup("`class?` constraint -> `class`");
                    g.Delete(cs.QuestionToken.Span);
                    Commit(g);
                }
                TypeConstraintSyntax tc = c as TypeConstraintSyntax;
                if (tc != null && tc.Type is IdentifierNameSyntax) {
                    string n = ((IdentifierNameSyntax)tc.Type).Identifier.ValueText;
                    if ((n == "notnull" || n == "unmanaged") && !(Model.GetSymbolInfo(tc.Type).Symbol is ITypeSymbol)) {
                        if (n == "notnull") drop.Add(i);
                        else Manual(c, "`unmanaged` constraint", "not available in C# 4: use `struct` if that is enough");
                    }
                }
                if (c is DefaultConstraintSyntax) drop.Add(i);
            }
            if (drop.Count > 0) {
                EditGroup g = new EditGroup("`notnull` / `default` constraint removed");
                if (drop.Count == node.Constraints.Count) {
                    g.StatCount = drop.Count;
                    int start = node.SpanStart;
                    string text = _c.Text.ToString();
                    while (start > 0 && (text[start - 1] == ' ' || text[start - 1] == '\t')) start--;
                    g.Replace(start, node.Span.End, "");
                } else {
                    RemoveFromList(g, node.Constraints, drop[0]);
                }
                Commit(g);
            }
            base.VisitTypeParameterConstraintClause(node);
        }

        // ------------------------------------------------------------------ literals, nameof

        public override void VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.NumericLiteralExpression)) {
                string t = node.Token.Text;
                bool binary = t.Length > 2 && t[0] == '0' && (t[1] == 'b' || t[1] == 'B');
                if (binary) {
                    string digits = t.Substring(2);
                    int e = digits.Length;
                    while (e > 0 && "uUlL".IndexOf(digits[e - 1]) >= 0) e--;
                    string suffix = digits.Substring(e);
                    object v = node.Token.Value;
                    ulong value = v is int ? (ulong)(uint)(int)v : v is uint ? (uint)v : v is long ? (ulong)(long)v : v is ulong ? (ulong)v : 0;
                    if (v is int || v is uint || v is long || v is ulong) {
                        EditGroup g = new EditGroup("binary literal -> hexadecimal literal");
                        g.Replace(node.Token.Span, "0x" + value.ToString("X", CultureInfo.InvariantCulture) + suffix);
                        Commit(g);
                    } else {
                        Manual(node, "binary literal", "unexpected literal value type");
                    }
                } else if (t.IndexOf('_') >= 0) {
                    EditGroup g = new EditGroup("digit separators removed");
                    g.Replace(node.Token.Span, t.Replace("_", ""));
                    Commit(g);
                }
            } else if (node.IsKind(SyntaxKind.DefaultLiteralExpression)) {
                TypeInfo ti = Model.GetTypeInfo(node);
                string tn = TypeName(ti.ConvertedType ?? ti.Type, node.SpanStart);
                if (tn == null) {
                    Manual(node, "`default` literal", "cannot determine the type; write `default(T)` by hand");
                } else {
                    EditGroup g = new EditGroup("`default` literal -> `default(T)`");
                    g.Replace(node.Span, "default(" + tn + ")");
                    Commit(g);
                }
            } else if (node.IsKind(SyntaxKind.Utf8StringLiteralExpression)) {
                Manual(node, "UTF-8 string literal", "not available in C# 4");
            } else if (node.IsKind(SyntaxKind.StringLiteralExpression)) {
                SyntaxKind k = node.Token.Kind();
                if (k == SyntaxKind.SingleLineRawStringLiteralToken || k == SyntaxKind.MultiLineRawStringLiteralToken) {
                    EditGroup g = new EditGroup("raw string literal -> regular string literal");
                    g.Replace(node.Token.Span, Quote((string)node.Token.Value));
                    Commit(g);
                }
            }
            base.VisitLiteralExpression(node);
        }

        internal static string Quote(string s)
        {
            StringBuilder sb = new StringBuilder("\"");
            foreach (char ch in s) {
                switch (ch) {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\0': sb.Append("\\0"); break;
                    default:
                        if (ch < ' ' || ch == (char)0x2028 || ch == (char)0x2029) sb.Append((char)92).Append('u').Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture));
                        else sb.Append(ch);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            IdentifierNameSyntax id = node.Expression as IdentifierNameSyntax;
            if (id != null && id.Identifier.ValueText == "nameof" && node.ArgumentList.Arguments.Count == 1) {
                SymbolInfo si = Model.GetSymbolInfo(node);
                Optional<object> cv = Model.GetConstantValue(node);
                if (si.Symbol == null && si.CandidateSymbols.Length == 0) {
                    string value = cv.HasValue ? cv.Value as string : null;
                    if (value == null) {
                        // unbound operand: the name is the last identifier of the operand
                        ExpressionSyntax arg = node.ArgumentList.Arguments[0].Expression;
                        SimpleNameSyntax last = arg as SimpleNameSyntax;
                        MemberAccessExpressionSyntax ma = arg as MemberAccessExpressionSyntax;
                        if (ma != null) last = ma.Name;
                        if (last != null) value = last.Identifier.ValueText;
                    }
                    if (value != null) {
                        EditGroup g = new EditGroup("`nameof(x)` -> string literal");
                        g.Replace(node.Span, Quote(value));
                        Commit(g);
                    } else {
                        Manual(node, "nameof", "cannot evaluate; replace by a string literal by hand");
                    }
                }
            }
            base.VisitInvocationExpression(node);
        }

        // ------------------------------------------------------------------ modifiers

        private void DeleteToken(EditGroup g, SyntaxToken tok)
        {
            SyntaxToken next = tok.GetNextToken();
            if (!next.IsKind(SyntaxKind.None) && string.IsNullOrWhiteSpace(TextOf(tok.Span.End, next.SpanStart)) &&
                _c.Text.Lines.GetLineFromPosition(tok.SpanStart).LineNumber == _c.Text.Lines.GetLineFromPosition(next.SpanStart).LineNumber) {
                g.Replace(tok.SpanStart, next.SpanStart, "");
            } else {
                g.Delete(tok.Span);
            }
        }

        private void DropModifier(SyntaxTokenList modifiers, SyntaxKind kind, string stat)
        {
            foreach (SyntaxToken m in modifiers) {
                if (m.IsKind(kind)) {
                    EditGroup g = new EditGroup(stat);
                    DeleteToken(g, m);
                    Commit(g);
                }
            }
        }

        private void CheckPrivateProtected(SyntaxTokenList modifiers)
        {
            SyntaxToken priv = default(SyntaxToken), prot = default(SyntaxToken);
            foreach (SyntaxToken m in modifiers) {
                if (m.IsKind(SyntaxKind.PrivateKeyword)) priv = m;
                if (m.IsKind(SyntaxKind.ProtectedKeyword)) prot = m;
            }
            if (priv.IsKind(SyntaxKind.None) || prot.IsKind(SyntaxKind.None)) return;
            EditGroup g = new EditGroup("`private protected` -> `internal`");
            if (priv.SpanStart < prot.SpanStart) { g.Replace(priv.Span, "internal"); DeleteTokenBefore(g, prot); }
            else { g.Replace(prot.Span, "internal"); DeleteTokenBefore(g, priv); }
            Commit(g);
        }

        private void DeleteTokenBefore(EditGroup g, SyntaxToken tok)
        {
            // removes the token and the blanks in front of it
            int start = tok.SpanStart;
            string text = _c.Text.ToString();
            while (start > 0 && (text[start - 1] == ' ' || text[start - 1] == '\t')) start--;
            g.Replace(start, tok.Span.End, "");
        }

        private void CommonModifiers(SyntaxTokenList modifiers, bool isType)
        {
            CheckPrivateProtected(modifiers);
            DropModifier(modifiers, SyntaxKind.RequiredKeyword, "`required` modifier removed");
            if (!isType) return;
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            DropModifier(node.Modifiers, SyntaxKind.ReadOnlyKeyword, "`readonly struct` -> `struct`");
            foreach (SyntaxToken m in node.Modifiers) {
                if (m.IsKind(SyntaxKind.RefKeyword)) Manual(node, "ref struct", "not available in C# 4");
            }
            CommonModifiers(node.Modifiers, true);
            base.VisitStructDeclaration(node);
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            CommonModifiers(node.Modifiers, true);
            base.VisitClassDeclaration(node);
        }

        public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            Manual(node, "record", "records are not available in C# 4: write the class by hand");
            base.VisitRecordDeclaration(node);
        }

        private bool InStruct(SyntaxNode node)
        {
            return node.Parent is StructDeclarationSyntax || (node.Parent is AccessorListSyntax && node.Parent.Parent != null && node.Parent.Parent.Parent is StructDeclarationSyntax);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (InStruct(node)) DropModifier(node.Modifiers, SyntaxKind.ReadOnlyKeyword, "`readonly` member modifier removed");
            CommonModifiers(node.Modifiers, false);
            foreach (SyntaxToken m in node.Modifiers) {
                if (m.IsKind(SyntaxKind.AsyncKeyword)) Manual(node, "async method", "async/await is C# 5: rewrite by hand");
            }
            if (node.ReturnType is RefTypeSyntax) Manual(node, "ref return", "not available in C# 4");
            base.VisitMethodDeclaration(node);
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            CommonModifiers(node.Modifiers, false);
            // C# 4/5: a struct constructor cannot assign an auto-property before every field is assigned;
            // `: this()` zero-initialises the struct first (no observable difference).
            StructDeclarationSyntax sd = node.Parent as StructDeclarationSyntax;
            if (sd != null && node.Initializer == null && node.ParameterList.Parameters.Count > 0 && !node.Modifiers.Any(SyntaxKind.StaticKeyword)) {
                INamedTypeSymbol st = Model.GetDeclaredSymbol(sd) as INamedTypeSymbol;
                if (st != null && st.GetMembers().OfType<IFieldSymbol>().Any(f => !f.IsStatic && f.AssociatedSymbol is IPropertySymbol)) {
                    EditGroup g = new EditGroup("struct constructor with auto-properties: `: this()` added");
                    g.Insert(node.ParameterList.Span.End, " : this()");
                    Commit(g);
                }
            }
            base.VisitConstructorDeclaration(node);
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            CommonModifiers(node.Modifiers, false);
            base.VisitFieldDeclaration(node);
        }

        public override void VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            if (InStruct(node)) DropModifier(node.Modifiers, SyntaxKind.ReadOnlyKeyword, "`readonly` member modifier removed");
            CommonModifiers(node.Modifiers, false);
            base.VisitIndexerDeclaration(node);
        }

        public override void VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            if (InStruct(node)) DropModifier(node.Modifiers, SyntaxKind.ReadOnlyKeyword, "`readonly` member modifier removed");
            CheckPrivateProtected(node.Modifiers);
            if (node.Keyword.IsKind(SyntaxKind.InitKeyword)) {
                EditGroup g = new EditGroup("`init` accessor -> `set`");
                g.Replace(node.Keyword.Span, "set");
                Commit(g);
            }
            base.VisitAccessorDeclaration(node);
        }

        public override void VisitParameter(ParameterSyntax node)
        {
            foreach (SyntaxToken m in node.Modifiers) {
                if (m.IsKind(SyntaxKind.InKeyword)) {
                    EditGroup g = new EditGroup("`in` parameter/argument modifier removed (passed by value)");
                    DeleteToken(g, m);
                    Commit(g);
                }
            }
            base.VisitParameter(node);
        }

        private void LambdaCommon(AnonymousFunctionExpressionSyntax node)
        {
            DropModifier(node.Modifiers, SyntaxKind.StaticKeyword, "`static` lambda -> plain lambda");
            foreach (SyntaxToken m in node.Modifiers) {
                if (m.IsKind(SyntaxKind.AsyncKeyword)) Manual(node, "async lambda", "async/await is C# 5: rewrite by hand");
            }
            if (node.ExpressionBody is ThrowExpressionSyntax) RequestLambdaBlock(node);
        }

        public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            LambdaCommon(node);
            // `(_, _) => ...`: two parameters named `_` are discards only since C# 9
            List<ParameterSyntax> underscores = node.ParameterList.Parameters.Where(p => p.Identifier.ValueText == "_").ToList();
            if (underscores.Count > 1) {
                EditGroup g = new EditGroup("lambda discard parameters `(_, _)` -> named parameters");
                int i = 1;
                foreach (ParameterSyntax p in underscores) g.Replace(p.Identifier.Span, "_" + (i++).ToString(CultureInfo.InvariantCulture));
                Commit(g);
            }
            if (node.ReturnType != null) Manual(node, "lambda return type", "explicit lambda return types are not available in C# 4");
            base.VisitParenthesizedLambdaExpression(node);
        }

        public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
            LambdaCommon(node);
            base.VisitSimpleLambdaExpression(node);
        }

        public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
        {
            LambdaCommon(node);
            base.VisitAnonymousMethodExpression(node);
        }

        // ------------------------------------------------------------------ expression-bodied members

        public override void VisitArrowExpressionClause(ArrowExpressionClauseSyntax node)
        {
            SyntaxNode parent = node.Parent;
            ExpressionSyntax expr = node.Expression;
            bool isThrow = expr is ThrowExpressionSyntax;
            bool isVoid = false, isProperty = false;
            SyntaxToken semi = default(SyntaxToken);
            MethodDeclarationSyntax m = parent as MethodDeclarationSyntax;
            LocalFunctionStatementSyntax lf = parent as LocalFunctionStatementSyntax;
            AccessorDeclarationSyntax acc = parent as AccessorDeclarationSyntax;
            if (m != null) { isVoid = IsVoid(m.ReturnType); semi = m.SemicolonToken; }
            else if (lf != null) { isVoid = IsVoid(lf.ReturnType); semi = lf.SemicolonToken; }
            else if (parent is ConstructorDeclarationSyntax) { isVoid = true; semi = ((ConstructorDeclarationSyntax)parent).SemicolonToken; }
            else if (parent is DestructorDeclarationSyntax) { isVoid = true; semi = ((DestructorDeclarationSyntax)parent).SemicolonToken; }
            else if (parent is OperatorDeclarationSyntax) semi = ((OperatorDeclarationSyntax)parent).SemicolonToken;
            else if (parent is ConversionOperatorDeclarationSyntax) semi = ((ConversionOperatorDeclarationSyntax)parent).SemicolonToken;
            else if (acc != null) { isVoid = !acc.Keyword.IsKind(SyntaxKind.GetKeyword); semi = acc.SemicolonToken; }
            else if (parent is PropertyDeclarationSyntax) { isProperty = true; semi = ((PropertyDeclarationSyntax)parent).SemicolonToken; }
            else if (parent is IndexerDeclarationSyntax) { isProperty = true; semi = ((IndexerDeclarationSyntax)parent).SemicolonToken; }
            else {
                Manual(node, "expression body", "unexpected kind of expression-bodied member");
                base.VisitArrowExpressionClause(node);
                return;
            }
            EditGroup g = new EditGroup("expression-bodied member -> block body");
            string open = isProperty ? "{ get {" : "{";
            if (!isVoid && !isThrow) open += " return";
            g.Replace(node.ArrowToken.Span, open);
            g.Replace(semi.Span, isProperty ? "; } }" : "; }");
            Commit(g);
            base.VisitArrowExpressionClause(node);
        }

        private static bool IsVoid(TypeSyntax t)
        {
            PredefinedTypeSyntax p = t as PredefinedTypeSyntax;
            return p != null && p.Keyword.IsKind(SyntaxKind.VoidKeyword);
        }

        // ------------------------------------------------------------------ target-typed conditional

        private bool ConditionalHasNaturalType(ConditionalExpressionSyntax node)
        {
            ITypeSymbol t1 = Model.GetTypeInfo(node.WhenTrue).Type, t2 = Model.GetTypeInfo(node.WhenFalse).Type;
            if (t1 != null && t1.TypeKind == TypeKind.Error || t2 != null && t2.TypeKind == TypeKind.Error) return true; // cannot tell
            if (t1 != null && t2 != null) {
                if (SymbolEqualityComparer.Default.Equals(t1, t2)) return true;
                Conversion a = Model.ClassifyConversion(node.WhenTrue, t2), b = Model.ClassifyConversion(node.WhenFalse, t1);
                return (a.Exists && a.IsImplicit) || (b.Exists && b.IsImplicit);
            }
            if (t1 != null) { Conversion c = Model.ClassifyConversion(node.WhenFalse, t1); return c.Exists && c.IsImplicit; }
            if (t2 != null) { Conversion c = Model.ClassifyConversion(node.WhenTrue, t2); return c.Exists && c.IsImplicit; }
            return false;
        }

        public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            if (!(node.WhenTrue is ThrowExpressionSyntax) && !(node.WhenFalse is ThrowExpressionSyntax) && !ConditionalHasNaturalType(node)) {
                string tn = TypeName(Model.GetTypeInfo(node).ConvertedType, node.SpanStart);
                if (tn == null) {
                    Manual(node, "target-typed conditional expression", "the branches have no common type (C# 9 feature): add a cast by hand");
                } else {
                    EditGroup g = new EditGroup("target-typed conditional `c ? x : null` -> cast added to one branch");
                    ExpressionSyntax target = StripParens(node.WhenFalse) is LiteralExpressionSyntax && Model.GetTypeInfo(node.WhenFalse).Type == null ? node.WhenFalse
                                            : StripParens(node.WhenTrue) is LiteralExpressionSyntax && Model.GetTypeInfo(node.WhenTrue).Type == null ? node.WhenTrue
                                            : node.WhenTrue;
                    g.Replace(target.Span, "(" + tn + ")" + Operand(target, Prec.Unary));
                    Commit(g);
                }
            }
            base.VisitConditionalExpression(node);
        }

        // ------------------------------------------------------------------ target-typed new

        public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
        {
            string tn = TypeName(Model.GetTypeInfo(node).Type, node.SpanStart);
            if (tn == null) {
                Manual(node, "target-typed `new`", "cannot determine the created type; write it by hand");
            } else {
                EditGroup g = new EditGroup("target-typed `new(...)` -> `new T(...)`");
                g.Insert(node.NewKeyword.Span.End, " " + tn);
                Commit(g);
            }
            base.VisitImplicitObjectCreationExpression(node);
        }

        // ------------------------------------------------------------------ interpolated strings

        public override void VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
        {
            HandleInterpolatedString(node);
            base.VisitInterpolatedStringExpression(node);
        }

        private void HandleInterpolatedString(InterpolatedStringExpressionSyntax node)
        {
            string start = node.StringStartToken.Text;
            if (start.IndexOf("\"\"\"", StringComparison.Ordinal) >= 0) {
                Manual(node, "interpolated raw string", "rewrite by hand as string.Format");
                return;
            }
            ITypeSymbol conv = Model.GetTypeInfo(node).ConvertedType;
            if (conv == null || conv.SpecialType != SpecialType.System_String) {
                Manual(node, "interpolated string", "is converted to " + (conv == null ? "?" : conv.ToDisplayString()) + " (not string): string.Format would not be equivalent, rewrite by hand");
                return;
            }
            bool verbatim = start.IndexOf('@') >= 0;
            string prefix = verbatim ? "@\"" : "\"";
            List<string> args = new List<string>();
            StringBuilder fmt = new StringBuilder();
            bool allConstantStrings = true;
            List<string> concat = new List<string>();
            foreach (InterpolatedStringContentSyntax c in node.Contents) {
                InterpolatedStringTextSyntax t = c as InterpolatedStringTextSyntax;
                if (t != null) {
                    fmt.Append(t.TextToken.Text);
                    concat.Add(prefix + t.TextToken.Text.Replace("{{", "{").Replace("}}", "}") + "\"");
                    continue;
                }
                InterpolationSyntax ip = (InterpolationSyntax)c;
                fmt.Append('{').Append(args.Count.ToString(CultureInfo.InvariantCulture));
                if (ip.AlignmentClause != null) fmt.Append(',').Append(ip.AlignmentClause.Value.ToString());
                if (ip.FormatClause != null) fmt.Append(':').Append(ip.FormatClause.FormatStringToken.Text);
                fmt.Append('}');
                ITypeSymbol at = Model.GetTypeInfo(ip.Expression).Type;
                Optional<object> cv = Model.GetConstantValue(ip.Expression);
                if (!(cv.HasValue && cv.Value is string) || ip.AlignmentClause != null || ip.FormatClause != null) allConstantStrings = false;
                string a = ip.Expression.ToString();
                if (Prec.Of(ip.Expression) <= Prec.Assignment && !(ip.Expression is ParenthesizedExpressionSyntax)) a = "(" + a + ")";
                concat.Add(Operand(ip.Expression, Prec.Multiplicative));
                if (at == null || at.TypeKind == TypeKind.Array) a = "(object)" + Operand(ip.Expression, Prec.Unary);
                args.Add(a);
            }
            EditGroup g = new EditGroup("interpolated string -> string.Format / literal / concatenation");
            string text;
            if (args.Count == 0) {
                text = prefix + fmt.ToString().Replace("{{", "{").Replace("}}", "}") + "\"";
            } else if (allConstantStrings && Model.GetConstantValue(node).HasValue) {
                text = ParenFor(node, string.Join(" + ", concat), Prec.Additive);
            } else {
                text = "string.Format(" + prefix + fmt + "\", " + string.Join(", ", args) + ")";
            }
            CheckComments(node, text, "interpolated string");
            g.Replace(node.Span, text);
            Commit(g);
        }

        // ------------------------------------------------------------------ tuples

        public override void VisitTupleExpression(TupleExpressionSyntax node)
        {
            AssignmentExpressionSyntax asg = node.Parent as AssignmentExpressionSyntax;
            if (asg != null && asg.Left == node) {
                Manual(node, "tuple deconstruction", "assign the elements one by one by hand");
            } else if (!_c.TupleNamesPending) {
                TypeInfo ti = Model.GetTypeInfo(node);
                string tn = TypeName(ti.ConvertedType ?? ti.Type, node.SpanStart);
                if (tn == null || !tn.StartsWith("System.ValueTuple<", StringComparison.Ordinal)) {
                    Manual(node, "tuple", "cannot determine the tuple type; use an explicit struct by hand");
                } else {
                    EditGroup g = new EditGroup("tuple literal -> `new System.ValueTuple<...>(...)`");
                    string args = string.Join(", ", node.Arguments.Select(a => a.Expression.ToString()));
                    g.Replace(node.Span, "new " + tn + "(" + args + ")");
                    Commit(g);
                }
            }
            base.VisitTupleExpression(node);
        }

        public override void VisitTupleType(TupleTypeSyntax node)
        {
            if (!_c.TupleNamesPending) {
                if (node.Elements.Count > 7) {
                    Manual(node, "tuple type", "more than 7 elements");
                } else {
                    EditGroup g = new EditGroup("tuple type -> `System.ValueTuple<...>`");
                    g.Replace(node.Span, "System.ValueTuple<" + string.Join(", ", node.Elements.Select(e => e.Type.ToString())) + ">");
                    Commit(g);
                }
            }
            base.VisitTupleType(node);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (_c.TupleNamesPending && node.IsKind(SyntaxKind.SimpleMemberAccessExpression) && node.Name is IdentifierNameSyntax) {
                IFieldSymbol f = Model.GetSymbolInfo(node).Symbol as IFieldSymbol;
                if (f != null && f.ContainingType != null && f.ContainingType.IsTupleType && f.CorrespondingTupleField != null &&
                    f.CorrespondingTupleField.Name != node.Name.Identifier.ValueText) {
                    EditGroup g = new EditGroup("tuple element name -> `ItemN`");
                    g.Replace(node.Name.Span, f.CorrespondingTupleField.Name);
                    Commit(g);
                }
            }
            base.VisitMemberAccessExpression(node);
        }

        /// <summary>Whether a tree uses a named tuple element (those must be renamed before the tuple syntax itself goes).</summary>
        internal static bool HasNamedTupleElementAccess(SyntaxNode root, SemanticModel model)
        {
            foreach (SyntaxNode n in root.DescendantNodes()) {
                MemberAccessExpressionSyntax ma = n as MemberAccessExpressionSyntax;
                if (ma == null || !ma.IsKind(SyntaxKind.SimpleMemberAccessExpression) || !(ma.Name is IdentifierNameSyntax)) continue;
                string name = ma.Name.Identifier.ValueText;
                if (name.StartsWith("Item", StringComparison.Ordinal)) continue;
                IFieldSymbol f = model.GetSymbolInfo(ma).Symbol as IFieldSymbol;
                if (f != null && f.ContainingType != null && f.ContainingType.IsTupleType && f.CorrespondingTupleField != null &&
                    f.CorrespondingTupleField.Name != name) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ things that are only reported

        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            if (!node.StaticKeyword.IsKind(SyntaxKind.None)) Manual(node, "using static", "not available in C# 4: qualify the members by hand");
            if (!node.GlobalKeyword.IsKind(SyntaxKind.None)) Manual(node, "global using", "not available in C# 4");
            base.VisitUsingDirective(node);
        }

        public override void VisitDeclarationExpression(DeclarationExpressionSyntax node)
        {
            if (node.Designation is ParenthesizedVariableDesignationSyntax) Manual(node, "deconstruction", "declare and assign the variables one by one by hand");
            base.VisitDeclarationExpression(node);
        }

        public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
        {
            Manual(node, "deconstruction in foreach", "rewrite by hand");
            base.VisitForEachVariableStatement(node);
        }

        public override void VisitAwaitExpression(AwaitExpressionSyntax node)
        {
            Manual(node, "await", "async/await is C# 5: rewrite by hand");
            base.VisitAwaitExpression(node);
        }

        public override void VisitRangeExpression(RangeExpressionSyntax node)
        {
            Manual(node, "range expression", "not available in C# 4");
            base.VisitRangeExpression(node);
        }

        public override void VisitStackAllocArrayCreationExpression(StackAllocArrayCreationExpressionSyntax node)
        {
            Manual(node, "stackalloc", "stackalloc outside unsafe code is not available in C# 4");
            base.VisitStackAllocArrayCreationExpression(node);
        }

        public override void VisitWithExpression(WithExpressionSyntax node)
        {
            Manual(node, "with expression", "not available in C# 4");
            base.VisitWithExpression(node);
        }

    }
}
