// CSharp4Downgrader - transformations that need declarations/statements:
// out variables, patterns, ?. and ?[], ??=, exception filters, using declarations,
// throw expressions, switch expressions, auto-properties, discards.
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
        // ------------------------------------------------------------------ out variables, `in` arguments

        public override void VisitArgument(ArgumentSyntax node)
        {
            if (node.RefKindKeyword.IsKind(SyntaxKind.InKeyword)) {
                EditGroup g = new EditGroup("`in` parameter/argument modifier removed (passed by value)");
                DeleteToken(g, node.RefKindKeyword);
                Commit(g);
            } else if (node.RefKindKeyword.IsKind(SyntaxKind.OutKeyword)) {
                DeclarationExpressionSyntax de = node.Expression as DeclarationExpressionSyntax;
                IdentifierNameSyntax id = node.Expression as IdentifierNameSyntax;
                if (de != null) {
                    SingleVariableDesignationSyntax sv = de.Designation as SingleVariableDesignationSyntax;
                    if (sv != null) {
                        ILocalSymbol sym = Model.GetDeclaredSymbol(sv) as ILocalSymbol;
                        if (sym == null) {
                            Manual(node, "out variable", "declared outside a method body (field initializer?): declare the variable by hand");
                        } else {
                            string tn = de.Type.IsVar ? TypeName(sym.Type, node.SpanStart) : de.Type.ToString();
                            if (tn == null) {
                                Manual(node, "out var", "cannot name the inferred type of '" + sv.Identifier.ValueText + "'; declare it by hand");
                            } else {
                                EditGroup g = new EditGroup("inline `out` variable declaration -> declaration before the statement");
                                string name = HoistDeclaration(g, node, tn, sym.Type, sv.Identifier.ValueText, sym, "out variable");
                                if (name != null) {
                                    g.Replace(de.Span, name);
                                    Commit(g);
                                }
                            }
                        }
                    } else if (de.Designation is DiscardDesignationSyntax) {
                        OutDiscard(node, de, Model.GetTypeInfo(de).Type, de.Type.IsVar ? null : de.Type.ToString());
                    } else {
                        Manual(node, "out deconstruction", "rewrite by hand");
                    }
                } else if (id != null && id.Identifier.ValueText == "_") {
                    IDiscardSymbol ds = Model.GetSymbolInfo(id).Symbol as IDiscardSymbol;
                    if (ds != null) OutDiscard(node, id, ds.Type, null);
                }
            }
            base.VisitArgument(node);
        }

        private void OutDiscard(ArgumentSyntax node, ExpressionSyntax expr, ITypeSymbol type, string explicitType)
        {
            string tn = explicitType ?? TypeName(type, node.SpanStart);
            if (tn == null) {
                Manual(node, "out discard", "cannot name the type of the discarded out argument; declare a dummy variable by hand");
                return;
            }
            EditGroup g = new EditGroup("`out _` discard -> dummy variable");
            string name = HoistDeclaration(g, node, tn, type, Fresh(node, "unused"), null, "out discard");
            if (name == null) return;
            g.Replace(expr.Span, name);
            Commit(g);
        }

        // ------------------------------------------------------------------ patterns

        private static bool IsEqualityComparable(ITypeSymbol t)
        {
            if (t == null) return false;
            if (IsNullableValue(t)) t = ((INamedTypeSymbol)t).TypeArguments[0];
            if (t.TypeKind == TypeKind.Enum) return true;
            switch (t.SpecialType) {
                case SpecialType.System_Boolean: case SpecialType.System_Char: case SpecialType.System_String:
                case SpecialType.System_SByte: case SpecialType.System_Byte: case SpecialType.System_Int16:
                case SpecialType.System_UInt16: case SpecialType.System_Int32: case SpecialType.System_UInt32:
                case SpecialType.System_Int64: case SpecialType.System_UInt64: case SpecialType.System_Decimal:
                    return true;
            }
            return false;
        }

        /// <summary>
        /// A boolean expression equivalent to `e is pattern` for patterns without variables, or null.
        /// <paramref name="e"/> must be operand text of at least shift precedence; it may be repeated when
        /// <paramref name="canRepeat"/> is set.
        /// </summary>
        private string PatternCondition(PatternSyntax p, string e, ITypeSymbol et, bool canRepeat, out int prec)
        {
            prec = Prec.Primary;
            if (p is DiscardPatternSyntax) return "true";
            ParenthesizedPatternSyntax pp = p as ParenthesizedPatternSyntax;
            if (pp != null) {
                int ip;
                string inner = PatternCondition(pp.Pattern, e, et, canRepeat, out ip);
                return inner == null ? null : "(" + inner + ")";
            }
            ConstantPatternSyntax cp = p as ConstantPatternSyntax;
            if (cp != null) {
                if (cp.Expression.IsKind(SyntaxKind.NullLiteralExpression)) {
                    prec = NullTestPrec(et, true);
                    return NullTest(e, et, true);
                }
                if (Model.GetSymbolInfo(cp.Expression).Symbol is ITypeSymbol) {
                    prec = Prec.Relational;
                    return e + " is " + cp.Expression;
                }
                ITypeSymbol ct = Model.GetTypeInfo(cp.Expression).Type;
                if (IsEqualityComparable(et) && Model.GetConstantValue(cp.Expression).HasValue) {
                    prec = Prec.Equality;
                    return e + " == " + Operand(cp.Expression, Prec.Relational);
                }
                return null;
            }
            TypePatternSyntax tp = p as TypePatternSyntax;
            if (tp != null) {
                prec = Prec.Relational;
                return e + " is " + tp.Type;
            }
            RelationalPatternSyntax rp = p as RelationalPatternSyntax;
            if (rp != null) {
                if (!IsEqualityComparable(et) || IsNullableValue(et) || et.SpecialType == SpecialType.System_String || et.SpecialType == SpecialType.System_Boolean) return null;
                prec = Prec.Relational;
                return e + " " + rp.OperatorToken.Text + " " + Operand(rp.Expression, Prec.Shift);
            }
            UnaryPatternSyntax up = p as UnaryPatternSyntax;
            if (up != null) {
                ConstantPatternSyntax ucp = up.Pattern as ConstantPatternSyntax;
                if (ucp != null && ucp.Expression.IsKind(SyntaxKind.NullLiteralExpression)) {
                    prec = NullTestPrec(et, false);
                    return NullTest(e, et, false);
                }
                int ip;
                string inner = PatternCondition(up.Pattern, e, et, canRepeat, out ip);
                if (inner == null) return null;
                prec = Prec.Unary;
                return "!(" + inner + ")";
            }
            BinaryPatternSyntax bp = p as BinaryPatternSyntax;
            if (bp != null) {
                if (!canRepeat) return null;
                int lp, rp2;
                string l = PatternCondition(bp.Left, e, et, true, out lp);
                string r = PatternCondition(bp.Right, e, et, true, out rp2);
                if (l == null || r == null) return null;
                bool or = bp.IsKind(SyntaxKind.OrPattern);
                int my = or ? Prec.LogicalOr : Prec.LogicalAnd;
                if (lp < my) l = "(" + l + ")";
                if (rp2 <= my) r = "(" + r + ")";
                prec = my;
                return l + (or ? " || " : " && ") + r;
            }
            return null;
        }

        public override void VisitIsPatternExpression(IsPatternExpressionSyntax node)
        {
            HandleIsPattern(node);
            base.VisitIsPatternExpression(node);
        }

        private void HandleIsPattern(IsPatternExpressionSyntax node)
        {
            ITypeSymbol et = Model.GetTypeInfo(node.Expression).Type;
            DeclarationPatternSyntax dp = node.Pattern as DeclarationPatternSyntax;
            if (dp != null) {
                ITypeSymbol t = Model.GetTypeInfo(dp.Type).Type;
                if (dp.Designation is DiscardDesignationSyntax) {
                    EditGroup g0 = new EditGroup("`is` pattern -> boolean expression");
                    g0.Replace(node.Span, ParenFor(node, Operand(node.Expression, Prec.Shift) + " is " + dp.Type, Prec.Relational));
                    Commit(g0);
                    return;
                }
                SingleVariableDesignationSyntax sv = dp.Designation as SingleVariableDesignationSyntax;
                ILocalSymbol sym = sv == null ? null : Model.GetDeclaredSymbol(sv) as ILocalSymbol;
                if (sv == null || sym == null || IsErrorOrNull(t)) {
                    Manual(node, "declaration pattern", "cannot analyse the pattern variable; rewrite with `as` by hand");
                    return;
                }
                if (!t.IsReferenceType) {
                    HandleValueTypePattern(node, dp, sv, sym, t);
                    return;
                }
                EditGroup g = new EditGroup("`x is T v` pattern -> `(v = x as T) != null` with `T v;` declared before the statement");
                string name = HoistDeclaration(g, node, dp.Type.ToString(), t, sv.Identifier.ValueText, sym, "pattern variable");
                if (name == null) return;
                string text = "(" + name + " = " + Operand(node.Expression, Prec.Shift) + " as " + dp.Type + ") != null";
                CheckComments(node, text, "is pattern");
                g.Replace(node.Span, ParenFor(node, text, Prec.Equality));
                Commit(g);
                return;
            }
            if (node.Pattern is VarPatternSyntax || node.Pattern is RecursivePatternSyntax || node.Pattern is ListPatternSyntax) {
                Manual(node, "pattern (" + node.Pattern.Kind() + ")", "rewrite by hand with explicit tests");
                return;
            }
            bool simple = IsSimpleOrLiteral(node.Expression);
            int prec;
            string cond = PatternCondition(node.Pattern, Operand(node.Expression, Prec.Shift), et, simple, out prec);
            if (cond == null) {
                Manual(node, "pattern (" + node.Pattern.Kind() + ")", "rewrite by hand with explicit tests");
                return;
            }
            EditGroup g2 = new EditGroup("`is` pattern -> boolean expression");
            g2.Replace(node.Span, ParenFor(node, cond, prec));
            Commit(g2);
        }

        /// <summary>
        /// `if (A &amp;&amp; x is T v) S` with a value type T: `T v; if (A &amp;&amp; x is T) { v = (T)x; S }`
        /// (also for `while`). The variable is assigned exactly where the original guaranteed it.
        /// </summary>
        private void HandleValueTypePattern(IsPatternExpressionSyntax node, DeclarationPatternSyntax dp, SingleVariableDesignationSyntax sv, ILocalSymbol sym, ITypeSymbol t)
        {
            const string what = "declaration pattern on a value type";
            if (t.TypeKind == TypeKind.TypeParameter) { Manual(node, what, "generic type parameter; rewrite by hand"); return; }
            SyntaxNode cur = node;
            while (true) {
                SyntaxNode p = cur.Parent;
                BinaryExpressionSyntax b = p as BinaryExpressionSyntax;
                if (p is ParenthesizedExpressionSyntax) cur = p;
                else if (b != null && b.IsKind(SyntaxKind.LogicalAndExpression) && b.Right == cur) cur = p;
                else break;
            }
            IfStatementSyntax ifs = cur.Parent as IfStatementSyntax;
            WhileStatementSyntax ws = cur.Parent as WhileStatementSyntax;
            StatementSyntax body = ifs != null && ifs.Condition == cur ? ifs.Statement : ws != null && ws.Condition == cur ? ws.Statement : null;
            if (body == null) {
                Manual(node, what, "`x is " + dp.Type + " v` is not the last `&&` operand of an if/while condition: rewrite by hand as `if (x is " + dp.Type + ") { " + dp.Type + " v = (" + dp.Type + ")x; ... }`");
                return;
            }
            EditGroup g = new EditGroup("`x is T v` pattern (value type T) -> `T v; if (x is T) { v = (T)x; ... }`");
            string name = HoistDeclaration(g, node, dp.Type.ToString(), t, sv.Identifier.ValueText, sym, what);
            if (name == null) return;
            string source;
            string test;
            if (IsSimple(node.Expression)) {
                source = Operand(node.Expression, Prec.Unary);
                test = Operand(node.Expression, Prec.Shift) + " is " + dp.Type;
            } else {
                ITypeSymbol et = Model.GetTypeInfo(node.Expression).Type;
                string tn = TypeName(et, node.SpanStart);
                if (tn == null) { Manual(node, what, "cannot name the operand type for a temporary; rewrite by hand"); return; }
                string tmp = Fresh(node, "tmp");
                if (HoistDeclaration(g, node, tn, et, tmp, null, what) == null) return;
                source = tmp;
                test = "(" + tmp + " = " + node.Expression + ") is " + dp.Type;
            }
            string assign = name + " = (" + dp.Type + ")" + source + ";";
            BlockSyntax block = body as BlockSyntax;
            if (block != null) {
                int open = block.OpenBraceToken.Span.End;
                bool multiLine = _c.Text.Lines.GetLineFromPosition(open).LineNumber != _c.Text.Lines.GetLineFromPosition(block.CloseBraceToken.SpanStart).LineNumber;
                if (multiLine && string.IsNullOrWhiteSpace(TextOf(open, _c.Text.Lines.GetLineFromPosition(open).End))) {
                    string bi = block.Statements.Count > 0 && IsFirstOnLine(block.Statements[0].SpanStart)
                        ? IndentOf(block.Statements[0].SpanStart) : IndentOf(block.CloseBraceToken.SpanStart) + "    ";
                    g.Insert(open, NL + bi + assign);
                } else {
                    g.Insert(open, " " + assign);
                }
            } else {
                g.Insert(body.SpanStart, "{ " + assign + " ");
                g.Insert(body.Span.End, " }");
            }
            g.Replace(node.Span, ParenFor(node, test, Prec.Relational));
            Commit(g);
        }

        public override void VisitSwitchStatement(SwitchStatementSyntax node)
        {
            foreach (SwitchSectionSyntax s in node.Sections) {
                foreach (SwitchLabelSyntax l in s.Labels) {
                    if (l is CasePatternSwitchLabelSyntax) {
                        Manual(l, "pattern-based `case` label", "rewrite the switch as an if/else chain by hand");
                    }
                }
            }
            base.VisitSwitchStatement(node);
        }

        // ------------------------------------------------------------------ ?. and ?[]

        public override void VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
        {
            ConditionalAccessExpressionSyntax outer = node.Parent as ConditionalAccessExpressionSyntax;
            if (outer == null || outer.WhenNotNull != node) HandleConditionalAccess(node);
            base.VisitConditionalAccessExpression(node);
        }

        private string WrapIf(StatementSyntax replaced, string ifText)
        {
            return DanglingElseRisk(replaced) ? "{ " + ifText + " }" : ifText;
        }

        private void HandleConditionalAccess(ConditionalAccessExpressionSyntax node)
        {
            const string what = "null-conditional access";
            ExpressionSyntax recv = node.Expression;
            ITypeSymbol tr = Model.GetTypeInfo(recv).Type;
            if (IsErrorOrNull(tr)) {
                Manual(node, what, "the receiver type does not bind; rewrite by hand");
                return;
            }
            bool nullableValue = IsNullableValue(tr);
            if (!nullableValue && !tr.IsReferenceType) {
                Manual(node, what, "the receiver is of an unconstrained generic type; rewrite by hand");
                return;
            }
            if (node.Parent is ArrowExpressionClauseSyntax) return; // after the member got a block body
            string rest = TextOf(node.OperatorToken.Span.End, node.Span.End);
            string valueAccess = nullableValue ? ".GetValueOrDefault()" : "";

            AnonymousFunctionExpressionSyntax af = node.Parent as AnonymousFunctionExpressionSyntax;
            if (af != null && af.ExpressionBody == node) {
                IMethodSymbol ms = Model.GetSymbolInfo(af).Symbol as IMethodSymbol;
                if (ms != null && ms.ReturnsVoid) { RequestLambdaBlock(af); return; }
            }

            ExpressionStatementSyntax es = node.Parent as ExpressionStatementSyntax;
            if (es != null) {
                EditGroup g = new EditGroup("`a?.M();` statement -> `if (a != null) a.M();`");
                string cond, access, prefix = "";
                BinaryExpressionSyntax asx = StripParens(recv) as BinaryExpressionSyntax;
                if (asx != null && asx.IsKind(SyntaxKind.AsExpression) && IsSimple(asx.Left)) {
                    ITypeSymbol tt = Model.GetTypeInfo(asx.Right).Type;
                    if (tt != null && tt.IsReferenceType && !(asx.Right is NullableTypeSyntax)) {
                        cond = asx.Left + " is " + asx.Right;
                        access = "((" + asx.Right + ")" + Operand(asx.Left, Prec.Unary) + ")" + rest;
                        g = new EditGroup("`(x as T)?.M();` statement -> `if (x is T) ((T)x).M();`");
                        string t0 = WrapIf(es, "if (" + cond + ") " + access + ";");
                        CheckComments(es, t0, what);
                        g.Replace(es.Span, t0);
                        Commit(g);
                        return;
                    }
                }
                if (IsSimple(recv)) {
                    string r = Operand(recv, Prec.Primary);
                    cond = NullTest(r, tr, false);
                    access = r + valueAccess + rest;
                } else {
                    string tn = TypeName(tr, node.SpanStart);
                    if (tn == null) { Manual(node, what, "cannot name the receiver type for a temporary; rewrite by hand"); return; }
                    string tmp = Fresh(node, "tmp");
                    if (!IsEmbedded(es)) {
                        prefix = tn + " " + tmp + " = " + recv + ";" + (IsFirstOnLine(es.SpanStart) ? NL + IndentOf(es.SpanStart) : " ");
                        cond = NullTest(tmp, tr, false);
                    } else {
                        if (HoistDeclaration(g, node, tn, tr, tmp, null, what) == null) return;
                        cond = NullTest("(" + tmp + " = " + recv + ")", tr, false);
                    }
                    access = tmp + valueAccess + rest;
                    g.StatCount = 1;
                }
                string text = prefix + WrapIf(es, "if (" + cond + ") " + access + ";");
                CheckComments(es, text, what);
                g.Replace(es.Span, text);
                Commit(g);
                return;
            }

            // value form
            ITypeSymbol tn2 = Model.GetTypeInfo(node).Type;
            string nullAlt;
            if (IsErrorOrNull(tn2) || tn2.SpecialType == SpecialType.System_Void) {
                Manual(node, what, "used in a context that is neither a statement nor a typed value; rewrite by hand");
                return;
            }
            if (IsNullableValue(tn2)) {
                string n = TypeName(tn2, node.SpanStart);
                if (n == null) { Manual(node, what, "cannot name the result type; rewrite by hand"); return; }
                nullAlt = "(" + n + ")null";
            } else if (tn2.TypeKind == TypeKind.TypeParameter) {
                nullAlt = "default(" + TypeName(tn2, node.SpanStart) + ")";
            } else if (tn2.IsReferenceType) {
                nullAlt = "null";
            } else {
                Manual(node, what, "unexpected result type " + tn2.ToDisplayString() + "; rewrite by hand");
                return;
            }
            EditGroup gv = new EditGroup("`a?.b` value -> `a != null ? a.b : null` (temporary for a non-trivial receiver)");
            string vcond, vaccess;
            if (IsSimple(recv)) {
                string r = Operand(recv, Prec.Primary);
                vcond = NullTest(r, tr, false);
                vaccess = r + valueAccess + rest;
            } else {
                string tn = TypeName(tr, node.SpanStart);
                if (tn == null) { Manual(node, what, "cannot name the receiver type for a temporary; rewrite by hand"); return; }
                string tmp = Fresh(node, "tmp");
                if (HoistDeclaration(gv, node, tn, tr, tmp, null, what) == null) return;
                vcond = NullTest("(" + tmp + " = " + recv + ")", tr, false);
                vaccess = tmp + valueAccess + rest;
            }
            string vtext = vcond + " ? " + vaccess + " : " + nullAlt;
            CheckComments(node, vtext, what);
            gv.Replace(node.Span, ParenFor(node, vtext, Prec.Conditional));
            Commit(gv);
        }

        // ------------------------------------------------------------------ ??= , discards

        public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.CoalesceAssignmentExpression)) HandleCoalesceAssignment(node);
            else if (node.IsKind(SyntaxKind.SimpleAssignmentExpression)) HandleDiscardAssignment(node);
            base.VisitAssignmentExpression(node);
        }

        private void HandleCoalesceAssignment(AssignmentExpressionSyntax node)
        {
            const string what = "`??=`";
            ITypeSymbol tl = Model.GetTypeInfo(node.Left).Type;
            if (IsErrorOrNull(tl)) { Manual(node, what, "the target type does not bind; rewrite by hand"); return; }
            if (!IsReevaluableTarget(node.Left)) {
                Manual(node, what, "the target has side effects that must not be evaluated twice; rewrite by hand with temporaries");
                return;
            }
            if (node.Parent is ArrowExpressionClauseSyntax) return;
            AnonymousFunctionExpressionSyntax af = node.Parent as AnonymousFunctionExpressionSyntax;
            if (af != null && af.ExpressionBody == node) {
                IMethodSymbol ms = Model.GetSymbolInfo(af).Symbol as IMethodSymbol;
                if (ms != null && ms.ReturnsVoid) { RequestLambdaBlock(af); return; }
            }
            string left = node.Left.ToString();
            ExpressionStatementSyntax es = node.Parent as ExpressionStatementSyntax;
            if (es != null) {
                EditGroup g = new EditGroup("`a ??= b;` statement -> `if (a == null) a = b;`");
                string text = WrapIf(es, "if (" + NullTest(Operand(node.Left, Prec.Primary), tl, true) + ") " + left + " = " + node.Right + ";");
                CheckComments(es, text, what);
                g.Replace(es.Span, text);
                Commit(g);
                return;
            }
            if (!tl.IsReferenceType) {
                Manual(node, what, "used as a value on a non-reference type; rewrite by hand");
                return;
            }
            EditGroup gv = new EditGroup("`a ??= b` value -> `a ?? (a = b)`");
            string vtext = Operand(node.Left, Prec.LogicalOr) + " ?? (" + left + " = " + node.Right + ")";
            CheckComments(node, vtext, what);
            gv.Replace(node.Span, ParenFor(node, vtext, Prec.Coalesce));
            Commit(gv);
        }

        private void HandleDiscardAssignment(AssignmentExpressionSyntax node)
        {
            IdentifierNameSyntax id = node.Left as IdentifierNameSyntax;
            if (id == null || id.Identifier.ValueText != "_") return;
            IDiscardSymbol ds = Model.GetSymbolInfo(id).Symbol as IDiscardSymbol;
            if (ds == null) return;
            ExpressionStatementSyntax es = node.Parent as ExpressionStatementSyntax;
            if (es == null) { Manual(node, "discard `_ = ...`", "used as a value; rewrite by hand"); return; }
            ExpressionSyntax r = StripParens(node.Right);
            EditGroup g = new EditGroup("discard assignment `_ = e;` -> `e;` / dummy variable");
            if (r is InvocationExpressionSyntax || r is ObjectCreationExpressionSyntax ||
                r is AssignmentExpressionSyntax || r.IsKind(SyntaxKind.PostIncrementExpression) || r.IsKind(SyntaxKind.PostDecrementExpression) ||
                r.IsKind(SyntaxKind.PreIncrementExpression) || r.IsKind(SyntaxKind.PreDecrementExpression)) {
                g.Replace(node.SpanStart, node.Right.SpanStart, "");
            } else {
                string tn = TypeName(ds.Type, node.SpanStart);
                if (tn == null || IsEmbedded(es)) { Manual(node, "discard `_ = ...`", "declare a dummy variable by hand"); return; }
                g.Replace(id.Span, tn + " " + Fresh(node, "unused"));
            }
            Commit(g);
        }

        // ------------------------------------------------------------------ exception filters

        public override void VisitTryStatement(TryStatementSyntax node)
        {
            HandleExceptionFilters(node);
            base.VisitTryStatement(node);
        }

        private static bool DerivesFromOrEquals(ITypeSymbol t, ITypeSymbol baseType)
        {
            for (ITypeSymbol cur = t; cur != null; cur = cur.BaseType) {
                if (SymbolEqualityComparer.Default.Equals(cur, baseType)) return true;
            }
            return false;
        }

        private void HandleExceptionFilters(TryStatementSyntax node)
        {
            const string what = "exception filter (`catch ... when`)";
            int index = -1;
            for (int i = 0; i < node.Catches.Count; i++) if (node.Catches[i].Filter != null) index = i;
            if (index < 0) return;
            CatchClauseSyntax c = node.Catches[index];
            INamedTypeSymbol exceptionType = _c.Compilation.GetTypeByMetadataName("System.Exception");
            ITypeSymbol e = c.Declaration == null ? exceptionType : Model.GetTypeInfo(c.Declaration.Type).Type;
            if (IsErrorOrNull(e)) { Manual(c, what, "the exception type does not bind; rewrite by hand"); return; }
            string ourId = c.Declaration != null && !c.Declaration.Identifier.IsKind(SyntaxKind.None) ? c.Declaration.Identifier.ValueText : null;

            // where does an exception go when the filter says no?
            List<KeyValuePair<CatchClauseSyntax, bool>> later = new List<KeyValuePair<CatchClauseSyntax, bool>>(); // clause, needs a type test
            bool definitive = false;
            CatchClauseSyntax subsumed = null;
            for (int j = index + 1; j < node.Catches.Count && !definitive; j++) {
                CatchClauseSyntax cj = node.Catches[j];
                if (cj.Declaration == null) { later.Add(new KeyValuePair<CatchClauseSyntax, bool>(cj, false)); definitive = true; break; }
                ITypeSymbol tj = Model.GetTypeInfo(cj.Declaration.Type).Type;
                if (IsErrorOrNull(tj)) { Manual(c, what, "a later catch type does not bind; rewrite by hand"); return; }
                if (DerivesFromOrEquals(e, tj)) {
                    later.Add(new KeyValuePair<CatchClauseSyntax, bool>(cj, false));
                    definitive = true;
                    if (SymbolEqualityComparer.Default.Equals(e, tj)) subsumed = cj;
                } else if (DerivesFromOrEquals(tj, e)) {
                    later.Add(new KeyValuePair<CatchClauseSyntax, bool>(cj, true));
                }
            }
            bool needName = false;
            foreach (KeyValuePair<CatchClauseSyntax, bool> kv in later) {
                bool hasId = kv.Key.Declaration != null && !kv.Key.Declaration.Identifier.IsKind(SyntaxKind.None);
                if (kv.Value || (hasId && kv.Key.Declaration.Identifier.ValueText != ourId)) needName = true;
                if (kv.Value && hasId && kv.Key.Declaration.Identifier.ValueText == ourId) {
                    Manual(c, what, "a later catch clause for a derived type uses the same variable name; rewrite by hand");
                    return;
                }
            }
            string name = ourId;
            EditGroup g = new EditGroup("exception filter `catch (E) when (c) {..}` -> `catch (E) { if (c) {..} else <what the later clauses would do> }`");
            if (needName && ourId == null) {
                name = Fresh(node, "ex");
                if (c.Declaration != null) g.Insert(c.Declaration.Type.Span.End, " " + name);
                else g.Insert(c.CatchKeyword.Span.End, " (System.Exception " + name + ")");
            }
            string ind = IndentOf(c.Block.CloseBraceToken.SpanStart);
            string i1 = ind + "    ";
            StringBuilder sb = new StringBuilder();
            sb.Append("{").Append(NL);
            sb.Append(i1).Append("if (").Append(c.Filter.FilterExpression.ToString()).Append(") {").Append(NL);
            sb.Append(BlockBody(c.Block, i1, "    "));
            foreach (KeyValuePair<CatchClauseSyntax, bool> kv in later) {
                CatchClauseSyntax cj = kv.Key;
                bool hasId = cj.Declaration != null && !cj.Declaration.Identifier.IsKind(SyntaxKind.None);
                if (kv.Value) sb.Append(i1).Append("} else if (").Append(name).Append(" is ").Append(cj.Declaration.Type.ToString()).Append(") {").Append(NL);
                else sb.Append(i1).Append("} else {").Append(NL);
                if (hasId && cj.Declaration.Identifier.ValueText != name) {
                    sb.Append(i1).Append("    ").Append(cj.Declaration.Type.ToString()).Append(' ').Append(cj.Declaration.Identifier.ValueText)
                      .Append(" = ").Append(kv.Value ? "(" + cj.Declaration.Type + ")" : "").Append(name).Append(";").Append(NL);
                }
                sb.Append(BlockBody(cj.Block, i1, "    "));
            }
            if (!definitive) {
                sb.Append(i1).Append("} else {").Append(NL);
                sb.Append(i1).Append("    throw;").Append(NL);
            }
            sb.Append(i1).Append("}").Append(NL);
            sb.Append(ind).Append("}");
            g.Replace(c.Filter.SpanStart, c.Block.Span.End, sb.ToString());
            if (subsumed != null) {
                // the unfiltered clause for the same type can no longer be reached
                int idx = node.Catches.IndexOf(subsumed);
                g.Replace(node.Catches[idx - 1].Span.End, subsumed.Span.End, "");
            }
            Commit(g);
        }

        // ------------------------------------------------------------------ using declarations

        public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            if (!node.UsingKeyword.IsKind(SyntaxKind.None)) HandleUsingDeclaration(node);
            foreach (SyntaxToken m in node.Modifiers) {
                if (m.IsKind(SyntaxKind.RefKeyword)) Manual(node, "ref local", "not available in C# 4");
            }
            if (node.Declaration.Type is RefTypeSyntax) Manual(node, "ref local", "not available in C# 4");
            base.VisitLocalDeclarationStatement(node);
        }

        private void HandleUsingDeclaration(LocalDeclarationStatementSyntax node)
        {
            const string what = "using declaration";
            BlockSyntax block = node.Parent as BlockSyntax;
            if (!node.AwaitKeyword.IsKind(SyntaxKind.None) || block == null) {
                Manual(node, what, "not directly inside a block (or `await using`); rewrite as a using statement by hand");
                return;
            }
            if (!IsFirstOnLine(node.SpanStart) || !IsFirstOnLine(block.CloseBraceToken.SpanStart)) {
                Manual(node, what, "unusual layout; rewrite as a using statement by hand");
                return;
            }
            if (!_usingBlocks.Add(block)) return; // one per block and pass: the next pass sees the new nesting
            foreach (StatementSyntax s in block.Statements) {
                if (s is LabeledStatementSyntax && s.SpanStart > node.SpanStart) {
                    Manual(node, what, "a label follows in the same block; rewrite by hand");
                    return;
                }
            }
            EditGroup g = new EditGroup("`using var x = ...;` -> `using (var x = ...) { rest of the block }`");
            string ind = IndentOf(node.SpanStart);
            string decl = node.Declaration.ToString();
            g.Replace(node.Span, "using (" + decl + ")" + NL + ind + "{");
            TextLine next = _c.Text.Lines[_c.Text.Lines.GetLineFromPosition(node.Span.End).LineNumber + 1];
            TextLine closeLine = _c.Text.Lines.GetLineFromPosition(block.CloseBraceToken.SpanStart);
            IndentLines(g, _c.Tree.GetRoot(), _c.Text, next.Start, closeLine.Start, "    ");
            g.Insert(closeLine.Start, ind + "}" + NL);
            Commit(g);
        }

        // ------------------------------------------------------------------ throw expressions

        public override void VisitThrowExpression(ThrowExpressionSyntax node)
        {
            HandleThrowExpression(node);
            base.VisitThrowExpression(node);
        }

        /// <summary>
        /// The statement in which <paramref name="value"/> is the first thing evaluated:
        /// `return value;`, `T v = value;`, or `simple = value;`.
        /// </summary>
        private StatementSyntax FirstEvaluatedIn(ExpressionSyntax value)
        {
            SyntaxNode p = value.Parent;
            ReturnStatementSyntax rs = p as ReturnStatementSyntax;
            if (rs != null) return rs;
            EqualsValueClauseSyntax ev = p as EqualsValueClauseSyntax;
            if (ev != null && ev.Parent is VariableDeclaratorSyntax && ev.Parent.Parent is VariableDeclarationSyntax) {
                VariableDeclarationSyntax vdn = (VariableDeclarationSyntax)ev.Parent.Parent;
                LocalDeclarationStatementSyntax lds = vdn.Parent as LocalDeclarationStatementSyntax;
                if (lds != null && vdn.Variables.Count == 1 && lds.UsingKeyword.IsKind(SyntaxKind.None)) return lds;
                return null;
            }
            AssignmentExpressionSyntax asg = p as AssignmentExpressionSyntax;
            if (asg != null && asg.IsKind(SyntaxKind.SimpleAssignmentExpression) && asg.Right == value &&
                asg.Parent is ExpressionStatementSyntax && IsReevaluableTarget(asg.Left) && !(StripParens(asg.Left) is ElementAccessExpressionSyntax)) {
                return (ExpressionStatementSyntax)asg.Parent;
            }
            return null;
        }

        private void HandleThrowExpression(ThrowExpressionSyntax node)
        {
            const string what = "throw expression";
            SyntaxNode p = node.Parent;
            if (p is ArrowExpressionClauseSyntax || p is AnonymousFunctionExpressionSyntax) return; // handled there
            if (p is SwitchExpressionArmSyntax) return;                                             // handled with the switch expression
            ExpressionSyntax host = p as ExpressionSyntax;
            BinaryExpressionSyntax co = p as BinaryExpressionSyntax;
            ConditionalExpressionSyntax cond = p as ConditionalExpressionSyntax;
            if (host == null || !((co != null && co.IsKind(SyntaxKind.CoalesceExpression) && co.Right == node) || (cond != null && cond.Condition != node))) {
                Manual(node, what, "unexpected position; rewrite as a throw statement by hand");
                return;
            }
            if (host.Parent is ArrowExpressionClauseSyntax) return; // next pass
            StatementSyntax stmt = FirstEvaluatedIn(host);
            if (stmt == null || (IsEmbedded(stmt) && stmt is LocalDeclarationStatementSyntax)) {
                Manual(node, what, "is not the first thing evaluated by a simple statement; rewrite by hand as `if (...) throw ...;` before the statement");
                return;
            }
            EditGroup g = new EditGroup("throw expression (`a ?? throw`, `c ? x : throw`) -> `if (...) throw ...;` before the statement");
            bool embedded = IsEmbedded(stmt);
            string ind = IndentOf(stmt.SpanStart);
            string sep = embedded ? " " : IsFirstOnLine(stmt.SpanStart) ? NL + ind : " ";
            string throwText = "throw " + node.Expression + ";";
            string pre, replacement;
            if (co != null) {
                ITypeSymbol tl = Model.GetTypeInfo(co.Left).Type;
                if (IsErrorOrNull(tl)) { Manual(node, what, "the tested type does not bind; rewrite by hand"); return; }
                string valueSuffix = IsNullableValue(tl) ? ".GetValueOrDefault()" : "";
                if (IsSimple(co.Left)) {
                    string l = Operand(co.Left, Prec.Primary);
                    pre = "if (" + NullTest(l, tl, true) + ") " + throwText;
                    replacement = (valueSuffix.Length > 0 ? l : co.Left.ToString()) + valueSuffix;
                } else {
                    string tn = TypeName(tl, node.SpanStart);
                    if (tn == null) { Manual(node, what, "cannot name the type for a temporary; rewrite by hand"); return; }
                    string tmp = Fresh(node, "tmp");
                    pre = tn + " " + tmp + " = " + co.Left + ";" + sep + "if (" + NullTest(tmp, tl, true) + ") " + throwText;
                    replacement = tmp + valueSuffix;
                }
            } else {
                bool throwWhenTrue = cond.WhenTrue == node;
                string cnd = throwWhenTrue ? cond.Condition.ToString() : "!" + Operand(cond.Condition, Prec.Primary);
                pre = "if (" + cnd + ") " + throwText;
                ExpressionSyntax keep = throwWhenTrue ? cond.WhenFalse : cond.WhenTrue;
                replacement = ParenFor(host, keep.ToString(), Prec.Of(keep));
                // the conditional gave its other branch the converted type; keep it explicit when it differs
                TypeInfo hi = Model.GetTypeInfo(host), ki = Model.GetTypeInfo(keep);
                if (hi.Type != null && ki.Type != null && !SymbolEqualityComparer.Default.Equals(hi.Type, ki.Type)) {
                    string tn = TypeName(hi.Type, node.SpanStart);
                    if (tn != null) replacement = ParenFor(host, "(" + tn + ")" + Operand(keep, Prec.Unary), Prec.Unary);
                }
            }
            if (embedded) {
                g.Insert(stmt.SpanStart, "{ " + pre + " ");
                g.Insert(stmt.Span.End, " }");
            } else {
                g.Insert(stmt.SpanStart, pre + sep);
            }
            g.Replace(host.Span, replacement);
            CheckComments(host, replacement + pre, what);
            Commit(g);
        }

        // ------------------------------------------------------------------ switch expressions

        public override void VisitSwitchExpression(SwitchExpressionSyntax node)
        {
            HandleSwitchExpression(node);
            base.VisitSwitchExpression(node);
        }

        private void HandleSwitchExpression(SwitchExpressionSyntax node)
        {
            const string what = "switch expression";
            if (node.Parent is ArrowExpressionClauseSyntax) return;
            StatementSyntax stmt = FirstEvaluatedIn(node);
            if (stmt == null) {
                Manual(node, what, "only `return x switch {..};`, `T v = x switch {..};` and `v = x switch {..};` are rewritten; rewrite this one by hand as if/else");
                return;
            }
            ITypeSymbol tg = Model.GetTypeInfo(node.GoverningExpression).Type;
            if (IsErrorOrNull(tg)) { Manual(node, what, "the governing type does not bind; rewrite by hand"); return; }
            bool embedded = IsEmbedded(stmt);
            if (embedded && stmt is LocalDeclarationStatementSyntax) { Manual(node, what, "rewrite by hand"); return; }
            string ind = IndentOf(stmt.SpanStart);
            string lineInd = embedded ? ind + "    " : ind;
            List<string> lines = new List<string>();

            string gov;
            if (IsSimpleOrLiteral(node.GoverningExpression)) {
                gov = Operand(node.GoverningExpression, Prec.Shift);
            } else {
                string tn = TypeName(tg, node.SpanStart);
                if (tn == null) { Manual(node, what, "cannot name the governing type; rewrite by hand"); return; }
                gov = Fresh(node, "tmp");
                lines.Add(tn + " " + gov + " = " + node.GoverningExpression + ";");
            }
            string actionPrefix;
            LocalDeclarationStatementSyntax lds = stmt as LocalDeclarationStatementSyntax;
            if (stmt is ReturnStatementSyntax) {
                actionPrefix = "return ";
            } else if (lds != null) {
                VariableDeclaratorSyntax v = lds.Declaration.Variables[0];
                string tn = lds.Declaration.Type.IsVar ? TypeName(Model.GetTypeInfo(node).ConvertedType ?? Model.GetTypeInfo(node).Type, node.SpanStart) : lds.Declaration.Type.ToString();
                if (tn == null) { Manual(node, what, "cannot name the result type; rewrite by hand"); return; }
                string mods = lds.Modifiers.Count > 0 ? lds.Modifiers.ToString() + " " : "";
                if (lds.IsConst) { Manual(node, what, "rewrite by hand"); return; }
                lines.Insert(0, mods + tn + " " + v.Identifier.ValueText + ";");
                actionPrefix = v.Identifier.ValueText + " = ";
            } else {
                AssignmentExpressionSyntax asg = (AssignmentExpressionSyntax)node.Parent;
                actionPrefix = asg.Left + " = ";
            }
            bool exhaustive = false;
            bool first = true;
            foreach (SwitchExpressionArmSyntax arm in node.Arms) {
                int prec;
                string cond = PatternCondition(arm.Pattern, gov, tg, true, out prec);
                if (cond == null) { Manual(arm, what, "pattern `" + arm.Pattern + "` is not rewritten automatically; rewrite the switch expression by hand"); return; }
                if (arm.WhenClause != null) {
                    string w = Operand(arm.WhenClause.Condition, Prec.LogicalAnd);
                    cond = cond == "true" ? arm.WhenClause.Condition.ToString() : (prec < Prec.LogicalAnd ? "(" + cond + ")" : cond) + " && " + w;
                }
                string action = arm.Expression is ThrowExpressionSyntax
                    ? "throw " + ((ThrowExpressionSyntax)arm.Expression).Expression + ";"
                    : actionPrefix + arm.Expression + ";";
                if (cond == "true") {
                    lines.Add(first ? action : "else " + action);
                    exhaustive = true;
                    break;
                }
                lines.Add((first ? "if (" : "else if (") + cond + ") " + action);
                first = false;
            }
            if (!exhaustive) lines.Add((first ? "" : "else ") + "throw new System.InvalidOperationException(\"Non-exhaustive switch expression.\");");
            string text;
            if (!IsFirstOnLine(stmt.SpanStart)) text = embedded ? "{ " + string.Join(" ", lines) + " }" : string.Join(" ", lines);
            else if (embedded) text = "{" + NL + lineInd + string.Join(NL + lineInd, lines) + NL + ind + "}";
            else text = string.Join(NL + lineInd, lines);
            CheckComments(stmt, text, what);
            EditGroup g = new EditGroup("switch expression -> if/else chain");
            g.Replace(stmt.Span, text);
            Commit(g);
        }

        // ------------------------------------------------------------------ auto-properties

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            if (InStruct(node)) DropModifier(node.Modifiers, SyntaxKind.ReadOnlyKeyword, "`readonly` member modifier removed");
            CommonModifiers(node.Modifiers, false);
            HandleAutoProperty(node);
            base.VisitPropertyDeclaration(node);
        }

        private void HandleAutoProperty(PropertyDeclarationSyntax node)
        {
            if (node.AccessorList == null) return;
            if (node.Parent is InterfaceDeclarationSyntax) return;
            foreach (SyntaxToken m in node.Modifiers) {
                if (m.IsKind(SyntaxKind.AbstractKeyword) || m.IsKind(SyntaxKind.ExternKeyword)) return;
            }
            AccessorDeclarationSyntax get = null, set = null;
            foreach (AccessorDeclarationSyntax a in node.AccessorList.Accessors) {
                if (a.Body != null || a.ExpressionBody != null) return; // not an auto-property
                if (a.Keyword.IsKind(SyntaxKind.GetKeyword)) get = a; else set = a;
            }
            if (get == null) return;
            bool isStatic = node.Modifiers.Any(SyntaxKind.StaticKeyword);
            bool isOverride = node.Modifiers.Any(SyntaxKind.OverrideKeyword) || node.ExplicitInterfaceSpecifier != null;
            if (node.Initializer == null) {
                if (set != null) return; // { get; set; } is C# 4
                if (isOverride) {
                    Manual(node, "getter-only auto-property (override / explicit implementation)", "use a readonly backing field by hand");
                    return;
                }
                EditGroup g = new EditGroup("getter-only auto-property `{ get; }` -> `{ get; private set; }`");
                g.Insert(get.Span.End, " private set;");
                Commit(g);
                return;
            }
            // initialised auto-property: explicit backing field with the same initializer (same initialisation order)
            IPropertySymbol ps = Model.GetDeclaredSymbol(node) as IPropertySymbol;
            if (ps == null || isOverride && set == null) {
                Manual(node, "auto-property initializer", "use a backing field by hand");
                return;
            }
            if (set != null && set.Keyword.IsKind(SyntaxKind.InitKeyword)) return; // after `init` became `set`
            string pname = node.Identifier.ValueText;
            string field = "_" + char.ToLowerInvariant(pname[0]) + pname.Substring(1);
            INamedTypeSymbol ct = ps.ContainingType;
            int k = 0;
            while (TypeHasMember(ct, field)) field = "_" + char.ToLowerInvariant(pname[0]) + pname.Substring(1) + "Field" + (k++ == 0 ? "" : k.ToString(CultureInfo.InvariantCulture));
            string ind = IndentOf(node.SpanStart);
            EditGroup g2 = new EditGroup("auto-property initializer -> explicit backing field");
            g2.Insert(node.SpanStart, "private " + (isStatic ? "static " : "") + node.Type + " " + field + " " + node.Initializer + ";" +
                      (IsFirstOnLine(node.SpanStart) ? NL + ind : " "));
            g2.Replace(get.SemicolonToken.Span, " { return " + field + "; }");
            if (set != null) g2.Replace(set.SemicolonToken.Span, " { " + field + " = value; }");
            else g2.Insert(get.Span.End, " private set { " + field + " = value; }");
            g2.Replace(node.AccessorList.Span.End, node.SemicolonToken.Span.End, "");
            Commit(g2);
        }

        private static bool TypeHasMember(INamedTypeSymbol t, string name)
        {
            for (INamedTypeSymbol cur = t; cur != null; cur = cur.BaseType) {
                if (cur.GetMembers(name).Length > 0) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ foreach closures (C# 4 semantics)

        /// <summary>
        /// Before C# 5 a foreach iteration variable is ONE variable for the whole loop, so a lambda that
        /// captures it sees the last element once the loop has moved on. A per-iteration copy with the
        /// original name keeps the C# 5+ behaviour under every compiler:
        /// `foreach (T x in xs) { use(() =&gt; x); }` -> `foreach (T x_it1 in xs) { T x = x_it1; use(() =&gt; x); }`.
        /// </summary>
        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            ILocalSymbol sym = Model.GetDeclaredSymbol(node) as ILocalSymbol;
            if (sym != null && IsCaptured(sym, node.Statement)) {
                string name = node.Identifier.ValueText;
                string iter = Fresh(node, name + "_it");
                string decl = node.Type + " " + name + " = " + iter + ";";
                EditGroup g = new EditGroup("foreach variable captured by a lambda -> per-iteration copy (C# 4 closure semantics)");
                g.Replace(node.Identifier.Span, iter);
                BlockSyntax block = node.Statement as BlockSyntax;
                if (block != null) {
                    int open = block.OpenBraceToken.Span.End;
                    bool multiLine = _c.Text.Lines.GetLineFromPosition(open).LineNumber != _c.Text.Lines.GetLineFromPosition(block.CloseBraceToken.SpanStart).LineNumber;
                    if (multiLine && string.IsNullOrWhiteSpace(TextOf(open, _c.Text.Lines.GetLineFromPosition(open).End))) {
                        string bi = block.Statements.Count > 0 && IsFirstOnLine(block.Statements[0].SpanStart)
                            ? IndentOf(block.Statements[0].SpanStart) : IndentOf(block.CloseBraceToken.SpanStart) + "    ";
                        g.Insert(open, NL + bi + decl);
                    } else {
                        g.Insert(open, " " + decl);
                    }
                } else {
                    g.Insert(node.Statement.SpanStart, "{ " + decl + " ");
                    g.Insert(node.Statement.Span.End, " }");
                }
                Commit(g);
            }
            base.VisitForEachStatement(node);
        }

        // ------------------------------------------------------------------ local functions (reported)

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            HandleLocalFunction(node);
            base.VisitLocalFunctionStatement(node);
        }
    }
}
