# ClipTray Redux — Notes for Claude

Standing reference for working in this repo. Keep it short; update when the process changes.

## What this app is

System-tray clipboard manager. C# / WinForms / .NET Framework 4.8. Single portable exe — no installer, no runtime download. Entries live in a plain-text file (`ClipTray.txt`) next to the exe; the original ClipTray file format is preserved so users can migrate.

Source: `ClipTray/` (app), `ClipTray.Tests/` (tests). SDK-style csproj — new `.cs` files under `ClipTray/` are picked up automatically.

## Versioning

- **SemVer.** Additive feature → bump minor (`1.0 → 1.1`). Breaking change → bump major. Bug fix only → bump patch.
- Edit both `AssemblyVersion` and `AssemblyFileVersion` in [ClipTray/Properties/AssemblyInfo.cs](ClipTray/Properties/AssemblyInfo.cs). Include the bump in the same commit as the feature.

## Release procedure

Replace `X.Y.Z` and `<feature name>` everywhere below.

```powershell
# 1. After feature is committed and AssemblyInfo is bumped:
git tag -a vX.Y.Z -m "vX.Y.Z — <feature name>"
git push origin master
git push origin vX.Y.Z

# 2. Build Release. SDK auto-generates ClipTray.exe.config alongside the exe.
dotnet build "ClipTray/ClipTray.csproj" -c Release

# 3. Verify the FileVersion baked into the exe matches the tag.
Get-Item 'ClipTray\bin\Release\net48\ClipTray.exe' | Select-Object -ExpandProperty VersionInfo

# 4. Package both files at the zip root (no subfolder). Match v1.0.0's structure.
Compress-Archive -Path `
    'ClipTray\bin\Release\net48\ClipTray.exe', `
    'ClipTray\bin\Release\net48\ClipTray.exe.config' `
    -DestinationPath 'ClipTray-vX.Y.Z.zip' -Force

# 5. Publish. Notes go in a HEREDOC via Bash (see structure below).
gh release create vX.Y.Z ClipTray-vX.Y.Z.zip `
    --title "vX.Y.Z — <feature name>" `
    --notes-file release-notes.md   # or inline via heredoc from Bash tool
```

## Packaging rules

- **Always ship the `.exe` + `.exe.config` pair, zipped.** Never upload the raw exe alone.
  - The `.config` contains the `<supportedRuntime>` block and the WinForms `PerMonitorV2` setting. Without it, the app loses both explicit .NET 4.8 binding and enhanced DPI handling.
  - The SDK emits `ClipTray.exe.config` from [ClipTray/App.config](ClipTray/App.config) during the Release build.
- **Zip name**: `ClipTray-vX.Y.Z.zip` at the repo root. Files at the zip root (no subfolder).
- **Don't commit the zip.** `.gitignore` already excludes `*.zip`. The zip is a release artifact, not source.
- Release config in [ClipTray/ClipTray.csproj](ClipTray/ClipTray.csproj) already strips PDBs and enables optimizations — don't disable those for a release build.

## Release notes structure

Match the tone of [v1.1.0](https://github.com/ldinino/ClipTray_Redux/releases/tag/v1.1.0). Sections:

1. **One-paragraph intro** — what the release does for the user, in plain language.
2. **Feature sections** (`## Tokens`, `## Composer changes`, etc.) — tables and bullets, not prose.
3. **`## Compatibility`** — note whether the file format changed and whether older versions can read newer files.
4. **`## Download`** — name the actual zip filename and tell the user what to do with it ("unzip, drop the `.exe` and `.config` in any folder, run").

Update the README's **Features** section in the same commit if the change is user-visible.

## Commit style

Convention from existing history: imperative subject ("Add X", "Fix Y", or "Phase N: …"), body explains the *why*, trailer:

```
Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
```

## Gotchas

- `gh` tokens go stale. If `gh release create` 401s, the user runs `gh auth login -h github.com` (web flow, HTTPS) and pings me back.
- The Bash tool eats `$env:VAR` and other PowerShell `$`-references. Use the PowerShell tool for any command with PS variable references or pipelines that touch them.
- Don't `dotnet build` with a backslash path through Bash — `ClipTray\ClipTray.csproj` gets mangled. Use forward slashes (`ClipTray/ClipTray.csproj`) or use the PowerShell tool.
