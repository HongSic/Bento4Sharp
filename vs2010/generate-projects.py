#!/usr/bin/env python3
# Generates the Visual Studio 2010 solution and projects of Bento4Sharp:
#
#   vs2010/Bento4Sharp.VS2010.sln
#   vs2010/Bento4Sharp/Bento4Sharp.csproj      the library         (src/Bento4Sharp/**/*.cs)
#   vs2010/<Tool>/<Tool>.csproj                the 26 tools        (src/Tools/<Tool>/**/*.cs + Core/Compat.cs)
#   vs2010/RoundTrip/RoundTrip.csproj          the test helper     (tests/RoundTrip/**/*.cs + Core/Compat.cs)
#
# usage: python vs2010/generate-projects.py          (run it again whenever a .cs file or a tool is added or removed)
#
# The projects are old-style MSBuild 4.0 projects for .NET Framework 2.0. They contain no source of their own:
# every file is the shared source of the SDK build, linked by relative path. The file lists are written out
# explicitly (the form Visual Studio 2010 itself writes for "Add As Link"), which is why this script exists.
# Assembly names, preprocessor symbols and suppressed warnings are read from the SDK projects, so that a
# VS2010 build compiles the same thing as `dotnet build -f net20`.
#
# What the script decides:
#   * No <LangVersion>: Visual Studio 2010 compiles with its own C# 4.0 compiler and rejects the element's
#     newer values (CS1617). The source tree is kept at C# 4.0 by the SDK build (Directory.Build.props).
#   * Output goes to vs2010/bin/<Configuration>/ (all tools and Bento4Sharp.dll in one directory), intermediate
#     files to vs2010/obj/<Project>/. Nothing is written outside vs2010/.
#   * RELEASE_TOOLS_PLATFORM_TARGET below (AnyCPU or x86 for the executables).
#   * FRAMEWORK_PATH_OVERRIDE below (where mscorlib.dll comes from on a machine that has reference assemblies
#     for .NET Framework 2.0).
import hashlib
import io
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)

# PlatformTarget of the executables in the Release configuration (the library and Debug builds are AnyCPU).
#
# "AnyCPU" is the same as the SDK net20 build: a 64-bit process on 64-bit Windows.
#
# Background: as a 64-bit process on .NET Framework 2.0 / 3.5 (CLR 2.0), the optimized mp4mux.exe produced by the
# Visual Studio 2010 compiler crashed intermittently with an AccessViolationException in
# Program.AddH265Track. It is a defect of the optimizing 64-bit JIT of CLR 2.0 that depends on the exact
# shape of the IL (see docs/VERIFICATION.md); that method is now marked NoOptimization for NET20 in
# src/Tools/Mp4Mux/Program.cs, and with that the six comparison suites pass on the VS2010 build.
# Should another method ever run into the same defect, "x86" here sidesteps the 64-bit JIT altogether
# (the crash did not occur in a 32-bit process); the price is 32-bit executables.
RELEASE_TOOLS_PLATFORM_TARGET = "AnyCPU"

# A plain Visual Studio 2010 machine has no reference assemblies for .NET Framework 2.0: MSBuild compiles against
# the assemblies of the runtime directory (%WINDIR%\\Microsoft.NET\\Framework\\v2.0.50727). Some machines do have
# them, in "Reference Assemblies\\Microsoft\\Framework\\.NETFramework\\v2.0". MSBuild 4 then takes System.dll from
# there but still mscorlib.dll from the runtime directory, no longer recognizes that mscorlib.dll as part of the
# framework, copies it (and its .nlp and resource files) to the output directory and reports warning MSB3270.
# With this property group mscorlib.dll comes from the same directory as the other references in that case.
FRAMEWORK_PATH_OVERRIDE = [
    "  <PropertyGroup Condition=\" '$(FrameworkPathOverride)' == '' and '$(TargetFrameworkRootPath)' == '' and"
    " Exists('$(MSBuildProgramFiles32)\\Reference Assemblies\\Microsoft\\Framework\\.NETFramework\\v2.0\\mscorlib.dll') \">",
    "    <FrameworkPathOverride>$(MSBuildProgramFiles32)\\Reference Assemblies\\Microsoft\\Framework\\.NETFramework\\v2.0</FrameworkPathOverride>",
    "  </PropertyGroup>",
]

