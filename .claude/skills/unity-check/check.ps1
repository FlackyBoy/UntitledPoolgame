# Compile-checks the game and editor scripts without Unity, from copies of the
# csproj Unity generates. The repo's own csproj are never touched.
# Exit code: 0 = compiled, 1 = compile errors, 2 = could not run the check.
param(
    [switch]$ShowWarnings,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$work = Join-Path $env:TEMP 'untitledpool-unity-check'

function Stop-Check([string]$message) {
    Write-Output "IMPOSSIBLE: $message"
    exit 2
}

function Find-Dotnet {
    if ($env:UNITY_EDITOR_PATH) {
        $custom = Join-Path $env:UNITY_EDITOR_PATH 'Data\DotNetSdk\dotnet.exe'
        if (Test-Path -LiteralPath $custom) { return $custom }
    }
    $hub = 'C:\Program Files\Unity\Hub\Editor'
    $version = (Select-String -LiteralPath (Join-Path $root 'ProjectSettings\ProjectVersion.txt') -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value.Trim()
    $wanted = Join-Path $hub "$version\Editor\Data\DotNetSdk\dotnet.exe"
    if (Test-Path -LiteralPath $wanted) { return $wanted }
    # Any installed editor's SDK can build: the Unity references come from the csproj.
    $any = Get-ChildItem -Path "$hub\*\Editor\Data\DotNetSdk\dotnet.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($any) { return $any.FullName }
    Stop-Check 'dotnet introuvable. Installer Unity via le Hub, ou definir UNITY_EDITOR_PATH (dossier Editor).'
}

function Get-Absolute([string]$path) {
    if ([IO.Path]::IsPathRooted($path)) { return $path }
    return [IO.Path]::GetFullPath((Join-Path $root $path))
}

# Writes the adjusted copy of a project. Returns how many new .cs files it added.
function New-ProjectCopy([string]$name, [bool]$isEditor, [string]$builtGameDll) {
    $source = Join-Path $root "$name.csproj"
    if (-not (Test-Path -LiteralPath $source)) {
        Stop-Check "$name.csproj absent. Ouvrir le projet dans Unity une fois pour le generer."
    }

    $xml = New-Object System.Xml.XmlDocument
    $xml.PreserveWhitespace = $true
    $xml.Load($source)

    $known = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $compileGroup = $null
    foreach ($node in @($xml.SelectNodes('//Compile'))) {
        if (-not $compileGroup) { $compileGroup = $node.ParentNode }
        $path = Get-Absolute $node.GetAttribute('Include')
        # A script deleted since Unity last generated the project.
        if (-not (Test-Path -LiteralPath $path)) { [void]$node.ParentNode.RemoveChild($node); continue }
        $node.SetAttribute('Include', $path)
        [void]$known.Add($path)
    }

    # Scripts created since Unity last generated the project. Assets/Scripts has
    # no asmdef, so the Editor folders alone split the two assemblies.
    $added = 0
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root 'Assets\Scripts') -Filter '*.cs' -Recurse -File) {
        $inEditor = $file.FullName -match '\\Editor\\'
        if ($inEditor -ne $isEditor -or $known.Contains($file.FullName)) { continue }
        $item = $xml.CreateElement('Compile')
        $item.SetAttribute('Include', $file.FullName)
        [void]$compileGroup.AppendChild($item)
        $added++
    }

    foreach ($hint in @($xml.SelectNodes('//HintPath'))) {
        $hint.InnerText = Get-Absolute $hint.InnerText
    }

    # The VS Code analyzer belongs to one machine and only adds noise here.
    foreach ($analyzer in @($xml.SelectNodes('//Analyzer'))) {
        $path = Get-Absolute $analyzer.GetAttribute('Include')
        if ($path -match '\\\.vscode\\' -or -not (Test-Path -LiteralPath $path)) {
            [void]$analyzer.ParentNode.RemoveChild($analyzer)
        } else {
            $analyzer.SetAttribute('Include', $path)
        }
    }

    # Unity's built DLLs replace the sibling projects, so only ours are rebuilt.
    foreach ($reference in @($xml.SelectNodes('//ProjectReference'))) {
        $assembly = [IO.Path]::GetFileNameWithoutExtension($reference.GetAttribute('Include'))
        $dll = Join-Path $root "Library\ScriptAssemblies\$assembly.dll"
        if ($assembly -eq 'Assembly-CSharp' -and $builtGameDll) { $dll = $builtGameDll }
        $item = $xml.CreateElement('Reference')
        $item.SetAttribute('Include', $assembly)
        $hint = $xml.CreateElement('HintPath')
        $hint.InnerText = $dll
        [void]$item.AppendChild($hint)
        [void]$reference.ParentNode.ReplaceChild($item, $reference)
    }

    $xml.Save((Join-Path $work "$name.csproj"))
    return $added
}

