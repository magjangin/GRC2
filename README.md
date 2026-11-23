# GRC2

GRC2는 GUNVOLT RECORDS Cychronicle에 커스텀 차트, BGM, BGA, 아트워크, 메타데이터를 주입하기 위한 MelonLoader/Harmony 모드 프로젝트입니다.

## 문서

아래 문서부터 확인하세요.

- [문서 인덱스](GRC%20리드미/README.md)
- [현재 훅 맵](GRC%20리드미/maintenance/HOOK_MAP.md)
- [게임 업데이트 대응 가이드](GRC%20리드미/maintenance/업데이트_대응_가이드.md): 스팀 업데이트 뒤 무엇이 깨졌는지 찾는 절차
- [알려진 문제](GRC%20리드미/maintenance/알려진_문제.md): 아직 고치지 않은 문제. 게임을 새로 설치하거나 PC를 옮기기 전에 A절을 먼저 확인하세요.
- [차트 작성 규칙](GRC%20리드미/bms/BMS_파싱_및_변환_로직_가이드.md): BMS 채널·노트 값, BGM은 48kHz, `info.txt`는 UTF-8
- [Harmony 레이어 README](GRC2/Harmony/README.md)

정리 전 문서나 이전에 생성된 메모는 [GRC 리드미/archive](GRC%20리드미/archive)에 보관되어 있습니다. 이 문서들은 현재 기준 문서가 아니라 과거 맥락을 확인하는 용도로 참고하세요.

## 사용자용 요약

게임 폴더(`GUNVOLT RECORDS Cychronicle\`) 기준입니다.

| 위치 | 용도 |
| --- | --- |
| `Mods\GRC2.dll` | 모드 본체 |
| `hwa\<곡 폴더>\` | 곡 하나: `*.bms`, `info.txt`(UTF-8), BGM(`.ogg/.mp3/.wav`, 48kHz), 아트워크(`.png/.jpg`), BGA(`.mp4`) |
| `hwa\fonts\` | (선택) 게임 폰트에 없는 글자를 그릴 폰트(`.ttf/.otf/.ttc`). 없으면 윈도우 폰트(YaHei·JhengHei·Yu Gothic·맑은 고딕)를 씁니다. 곡 폴더로 스캔하지 않습니다. |
| `savecustomkey\config.txt` | AutoPlay·판정바·BlockSave 등 설정. 고치면 다음 플레이부터 적용. `VerboseLog=1`이면 곡·노트별 상세 로그를 남깁니다(기본 0). |

- **곡 수:** 약 7,900곡까지(MusicID 54~7999, 게임 예약 값 제외, 외부 맵 파일 없이 메모리에서 자동 배정). 예전 상한은 458곡이었습니다.
- **곡 추가·수정:** 게임을 켠 채 `hwa`에 곡을 넣거나 `info.txt`·BMS·아트워크를 고치면, 곡 선택 화면에 다시 들어올 때 반영됩니다.
- **긴 곡 제목:** 곡 선택 화면에서 선택한 곡의 제목이 칸보다 길면 옆으로 흘러가며 끝까지 보여 줍니다.
- **글자 깨짐(□):** 게임 폰트에 없는 글자(간체 한자 등)는 위 fallback 폰트로 그립니다.

## 소스 구성

- 메인 모드 소스: `GRC2/`
- 테스트: `GRC2.Tests/`
- `bin/obj`를 제외한 현재 관리 C# 소스: `GRC2` 61개, `GRC2.Tests` 12개
- SDK 스타일 프로젝트 규칙에 따라 `GRC2/` 및 `GRC2.Tests/` 하위의 `.cs` 소스는 자동으로 컴파일에 포함됩니다.
- 현재 훅 소유 구조는 [GRC 리드미/maintenance/HOOK_MAP.md](GRC%20리드미/maintenance/HOOK_MAP.md)에 정리되어 있습니다.

## 검증

솔루션 전체 또는 테스트 프로젝트를 지정하여 테스트를 실행할 수 있습니다.

```powershell
dotnet test GRC2.sln --logger "console;verbosity=normal"
```

또는 `test_debug.bat`을 실행하거나 아래처럼 테스트 프로젝트를 직접 지정하세요(2026-11-23 기준 158개 통과). 설치된 게임이 검증 빌드와 같은지는 `tools\check_game_version.ps1`로 게임을 켜지 않고 확인합니다.

```powershell
dotnet test GRC2.Tests\GRC2.Tests.csproj --no-restore --logger "console;verbosity=normal"
```
