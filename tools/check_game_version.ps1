# 설치된 게임이 모드가 검증한 기준 빌드와 같은지 게임을 켜지 않고 확인합니다.
# 기준 값은 GRC2/Core/GameCompatibility.cs의 TestedBuildId / TestedAssemblyMvid에서 읽습니다(문서와 값이 어긋나지 않게).
#
# 사용: powershell -ExecutionPolicy Bypass -File tools\check_game_version.ps1 [-GamePath "다른 설치 경로"]
# 종료 코드: 0 = 기준 빌드와 같음, 1 = 다름(업데이트 대응 가이드 2단계로), 2 = 파일을 찾지 못함
#
# 기본 대상은 바탕화면 바로가기가 실행하는 독립 사본(H:\cychronicle hwa, 2026-11-24)입니다. 사본은 스팀이 업데이트하지 않으므로,
# 스팀 설치본(-SteamPath)의 Build ID도 참고로 보여 줍니다. 스팀판이 업데이트되면 여기서 알 수 있고, 사본은 그대로라 종료 코드에는 넣지 않습니다.
param(
    [string]$GamePath = $(if ($env:GRC2_GAME_PATH) { $env:GRC2_GAME_PATH } else { 'H:\cychronicle hwa' }),
    [string]$SteamPath = 'H:\steam\steamapps\common\GUNVOLT RECORDS Cychronicle'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$compatSource = Join-Path $repoRoot 'GRC2\Core\GameCompatibility.cs'
$assemblyPath = Join-Path $GamePath 'GUNVOLT_RECORDS_Cychronicle_Data\Managed\Assembly-CSharp.dll'

# steamapps\common\<게임> 아래일 때만 .acf가 있습니다. 사본(H:\cychronicle hwa)은 두 단계 위가 없어 Join-Path가 실패하므로 먼저 확인합니다.
function Get-AcfPath([string]$installPath) {
    $common = Split-Path -Parent $installPath
    if (-not $common) { return $null }
    $steamApps = Split-Path -Parent $common
    if (-not $steamApps) { return $null }
    return Join-Path $steamApps 'appmanifest_2585040.acf'
}
$acfPath = Get-AcfPath $GamePath

if (-not (Test-Path $compatSource)) { Write-Host "GameCompatibility.cs 없음: $compatSource"; exit 2 }
$source = Get-Content -Raw -Encoding UTF8 $compatSource
$testedBuild = [regex]::Match($source, 'TestedBuildId\s*=\s*"(\d+)"').Groups[1].Value
$testedMvid = [regex]::Match($source, 'TestedAssemblyMvid\s*=\s*"([0-9a-fA-F-]+)"').Groups[1].Value
$testedMelon = [regex]::Match($source, 'TestedMelonLoaderVersion\s*=\s*"([0-9.]+)"').Groups[1].Value
$testedUnity = [regex]::Match($source, 'TestedUnityVersion\s*=\s*"([^"]+)"').Groups[1].Value

$ok = $true

if ($acfPath -and (Test-Path $acfPath)) {
    $acf = Get-Content -Raw $acfPath
    $build = [regex]::Match($acf, '"buildid"\s+"(\d+)"', 'IgnoreCase').Groups[1].Value
    $autoUpdate = [regex]::Match($acf, '"AutoUpdateBehavior"\s+"(\d+)"', 'IgnoreCase').Groups[1].Value
    $readOnly = (Get-Item $acfPath).IsReadOnly
    if ($build -eq $testedBuild) {
        Write-Host "[같음] Steam Build ID $build"
    } else {
        Write-Host "[다름] Steam Build ID $build (검증 $testedBuild)"
        $ok = $false
    }
    Write-Host "       AutoUpdateBehavior=$autoUpdate, .acf 읽기 전용=$readOnly"
} else {
    Write-Host "[건너뜀] .acf 없음(독립 사본이거나 비 Steam 설치): $GamePath"
    # 사본을 점검할 때는 스팀 설치본이 업데이트됐는지만 참고로 알립니다(사본 버전에는 영향 없음).
    $steamAcf = Get-AcfPath $SteamPath
    if ($steamAcf -and (Test-Path $steamAcf)) {
        $steamBuild = [regex]::Match((Get-Content -Raw $steamAcf), '"buildid"\s+"(\d+)"', 'IgnoreCase').Groups[1].Value
        if ($steamBuild -eq $testedBuild) {
            Write-Host "[참고] 스팀 설치본 Build ID $steamBuild - 검증 빌드와 같음"
        } else {
            Write-Host "[참고] 스팀 설치본이 Build ID $steamBuild로 업데이트됨(검증 $testedBuild). 사본은 그대로입니다. 새 빌드에 맞추려면 업데이트_대응_가이드.md를 따르세요."
        }
    }
}

if (-not (Test-Path $assemblyPath)) { Write-Host "Assembly-CSharp.dll 없음: $assemblyPath"; exit 2 }
# ReflectionOnly로 읽어 게임 코드를 실행하지 않습니다. 별도 프로세스라 파일 잠금도 남지 않습니다.
$mvid = [Reflection.Assembly]::ReflectionOnlyLoadFrom($assemblyPath).ManifestModule.ModuleVersionId.ToString()
if ($mvid -eq $testedMvid) {
    Write-Host "[같음] Assembly-CSharp MVID $mvid"
} else {
    Write-Host "[다름] Assembly-CSharp MVID $mvid (검증 $testedMvid)"
    $ok = $false
}

# 게임 파일이 그대로여도 MelonLoader나 Unity 엔진이 바뀌면 훅·TMP·영상 동작이 달라질 수 있습니다(2026-11-24).
$melonDll = Join-Path $GamePath 'MelonLoader\net35\MelonLoader.dll'
if (Test-Path $melonDll) {
    $melon = ((Get-Item $melonDll).VersionInfo.ProductVersion -split '\+')[0]
    if ($melon -eq $testedMelon) {
        Write-Host "[같음] MelonLoader $melon"
    } else {
        Write-Host "[다름] MelonLoader $melon (검증 $testedMelon)"
        $ok = $false
    }
} else {
    Write-Host "[건너뜀] MelonLoader.dll 없음: $melonDll"
}

$unityPlayer = Join-Path $GamePath 'UnityPlayer.dll'
if (Test-Path $unityPlayer) {
    $unity = ((Get-Item $unityPlayer).VersionInfo.ProductVersion -split ' ')[0]
    if ($unity -eq $testedUnity) {
        Write-Host "[같음] Unity $unity"
    } else {
        Write-Host "[다름] Unity $unity (검증 $testedUnity)"
        $ok = $false
    }
} else {
    Write-Host "[건너뜀] UnityPlayer.dll 없음: $unityPlayer"
}

if ($ok) {
    Write-Host '기준 빌드와 같습니다.'
    exit 0
}

Write-Host '기준 빌드와 다릅니다. GRC 리드미\maintenance\업데이트_대응_가이드.md 2단계(dotnet test)부터 진행하세요.'
exit 1
