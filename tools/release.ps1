<#
.SYNOPSIS
  GRC2 릴리즈 스크립트 (게임이 설치된 PC에서 실행 — 빌드·테스트에 게임 DLL이 필요해 클라우드/CI에서는 불가).

.DESCRIPTION
  1) 버전 일치 확인: AssemblyVersion(앞 3자리) = AssemblyFileVersion = MelonInfo = CHANGELOG.md 최상단 ## [버전]
  2) tools\check_game_version.ps1 (설치본이 검증 기준 빌드와 다르면 경고만)
  3) dotnet build -c Release + dotnet test (158개 이상 모두 통과해야 함)
  4) dist\GRC2.dll 복사 + SHA256 + 릴리즈 노트 초안(dist\release-notes-v<버전>.md)
  -Publish: main이 origin/main과 같고 작업 트리가 깨끗한지 확인 → 태그 v<버전> 푸시 → gh release create (첨부: GRC2.dll)

.EXAMPLE
  .\tools\release.ps1
  .\tools\release.ps1 -Publish
  .\tools\release.ps1 -GamePath "D:\SteamLibrary\steamapps\common\GUNVOLT RECORDS Cychronicle"
#>
param(
    [switch]$Publish,
    [string]$GamePath = $(if ($env:GRC2_GAME_PATH) { $env:GRC2_GAME_PATH } else { 'H:\steam\steamapps\common\GUNVOLT RECORDS Cychronicle' })
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$asmInfo = Join-Path $root 'GRC2\Properties\AssemblyInfo.cs'
$changelog = Join-Path $root 'CHANGELOG.md'

# ── 1. 버전 일치 ──
$asmText = Get-Content -Raw -Encoding UTF8 $asmInfo
$asmVer = ([regex]::Match($asmText, 'AssemblyVersion\("([0-9]+\.[0-9]+\.[0-9]+)')).Groups[1].Value
$fileVer = ([regex]::Match($asmText, 'AssemblyFileVersion\("([0-9]+\.[0-9]+\.[0-9]+)')).Groups[1].Value
$melonVer = ([regex]::Match($asmText, 'MelonInfo\([^)]*?"([0-9]+\.[0-9]+\.[0-9]+)"')).Groups[1].Value
$logVer = ([regex]::Match((Get-Content -Raw -Encoding UTF8 $changelog), '(?m)^## \[([0-9.]+)\]')).Groups[1].Value
if (-not $asmVer -or $asmVer -ne $fileVer -or $asmVer -ne $melonVer -or $asmVer -ne $logVer) {
    throw "버전 불일치: AssemblyVersion=$asmVer, AssemblyFileVersion=$fileVer, MelonInfo=$melonVer, CHANGELOG=$logVer"
}
$tag = "v$asmVer"
Write-Host "버전 $asmVer 확인 (AssemblyInfo / MelonInfo / CHANGELOG 일치)"

# ── 2. 게임 버전 점검(경고만) ──
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'check_game_version.ps1') -GamePath $GamePath
if ($LASTEXITCODE -ne 0) { Write-Warning "설치본이 검증 기준 빌드와 다릅니다. 업데이트_대응_가이드.md를 먼저 확인하세요." }

# ── 3. 빌드 + 테스트 ──
& dotnet build (Join-Path $root 'GRC2.sln') -c Release -nologo "-p:GamePath=$GamePath"
if ($LASTEXITCODE -ne 0) { throw '빌드 실패' }
& dotnet test (Join-Path $root 'GRC2.Tests\GRC2.Tests.csproj') -c Release --no-build -nologo "-p:GamePath=$GamePath"
if ($LASTEXITCODE -ne 0) { throw '테스트 실패' }

$dll = Join-Path $root 'GRC2\bin\Release\net472\GRC2.dll'
if (-not (Test-Path $dll)) { throw "DLL을 찾을 수 없습니다: $dll" }

# ── 4. 산출물 + 노트 ──
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$distDll = Join-Path $dist 'GRC2.dll'
Copy-Item $dll $distDll -Force
$dllSha = (Get-FileHash $distDll -Algorithm SHA256).Hash.ToLower()

$section = [regex]::Match((Get-Content -Raw -Encoding UTF8 $changelog), "(?ms)^## \[$([regex]::Escape($asmVer))\][^\n]*\n(.*?)(?=^## \[|\z)").Groups[1].Value.Trim()
$prevTag = (git describe --tags --abbrev=0 2>$null)
$compare = if ($prevTag) { "`n**전체 변경 내역**: https://github.com/magjangin/GRC2/compare/$prevTag...$tag" } else { '' }
$notes = @"
$section

### 설치

``GRC2.dll``을 게임 폴더의 ``Mods\``에 넣으세요. MelonLoader 0.7.3이 설치되어 있어야 합니다.

### SHA256

- ``GRC2.dll``: ``$dllSha``
$compare
"@
$notesFile = Join-Path $dist "release-notes-$tag.md"
Set-Content -Path $notesFile -Value $notes -Encoding utf8
Write-Host "생성: $distDll"
Write-Host "생성: $notesFile"

if (-not $Publish) { return }

# ── 5. 게시 ──
git fetch origin main | Out-Null
$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne 'main') { throw "main 브랜치에서 실행하세요 (현재 $branch)" }
if ((git rev-parse HEAD).Trim() -ne (git rev-parse origin/main).Trim()) { throw 'main이 origin/main과 다릅니다. pull/push 후 다시 실행하세요.' }
if (git status --porcelain --untracked-files=no) { throw '작업 트리가 깨끗하지 않습니다.' }
if (git tag --list $tag) { throw "태그 $tag 가 이미 있습니다. 버전을 먼저 올리세요." }

git tag -a $tag -m "GRC2 $tag"
git push origin $tag
gh release create $tag $distDll --title "GRC2 $tag" --notes-file $notesFile
Write-Host "게시 완료: $tag"
