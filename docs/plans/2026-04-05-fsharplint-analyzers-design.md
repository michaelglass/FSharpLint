# FSharpLintAnalyzers Design

Expose all 97 FSharpLint rules as FSharp.Analyzers.SDK analyzers so they can run
alongside custom project analyzers via the `fsharp-analyzers` CLI tool.

## Motivation

The intelligence project already runs custom `[<CliAnalyzer>]` analyzers via
`fsharp-analyzers`. FSharpLint is run separately. Merging both into one analyzer
pass eliminates duplicate project loading and lets all diagnostics surface through
a single tool.

## Architecture

### Single assembly, thin adapter

```
FSharpLintAnalyzers/
  FSharpLintAnalyzers.fsproj   # net8.0, refs FSharp.Analyzers.SDK 0.36.0 + FSharpLint.Core
  LintAnalyzer.fs              # Single [<CliAnalyzer>] entry point
```

The project depends on `FSharpLint.Core` as a project reference (or NuGet package).
It does **not** rewrite rule logic. The adapter:

1. Loads `fsharplint.json` config (reusing FSharpLint's config discovery)
2. Converts `CliContext` into FSharpLint's `ParsedFileInformation`
3. Calls FSharpLint's `lintParsedFile`
4. Maps each `LintWarning` to an Analyzer SDK `Message`

### Entry point

```fsharp
[<CliAnalyzer("FSharpLint", "All FSharpLint rules as a single analyzer")>]
let lintAnalyzer: Analyzer<CliContext> =
    fun (context: CliContext) ->
        async {
            let config = loadConfig context.FileName
            let parsedFileInfo = {
                Ast = context.ParseFileResults.ParseTree
                Source = context.SourceText.ToString()
                TypeCheckResults = Some context.CheckFileResults
                ProjectCheckResults = Some context.CheckProjectResults
            }
            let result = lintParsedFile { OptionalLintParameters.Default with Configuration = config } parsedFileInfo context.FileName
            return mapResult result
        }
```

### Mapping LintWarning to Message

| LintWarning field | Message field | Notes |
|---|---|---|
| `RuleIdentifier` | `Code` | e.g. "FL0014" |
| `RuleName` | `Type` | e.g. "RedundantNewKeyword" |
| `Details.Message` | `Message` | Human-readable description |
| `Details.Range` | `Range` | Same FCS `range` type, direct pass-through |
| `Details.SuggestedFix` | `Fixes` | Map `SuggestedFix` -> `Fix` (FromRange/ToText) |
| (all warnings) | `Severity` | `Severity.Warning` |

### Config loading

Reuse FSharpLint's `ConfigurationParam.Default` which walks up from the file's
directory looking for `fsharplint.json`. Cache the loaded config per directory to
avoid re-parsing on every file.

### Suppression

FSharpLint's own `// fsharplint:disable` comments are handled internally by
`lintParsedFile`. The Analyzer SDK's `// AnalyzerIgnore` is handled by the SDK
runner. Both work independently — no bridging needed.

### Shared walker strategy

FSharpLint.Core already has its own shared walker (`AbstractSyntaxArray` + visitor
dispatch). Since we call `lintParsedFile`, we reuse that walker. The analyzer is
not doing its own tree walk — it delegates entirely to FSharpLint.Core.

This means all 97 rules run in a single tree traversal per file, same as today.

## Rule coverage: no gaps

All 97 rules work because `CliContext` provides everything FSharpLint needs:

| FSharpLint needs | CliContext provides |
|---|---|
| `ParsedInput` (untyped AST) | `context.ParseFileResults.ParseTree` |
| `FSharpCheckFileResults` | `context.CheckFileResults` |
| `FSharpCheckProjectResults` | `context.CheckProjectResults` |
| Source text | `context.SourceText.ToString()` |
| File path | `context.FileName` |

### Rules by type-checking dependency

**Untyped AST only (~75 rules):** All formatting, source length, number-of-items,
most smells, basic naming. No type info needed.

**File-level type checking (~21 rules):** HintMatcher, NoPartialFunctions,
UnneededRecKeyword, EnsureTailCallDiagnostics, RedundantNewKeyword, UselessBinding,
DisallowShadowing, FavourNestedFunctions, UsedUnderscorePrefixedElements, and
naming rules that distinguish values/functions/union cases (PrivateValuesNames,
PublicValuesNames, ParameterNames, InternalValuesNames, SynchronousFunctionNames,
AsynchronousFunctionNames, SimpleAsyncComplementaryHelpers).

**Project-level type checking (1 rule):** NoAsyncRunSynchronouslyInLibrary — checks
`ProjectCheckResults.AssemblyContents` for entry points and test attributes.

All three levels are available via `CliContext`.

### EditorAnalyzer consideration

If an `[<EditorAnalyzer>]` were added later, `CheckFileResults` and
`CheckProjectResults` become optional. The ~21 type-dependent rules would degrade
gracefully — FSharpLint already handles `None` for these fields by skipping
type-dependent checks. Not needed for v1.

## Implementation plan

1. Create `../FSharpLintAnalyzers/` directory and `.fsproj`
2. Write `LintAnalyzer.fs` with config loading + adapter
3. Write mapping functions (LintWarning -> Message, SuggestedFix -> Fix)
4. Add config caching (per-directory, so multi-file runs don't re-parse config)
5. Test: build, point `fsharp-analyzers` at the output DLL, run against a sample project
6. Verify all 97 rule codes appear in output when violations exist

## Open questions for later

- **NuGet packaging**: Ship as a NuGet package for easy consumption? Needs
  `dotnet publish` output in `lib/` to include transitive deps.
- **FSharp.Core version pinning**: Same issue as Intelligence.Analyzers — the
  fsharp-analyzers CLI bundles a specific FSharp.Core version.
- **Performance**: Config caching granularity. One config per solution? Per directory?
- **Filtering**: Should the analyzer accept a config option to disable rule categories
  at the analyzer level (in addition to fsharplint.json)?
