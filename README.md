# GRC2

GRC2는 GUNVOLT RECORDS Cychronicle에 커스텀 차트, BGM, BGA, 아트워크, 메타데이터를 주입하기 위한 MelonLoader/Harmony 모드 프로젝트입니다.

## 문서

아래 문서부터 확인하세요.

- [문서 인덱스](GRC%20리드미/README.md)
- [현재 훅 맵](GRC%20리드미/maintenance/HOOK_MAP.md)
- [알려진 문제](GRC%20리드미/maintenance/알려진_문제.md): 아직 고치지 않은 문제. 게임을 새로 설치하거나 PC를 옮기기 전에 A절을 먼저 확인하세요.
- [차트 작성 규칙](GRC%20리드미/bms/BMS_파싱_및_변환_로직_가이드.md): BMS 채널·노트 값, BGM은 48kHz, `info.txt`는 UTF-8
- [Harmony 레이어 README](GRC2/Harmony/README.md)

정리 전 문서나 이전에 생성된 메모는 [GRC 리드미/archive](GRC%20리드미/archive)에 보관되어 있습니다. 이 문서들은 현재 기준 문서가 아니라 과거 맥락을 확인하는 용도로 참고하세요.

## 소스 구성

- 메인 모드 소스: `GRC2/`
- 테스트: `GRC2.Tests/`
- `bin/obj`를 제외한 현재 관리 C# 소스: `GRC2` 50개, `GRC2.Tests` 5개
- `GRC2.csproj`는 `EnableDefaultCompileItems=false`라 소스를 추가하면 `<Compile Include>` 항목도 같이 넣어야 합니다(빠뜨려도 빌드는 통과하고 그 파일만 조용히 빠집니다)
- 현재 훅 소유 구조는 [GRC 리드미/maintenance/HOOK_MAP.md](GRC%20리드미/maintenance/HOOK_MAP.md)에 정리되어 있습니다.

## 검증

아래 명령으로 테스트를 실행할 수 있습니다.

```powershell
dotnet test GRC2.Tests\GRC2.Tests.csproj --no-restore --logger "console;verbosity=normal"
```

`GRC2.sln`에는 테스트 프로젝트가 들어 있지 않아서, `test_debug.bat`(`dotnet test GRC2.sln`)은 현재 테스트를 하나도 실행하지 않고 성공으로 끝납니다. 위 명령처럼 테스트 프로젝트를 직접 지정하세요(2026-10-08 기준 72개 통과).
