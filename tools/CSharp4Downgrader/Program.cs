// CSharp4Downgrader - rewrites a C# source tree in place so that it only uses C# 4.0 syntax.
//
//   CSharp4Downgrader <src-root> [<src-root>...] [--check] [--no-verify] [--ref <tfm>=<dir>] [--tfm <tfm>[;<tfm>...]]
//
// Several roots are analysed together (e.g. `src tests/RoundTrip`: the test project references the library).
// See README.md for the list of transformations.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSharp4Downgrader
{
    internal sealed class Stats
    {
        public readonly SortedDictionary<string, int> Counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public void Add(string key, int n)
        {
            int v;
            Counts.TryGetValue(key, out v);
            Counts[key] = v + n;
        }
    }

    internal sealed class Reporter
    {
        private readonly SortedSet<string> _final = new SortedSet<string>(StringComparer.Ordinal);
        private List<string> _pass = new List<string>();

        public void BeginPass() { _pass = new List<string>(); }
        public void Manual(string file, int line, string construct, string message)
        {
            _pass.Add(file + "(" + line.ToString("D5") + "): " + construct + ": " + message);
        }
        /// <summary>Only what is still reported by a pass that changed nothing is real.</summary>
        public void KeepPass() { foreach (string s in _pass) _final.Add(s); }
        public void Add(string s) { _final.Add(s); }
        public IEnumerable<string> Items
        {
            get { return _final.Select(s => Regex.Replace(s, @"\((0*)(\d+)\): ", m => "(" + m.Groups[2].Value + "): ")); }
        }
        public int Count { get { return _final.Count; } }
    }

    internal sealed class SourceFile
    {
        public string Path;
        public string DisplayPath;
        public string Text;
        public string Original;
        public bool Bom;
        public bool HasDirectives;
    }

    internal sealed class Project
    {
        public string Name;
        public string Dir;
        public List<SourceFile> Files = new List<SourceFile>();
        public List<string> Tfms = new List<string>();
        public List<string> Defines = new List<string>();
        public List<string> ReferencePaths = new List<string>();
        public List<Project> References = new List<Project>();
        public List<HashSet<string>> ProcessedSymbolSets = new List<HashSet<string>>();
    }

    /// <summary>A `&lt;Compile Include=...&gt;` item of a Directory.Build.props with simple conditions (files linked into several projects).</summary>
    internal sealed class LinkRule
    {
        public string File;
        public List<string[]> Conditions = new List<string[]>(); // { property, "==" or "!=", value }

        public bool Applies(Project p, string tfm)
        {
            foreach (string[] c in Conditions) {
                string actual;
                if (c[0] == "TargetFramework") actual = tfm;
                else if (c[0] == "MSBuildProjectName") actual = p.Name;
                else return false;
                bool equal = string.Equals(actual, c[2], StringComparison.OrdinalIgnoreCase);
                if (equal != (c[1] == "==")) return false;
            }
            return true;
        }
    }

    internal sealed class Profile
    {
        public string Tfm;
        public string Toggle;   // null, "+SYMBOL" or "-SYMBOL"
        public string Name { get { return Toggle == null ? Tfm : Tfm + " " + Toggle; } }
    }

    internal static class Program
    {
        private static readonly Dictionary<string, List<MetadataReference>> RefCache = new Dictionary<string, List<MetadataReference>>();
        private static readonly Dictionary<string, string> RefOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, KeyValuePair<string, SyntaxTree>> ParseCache = new Dictionary<string, KeyValuePair<string, SyntaxTree>>();
        private static readonly Stats Stats = new Stats();
        private static readonly Reporter Reporter = new Reporter();
        private static readonly List<string> Notes = new List<string>();
        private static readonly List<string> ToolErrors = new List<string>();
        private static readonly List<LinkRule> LinkRules = new List<LinkRule>();
        private static readonly Dictionary<string, SourceFile> FilesByPath = new Dictionary<string, SourceFile>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Broken = new HashSet<string>();

        private static int Main(string[] args)
        {
            List<string> roots = new List<string>();
            bool check = false, verify = true;
            List<string> forcedTfms = null;
            for (int i = 0; i < args.Length; i++) {
                string a = args[i];
                if (a == "--check") check = true;
                else if (a == "--no-verify") verify = false;
                else if (a == "--ref" && i + 1 < args.Length) {
                    string[] kv = args[++i].Split(new[] { '=' }, 2);
                    if (kv.Length != 2) return Usage();
                    RefOverrides[kv[0]] = kv[1];
                } else if (a == "--tfm" && i + 1 < args.Length) {
                    forcedTfms = args[++i].Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                } else if (a.StartsWith("--", StringComparison.Ordinal)) return Usage();
                else roots.Add(a);
            }
            if (roots.Count == 0) return Usage();
            for (int i = 0; i < roots.Count; i++) {
                roots[i] = Path.GetFullPath(roots[i]).TrimEnd('\\', '/');
                if (!Directory.Exists(roots[i])) { Console.Error.WriteLine("error: no such directory: " + roots[i]); return 2; }
            }
            // paths are displayed relative to the common parent of the roots
            string root = roots[0];
            while (roots.Any(r => !(r + Path.DirectorySeparatorChar).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) {
                root = Path.GetDirectoryName(root);
                if (root == null) { Console.Error.WriteLine("error: the roots have no common parent directory"); return 2; }
                root = root.TrimEnd('\\', '/');
            }

            List<Project> projects = DiscoverProjects(root, roots, forcedTfms);
            foreach (Project p in projects) foreach (SourceFile f in p.Files) FilesByPath[f.Path] = f;
            ReadDirectoryBuildProps(roots[0]);
            int fileCount = projects.Sum(p => p.Files.Count);
            Console.WriteLine("CSharp4Downgrader: " + fileCount + " files in " + projects.Count + " project(s) under " + string.Join(", ", roots));

            // every target framework, then every other #if symbol toggled against every target framework
            List<string> tfms = new List<string>();
            foreach (Project p in projects) foreach (string t in p.Tfms) if (!tfms.Contains(t)) tfms.Add(t);
            SortedSet<string> extra = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Project p in projects) foreach (SourceFile f in p.Files) CollectSymbols(f, extra);
            List<Profile> profiles = new List<Profile>();
            foreach (string t in tfms) profiles.Add(new Profile { Tfm = t });
            foreach (string s in extra) {
                foreach (string t in tfms) {
                    profiles.Add(new Profile { Tfm = t, Toggle = "+" + s });
                    profiles.Add(new Profile { Tfm = t, Toggle = "-" + s });
                }
            }

            foreach (Profile prof in profiles) {
                List<Project> todo = new List<Project>();
                foreach (Project p in projects) {
                    if (!p.Tfms.Contains(prof.Tfm)) continue;
                    HashSet<string> symbols = SymbolsFor(p, prof);
                    if (symbols == null) continue;                      // the toggle changes nothing for this project
                    if (IsCovered(p, symbols)) continue;
                    todo.Add(p);
                }
                if (todo.Count == 0) continue;
                Console.WriteLine("== profile " + prof.Name + ": " + todo.Count + " project(s)");
                RunPhase(projects, todo, prof, true);
                RunPhase(projects, todo, prof, false);
                foreach (Project p in todo) p.ProcessedSymbolSets.Add(SymbolsFor(p, prof));
            }

            ReportUncoveredRegions(projects, profiles);
            foreach (string e in ToolErrors) Reporter.Add(e);

            int changed = 0;
            foreach (Project p in projects) {
                foreach (SourceFile f in p.Files) {
                    if (f.Text == f.Original) continue;
                    changed++;
                    if (!check) {
                        File.WriteAllText(f.Path, f.Text, new UTF8Encoding(f.Bom));
                    } else if (changed <= 20) {
                        Console.WriteLine("would change: " + f.DisplayPath);
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("== transformations applied");
            if (Stats.Counts.Count == 0) Console.WriteLine("  (none)");
            foreach (KeyValuePair<string, int> kv in Stats.Counts) Console.WriteLine("  " + kv.Value.ToString().PadLeft(6) + "  " + kv.Key);
            Console.WriteLine();
            Console.WriteLine("== needs a manual edit: " + Reporter.Count);
            foreach (string s in Reporter.Items) Console.WriteLine("  " + s);
            foreach (string s in Notes.Distinct()) Console.WriteLine("  note: " + s);

            int verifyErrors = 0;
            if (verify) verifyErrors = Verify(projects, tfms);

            Console.WriteLine();
            Console.WriteLine("== result: " + changed + " of " + fileCount + " files " + (check ? "would change" : "changed") +
                              "; " + Reporter.Count + " manual item(s)" + (verify ? "; " + verifyErrors + " C# 4 language-version error(s) left" : ""));
            if (check && changed > 0) return 1;
            return 0;
        }

        private static int Usage()
        {
            Console.Error.WriteLine("usage: CSharp4Downgrader <src-root> [<src-root>...] [--check] [--no-verify] [--tfm <tfm>[;<tfm>...]] [--ref <tfm>=<reference-assembly-dir>]");
            Console.Error.WriteLine("  Rewrites every .cs file under <src-root> (bin/ and obj/ are skipped) to C# 4.0 syntax, in place.");
            Console.Error.WriteLine("  --check      change nothing; exit code 1 if a file would change");
            Console.Error.WriteLine("  --no-verify  skip the final in-process compilation with LangVersion 4");
            Console.Error.WriteLine("  --tfm        target frameworks to analyse when they cannot be read from *.csproj files");
            return 2;
        }

        // ------------------------------------------------------------------ discovery

        private static bool IsBuildDir(string path, string root)
        {
            string rel = path.Substring(root.Length).Replace('\\', '/');
            return rel.Contains("/bin/") || rel.Contains("/obj/") || rel.Contains("/.git/") || rel.Contains("/.vs/");
        }

        private static List<Project> DiscoverProjects(string root, List<string> roots, List<string> forcedTfms)
        {
            List<Project> projects = new List<Project>();
            List<string> csprojs = roots.SelectMany(r => Directory.GetFiles(r, "*.csproj", SearchOption.AllDirectories)).Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(p => !IsBuildDir(p, root)).OrderBy(p => p, StringComparer.Ordinal).ToList();
            Dictionary<string, Project> byPath = new Dictionary<string, Project>(StringComparer.OrdinalIgnoreCase);
            foreach (string cp in csprojs) {
                string dir = Path.GetDirectoryName(cp);
                if (projects.Any(x => string.Equals(x.Dir, dir, StringComparison.OrdinalIgnoreCase))) continue; // one project per directory
                Project p = new Project { Name = Path.GetFileNameWithoutExtension(cp), Dir = dir };
                string xml = File.ReadAllText(cp);
                Match m = Regex.Match(xml, @"<TargetFrameworks?>\s*([^<]+?)\s*</TargetFrameworks?>");
                if (m.Success) p.Tfms = m.Groups[1].Value.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0 && !s.Contains("$")).ToList();
                foreach (Match d in Regex.Matches(xml, @"<DefineConstants>([^<]*)</DefineConstants>")) {
                    foreach (string s in d.Groups[1].Value.Split(';', ',')) {
                        string t = s.Trim();
                        if (t.Length > 0 && !t.Contains("$") && !p.Defines.Contains(t)) p.Defines.Add(t);
                    }
                }
                foreach (Match r in Regex.Matches(xml, @"<ProjectReference\s+Include=""([^""]+)""")) {
                    p.ReferencePaths.Add(Path.GetFullPath(Path.Combine(dir, r.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar))));
                }
                projects.Add(p);
                byPath[Path.GetFullPath(cp)] = p;
            }
            if (projects.Count == 0) projects.Add(new Project { Name = Path.GetFileName(root), Dir = root });
            foreach (Project p in projects) {
                foreach (string r in p.ReferencePaths) {
                    Project q;
                    if (byPath.TryGetValue(r, out q)) p.References.Add(q);
                }
                if (forcedTfms != null) p.Tfms = new List<string>(forcedTfms);
                if (p.Tfms.Count == 0) p.Tfms = new List<string> { "net8.0", "netstandard2.0", "netstandard1.3", "net20" };
            }
            // files: each belongs to the project with the deepest containing directory
            List<string> files = roots.SelectMany(r => Directory.GetFiles(r, "*.cs", SearchOption.AllDirectories)).Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(f => !IsBuildDir(f, root)).OrderBy(f => f, StringComparer.Ordinal).ToList();
            foreach (string f in files) {
                Project owner = null;
                foreach (Project p in projects) {
                    string prefix = p.Dir.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
                    if (f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && (owner == null || p.Dir.Length > owner.Dir.Length)) owner = p;
                }
                if (owner == null) {
                    owner = projects.FirstOrDefault(p => p.Name == "(loose files)");
                    if (owner == null) {
                        owner = new Project { Name = "(loose files)", Dir = root, Tfms = forcedTfms ?? new List<string> { "net8.0", "netstandard2.0", "netstandard1.3", "net20" } };
                        projects.Add(owner);
                    }
                }
                byte[] bytes = File.ReadAllBytes(f);
                bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                string text = new UTF8Encoding(false).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
                owner.Files.Add(new SourceFile {
                    Path = f, DisplayPath = f.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/'),
                    Text = text, Original = text, Bom = bom, HasDirectives = Regex.IsMatch(text, @"(?m)^[ \t]*#[ \t]*(if|define|undef)\b")
                });
            }
            // referenced projects first
            List<Project> ordered = new List<Project>();
            Action<Project> visit = null;
            visit = delegate(Project p) {
                if (ordered.Contains(p)) return;
                foreach (Project r in p.References) visit(r);
                ordered.Add(p);
            };
            foreach (Project p in projects) visit(p);
            return ordered;
        }

        /// <summary>
        /// Files that the nearest Directory.Build.props links into other projects (in Bento4Sharp: Compat.cs into the
        /// tools for the old frameworks). They are only added to the analysis of those projects; they are rewritten once,
        /// in the project that owns them.
        /// </summary>
        private static void ReadDirectoryBuildProps(string root)
        {
            string dir = root;
            for (int up = 0; up < 4 && dir != null; up++, dir = Path.GetDirectoryName(dir)) {
                string props = Path.Combine(dir, "Directory.Build.props");
                if (!File.Exists(props)) continue;
                string xml = File.ReadAllText(props);
                foreach (Match ig in Regex.Matches(xml, @"<ItemGroup(?:\s+Condition=""([^""]*)"")?\s*>(.*?)</ItemGroup>", RegexOptions.Singleline)) {
                    foreach (Match c in Regex.Matches(ig.Groups[2].Value, @"<Compile\s+Include=""([^""]+)""")) {
                        string include = c.Groups[1].Value.Replace("$(MSBuildThisFileDirectory)", dir + Path.DirectorySeparatorChar);
                        if (include.Contains("$(") || include.Contains("*")) continue;
                        LinkRule rule = new LinkRule { File = Path.GetFullPath(include.Replace('/', Path.DirectorySeparatorChar)) };
                        bool ok = true;
                        string cond = ig.Groups[1].Value.Trim();
                        if (cond.Length > 0) {
                            foreach (string term in Regex.Split(cond, @"\s+and\s+", RegexOptions.IgnoreCase)) {
                                Match t = Regex.Match(term.Trim(), @"^'\$\((\w+)\)'\s*(==|!=)\s*'([^']*)'$");
                                if (!t.Success) { ok = false; break; }
                                rule.Conditions.Add(new[] { t.Groups[1].Value, t.Groups[2].Value, t.Groups[3].Value });
                            }
                        }
                        if (ok && File.Exists(rule.File)) LinkRules.Add(rule);
                    }
                }
                if (LinkRules.Count > 0) Console.WriteLine("   " + props + ": " + LinkRules.Count + " linked source file rule(s) used for analysis");
                return;
            }
        }

        private static SourceFile LinkedFile(string path)
        {
            SourceFile f;
            if (FilesByPath.TryGetValue(path, out f)) return f;
            // outside the root: read-only, for binding
            string text = File.ReadAllText(path);
            f = new SourceFile { Path = path, DisplayPath = path, Text = text, Original = text, HasDirectives = true };
            FilesByPath[path] = f;
            return f;
        }

        // ------------------------------------------------------------------ preprocessor symbols

        private static bool IsTfmSymbol(string s)
        {
            return Regex.IsMatch(s, @"^(NET|NETCOREAPP|NETSTANDARD|NETFRAMEWORK)(\d[\d_]*)?(_OR_GREATER)?$") || s == "true" || s == "false";
        }

        private static void CollectSymbols(SourceFile f, SortedSet<string> extra)
        {
            if (!f.HasDirectives) return;
            foreach (Match m in Regex.Matches(f.Text, @"(?m)^[ \t]*#[ \t]*(?:if|elif)\b([^\r\n]*)")) {
                string cond = m.Groups[1].Value;
                int c = cond.IndexOf("//", StringComparison.Ordinal);
                if (c >= 0) cond = cond.Substring(0, c);
                foreach (Match id in Regex.Matches(cond, @"[A-Za-z_][A-Za-z0-9_]*")) {
                    if (!IsTfmSymbol(id.Value)) extra.Add(id.Value);
                }
            }
        }

        internal static List<string> TfmSymbols(string tfm)
        {
            List<string> s = new List<string>();
            tfm = tfm.ToLowerInvariant();
            int dash = tfm.IndexOf('-');
            if (dash >= 0) tfm = tfm.Substring(0, dash);
            Match m;
            if ((m = Regex.Match(tfm, @"^netstandard(\d+)\.(\d+)$")).Success) {
                int major = int.Parse(m.Groups[1].Value), minor = int.Parse(m.Groups[2].Value);
                s.Add("NETSTANDARD");
                s.Add("NETSTANDARD" + major + "_" + minor);
                int[][] all = { new[] {1,0}, new[] {1,1}, new[] {1,2}, new[] {1,3}, new[] {1,4}, new[] {1,5}, new[] {1,6}, new[] {2,0}, new[] {2,1} };
                foreach (int[] v in all) if (v[0] < major || (v[0] == major && v[1] <= minor)) s.Add("NETSTANDARD" + v[0] + "_" + v[1] + "_OR_GREATER");
            } else if ((m = Regex.Match(tfm, @"^net(coreapp)?(\d+)\.(\d+)$")).Success) {
                int major = int.Parse(m.Groups[2].Value), minor = int.Parse(m.Groups[3].Value);
                s.Add("NETCOREAPP");
                if (major >= 5) { s.Add("NET"); s.Add("NET" + major + "_" + minor); } else s.Add("NETCOREAPP" + major + "_" + minor);
                int[][] core = { new[] {1,0}, new[] {1,1}, new[] {2,0}, new[] {2,1}, new[] {2,2}, new[] {3,0}, new[] {3,1} };
                foreach (int[] v in core) if (v[0] < major || (v[0] == major && v[1] <= minor)) s.Add("NETCOREAPP" + v[0] + "_" + v[1] + "_OR_GREATER");
                for (int v = 5; v <= major; v++) s.Add("NET" + v + "_0_OR_GREATER");
            } else if ((m = Regex.Match(tfm, @"^net(\d{2,3})$")).Success) {
                string[] all = { "20", "30", "35", "40", "45", "451", "452", "46", "461", "462", "47", "471", "472", "48", "481" };
                string v = m.Groups[1].Value;
                s.Add("NETFRAMEWORK");
                s.Add("NET" + v);
                int idx = Array.IndexOf(all, v);
                for (int i = 0; i <= idx; i++) s.Add("NET" + all[i] + "_OR_GREATER");
            }
            return s;
        }

        /// <summary>The symbols of a project under a profile; null when the profile's toggle is a no-op for the project.</summary>
        private static HashSet<string> SymbolsFor(Project p, Profile prof)
        {
            HashSet<string> s = new HashSet<string>(TfmSymbols(prof.Tfm), StringComparer.Ordinal);
            s.Add("TRACE");
            foreach (string d in p.Defines) s.Add(d);
            if (prof.Toggle != null) {
                string name = prof.Toggle.Substring(1);
                if (prof.Toggle[0] == '+') { if (!s.Add(name)) return null; }
                else { if (!s.Remove(name)) return null; }
            }
            return s;
        }

        private static string Key(IEnumerable<string> symbols) { return string.Join(";", symbols.OrderBy(x => x, StringComparer.Ordinal)); }

        private static SyntaxTree Parse(SourceFile f, HashSet<string> symbols, LanguageVersion version)
        {
            string key = f.Path + "|" + version + "|" + Key(symbols);
            KeyValuePair<string, SyntaxTree> hit;
            if (ParseCache.TryGetValue(key, out hit) && ReferenceEquals(hit.Key, f.Text)) return hit.Value;
            SyntaxTree tree = CSharpSyntaxTree.ParseText(SourceText.From(f.Text), new CSharpParseOptions(version, DocumentationMode.None, SourceCodeKind.Regular, symbols), f.Path);
            ParseCache[key] = new KeyValuePair<string, SyntaxTree>(f.Text, tree);
            return tree;
        }

        private static string DisabledSignature(SourceFile f, HashSet<string> symbols)
        {
            SyntaxTree tree = Parse(f, symbols, LanguageVersion.Preview);
            StringBuilder sb = new StringBuilder();
            foreach (SyntaxTrivia t in tree.GetRoot().DescendantTrivia()) {
                if (t.IsKind(SyntaxKind.DisabledTextTrivia)) sb.Append(t.SpanStart).Append('-').Append(t.Span.End).Append(';');
            }
            return sb.ToString();
        }

        /// <summary>True when every file's active text under these symbols was already the active text of a processed symbol set.</summary>
        private static bool IsCovered(Project p, HashSet<string> symbols)
        {
            if (p.ProcessedSymbolSets.Count == 0) return false;
            foreach (SourceFile f in p.Files) {
                if (!f.HasDirectives) continue;
                string sig = DisabledSignature(f, symbols);
                bool found = false;
                foreach (HashSet<string> done in p.ProcessedSymbolSets) {
                    if (DisabledSignature(f, done) == sig) { found = true; break; }
                }
                if (!found) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ references

        private static string NuGetRoot()
        {
            string env = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            if (!string.IsNullOrEmpty(env)) return env;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        }

        private static string HighestVersionDir(string dir, Func<string, bool> accept)
        {
            if (!Directory.Exists(dir)) return null;
            string best = null;
            Version bestV = null;
            foreach (string d in Directory.GetDirectories(dir)) {
                Version v;
                string name = Path.GetFileName(d);
                int dash = name.IndexOf('-');
                if (!Version.TryParse(dash >= 0 ? name.Substring(0, dash) : name, out v)) continue;
                if (!accept(d)) continue;
                if (bestV == null || v > bestV) { bestV = v; best = d; }
            }
            return best;
        }

        private static List<string> ReferenceFiles(string tfm, out string description)
        {
            string over;
            if (RefOverrides.TryGetValue(tfm, out over)) {
                description = over;
                return Directory.GetFiles(over, "*.dll").ToList();
            }
            string t = tfm.ToLowerInvariant();
            Match m;
            if ((m = Regex.Match(t, @"^net(\d{2,3})$")).Success) {
                string v = "v" + string.Join(".", m.Groups[1].Value.ToCharArray());
                string pkg = HighestVersionDir(Path.Combine(NuGetRoot(), "microsoft.netframework.referenceassemblies." + t), d => Directory.Exists(Path.Combine(d, "build", ".NETFramework", v)));
                List<string> dirs = new List<string>();
                if (pkg != null) dirs.Add(Path.Combine(pkg, "build", ".NETFramework", v));
                string pf = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? Environment.GetEnvironmentVariable("ProgramFiles") ?? "";
                dirs.Add(Path.Combine(pf, "Reference Assemblies", "Microsoft", "Framework", ".NETFramework", v));
                foreach (string dir in dirs) {
                    if (!File.Exists(Path.Combine(dir, "mscorlib.dll"))) continue;
                    description = dir;
                    return new[] { "mscorlib.dll", "System.dll", "System.Core.dll", "System.Xml.dll", "System.Xml.Linq.dll", "System.Data.dll" }
                        .Select(n => Path.Combine(dir, n)).Where(File.Exists).ToList();
                }
            } else if (t.StartsWith("netstandard", StringComparison.Ordinal)) {
                string dir = Path.Combine(NuGetRoot(), "netstandard.library", "2.0.3", "build", "netstandard2.0", "ref");
                if (Directory.Exists(dir)) {
                    description = dir + (t == "netstandard2.0" ? "" : " (approximation for " + tfm + ")");
                    return Directory.GetFiles(dir, "*.dll").ToList();
                }
            } else if ((m = Regex.Match(t, @"^net(\d+)\.(\d+)")).Success) {
                string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
                string dotnetRoot = Path.GetFullPath(Path.Combine(runtimeDir, "..", "..", ".."));
                string sub = "net" + m.Groups[1].Value + "." + m.Groups[2].Value;
                string pack = HighestVersionDir(Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref"), d => Directory.Exists(Path.Combine(d, "ref", sub)));
                if (pack != null) {
                    description = Path.Combine(pack, "ref", sub);
                    return Directory.GetFiles(description, "*.dll").ToList();
                }
            }
            description = null;
            return null;
        }

        private static List<MetadataReference> References(string tfm)
        {
            List<MetadataReference> refs;
            if (RefCache.TryGetValue(tfm, out refs)) return refs;
            string description;
            List<string> files = ReferenceFiles(tfm, out description);
            if (files == null) {
                // fall back to the assemblies this tool itself runs on
                string tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "";
                files = tpa.Split(Path.PathSeparator).Where(f => f.Length > 0).ToList();
                description = "the runtime assemblies of this tool (no reference assemblies found for " + tfm + ": types that only exist there will not bind)";
            }
            Console.WriteLine("   references for " + tfm + ": " + description);
            refs = files.Select(f => (MetadataReference)MetadataReference.CreateFromFile(f)).ToList();
            RefCache[tfm] = refs;
            return refs;
        }

        // ------------------------------------------------------------------ compilations and passes

        private static Dictionary<Project, CSharpCompilation> Compile(List<Project> all, Profile prof, LanguageVersion version, NullableContextOptions nullable)
        {
            Dictionary<Project, CSharpCompilation> result = new Dictionary<Project, CSharpCompilation>();
            foreach (Project p in all) {
                if (!p.Tfms.Contains(prof.Tfm)) continue;
                HashSet<string> symbols = SymbolsFor(p, prof) ?? SymbolsFor(p, new Profile { Tfm = prof.Tfm });
                List<SyntaxTree> trees = p.Files.Select(f => Parse(f, symbols, version)).ToList();
                foreach (LinkRule rule in LinkRules) {
                    if (!rule.Applies(p, prof.Tfm)) continue;
                    if (p.Files.Any(f => string.Equals(f.Path, rule.File, StringComparison.OrdinalIgnoreCase))) continue;
                    trees.Add(Parse(LinkedFile(rule.File), symbols, version));
                }
                List<MetadataReference> refs = new List<MetadataReference>(References(prof.Tfm));
                foreach (Project r in p.References) {
                    CSharpCompilation rc;
                    if (result.TryGetValue(r, out rc)) refs.Add(rc.ToMetadataReference());
                }
                CSharpCompilationOptions options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    allowUnsafe: true, nullableContextOptions: nullable, concurrentBuild: true);
                result[p] = CSharpCompilation.Create(p.Name, trees, refs, options);
            }
            return result;
        }

        private static string NewLineOf(string text)
        {
            int i = text.IndexOf('\n');
            return i > 0 && text[i - 1] == '\r' ? "\r\n" : "\n";
        }

        private static void RunPhase(List<Project> all, List<Project> todo, Profile prof, bool namespacePhase)
        {
            for (int pass = 1; pass <= 60; pass++) {
                Dictionary<Project, CSharpCompilation> comps = Compile(all, prof, LanguageVersion.Preview, NullableContextOptions.Enable);
                Reporter.BeginPass();
                int edits = 0, files = 0;
                foreach (Project p in todo) {
                    CSharpCompilation comp = comps[p];
                    HashSet<string> symbols = SymbolsFor(p, prof);
                    bool tuplePending = false;
                    if (!namespacePhase) {
                        foreach (SourceFile f in p.Files) {
                            SyntaxTree tree = Parse(f, symbols, LanguageVersion.Preview);
                            if (f.Text.IndexOf('(') < 0) continue;
                            if (!tree.GetRoot().DescendantNodes().Any(n => n is TupleExpressionSyntax || n is TupleTypeSyntax)) continue;
                            // tuple syntax in this project: element names must go first, everywhere in the project
                            foreach (SourceFile f2 in p.Files) {
                                SyntaxTree t2 = Parse(f2, symbols, LanguageVersion.Preview);
                                if (Rewriter.HasNamedTupleElementAccess(t2.GetRoot(), comp.GetSemanticModel(t2))) { tuplePending = true; break; }
                            }
                            break;
                        }
                    }
                    foreach (SourceFile f in p.Files) {
                        SyntaxTree tree = Parse(f, symbols, LanguageVersion.Preview);
                        if (Broken.Contains(f.Path + "|" + prof.Name + "|" + namespacePhase)) continue;
                        FileContext c = new FileContext {
                            Path = f.Path, DisplayPath = f.DisplayPath, Tree = tree, Text = tree.GetText(), NewLine = NewLineOf(f.Text),
                            Compilation = comp, Edits = new FileEdits(), Stats = Stats, Reporter = Reporter, TupleNamesPending = tuplePending
                        };
                        try {
                            if (namespacePhase) NamespaceConverter.Run(c);
                            else new Rewriter(c).Visit(tree.GetRoot());
                            if (c.Edits.IsEmpty) continue;
                            string rewritten = c.Edits.Apply(f.Text);
                            edits += c.Edits.Count;
                            files++;
                            f.Text = rewritten;
                        } catch (Exception ex) {
                            // never lose a file because of a bug in the tool: leave it as it is and say so
                            string msg = "(tool) " + f.DisplayPath + ": internal error under profile " + prof.Name + " (" + ex.GetType().Name + ": " + ex.Message + "); the file was left unfinished, check the verification output";
                            if (!ToolErrors.Contains(msg)) ToolErrors.Add(msg);
                            if (ToolErrors.Count > 200) throw;
                            Broken.Add(f.Path + "|" + prof.Name + "|" + namespacePhase);
                        }
                    }
                }
                if (edits == 0) {
                    if (!namespacePhase) Reporter.KeepPass();
                    return;
                }
                Console.WriteLine("   " + (namespacePhase ? "namespaces" : "pass " + pass) + ": " + edits + " edit(s) in " + files + " file(s)");
            }
            Reporter.Add("(tool) the rewrite did not reach a fixed point for profile " + prof.Name + " after 60 passes");
        }

        // ------------------------------------------------------------------ inactive regions nobody saw

        private static void ReportUncoveredRegions(List<Project> projects, List<Profile> profiles)
        {
            foreach (Project p in projects) {
                foreach (SourceFile f in p.Files) {
                    if (!f.HasDirectives) continue;
                    bool[] inactiveEverywhere = null;
                    foreach (HashSet<string> symbols in p.ProcessedSymbolSets) {
                        bool[] inactive = new bool[f.Text.Length];
                        foreach (SyntaxTrivia t in Parse(f, symbols, LanguageVersion.Preview).GetRoot().DescendantTrivia()) {
                            if (!t.IsKind(SyntaxKind.DisabledTextTrivia)) continue;
                            for (int i = t.SpanStart; i < t.Span.End && i < inactive.Length; i++) inactive[i] = true;
                        }
                        if (inactiveEverywhere == null) inactiveEverywhere = inactive;
                        else for (int i = 0; i < inactive.Length; i++) inactiveEverywhere[i] &= inactive[i];
                    }
                    if (inactiveEverywhere == null) continue;
                    SourceText st = SourceText.From(f.Text);
                    int pos = 0;
                    while (pos < inactiveEverywhere.Length) {
                        if (!inactiveEverywhere[pos]) { pos++; continue; }
                        int start = pos;
                        while (pos < inactiveEverywhere.Length && inactiveEverywhere[pos]) pos++;
                        if (f.Text.Substring(start, pos - start).Trim().Length == 0) continue;
                        int l1 = st.Lines.GetLineFromPosition(start).LineNumber + 1, l2 = st.Lines.GetLineFromPosition(pos - 1).LineNumber + 1;
                        Reporter.Manual(f.DisplayPath, l1, "inactive #if region", "lines " + l1 + "-" + l2 + " are not active under any analysed symbol set and were not rewritten: check them by hand");
                    }
                }
            }
            Reporter.KeepPass();
        }

        // ------------------------------------------------------------------ verification

        private static bool IsLanguageVersionError(Diagnostic d)
        {
            string msg = d.GetMessage(System.Globalization.CultureInfo.InvariantCulture);
            return msg.Contains("language version") || msg.Contains("is not available in C#") || msg.Contains("Feature '") ||
                   d.Id == "CS8022" || d.Id == "CS8023" || d.Id == "CS8024" || d.Id == "CS8025" || d.Id == "CS8026" || d.Id == "CS8059" || d.Id == "CS8107";
        }

        private static int Verify(List<Project> projects, List<string> tfms)
        {
            Console.WriteLine();
            Console.WriteLine("== verification: in-process compilation with LangVersion 4, nullable disabled");
            int total = 0;
            foreach (string tfm in tfms) {
                Profile prof = new Profile { Tfm = tfm };
                Dictionary<Project, CSharpCompilation> comps = Compile(projects, prof, LanguageVersion.CSharp4, NullableContextOptions.Disable);
                int lang = 0, other = 0, shown = 0;
                SortedDictionary<string, int> ids = new SortedDictionary<string, int>();
                List<string> lines = new List<string>();
                foreach (KeyValuePair<Project, CSharpCompilation> kv in comps) {
                    foreach (Diagnostic d in kv.Value.GetDiagnostics()) {
                        if (d.Severity != DiagnosticSeverity.Error) continue;
                        bool isLang = IsLanguageVersionError(d);
                        if (isLang) lang++; else other++;
                        int n;
                        ids.TryGetValue(d.Id, out n);
                        ids[d.Id] = n + 1;
                        if ((isLang && shown < 40) || (!isLang && lines.Count < 15)) {
                            FileLinePositionSpan s = d.Location.GetLineSpan();
                            string path = s.Path ?? "";
                            lines.Add("     " + (isLang ? "[C#4] " : "[other] ") + kv.Key.Name + ": " + Path.GetFileName(path) + "(" + (s.StartLinePosition.Line + 1) + "," + (s.StartLinePosition.Character + 1) + "): " + d.Id + ": " + d.GetMessage(System.Globalization.CultureInfo.InvariantCulture));
                            if (isLang) shown++;
                        }
                    }
                }
                total += lang;
                Console.WriteLine("   " + tfm + ": " + lang + " language-version error(s), " + other + " other error(s)" +
                                  (ids.Count > 0 ? " [" + string.Join(", ", ids.Select(k => k.Key + " x" + k.Value)) + "]" : ""));
                foreach (string l in lines) Console.WriteLine(l);
            }
            return total;
        }
    }

    /// <summary>`namespace X;` -> `namespace X { ... }` with the body indented by four blanks.</summary>
    internal static class NamespaceConverter
    {
        public static void Run(FileContext c)
        {
            CompilationUnitSyntax root = c.Tree.GetCompilationUnitRoot();
            FileScopedNamespaceDeclarationSyntax ns = root.Members.OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
            if (ns == null) return;
            SourceText text = c.Text;
            string nl = c.NewLine;

            // the closing brace goes to the end of the file, or in front of the #endif/#else of an #if opened before the namespace
            int closePos = text.Length;
            bool atEof = true;
            foreach (SyntaxTrivia tr in root.EndOfFileToken.LeadingTrivia) {
                if (!tr.IsDirective) continue;
                DirectiveTriviaSyntax d = tr.GetStructure() as DirectiveTriviaSyntax;
                if (!(d is EndIfDirectiveTriviaSyntax || d is ElseDirectiveTriviaSyntax || d is ElifDirectiveTriviaSyntax)) continue;
                DirectiveTriviaSyntax first = d.GetRelatedDirectives().FirstOrDefault();
                if (first != null && first.SpanStart < ns.SpanStart) {
                    closePos = text.Lines.GetLineFromPosition(d.SpanStart).Start;
                    atEof = false;
                    break;
                }
            }

            EditGroup g = new EditGroup("file-scoped namespace -> block namespace (body indented)");
            TextLine nsLine = text.Lines.GetLineFromPosition(ns.NamespaceKeyword.SpanStart);
            string lineText = nsLine.ToString();
            string ind = lineText.Substring(0, lineText.Length - lineText.TrimStart().Length);
            g.Replace(ns.SemicolonToken.Span, nl + ind + "{");
            TextLine semiLine = text.Lines.GetLineFromPosition(ns.SemicolonToken.Span.End);
            if (semiLine.LineNumber + 1 < text.Lines.Count) {
                Rewriter.IndentLines(g, root, text, text.Lines[semiLine.LineNumber + 1].Start, closePos, "    ");
            }
            if (atEof) {
                string all = text.ToString();
                bool endsWithNewLine = all.EndsWith("\n", StringComparison.Ordinal);
                g.Insert(text.Length, (endsWithNewLine ? "" : nl) + ind + "}" + nl);
            } else {
                g.Insert(closePos, ind + "}" + nl);
            }
            if (c.Edits.TryAdd(g, false)) c.Stats.Add(g.Stat, 1);
        }
    }
}