CSHARP_PROJECT_TYPE = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"
SOLUTION_FOLDER_TYPE = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}"

# Symbols the SDK defines for net20 in addition to the project's own DefineConstants.
FRAMEWORK_DEFINES = ["NETFRAMEWORK", "NET20", "NET20_OR_GREATER"]


def guid(name):
    """Stable GUID derived from the project name."""
    h = hashlib.md5(("Bento4Sharp.VS2010/" + name).encode("utf-8")).hexdigest().upper()
    return "{%s-%s-%s-%s-%s}" % (h[0:8], h[8:12], h[12:16], h[16:20], h[20:32])


def read_sdk_project(path):
    """AssemblyName, RootNamespace, OutputType, own DefineConstants and NoWarn of an SDK project."""
    xml = io.open(path, encoding="utf-8-sig").read()

    def prop(name, default=None):
        m = re.search(r"<%s>\s*([^<]*?)\s*</%s>" % (name, name), xml)
        return m.group(1) if m else default

    def values(name):
        result = []
        for v in (prop(name, "") or "").split(";"):
            v = v.strip()
            if v and not v.startswith("$("):
                result.append(v)
        return result

    name = os.path.splitext(os.path.basename(path))[0]
    return {
        "name": name,
        "assembly": prop("AssemblyName", name),
        "namespace": prop("RootNamespace", name),
        "output_type": prop("OutputType", "Library"),
        "defines": values("DefineConstants"),
        "nowarn": [re.sub(r"^CS0*", "", w) for w in values("NoWarn")],
    }


def source_files(directory):
    """Every .cs file under directory (bin/ and obj/ skipped), as sorted paths relative to it."""
    found = []
    for base, dirs, files in os.walk(directory):
        dirs[:] = sorted(d for d in dirs if d.lower() not in ("bin", "obj") and not d.startswith("."))
        for f in sorted(files):
            if f.lower().endswith(".cs"):
                found.append(os.path.relpath(os.path.join(base, f), directory))
    return sorted(found, key=lambda p: p.replace(os.sep, "/").lower())


def win(path):
    return path.replace("/", "\\")


