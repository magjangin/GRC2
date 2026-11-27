using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MelonLoader;

// 어셈블리에 대한 일반 정보는 다음 특성 집합을 통해 
// 제어됩니다. 어셈블리와 관련된 정보를 수정하려면
// 이러한 특성 값을 변경하세요.
[assembly: AssemblyTitle("GRC2")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("GRC2")]
[assembly: AssemblyCopyright("Copyright ©  2025")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// ComVisible을 false로 설정하면 이 어셈블리의 형식이 COM 구성 요소에 
// 표시되지 않습니다. COM에서 이 어셈블리의 형식에 액세스하려면
// 해당 형식에 대해 ComVisible 특성을 true로 설정하세요.
[assembly: ComVisible(false)]

// 이 프로젝트가 COM에 노출되는 경우 다음 GUID는 typelib의 ID를 나타냅니다.
[assembly: Guid("3d3a249f-d5f7-4f00-bc94-ccf741981ef6")]

// 어셈블리의 버전 정보는 다음 네 가지 값으로 구성됩니다.
//
//      주 버전
//      부 버전 
//      빌드 번호
//      수정 버전
//
[assembly: AssemblyVersion("0.7.6.0")]
[assembly: AssemblyFileVersion("0.7.6.0")]

// MelonLoader 모드 정보
[assembly: MelonInfo(typeof(GRC2.Core.SceneDetector), "GUNVOLT RECORDS Cychronicle", "0.7.6", "화영왕")]
[assembly: MelonGame("INTI CREATES", "GUNVOLT RECORDS Cychronicle")]
[assembly: MelonColor(0, 255, 255, 255)] // Cyan (R, G, B, A)

// MelonLoader는 기본으로 모드 어셈블리의 [HarmonyPatch]를 자동 적용합니다. 모드도 SceneDetector.InitializeHarmony에서
// 직접 적용(실패 시 클래스별 격리)하므로, 이 특성이 없으면 모든 훅이 두 번 걸립니다(알려진_문제 P4: 노트 변환 2회,
// 판정바 틱 2회, 제목 이중 치환). 자동 적용을 끄고 모드 쪽 적용만 남깁니다.
[assembly: MelonLoader.HarmonyDontPatchAll]

// 테스트 프로젝트가 internal 판정 함수(ModLog.ShouldLogOnChange 등)를 확인합니다.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("GRC2.Tests")]
