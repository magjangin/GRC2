# GRC2 문서 인덱스

이 폴더가 프로젝트 문서의 현재 위치입니다. 문서는 만든 날짜가 아니라 유지보수 용도별로 묶었습니다.
(2026-09-29 기준. 변경 이력은 [정리_이력.md](maintenance/정리_이력.md)와 HOOK_MAP의 Cleanup Log에 있습니다.)

## 먼저 볼 문서

- [HOOK_MAP.md](maintenance/HOOK_MAP.md): 현재 Harmony/MelonLoader 훅의 소유 파일·목적·제거 위험. **훅 소유 관계의 정본**입니다.
- [알려진_문제.md](maintenance/알려진_문제.md): 아직 고치지 않은 문제와 고치는 방향
  (게임을 새로 설치하거나 PC를 옮기기 전에 A절을 먼저 읽으세요).
- [게임_코드_분석.md](architecture/게임_코드_분석.md): 모든 현재 문서의 주제별 인덱스.
- [BMS 차트 작성 규칙](bms/BMS_파싱_및_변환_로직_가이드.md): 차트, BGM, `info.txt`가 갖춰야 할 형식.
- [Harmony 레이어 README](../GRC2/Harmony/README.md): `GRC2/Harmony` 소스 폴더 안내.
- [README_legacy.md](archive/README_legacy.md): 예전 종합 문서. 참고용으로만 남겼습니다.

모드는 `Assembly-CSharp`를 컴파일 타임 타입으로 직접 참조합니다. 비공개 필드는 `AccessTools.FieldRefAccess`로 접근하고,
문자열로 찾는 메서드는 `cRythmGameManager.setPauseButtonPusable`(`SceneDetector`의 `AccessTools.MethodDelegate`) 하나뿐입니다.
`Decompiled/`(게임 원본 디컴파일)는 저작권상 git에 없으므로, 문서에서 "`Decompiled/`로 확인"이라고 적은 내용은 로컬에서만 재현할 수 있습니다.

## 폴더

### `architecture`

게임과 모드 구조:

- `게임_아키텍처_개요.md`
- `게임_코드_분석.md`
- `게임_클래스_구조.md`
- `게임_플로우_및_메서드.md`
- `씬_관리_및_라이프사이클.md`

### `systems`

런타임 시스템과 사용자에게 보이는 모드 동작:

- `앨범_관리_시스템_분석.md`
- `커스텀_곡_주입_시스템_분석.md`
- `커스텀_에셋_로딩_시스템.md`
- `BGM_BGA_관리_시스템.md`
- `텍스트_패치_시스템_분석.md`
- `아티스트_ID_기반_시스템_분석.md`
- `터치_판정_영역_시스템_분석.md`

### `bms`

BMS 파싱, 노트 변환, 노트 처리:

- `BMS_파서_내부_구조_분석.md`
- `BMS_파싱_및_변환_로직_가이드.md`
- `노트_생성_및_변환_파이프라인.md`
- `홀드_노트_처리_가이드.md`

### `harmony`

Harmony 패칭과 게임 enum:

- `Harmony_패칭_시스템_상세_가이드.md`
- `Enum_및_타입_시스템_관리.md`

### `maintenance`

유지보수 기준 문서, 알려진 문제, 정리 이력, 타이밍:

- `HOOK_MAP.md`
- `알려진_문제.md`
- `정리_이력.md`: 소스 파일 수와 리팩터링 단계의 시간순 기록
- `게임_종료_로직.md`
- `게임_종료_시간_조정_가이드.md`
- `코루틴_및_비동기_처리_패턴.md`

### `archive`

정리 전이거나 과거 맥락을 위한 문서입니다. 삭제된 코드(`HarmonyHookManager`, `BgaVideoHooks`, `GameTypeInspector`,
`ReflectionHelper`, `FieldAccessHelper`, `NoteArrayJsonDumper` 등)를 언급할 수 있고, **현재 코드와 맞지 않습니다.**
각 문서 맨 위의 `[보관]`/`[폐기됨]` 머리말을 먼저 확인하세요.

- 리플렉션 계층만 설명해서 2026-09-28에 옮긴 문서: `리플렉션_및_필드_접근_시스템.md`, `성능_분석_및_최적화_권장사항.md`,
  `성능_최적화_기법_종합_가이드.md`, `최적화_완료_보고서.md`
- 예전 종합·비유 문서: `README_legacy.md`, `비유와_수학공식_가이드.md`, `코드_리뷰_및_비유.md`,
  `에러_처리_및_디버깅_시스템_legacy.md`

옛 결정을 조사할 때만 아카이브를 보세요.
