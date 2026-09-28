# BGM/BGA 관리 시스템

플레이 씬에서 커스텀 BGM(음악)과 BGA(배경 영상)를 넣고 서로 맞추는 방식을 설명합니다.
곡 선택 화면의 프리뷰 BGM은 [커스텀_에셋_로딩_시스템.md](커스텀_에셋_로딩_시스템.md)를 봅니다.
게임 쪽 설명은 `Decompiled/`의 원본 코드로 확인한 내용입니다. (2026-09-28 기준)

## 게임 원본: `cBGMBeatManager`

플레이 BGM을 재생하는 컴포넌트입니다(`[RequireComponent(typeof(AudioSource))]`). 모드가 쓰는 메서드:

| 메서드 | 원본 동작 |
|--------|-----------|
| `setClip(AudioClip clip, bool isNeedLoad)` | `AudioSource.clip = clip`, `isNeedLoad`면 `LoadAudioData()` |
| `getAudioClip()` / `getAudioSorce()` | 현재 clip / `AudioSource` |
| `requestPlayAudio()` | `AudioSource.Play()` (처음부터 재생) |
| `getAudioSorceCurrentTime()` | `AudioSource.time` |
| `getCurrentSample()` | `AudioSource.timeSamples` |

**노트 시계는 BGM 자체입니다.** `cFairyModeNotesManager.getCurrentJudgeSample()`은
`getCurrentSample()` + `mNoteMusicData.offset` + 옵션 판정 보정값이고, 노트의 `perfectSample`과 이 값을 비교합니다.
그래서:

- 모드가 BGM을 바꾸고 처음부터 다시 틀어도 노트는 새 BGM을 따라갑니다.
- `timeSamples`는 **BGM 파일의 샘플레이트** 단위이고, 모드는 `perfectSample`을 48000Hz로 계산합니다.
  **BGM은 반드시 48kHz로 저장해야** 노트가 밀리지 않습니다([알려진_문제.md](../maintenance/알려진_문제.md) B1).

## 주입 루프: `BgmBgaInjector`

[BgmBgaInjector.cs](../../GRC2/Injectors/BgmBgaInjector.cs). `SceneDetector`가 씬마다 시작/정지합니다.

| 씬 | 호출 |
|----|------|
| `FairyModeScene`, `PlayMovieScene`, `RenderCutinScene` | `StartInjection(isPlayScene: true)` |
| `RythmGameResultScene`, `MusicSelectScene*`, `SoundPlayerScene`, `MoviePlayer_MovieSelect` | `StopInjection()` + `ResetPlaySceneState()` |
| 그 밖의 씬 | 플레이 씬 상태가 아니면 `StartInjection(isPlayScene: false)` |

코루틴은 **2초마다**:

1. `ShouldInjectCustomContent()`가 false면 건너뜁니다.
2. 현재 앨범의 BGA/BGM 경로로 갱신합니다(앨범에 파일이 없으면 이전 경로 유지 → 알려진 문제 C1).
3. 플레이 씬이면 BGA 주입 → BGM 주입을 시도합니다.
4. 둘 다 끝나면 멈춥니다. BGA가 없는 앨범이면 루프가 씬이 끝날 때까지 계속 돕니다(가벼운 확인만 함).

실제 로그(2026-08-03)에서는 `FairyModeScene` 로드 후 약 3초 뒤 BGA·BGM이 주입되고, 그 2초 뒤 종료 시간이 적용됐습니다.

## BGM 주입: `BgmInjector` → `BgmLoader`

1. `cBGMBeatManager`를 찾습니다(`FindObjectOfType`, 없으면 모든 `AudioSource`에서 역으로). 곡당 최대 10번 시도합니다.
2. `UnityWebRequestMultimedia.GetAudioClip("file://…", 확장자별 AudioType)`로 **전체를 메모리에** 읽습니다(스트리밍 아님).
   타임아웃은 기본 600프레임 + 10MB당 60프레임, 최대 3600프레임입니다(60fps 기준 10~60초).
   50MB 이상이면 경고, 200MB 이상이면 오류 로그를 남깁니다.
3. `setClip(clip, false)` → `requestPlayAudio()`로 교체·재생합니다.
4. `BgmFinishTimeManager.SetFinishTime(clip.length)`로 곡 종료 시간을 정합니다([게임_종료_시간_조정_가이드.md](../maintenance/게임_종료_시간_조정_가이드.md)).

## BGA 주입: `BgaInjector`

1. 활성화된 모든 `VideoPlayer`를 찾습니다(없으면 조용히 종료 → 다음 루프에서 재시도).
2. 모두에 `source = Url`, `url = "file://…"`, `isLooping = true`를 넣고 `Prepare()`합니다.
3. 준비될 때까지 기다립니다(기본 300프레임 + 100MB당 60프레임, 최대 3600프레임). 일부만 준비돼도 준비된 것부터 재생합니다.
4. 0.5초 뒤 실제로 재생 중이면 주입 완료로 보고:
   - `BgaCrossfadePatch.TryStopSwapCoroutine()`: 원본의 BGA 자동 교체 코루틴(`coUpdateMovieInARow`)을 멈춥니다.
     멈추지 않으면 곡 끝 약 0.67초 전에 원본 영상으로 돌아갑니다.
   - `BgaBgmSyncManager.StartSync()`: BGM과 동기화를 시작합니다.

## BGA-BGM 동기화: `BgaBgmSyncManager`

[BgaBgmSyncManager.cs](../../GRC2/Injectors/BgaBgmSyncManager.cs)

- BGM 소스: `cBGMBeatManager.getAudioSorce()` 우선, 없으면 이름/재생 상태로 점수를 매겨 가장 그럴듯한 `AudioSource`.
- BGM 시간: `AudioSource.time`, 0이면 `getAudioSorceCurrentTime()`.
- 맞추는 식: `videoPlayer.time = bgmTime % videoPlayer.length` (영상이 짧으면 반복).
- 0.1초마다 확인합니다. BGM이 막 재생되기 시작하면 즉시 맞추고, 이후에는 차이가 0.1초를 넘고 마지막 동기화로부터
  BGM 시간이 1초 이상 지났을 때만 다시 맞춥니다(탐색으로 인한 끊김 최소화).
- 모드는 일시정지를 따로 처리하지 않습니다. 재개 후 차이가 생기면 다음 확인에서 맞춰집니다.
- 로그 태그는 예전 이름(`[BGAPlayerHook]`, `[BGABGMSyncHook]`)을 그대로 씁니다.

## 파일 준비 요령

- BGM: 48kHz ogg 권장. mp3/wav도 되지만 wav는 크고, 앨범의 키음 wav와 섞여 잘못 선택될 수 있습니다(ogg가 있으면 ogg 우선).
- BGA: mp4. BGM과 길이가 달라도 됩니다(짧으면 반복).
- 폴더에 BGA/BGM이 없으면 이전 곡의 것이 남을 수 있으니, 곡마다 넣어 두는 것이 안전합니다(알려진 문제 C1).

## 관련 문서

- [커스텀_에셋_로딩_시스템.md](커스텀_에셋_로딩_시스템.md)
- [게임_종료_시간_조정_가이드.md](../maintenance/게임_종료_시간_조정_가이드.md)
- [코루틴_및_비동기_처리_패턴.md](../maintenance/코루틴_및_비동기_처리_패턴.md)
- [HOOK_MAP.md](../maintenance/HOOK_MAP.md)
