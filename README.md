# GRC2

GRC2는 GUNVOLT RECORDS Cychronicle에 커스텀 차트, BGM, BGA, 아트워크, 메타데이터를 주입하기 위한 MelonLoader/Harmony 모드 프로젝트입니다.

## 문서

아래 문서부터 확인하세요.

- [문서 인덱스](docs/README.md)
- [현재 훅 맵](docs/maintenance/HOOK_MAP.md)
- [Harmony 레이어 README](GRC2/Harmony/README.md)

정리 전 문서나 이전에 생성된 메모는 [docs/archive](docs/archive)에 보관되어 있습니다. 이 문서들은 현재 기준 문서가 아니라 과거 맥락을 확인하는 용도로 참고하세요.

## 소스 구성

- 메인 모드 소스: `GRC2/`
- 테스트: `GRC2.Tests/`
- `bin/obj`를 제외한 현재 관리 C# 소스: `GRC2` 111개, `GRC2.Tests` 1개
- 현재 훅 소유 구조는 [docs/maintenance/HOOK_MAP.md](docs/maintenance/HOOK_MAP.md)에 정리되어 있습니다.

## 검증

아래 명령으로 테스트를 실행할 수 있습니다.

```powershell
dotnet test GRC2.Tests\GRC2.Tests.csproj --no-restore --logger "console;verbosity=normal"
```
