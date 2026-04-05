# FSharpLintAnalyzers Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create a standalone analyzer assembly that exposes all FSharpLint rules via the FSharp.Analyzers.SDK `[<CliAnalyzer>]` protocol.

**Architecture:** Single `[<CliAnalyzer>]` entry point calls `FSharpLint.Application.Lint.lintParsedFile` with data from `CliContext`, maps `LintWarning` list to `Message` list. Config loaded from `fsharplint.json` via FSharpLint's own config machinery.

**Tech Stack:** F#, FSharp.Analyzers.SDK 0.36.0, FSharpLint.Core (project reference), net10.0

---

### Task 1: Create project scaffolding

**Files:**
- Create: `../FSharpLintAnalyzers/FSharpLintAnalyzers.fsproj`

**Step 1: Create the project directory**

```bash
mkdir -p ../FSharpLintAnalyzers
```

**Step 2: Write the .fsproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <NoWarn>$(NoWarn);NU1608</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="LintAnalyzer.fs" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="FSharp.Analyzers.SDK" Version="0.36.0" />
    <PackageReference Update="FSharp.Core" Version="10.0.101" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../FSharpLint/src/FSharpLint.Core/FSharpLint.Core.fsproj" />
  </ItemGroup>
</Project>
```

Notes:
- `CopyLocalLockFileAssemblies` is required so the analyzer DLL includes all transitive deps.
- `FSharp.Core` pinned to 10.0.101 to match the `fsharp-analyzers` CLI tool (same pattern as Intelligence.Analyzers).
- Project reference to FSharpLint.Core. Path is relative from `../FSharpLintAnalyzers/`.

**Step 3: Verify it builds (empty project)**

Create a placeholder `LintAnalyzer.fs`:

```fsharp
module FSharpLintAnalyzers.LintAnalyzer
```

Run: `cd ../FSharpLintAnalyzers && dotnet build`
Expected: Build succeeded.

**Step 4: Commit**

```bash
git add -A
git commit -m "scaffold: FSharpLintAnalyzers project with deps"
```

---

### Task 2: Write the analyzer adapter

**Files:**
- Create: `../FSharpLintAnalyzers/LintAnalyzer.fs`

**Step 1: Write LintAnalyzer.fs**

```fsharp
module FSharpLintAnalyzers.LintAnalyzer

open System.Collections.Concurrent
open System.IO
open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open FSharpLint.Application
open FSharpLint.Framework.Suggestion

/// Cache loaded configs by directory to avoid re-reading fsharplint.json per file.
let private configCache = ConcurrentDictionary<string, ConfigurationParam>()

/// Walk up from filePath looking for fsharplint.json, cache result per directory.
let private getConfigParam (filePath: string) : ConfigurationParam =
    let dir = Path.GetDirectoryName(Path.GetFullPath(filePath))

    configCache.GetOrAdd(
        dir,
        fun dir ->
            let rec walkUp (d: string) =
                let candidate = Path.Combine(d, "fsharplint.json")

                if File.Exists(candidate) then
                    ConfigurationParam.FromFile candidate
                else
                    let parent = Directory.GetParent(d)

                    if isNull parent then
                        ConfigurationParam.Default
                    else
                        walkUp parent.FullName

            walkUp dir
    )

/// Map a FSharpLint SuggestedFix to an Analyzer SDK Fix.
let private mapFix (suggestedFix: SuggestedFix) : Fix =
    { FromRange = suggestedFix.FromRange
      FromText = suggestedFix.FromText
      ToText = suggestedFix.ToText }

/// Map a FSharpLint LintWarning to an Analyzer SDK Message.
let private mapWarning (warning: LintWarning) : Message =
    let fixes =
        match warning.Details.SuggestedFix with
        | Some lazySuggestion ->
            match lazySuggestion.Value with
            | Some fix -> [ mapFix fix ]
            | None -> []
        | None -> []

    { Type = warning.RuleName
      Message = warning.Details.Message
      Code = warning.RuleIdentifier
      Severity = Severity.Warning
      Range = warning.Details.Range
      Fixes = fixes }

