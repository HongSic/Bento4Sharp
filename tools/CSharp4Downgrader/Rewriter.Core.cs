// CSharp4Downgrader - shared helpers of the rewriter (precedence, naming, hoisting).
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSharp4Downgrader
{
    /// <summary>Everything a pass needs to know about one file.</summary>
    internal sealed class FileContext
    {
        public string Path;
        public string DisplayPath;
        public SyntaxTree Tree;
        public SourceText Text;
        public string NewLine;
        public CSharpCompilation Compilation;
        public FileEdits Edits;
        public Stats Stats;
        public Reporter Reporter;
        public bool TupleNamesPending;

        private SemanticModel _model;
        public SemanticModel Model
        {
            get { if (_model == null) _model = Compilation.GetSemanticModel(Tree); return _model; }
        }
    }

    internal static class Prec
    {
        public const int Assignment = 0, Conditional = 1, Coalesce = 2, LogicalOr = 3, LogicalAnd = 4, BitOr = 5,
                         Xor = 6, BitAnd = 7, Equality = 8, Relational = 9, Shift = 10, Additive = 11,
                         Multiplicative = 12, Unary = 13, Primary = 14;

        public static int OfBinary(SyntaxKind kind)
        {
            switch (kind) {
                case SyntaxKind.CoalesceExpression: return Coalesce;
                case SyntaxKind.LogicalOrExpression: return LogicalOr;
                case SyntaxKind.LogicalAndExpression: return LogicalAnd;
                case SyntaxKind.BitwiseOrExpression: return BitOr;
                case SyntaxKind.ExclusiveOrExpression: return Xor;
                case SyntaxKind.BitwiseAndExpression: return BitAnd;
                case SyntaxKind.EqualsExpression:
                case SyntaxKind.NotEqualsExpression: return Equality;
                case SyntaxKind.LessThanExpression:
                case SyntaxKind.LessThanOrEqualExpression:
                case SyntaxKind.GreaterThanExpression:
                case SyntaxKind.GreaterThanOrEqualExpression:
                case SyntaxKind.IsExpression:
                case SyntaxKind.AsExpression: return Relational;
                case SyntaxKind.LeftShiftExpression:
                case SyntaxKind.RightShiftExpression:
                case SyntaxKind.UnsignedRightShiftExpression: return Shift;
                case SyntaxKind.AddExpression:
                case SyntaxKind.SubtractExpression: return Additive;
                case SyntaxKind.MultiplyExpression:
                case SyntaxKind.DivideExpression:
                case SyntaxKind.ModuloExpression: return Multiplicative;
            }
            return Assignment;
        }

        /// <summary>Precedence of an existing expression node.</summary>
        public static int Of(ExpressionSyntax e)
        {
            if (e is BinaryExpressionSyntax) return OfBinary(e.Kind());
            if (e is AssignmentExpressionSyntax || e is AnonymousFunctionExpressionSyntax || e is ThrowExpressionSyntax) return Assignment;
            if (e is ConditionalExpressionSyntax) return Conditional;
            if (e is IsPatternExpressionSyntax) return Relational;
            if (e is PrefixUnaryExpressionSyntax || e is CastExpressionSyntax || e is AwaitExpressionSyntax) return Unary;
            if (e is SwitchExpressionSyntax || e is WithExpressionSyntax || e is RangeExpressionSyntax) return Unary - 1;
            if (e is LiteralExpressionSyntax || e is IdentifierNameSyntax || e is GenericNameSyntax ||
                e is MemberAccessExpressionSyntax || e is InvocationExpressionSyntax || e is ElementAccessExpressionSyntax ||
                e is ParenthesizedExpressionSyntax || e is ThisExpressionSyntax || e is BaseExpressionSyntax ||
                e is TypeOfExpressionSyntax || e is DefaultExpressionSyntax || e is BaseObjectCreationExpressionSyntax ||
                e is ArrayCreationExpressionSyntax || e is ImplicitArrayCreationExpressionSyntax ||
                e is AnonymousObjectCreationExpressionSyntax || e is CheckedExpressionSyntax || e is SizeOfExpressionSyntax ||
                e is PostfixUnaryExpressionSyntax || e is ConditionalAccessExpressionSyntax || e is PredefinedTypeSyntax ||
                e is InterpolatedStringExpressionSyntax || e is QualifiedNameSyntax || e is AliasQualifiedNameSyntax ||
                e is TupleExpressionSyntax) return Primary;
            return Assignment;
        }

        /// <summary>The minimum precedence an expression must have to stand where <paramref name="node"/> stands without parentheses.</summary>
        public static int Required(ExpressionSyntax node)
        {
            SyntaxNode p = node.Parent;
            if (p is ParenthesizedExpressionSyntax) return Assignment;
            AssignmentExpressionSyntax a = p as AssignmentExpressionSyntax;
            if (a != null) return a.Right == node ? Assignment : Unary;
            ConditionalExpressionSyntax c = p as ConditionalExpressionSyntax;
            if (c != null) return c.Condition == node ? Coalesce : Conditional;
            BinaryExpressionSyntax b = p as BinaryExpressionSyntax;
            if (b != null) {
                int bp = OfBinary(b.Kind());
                if (b.IsKind(SyntaxKind.CoalesceExpression)) return b.Left == node ? bp + 1 : bp;
                return b.Left == node ? bp : bp + 1;
            }
            if (p is IsPatternExpressionSyntax) return Shift;
            if (p is InterpolationSyntax) return Coalesce;
            if (p is InitializerExpressionSyntax) return Assignment;
            AnonymousFunctionExpressionSyntax af = p as AnonymousFunctionExpressionSyntax;
            if (af != null) return Assignment;
            if (p is ExpressionSyntax) return Primary;
            return Assignment;
        }
    }

    internal sealed partial class Rewriter : CSharpSyntaxWalker
    {
        private readonly FileContext _c;
        private readonly Dictionary<SyntaxNode, HashSet<string>> _memberNames = new Dictionary<SyntaxNode, HashSet<string>>();
        private readonly Dictionary<Tuple<SyntaxNode, string>, ITypeSymbol> _hoisted = new Dictionary<Tuple<SyntaxNode, string>, ITypeSymbol>();
        private readonly HashSet<SyntaxNode> _lambdaBlockRequested = new HashSet<SyntaxNode>();
        private readonly HashSet<SyntaxNode> _usingBlocks = new HashSet<SyntaxNode>();

        public Rewriter(FileContext c) : base(SyntaxWalkerDepth.StructuredTrivia) { _c = c; }

        private SemanticModel Model { get { return _c.Model; } }
        private string NL { get { return _c.NewLine; } }

        // ------------------------------------------------------------------ basics

        private bool Commit(EditGroup g)
        {
            if (!_c.Edits.TryAdd(g)) return false;
            _c.Stats.Add(g.Stat, g.StatCount);
            if (g.OnCommit != null) foreach (Action a in g.OnCommit) a();
            return true;
        }

        private void Manual(SyntaxNode node, string construct, string message)
        {
            int line = _c.Text.Lines.GetLineFromPosition(node.SpanStart).LineNumber + 1;
            _c.Reporter.Manual(_c.DisplayPath, line, construct, message);
        }

        private string TextOf(int start, int end) { return _c.Text.ToString(TextSpan.FromBounds(start, end)); }

        private static ExpressionSyntax StripParens(ExpressionSyntax e)
        {
            while (e is ParenthesizedExpressionSyntax) e = ((ParenthesizedExpressionSyntax)e).Expression;
            return e;
        }

        /// <summary>The text of an existing expression, parenthesized when its precedence is below <paramref name="minPrec"/>.</summary>
        private static string Operand(ExpressionSyntax e, int minPrec)
        {
            string t = e.ToString();
            return Prec.Of(e) < minPrec ? "(" + t + ")" : t;
        }

        /// <summary>New text of precedence <paramref name="prec"/> that replaces <paramref name="node"/>, parenthesized if the context requires.</summary>
        private static string ParenFor(ExpressionSyntax node, string text, int prec)
        {
            return prec < Prec.Required(node) ? "(" + text + ")" : text;
        }

        private string IndentOf(int position)
        {
            TextLine line = _c.Text.Lines.GetLineFromPosition(position);
            string s = line.ToString();
            int i = 0;
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) i++;
            return s.Substring(0, i);
        }

        private bool IsFirstOnLine(int position)
        {
            TextLine line = _c.Text.Lines.GetLineFromPosition(position);
            return string.IsNullOrWhiteSpace(TextOf(line.Start, position));
        }

        private static bool IsErrorOrNull(ITypeSymbol t) { return t == null || t.TypeKind == TypeKind.Error; }

        private static bool IsNullableValue(ITypeSymbol t)
        {
            return t != null && t.OriginalDefinition != null && t.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        }

        /// <summary>A C# 4 spelling of the type that is valid at <paramref name="position"/>, or null.</summary>
        private string TypeName(ITypeSymbol t, int position)
        {
            if (IsErrorOrNull(t)) return null;
            if (t.IsAnonymousType) return null;
            INamedTypeSymbol nt = t as INamedTypeSymbol;
            if (nt != null && nt.IsTupleType) {
                List<string> parts = new List<string>();
                foreach (IFieldSymbol f in nt.TupleElements) {
                    string p = TypeName(f.Type, position);
                    if (p == null) return null;
                    parts.Add(p);
                }
                if (parts.Count > 7) return null;
                return "System.ValueTuple<" + string.Join(", ", parts) + ">";
            }
            string s = t.ToMinimalDisplayString(Model, position, SymbolDisplayFormat.MinimallyQualifiedFormat);
            if (s.IndexOf('(') >= 0 || s.IndexOf('<') >= 0 && s.IndexOf("anonymous", StringComparison.Ordinal) >= 0) return null;
            if (s.Length == 0 || s == "?") return null;
            return s;
        }

        private static bool HasUserDefinedEquality(ITypeSymbol t)
        {
            if (t == null) return false;
            if (t.SpecialType == SpecialType.System_String || t.SpecialType == SpecialType.System_Object) return false;
            if (t.TypeKind == TypeKind.Delegate || t.TypeKind == TypeKind.Interface || t.TypeKind == TypeKind.Array) return false;
            if (t.TypeKind == TypeKind.TypeParameter) return false;
            for (ITypeSymbol cur = t; cur != null; cur = cur.BaseType) {
                if (cur.GetMembers("op_Equality").Length > 0 || cur.GetMembers("op_Inequality").Length > 0) return true;
            }
            return false;
        }

        /// <summary>`x == null` / `x != null` for operand text of at least relational precedence, without calling a user-defined operator.</summary>
        private static string NullTest(string operand, ITypeSymbol type, bool isNull)
        {
            if (IsNullableValue(type)) return (isNull ? "!" : "") + operand + ".HasValue";
            if (HasUserDefinedEquality(type)) return "(object)" + operand + (isNull ? " == null" : " != null");
            return operand + (isNull ? " == null" : " != null");
        }

        private static int NullTestPrec(ITypeSymbol type, bool isNull)
        {
            if (IsNullableValue(type)) return isNull ? Prec.Unary : Prec.Primary;
            return Prec.Equality;
        }

        private static bool FieldOk(IFieldSymbol f) { return !f.IsVolatile && f.Type.TypeKind != TypeKind.Delegate; }

        /// <summary>
        /// True when evaluating the expression twice in a row is indistinguishable from evaluating it once:
        /// locals, parameters, `this`, and (non-volatile, non-delegate) fields reached through such expressions.
        /// </summary>
        private bool IsSimple(ExpressionSyntax e)
        {
            e = StripParens(e);
            if (e is ThisExpressionSyntax || e is BaseExpressionSyntax) return true;
            IdentifierNameSyntax id = e as IdentifierNameSyntax;
            if (id != null) {
                ISymbol s = Model.GetSymbolInfo(id).Symbol;
                if (s is ILocalSymbol || s is IParameterSymbol) return true;
                IFieldSymbol f = s as IFieldSymbol;
                return f != null && FieldOk(f);
            }
            MemberAccessExpressionSyntax ma = e as MemberAccessExpressionSyntax;
            if (ma != null && ma.IsKind(SyntaxKind.SimpleMemberAccessExpression)) {
                IFieldSymbol f = Model.GetSymbolInfo(ma).Symbol as IFieldSymbol;
                if (f == null || !FieldOk(f)) return false;
                if (f.IsStatic || f.IsConst) return true;
                return IsSimple(ma.Expression);
            }
            return false;
        }

        private bool IsSimpleOrLiteral(ExpressionSyntax e)
        {
            e = StripParens(e);
            return e is LiteralExpressionSyntax || IsSimple(e);
        }

        /// <summary>An assignment target whose read followed by a write evaluates no side effect twice.</summary>
        private bool IsReevaluableTarget(ExpressionSyntax e)
        {
            e = StripParens(e);
            if (IsSimple(e)) return true;
            IdentifierNameSyntax id = e as IdentifierNameSyntax;
            if (id != null) return Model.GetSymbolInfo(id).Symbol is IPropertySymbol;
            MemberAccessExpressionSyntax ma = e as MemberAccessExpressionSyntax;
            if (ma != null && ma.IsKind(SyntaxKind.SimpleMemberAccessExpression)) {
                ISymbol s = Model.GetSymbolInfo(ma).Symbol;
                if (s is IPropertySymbol || s is IFieldSymbol) {
                    if (s.IsStatic) return true;
                    return IsSimple(ma.Expression);
                }
                return false;
            }
            ElementAccessExpressionSyntax ea = e as ElementAccessExpressionSyntax;
            if (ea != null) {
                if (!IsSimple(ea.Expression)) return false;
                foreach (ArgumentSyntax arg in ea.ArgumentList.Arguments) {
                    if (!IsSimpleOrLiteral(arg.Expression)) return false;
                }
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ fresh names

        private static SyntaxNode MemberOf(SyntaxNode node)
        {
            for (SyntaxNode cur = node; cur != null; cur = cur.Parent) {
                if (cur is BaseMethodDeclarationSyntax || cur is BasePropertyDeclarationSyntax ||
                    cur is BaseFieldDeclarationSyntax || cur is GlobalStatementSyntax) return cur;
                if (cur.Parent == null) return cur;
            }
            return node;
        }

        private string Fresh(SyntaxNode at, string baseName)
        {
            SyntaxNode member = MemberOf(at);
            HashSet<string> names;
            if (!_memberNames.TryGetValue(member, out names)) {
                names = new HashSet<string>(StringComparer.Ordinal);
                foreach (SyntaxToken t in member.DescendantTokens()) {
                    if (t.IsKind(SyntaxKind.IdentifierToken)) names.Add(t.ValueText);
                }
                _memberNames[member] = names;
            }
            for (int i = 1; ; i++) {
                string cand = baseName + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (names.Add(cand)) return cand;
            }
        }

        // ------------------------------------------------------------------ hoisting

        private enum HoistStatus { Ok, Deferred, Failed }

        /// <summary>
        /// Finds the statement before which a declaration needed by <paramref name="node"/> can be placed:
        /// the nearest enclosing statement that stands directly in a block or switch section.
        /// </summary>
        private HoistStatus FindHoistStatement(SyntaxNode node, string what, out StatementSyntax stmt)
        {
            stmt = null;
            for (SyntaxNode cur = node; cur != null && cur.Parent != null; cur = cur.Parent) {
                SyntaxNode p = cur.Parent;
                if (cur is StatementSyntax && (p is BlockSyntax || p is SwitchSectionSyntax)) {
                    if (cur is LocalFunctionStatementSyntax) break;
                    stmt = (StatementSyntax)cur;
                    return HoistStatus.Ok;
                }
                AnonymousFunctionExpressionSyntax af = p as AnonymousFunctionExpressionSyntax;
                if (af != null && af.ExpressionBody == cur) {
                    RequestLambdaBlock(af);
                    return HoistStatus.Deferred;
                }
                if (p is ArrowExpressionClauseSyntax) return HoistStatus.Deferred;
                if (cur is MemberDeclarationSyntax || cur is AccessorDeclarationSyntax || cur is ConstructorInitializerSyntax ||
                    cur is QueryClauseSyntax || cur is SelectOrGroupClauseSyntax || cur is AttributeSyntax ||
                    cur is ParameterSyntax || cur is LocalFunctionStatementSyntax) break;
            }
            Manual(node, what, "needs a local declaration but does not stand inside a statement of a block (field initializer, constructor initializer, query clause, ...): rewrite by hand");
            return HoistStatus.Failed;
        }

        /// <summary>The scope a declaration hoisted from <paramref name="node"/> would land in, without side effects; null if unknown in this pass.</summary>
        private static SyntaxNode PeekHoistScope(SyntaxNode node)
        {
            for (SyntaxNode cur = node; cur != null && cur.Parent != null; cur = cur.Parent) {
                SyntaxNode p = cur.Parent;
                if (cur is StatementSyntax && (p is BlockSyntax || p is SwitchSectionSyntax)) {
                    if (cur is LocalFunctionStatementSyntax) return null;
                    return p is SwitchSectionSyntax ? p.Parent : p;
                }
                if (p is AnonymousFunctionExpressionSyntax || p is ArrowExpressionClauseSyntax) return null;
                if (cur is MemberDeclarationSyntax || cur is AccessorDeclarationSyntax) return null;
            }
            return null;
        }

        private bool IsCaptured(ISymbol sym, SyntaxNode scope)
        {
            foreach (SyntaxNode n in scope.DescendantNodes()) {
                if (!(n is AnonymousFunctionExpressionSyntax || n is LocalFunctionStatementSyntax || n is QueryExpressionSyntax)) continue;
                foreach (SyntaxNode m in n.DescendantNodes()) {
                    IdentifierNameSyntax id = m as IdentifierNameSyntax;
                    if (id != null && id.Identifier.ValueText == sym.Name &&
                        SymbolEqualityComparer.Default.Equals(Model.GetSymbolInfo(id).Symbol, sym)) return true;
                }
            }
            return false;
        }

        /// <summary>`x => expr` becomes `x => { return expr; }` (or `{ expr; }` for a void delegate).</summary>
        private void RequestLambdaBlock(AnonymousFunctionExpressionSyntax af)
        {
            if (!_lambdaBlockRequested.Add(af)) return;
            ExpressionSyntax e = af.ExpressionBody;
            if (e == null) return;
            IMethodSymbol ms = Model.GetSymbolInfo(af).Symbol as IMethodSymbol;
            if (ms == null) {
                Manual(af, "lambda", "cannot determine the delegate type of the lambda; convert its body to a block by hand");
                return;
            }
            EditGroup g = new EditGroup("lambda expression body -> block body (to host a statement)");
            bool plain = ms.ReturnsVoid || e is ThrowExpressionSyntax;
            g.Insert(e.SpanStart, plain ? "{ " : "{ return ");
            g.Insert(e.Span.End, "; }");
            Commit(g);
        }

        private void AddDeclaration(EditGroup g, StatementSyntax stmt, string declaration)
        {
            int pos = stmt.SpanStart;
            TextLine line = _c.Text.Lines.GetLineFromPosition(pos);
            string before = TextOf(line.Start, pos);
            if (string.IsNullOrWhiteSpace(before)) g.Insert(pos, declaration + NL + before);
            else g.Insert(pos, declaration + " ");
        }

        private static bool IsMemberNamePosition(IdentifierNameSyntax id)
        {
            SyntaxNode p = id.Parent;
            MemberAccessExpressionSyntax ma = p as MemberAccessExpressionSyntax;
            if (ma != null && ma.Name == id) return true;
            if (p is MemberBindingExpressionSyntax) return true;
            QualifiedNameSyntax qn = p as QualifiedNameSyntax;
            if (qn != null && qn.Right == id) return true;
            if (p is NameColonSyntax || p is NameEqualsSyntax || p is AliasQualifiedNameSyntax) return true;
            AssignmentExpressionSyntax asg = p as AssignmentExpressionSyntax;
            if (asg != null && asg.Left == id && asg.Parent is InitializerExpressionSyntax &&
                asg.Parent.IsKind(SyntaxKind.ObjectInitializerExpression)) return true;
            return false;
        }

        private static bool IsLoop(SyntaxNode n)
        {
            return n is WhileStatementSyntax || n is DoStatementSyntax || n is ForStatementSyntax || n is CommonForEachStatementSyntax;
        }

        private bool CapturedInLoop(ILocalSymbol sym, SyntaxNode at, StatementSyntax stmt)
        {
            bool loop = false;
            for (SyntaxNode cur = at; cur != null; cur = cur.Parent) {
                if (IsLoop(cur)) loop = true;
                if (cur == stmt) break;
            }
            if (!loop) return false;
            foreach (SyntaxNode n in stmt.DescendantNodes()) {
                if (!(n is AnonymousFunctionExpressionSyntax || n is LocalFunctionStatementSyntax || n is QueryExpressionSyntax)) continue;
                foreach (SyntaxNode m in n.DescendantNodes()) {
                    IdentifierNameSyntax id = m as IdentifierNameSyntax;
                    if (id != null && id.Identifier.ValueText == sym.Name &&
                        SymbolEqualityComparer.Default.Equals(Model.GetSymbolInfo(id).Symbol, sym)) return true;
                }
            }
            return false;
        }

        private enum NameState { Free, Reuse, Conflict }

        private NameState CheckName(SyntaxNode scope, StatementSyntax stmt, string name, ILocalSymbol sym, ITypeSymbol type)
        {
            bool conflict = false, reuse = false;
            foreach (SyntaxToken tok in scope.DescendantTokens()) {
                if (!tok.IsKind(SyntaxKind.IdentifierToken) || tok.ValueText != name) continue;
                SyntaxNode parent = tok.Parent;
                if (parent is VariableDeclaratorSyntax || parent is SingleVariableDesignationSyntax || parent is ParameterSyntax ||
                    parent is ForEachStatementSyntax || parent is CatchDeclarationSyntax || parent is LocalFunctionStatementSyntax ||
                    parent is FromClauseSyntax || parent is LetClauseSyntax || parent is JoinClauseSyntax ||
                    parent is JoinIntoClauseSyntax || parent is QueryContinuationSyntax) {
                    ISymbol declared = Model.GetDeclaredSymbol(parent);
                    if (declared != null && SymbolEqualityComparer.Default.Equals(declared, sym)) continue;
                    ILocalSymbol l = declared as ILocalSymbol;
                    VariableDeclaratorSyntax vd = parent as VariableDeclaratorSyntax;
                    if (l != null && vd != null && vd.Initializer == null && vd.Parent != null &&
                        vd.Parent.Parent is LocalDeclarationStatementSyntax && vd.Parent.Parent.Parent == stmt.Parent &&
                        vd.Parent.Parent.SpanStart < stmt.SpanStart &&
                        SymbolEqualityComparer.Default.Equals(l.Type, type)) {
                        if (IsCaptured(l, scope) || IsCaptured(sym, scope)) { conflict = true; continue; }
                        reuse = true;
                        continue;
                    }
                    // another inline declaration that will be hoisted into the same scope with the same type:
                    // the two share one declaration (unless a lambda captures one of them)
                    if (l != null && parent is SingleVariableDesignationSyntax && PeekHoistScope(parent) == scope &&
                        SymbolEqualityComparer.Default.Equals(l.Type, type) && !IsCaptured(l, scope) && !IsCaptured(sym, scope) &&
                        (parent.Parent is DeclarationExpressionSyntax || parent.Parent is DeclarationPatternSyntax)) {
                        continue;
                    }
                    conflict = true;
                    continue;
                }
                IdentifierNameSyntax id = parent as IdentifierNameSyntax;
                if (id != null) {
                    if (IsMemberNamePosition(id)) continue;
                    ISymbol s = Model.GetSymbolInfo(id).Symbol;
                    if (s == null) continue;
                    if (SymbolEqualityComparer.Default.Equals(s, sym)) continue;
                    if (s is ILocalSymbol || s is IParameterSymbol || s is IRangeVariableSymbol) continue; // seen at its declaration
                    conflict = true;
                }
            }
            return conflict ? NameState.Conflict : reuse ? NameState.Reuse : NameState.Free;
        }

        /// <summary>
        /// Declares `type name;` before the statement that encloses <paramref name="at"/>.
        /// For a user-named variable (<paramref name="sym"/> != null) the widened scope is checked for
        /// clashes: an identical earlier hoisted declaration is reused, anything else renames the variable.
        /// Returns the final name, or null when nothing could be done in this pass.
        /// </summary>
        private string HoistDeclaration(EditGroup g, SyntaxNode at, string typeText, ITypeSymbol type, string name, ILocalSymbol sym, string what)
        {
            StatementSyntax stmt;
            if (FindHoistStatement(at, what, out stmt) != HoistStatus.Ok) return null;
            SyntaxNode scope = stmt.Parent is SwitchSectionSyntax ? stmt.Parent.Parent : stmt.Parent;
            string final = name;
            bool declare = true;
            if (sym != null) {
                if (CapturedInLoop(sym, at, stmt)) {
                    Manual(at, what, "variable '" + name + "' is declared inside a loop and captured by a lambda; hoisting it would change the capture semantics: rewrite by hand");
                    return null;
                }
                Tuple<SyntaxNode, string> key = Tuple.Create(scope, name);
                ITypeSymbol prev;
                if (_hoisted.TryGetValue(key, out prev)) {
                    if (SymbolEqualityComparer.Default.Equals(prev, type) && !IsCaptured(sym, scope)) declare = false;
                    else final = Fresh(at, name + "_");
                } else {
                    switch (CheckName(scope, stmt, name, sym, type)) {
                        case NameState.Reuse: declare = false; break;
                        case NameState.Conflict: final = Fresh(at, name + "_"); break;
                    }
                }
                if (final != name) {
                    foreach (SyntaxNode n in scope.DescendantNodes()) {
                        IdentifierNameSyntax id = n as IdentifierNameSyntax;
                        if (id != null && id.Identifier.ValueText == name && !IsMemberNamePosition(id) &&
                            SymbolEqualityComparer.Default.Equals(Model.GetSymbolInfo(id).Symbol, sym)) {
                            g.Replace(id.Identifier.Span, final);
                        }
                    }
                    _c.Stats.Add("hoisted variable renamed to avoid a clash in the widened scope", 1);
                } else if (declare) {
                    g.AfterCommit(delegate { _hoisted[key] = type; });
                } else {
                    _c.Stats.Add("hoisted declaration shared with an identical earlier one", 1);
                }
            }
            if (declare) AddDeclaration(g, stmt, typeText + " " + final + ";");
            return final;
        }

        private static bool IsEmbedded(StatementSyntax s)
        {
            return !(s.Parent is BlockSyntax || s.Parent is SwitchSectionSyntax || s.Parent is GlobalStatementSyntax);
        }

        /// <summary>True when replacing the statement by an `if` without `else` would capture an `else` that follows.</summary>
        private static bool DanglingElseRisk(StatementSyntax s)
        {
            for (SyntaxNode cur = s; cur != null; cur = cur.Parent) {
                SyntaxNode p = cur.Parent;
                if (p == null || p is BlockSyntax || p is SwitchSectionSyntax) return false;
                IfStatementSyntax ifs = p as IfStatementSyntax;
                if (ifs != null && ifs.Statement == cur && ifs.Else != null) return true;
                if (!(p is StatementSyntax || p is ElseClauseSyntax)) return false;
            }
            return false;
        }

        private void CheckComments(SyntaxNode replaced, string newText, string what)
        {
            foreach (SyntaxTrivia t in replaced.DescendantTrivia()) {
                if (!(t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia))) continue;
                if (!replaced.Span.Contains(t.Span)) continue;
                if (newText.IndexOf(t.ToString(), StringComparison.Ordinal) < 0) {
                    Manual(replaced, what, "a comment inside the rewritten code was dropped: " + t.ToString().Trim());
                }
            }
        }

        // ------------------------------------------------------------------ line re-indentation

        /// <summary>Adds an insertion of <paramref name="indent"/> at the start of every line that begins in [start, end).</summary>
        internal static void IndentLines(EditGroup g, SyntaxNode root, SourceText text, int start, int end, string indent)
        {
            int first = text.Lines.GetLineFromPosition(start).LineNumber;
            for (int i = first; i < text.Lines.Count; i++) {
                TextLine line = text.Lines[i];
                if (line.Start < start) continue;
                if (line.Start >= end) break;
                if (IsIndentable(root, text, line)) g.Insert(line.Start, indent);
            }
        }

        internal static bool IsIndentable(SyntaxNode root, SourceText text, TextLine line)
        {
            string s = line.ToString();
            int i = 0;
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) i++;
            if (i == s.Length) return false; // blank
            if (s[i] == '#') {
                SyntaxTrivia tr = root.FindTrivia(line.Start + i);
                if (tr.IsDirective) return false;
            }
            SyntaxToken tok = root.FindToken(line.Start);
            if (tok.SpanStart < line.Start && line.Start < tok.Span.End) {
                switch (tok.Kind()) {
                    case SyntaxKind.StringLiteralToken:
                    case SyntaxKind.InterpolatedStringTextToken:
                    case SyntaxKind.MultiLineRawStringLiteralToken:
                    case SyntaxKind.SingleLineRawStringLiteralToken:
                    case SyntaxKind.Utf8StringLiteralToken:
                    case SyntaxKind.Utf8MultiLineRawStringLiteralToken:
                    case SyntaxKind.InterpolatedStringToken:
                        return false;
                }
            }
            return true;
        }

        /// <summary>The lines strictly inside a block, re-indented by <paramref name="extra"/>, ready to be put inside a new block at <paramref name="baseIndent"/>.</summary>
        private string BlockBody(BlockSyntax block, string baseIndent, string extra)
        {
            int open = block.OpenBraceToken.Span.End;
            int close = block.CloseBraceToken.SpanStart;
            TextLine openLine = _c.Text.Lines.GetLineFromPosition(open);
            TextLine closeLine = _c.Text.Lines.GetLineFromPosition(close);
            if (openLine.LineNumber == closeLine.LineNumber || !IsFirstOnLine(close)) {
                string inner = TextOf(open, close).Trim();
                return inner.Length == 0 ? "" : baseIndent + extra + inner + NL;
            }
            // whole lines between the brace lines
            string head = TextOf(open, openLine.End).Trim();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (head.Length > 0) sb.Append(baseIndent).Append(extra).Append(head).Append(NL);
            SyntaxNode root = _c.Tree.GetRoot();
            for (int i = openLine.LineNumber + 1; i < closeLine.LineNumber; i++) {
                TextLine line = _c.Text.Lines[i];
                if (IsIndentable(root, _c.Text, line)) sb.Append(extra);
                sb.Append(_c.Text.ToString(line.SpanIncludingLineBreak));
            }
            return sb.ToString();
        }
    }
}