def project_xml(p):
    is_exe = p["output_type"].lower() == "exe"
    project_dir = os.path.join(HERE, p["name"])
    source_rel = win(os.path.relpath(p["source_dir"], project_dir))

    compile_items = []
    for f in source_files(p["source_dir"]):
        compile_items.append((source_rel + "\\" + win(f), win(f)))
    if p["name"] != "Bento4Sharp":
        # same as Directory.Build.props: the executables compile their own (internal) copy of the helpers
        compat = os.path.join(ROOT, "src", "Bento4Sharp", "Core", "Compat.cs")
        compile_items.append((win(os.path.relpath(compat, project_dir)), "Compat.cs"))
    compile_items.append(("..\\SharedAssemblyInfo.cs", "Properties\\SharedAssemblyInfo.cs"))

    base_defines = ["TRACE"] + p["defines"]
    nowarn = ";".join(p["nowarn"])

    def configuration(name, extra_define, optimize, debug_type, platform_target):
        lines = [
            "  <PropertyGroup Condition=\" '$(Configuration)|$(Platform)' == '%s|AnyCPU' \">" % name,
            "    <PlatformTarget>%s</PlatformTarget>" % platform_target,
        ]
        if name == "Debug":
            lines.append("    <DebugSymbols>true</DebugSymbols>")
        lines += [
            "    <DebugType>%s</DebugType>" % debug_type,
            "    <Optimize>%s</Optimize>" % optimize,
            "    <OutputPath>..\\bin\\%s\\</OutputPath>" % name,
            "    <DefineConstants>%s</DefineConstants>" % ";".join(base_defines + [extra_define] + FRAMEWORK_DEFINES),
            "    <ErrorReport>prompt</ErrorReport>",
            "    <WarningLevel>4</WarningLevel>",
        ]
        if nowarn:
            lines.append("    <NoWarn>%s</NoWarn>" % nowarn)
        lines.append("  </PropertyGroup>")
        return lines

    out = [
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
        "<!-- Generated by vs2010/generate-projects.py: do not edit, change the script and run it again. -->",
        "<Project ToolsVersion=\"4.0\" DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">",
        "  <PropertyGroup>",
        "    <Configuration Condition=\" '$(Configuration)' == '' \">Debug</Configuration>",
        "    <Platform Condition=\" '$(Platform)' == '' \">AnyCPU</Platform>",
        "    <ProductVersion>10.0.30319</ProductVersion>",
        "    <SchemaVersion>2.0</SchemaVersion>",
        "    <ProjectGuid>%s</ProjectGuid>" % guid(p["name"]),
        "    <OutputType>%s</OutputType>" % ("Exe" if is_exe else "Library"),
        "    <AppDesignerFolder>Properties</AppDesignerFolder>",
        "    <RootNamespace>%s</RootNamespace>" % p["namespace"],
        "    <AssemblyName>%s</AssemblyName>" % p["assembly"],
        "    <TargetFrameworkVersion>v2.0</TargetFrameworkVersion>",
        "    <TargetFrameworkProfile />",
        "    <FileAlignment>512</FileAlignment>",
        "    <AllowUnsafeBlocks>false</AllowUnsafeBlocks>",
        "    <BaseIntermediateOutputPath>..\\obj\\%s\\</BaseIntermediateOutputPath>" % p["name"],
        "    <IntermediateOutputPath>..\\obj\\%s\\$(Configuration)\\</IntermediateOutputPath>" % p["name"],
        "  </PropertyGroup>",
    ]
    out += FRAMEWORK_PATH_OVERRIDE
    out += configuration("Debug", "DEBUG", "false", "full", "AnyCPU")
    out += configuration("Release", "RELEASE", "true", "pdbonly",
                         RELEASE_TOOLS_PLATFORM_TARGET if is_exe else "AnyCPU")
    out += [
        "  <ItemGroup>",
        "    <Reference Include=\"System\" />",
        "  </ItemGroup>",
        "  <ItemGroup>",
    ]
    for include, link in compile_items:
        out += [
            "    <Compile Include=\"%s\">" % include,
            "      <Link>%s</Link>" % link,
            "    </Compile>",
        ]
    out.append("  </ItemGroup>")
    if is_exe:
        out += [
            "  <ItemGroup>",
            "    <None Include=\"..\\app.config\">",
            "      <Link>app.config</Link>",
            "    </None>",
            "  </ItemGroup>",
        ]
    if p["name"] != "Bento4Sharp":
        out += [
            "  <ItemGroup>",
            "    <ProjectReference Include=\"..\\Bento4Sharp\\Bento4Sharp.csproj\">",
            "      <Project>%s</Project>" % guid("Bento4Sharp"),
            "      <Name>Bento4Sharp</Name>",
            "    </ProjectReference>",
            "  </ItemGroup>",
        ]
    out += [
        "  <Import Project=\"$(MSBuildToolsPath)\\Microsoft.CSharp.targets\" />",
        "</Project>",
        "",
    ]
    return "\r\n".join(out)


def solution_text(projects):
    folders = [name for name in ("Tools", "Tests") if any(p["folder"] == name for p in projects)]
    out = [
        "",
        "Microsoft Visual Studio Solution File, Format Version 11.00",
        "# Visual Studio 2010",
    ]
    for p in projects:
        out += [
            "Project(\"%s\") = \"%s\", \"%s\\%s.csproj\", \"%s\"" % (CSHARP_PROJECT_TYPE, p["name"], p["name"], p["name"], guid(p["name"])),
            "EndProject",
        ]
    for f in folders:
        out += [
            "Project(\"%s\") = \"%s\", \"%s\", \"%s\"" % (SOLUTION_FOLDER_TYPE, f, f, guid("folder:" + f)),
            "EndProject",
        ]
    out += [
        "Global",
        "\tGlobalSection(SolutionConfigurationPlatforms) = preSolution",
        "\t\tDebug|Any CPU = Debug|Any CPU",
        "\t\tRelease|Any CPU = Release|Any CPU",
        "\tEndGlobalSection",
        "\tGlobalSection(ProjectConfigurationPlatforms) = postSolution",
    ]
    for p in projects:
        for c in ("Debug", "Release"):
            out += [
                "\t\t%s.%s|Any CPU.ActiveCfg = %s|Any CPU" % (guid(p["name"]), c, c),
                "\t\t%s.%s|Any CPU.Build.0 = %s|Any CPU" % (guid(p["name"]), c, c),
            ]
    out += [
        "\tEndGlobalSection",
        "\tGlobalSection(SolutionProperties) = preSolution",
        "\t\tHideSolutionNode = FALSE",
        "\tEndGlobalSection",
        "\tGlobalSection(NestedProjects) = preSolution",
    ]
    for p in projects:
        if p["folder"]:
            out.append("\t\t%s = %s" % (guid(p["name"]), guid("folder:" + p["folder"])))
    out += [
        "\tEndGlobalSection",
        "EndGlobal",
        "",
    ]
    return "\r\n".join(out)