[<CliAnalyzer("FSharpLint", "All FSharpLint rules via FSharp.Analyzers.SDK")>]
let lintAnalyzer: Analyzer<CliContext> =
    fun (context: CliContext) ->
        async {
            let configParam = getConfigParam context.FileName

            let parsedFileInfo: Lint.ParsedFileInformation =
                { Ast = context.ParseFileResults.ParseTree
                  Source = context.SourceText.ToString()
                  TypeCheckResults = Some context.CheckFileResults
                  ProjectCheckResults = Some context.CheckProjectResults }

            let optionalParams =
                { Lint.OptionalLintParameters.Default with
                    Configuration = configParam }

            match Lint.lintParsedFile optionalParams parsedFileInfo context.FileName with
            | LintResult.Success warnings -> return warnings |> List.map mapWarning
            | LintResult.Failure failure ->
                // Return a single diagnostic describing the lint failure
                return
                    [ { Type = "FSharpLint.Error"
                        Message = failure.Description
                        Code = "FL0000"
                        Severity = Severity.Info
                        Range = Range.range0
                        Fixes = [] } ]
        }
```

**Step 2: Build**

Run: `cd ../FSharpLintAnalyzers && dotnet build`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: FSharpLint analyzer adapter — all 97 rules via SDK"
```

---

### Task 3: Smoke test against a real project

**Step 1: Build the analyzer in Release mode**

Run: `cd ../FSharpLintAnalyzers && dotnet build -c Release`
Expected: Build succeeded. Output DLL at `bin/Release/net10.0/FSharpLintAnalyzers.dll`.

**Step 2: Run fsharp-analyzers against the FSharpLint project itself**

Run:
```bash
dotnet fsharp-analyzers \
  --project ../FSharpLint/src/FSharpLint.Core/FSharpLint.Core.fsproj \
  --analyzers-path ../FSharpLintAnalyzers/bin/Release/net10.0/ \
  --verbosity d
```

Expected: Analyzer output with FL-coded diagnostics. Some warnings are expected
(FSharpLint's own code isn't 100% clean to its own rules).

If `fsharp-analyzers` is not installed as a global tool, install it first:
```bash
dotnet tool install -g fsharp-analyzers
```

Or if available as a local tool in the intelligence project, use that path.

**Step 3: Verify rule codes in output**

Check that the output contains FL-prefixed codes (e.g., FL0060 for MaxCharactersOnLine).
If no output, check:
- Is the assembly name correct? Must contain "Analyzer" (it does: FSharpLintAnalyzers).
- Are deps copied locally? `CopyLocalLockFileAssemblies` should handle this.
- Check verbose output for assembly load errors.

**Step 4: Commit any fixes needed**

```bash
git add -A
git commit -m "fix: address smoke test findings"
```

---

### Task 4: Test in the intelligence project (optional)

**Step 1: Add the analyzer path to the intelligence project's analyzer invocation**

In the intelligence project, the `fsharp-analyzers` command likely already has an
`--analyzers-path`. Add a second `--analyzers-path` pointing to the
FSharpLintAnalyzers output, or copy the DLLs alongside Intelligence.Analyzers output.

**Step 2: Run and compare**

Run analyzers against a few intelligence source files. Compare output with
running FSharpLint directly. Results should match (same rule codes, same ranges).

**Step 3: Measure performance**

Time the analyzer run with and without FSharpLintAnalyzers to quantify overhead.
The advantage is eliminated duplicate project loading — both analyzer sets now
share one `fsharp-analyzers` invocation instead of two separate tools.

---

## Potential issues to watch for

1. **FSharp.Core version conflict**: FSharpLint.Core may pull in a different
   FSharp.Core than the analyzer SDK expects. The `<PackageReference Update=
   "FSharp.Core" Version="10.0.101" />` pin should fix this, but watch for
   runtime `MissingMethodException` or `FileLoadException`.

2. **Ionide.ProjInfo transitive dep**: FSharpLint.Core depends on Ionide.ProjInfo
   for project loading, but the analyzer adapter doesn't use that path (it calls
   `lintParsedFile`, not `lintProject`). The dep still gets copied locally though.
   If it causes assembly conflicts with the `fsharp-analyzers` host, consider
   trimming it via `ExcludeAssets` or moving to a NuGet reference with the
   project-loading parts excluded.

3. **Config cache lifetime**: The `ConcurrentDictionary` lives for the process.
   If `fsharp-analyzers` is a long-running process (IDE mode), config changes
   won't be picked up until restart. Acceptable for CLI; for IDE, consider a
   file watcher or TTL cache later.