# Prints the unique errors (and warnings on request), paths relative to the repo.
function Invoke-Build([string]$dotnet, [string]$name, [int]$added) {
    $log = Join-Path $work "$name.log"
    $project = Join-Path $work "$name.csproj"
    $env:DOTNET_CLI_UI_LANGUAGE = 'en'
    $env:DOTNET_NOLOGO = '1'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    # dotnet's stderr must not become a PowerShell error: the exit code decides.
    $ErrorActionPreference = 'Continue'
    & $dotnet build $project -v:m -nologo *> $log
    $code = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'

    $lines = Get-Content -LiteralPath $log
    $clean = { param($line) ($line -replace ' \[[^\]]+\.csproj\]\s*$', '').Replace("$root\", '').Trim() }
    $errors = @($lines | Where-Object { $_ -match ': error ' } | ForEach-Object { & $clean $_ } | Sort-Object -Unique)
    $warnings = @($lines | Where-Object { $_ -match ': warning ' } | ForEach-Object { & $clean $_ } | Sort-Object -Unique)

    $note = if ($added -gt 0) { " ($added nouveaux scripts ajoutes)" } else { '' }
    if ($code -eq 0) {
        Write-Host "OK  $name : 0 erreur, $($warnings.Count) avertissement(s)$note"
    } else {
        Write-Host "ECHEC  $name : $($errors.Count) erreur(s), $($warnings.Count) avertissement(s)$note"
        $errors | ForEach-Object { Write-Host "  $_" }
        if ($errors.Count -eq 0) {
            Write-Host "  Aucune erreur CS lue : voir $log"
            $lines | Select-Object -Last 15 | ForEach-Object { Write-Host "  $_" }
        }
    }
    if ($ShowWarnings) { $warnings | ForEach-Object { Write-Host "  $_" } }
    return $code
}

if ($Clean -and (Test-Path -LiteralPath $work)) { Remove-Item -LiteralPath $work -Recurse -Force }
New-Item -ItemType Directory -Force -Path $work | Out-Null

$dotnet = Find-Dotnet
$started = Get-Date

$added = New-ProjectCopy 'Assembly-CSharp' $false $null
$gameCode = Invoke-Build $dotnet 'Assembly-CSharp' $added
if ($gameCode -ne 0) {
    Write-Output 'Scripts d''editeur non verifies : le jeu ne compile pas.'
    exit 1
}

$gameDll = Join-Path $work 'Temp\bin\Debug\Assembly-CSharp.dll'
if (-not (Test-Path -LiteralPath $gameDll)) { Stop-Check "DLL de verification introuvable : $gameDll" }
$added = New-ProjectCopy 'Assembly-CSharp-Editor' $true $gameDll
$editorCode = Invoke-Build $dotnet 'Assembly-CSharp-Editor' $added

Write-Output ("Duree : {0:N0} s. Compile ne veut pas dire teste : Unity seul peut lancer le jeu." -f ((Get-Date) - $started).TotalSeconds)
if ($editorCode -ne 0) { exit 1 }
exit 0