APP_CONFIG = "\r\n".join([
    "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
    "<configuration>",
    "  <startup>",
    "    <supportedRuntime version=\"v2.0.50727\" />",
    "  </startup>",
    "</configuration>",
    "",
])

SHARED_ASSEMBLY_INFO = "\r\n".join([
    "// Generated by vs2010/generate-projects.py. Linked into every Visual Studio 2010 project:",
    "// the same version numbers the SDK build gives its assemblies.",
    "using System.Reflection;",
    "",
    "[assembly: AssemblyProduct(\"Bento4Sharp\")]",
    "[assembly: AssemblyCompany(\"HongSic (HSKernel)\")]",
    "[assembly: AssemblyCopyright(\"Copyright 2002-2026 Axiomatic Systems, LLC. C# implementation by HongSic (HSKernel).\")]",
    "[assembly: AssemblyVersion(\"1.0.0.0\")]",
    "[assembly: AssemblyFileVersion(\"1.0.0.0\")]",
    "",
])

GITIGNORE = "\n".join(["bin/", "obj/", "*.suo", "*.user", "*.cache", ""])


def write(path, text, bom):
    data = text.encode("utf-8-sig" if bom else "utf-8")
    if os.path.isfile(path):
        with open(path, "rb") as f:
            if f.read() == data:
                return False
    directory = os.path.dirname(path)
    if not os.path.isdir(directory):
        os.makedirs(directory)
    with open(path, "wb") as f:
        f.write(data)
    print("wrote " + os.path.relpath(path, ROOT).replace(os.sep, "/"))
    return True


def main():
    projects = []
    library = read_sdk_project(os.path.join(ROOT, "src", "Bento4Sharp", "Bento4Sharp.csproj"))
    library.update(source_dir=os.path.join(ROOT, "src", "Bento4Sharp"), folder=None)
    projects.append(library)
    for group, folder in (("src/Tools", "Tools"), ("tests", "Tests")):
        group_dir = os.path.join(ROOT, *group.split("/"))
        for d in sorted(os.listdir(group_dir), key=lambda s: s.lower()):
            sdk_project = os.path.join(group_dir, d, d + ".csproj")
            if not os.path.isfile(sdk_project):
                continue
            p = read_sdk_project(sdk_project)
            p.update(source_dir=os.path.join(group_dir, d), folder=folder)
            projects.append(p)

    names = [p["name"].lower() for p in projects]
    if len(set(names)) != len(names):
        sys.exit("error: two projects have the same name")

    changed = 0
    for p in projects:
        changed += write(os.path.join(HERE, p["name"], p["name"] + ".csproj"), project_xml(p), True)
    changed += write(os.path.join(HERE, "Bento4Sharp.VS2010.sln"), solution_text(projects), True)
    changed += write(os.path.join(HERE, "app.config"), APP_CONFIG, False)
    changed += write(os.path.join(HERE, "SharedAssemblyInfo.cs"), SHARED_ASSEMBLY_INFO, False)
    changed += write(os.path.join(HERE, ".gitignore"), GITIGNORE, False)

    # a project directory left over from a tool that no longer exists is reported, not deleted
    for d in sorted(os.listdir(HERE)):
        if os.path.isfile(os.path.join(HERE, d, d + ".csproj")) and d.lower() not in names:
            print("warning: vs2010/%s has no SDK project any more; delete it by hand" % d)

    print("%d project(s), %d file(s) written" % (len(projects), changed))


if __name__ == "__main__":
    main()
