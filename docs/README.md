# GRC2 Documentation

This folder is the current home for project documentation. Documents are grouped by maintenance use, not by original generation date.

## Start Here

- [Current Hook Map](maintenance/HOOK_MAP.md): current Harmony and MelonLoader hook ownership.
- [Harmony Layer README](../GRC2/Harmony/README.md): source-folder-level guide for `GRC2/Harmony`.
- [Legacy README](archive/README_legacy.md): older comprehensive notes kept for reference only.

## Folders

### `architecture`

Game and mod structure notes:

- `게임_아키텍처_개요.md`
- `게임_코드_분석.md`
- `게임_클래스_구조.md`
- `게임_플로우_및_메서드.md`
- `씬_관리_및_라이프사이클.md`

### `systems`

Runtime systems and user-visible mod behavior:

- `앨범_관리_시스템_분석.md`
- `커스텀_곡_주입_시스템_분석.md`
- `커스텀_에셋_로딩_시스템.md`
- `BGM_BGA_관리_시스템.md`
- `텍스트_패치_시스템_분석.md`
- `아티스트_ID_기반_시스템_분석.md`

### `bms`

BMS parsing, note conversion, and note processing:

- `BMS_파서_내부_구조_분석.md`
- `BMS_파싱_및_변환_로직_가이드.md`
- `노트_생성_및_변환_파이프라인.md`
- `홀드_노트_처리_가이드.md`

### `harmony`

Harmony, reflection, enum, and game type notes:

- `Harmony_패칭_시스템_상세_가이드.md`
- `리플렉션_및_필드_접근_시스템.md`
- `Enum_및_타입_시스템_관리.md`

### `maintenance`

Current maintenance references, cleanup history, performance, debugging, and timing:

- `HOOK_MAP.md`
- `게임_종료_로직.md`
- `게임_종료_시간_조정_가이드.md`
- `성능_분석_및_최적화_권장사항.md`
- `성능_최적화_기법_종합_가이드.md`
- `최적화_완료_보고서.md`
- `코루틴_및_비동기_처리_패턴.md`

### `archive`

Historical or pre-cleanup documents. These may mention removed code such as `HarmonyHookManager`, `BgaVideoHooks`, `GameTypeInspector`, or `NoteArrayJsonDumper`.

Use archive documents only when investigating old decisions.

## Current Cleanup Baseline

As of 2026-05-15:

- Deleted no-op and diagnostic hook files.
- Removed disabled note-array JSON dumping and field inspection helpers.
- Removed disabled music-scroll sort/filter/update/get-cell logging hooks.
- Reduced `CharactorLoadPatcher` to the dynamic prefix factory still used by `AudioClipPatcher`.
- Current managed source count is 113 files under `GRC2/`, excluding `bin/obj`.
