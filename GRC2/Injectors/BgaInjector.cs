using System;
using System.Collections;
using System.IO;
using System.Linq;
using GRC2.Harmony;
using IntiCreates;
using MelonLoader;
using GRC2.Helpers;
using UnityEngine;
using UnityEngine.Video;

namespace GRC2.Injectors
{
    internal static class BgaInjector
    {
        private static bool _bgaInjected = false;
        private static readonly char[] BgaSpecialChars = { '#', '%', '?' };

        public static bool IsInjected => _bgaInjected;

        public static void Reset()
        {
            _bgaInjected = false;
            BgaBgmSyncManager.Reset();
        }

        public static IEnumerator TryInjectBgaCoroutine(string bgaFilePath)
        {
            var activeVideoPlayers = FindActiveVideoPlayers();
            if (activeVideoPlayers.Count == 0)
                yield break;

            string fileUrl = BuildBgaFileUrl(bgaFilePath);
            long fileSizeBytes = GetBgaFileSizeBytes(bgaFilePath);

            MelonLogger.Msg($"[BgaInjector] 커스텀 BGA를 {activeVideoPlayers.Count}개 VideoPlayer에 설정");
            PrepareVideoPlayers(activeVideoPlayers, fileUrl);

            yield return WaitForPlayersPreparedCoroutine(activeVideoPlayers, fileSizeBytes);

            MelonLogger.Msg("[BgaInjector] BGA 준비 완료, 재생 시작");
            StartPreparedVideoPlayers(activeVideoPlayers);

            yield return new WaitForSeconds(0.5f);

            var playingVideoPlayers = CollectPlayingVideoPlayers(activeVideoPlayers);
            if (playingVideoPlayers.Length > 0)
            {
                MelonLogger.Msg($"[BGAPlayerHook] BGA 교체 완료 및 재생 시작: {Path.GetFileName(bgaFilePath)}");
                MelonLogger.Msg($"[BgaInjector] BGA 재생 중 ({playingVideoPlayers.Length}/{activeVideoPlayers.Count}개 VideoPlayer)");
                _bgaInjected = true;

                StopOriginalCrossfade();
                StartBgmSync(playingVideoPlayers);
            }
            else
            {
                MelonLogger.Warning("[BgaInjector] BGA 재생 실패 - VideoPlayer가 재생되지 않습니다");
            }
        }

        private static System.Collections.Generic.List<VideoPlayer> FindActiveVideoPlayers()
        {
            var activeVideoPlayers = new System.Collections.Generic.List<VideoPlayer>();
            try
            {
                var videoPlayers = UnityEngine.Object.FindObjectsOfType<VideoPlayer>();
                if (videoPlayers == null || videoPlayers.Length == 0)
                    return activeVideoPlayers;

                // FindObjectsOfType<VideoPlayer>()는 기본적으로 비활성 오브젝트를 포함하지 않으므로,
                // 여기서 걸러지는 vp는 전부 이미 activeInHierarchy == true입니다.
                foreach (var vp in videoPlayers)
                {
                    if (vp != null && vp.gameObject.activeInHierarchy)
                        activeVideoPlayers.Add(vp);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgaInjector] VideoPlayer 찾기 실패: {ex.Message}");
                MelonLogger.Warning($"[BgaInjector] 스택 트레이스: {ex.StackTrace}");
            }

            return activeVideoPlayers;
        }

        private static string BuildBgaFileUrl(string bgaFilePath)
        {
            // '#', '%', '?'가 든 경로는 문자열을 이어 붙인 URL이 잘못 해석됩니다(I1). 그런 경로만 인코딩된 URI를 쓰고,
            // 나머지는 예전 방식을 그대로 둡니다(인게임에서 재생이 확인된 형태).
            var fileUrl = "file://" + bgaFilePath.Replace("\\", "/");
            if (bgaFilePath.IndexOfAny(BgaSpecialChars) >= 0)
            {
                try { fileUrl = new Uri(Path.GetFullPath(bgaFilePath)).AbsoluteUri; }
                catch (Exception ex) { MelonLogger.Warning($"[BgaInjector] 경로 URI 변환 실패, 기본 방식 사용: {ex.Message}"); }
            }
            return fileUrl;
        }

        private static long GetBgaFileSizeBytes(string bgaFilePath)
        {
            try
            {
                var fileInfo = new FileInfo(bgaFilePath);
                long fileSizeBytes = fileInfo.Length;
                var fileSizeMB = fileSizeBytes / (1024.0 * 1024.0);
                MelonLogger.Msg($"[BgaInjector] BGA 주입 시도: {Path.GetFileName(bgaFilePath)} ({fileSizeMB:F2} MB)");

                if (fileSizeMB > 500)
                    MelonLogger.Warning($"[BgaInjector] 대용량 비디오 파일 감지 ({fileSizeMB:F2} MB). Prepare에 시간이 걸릴 수 있습니다.");

                return fileSizeBytes;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgaInjector] 파일 크기 확인 실패: {ex.Message}");
                return 0;
            }
        }

        private static void PrepareVideoPlayers(System.Collections.Generic.List<VideoPlayer> activeVideoPlayers, string fileUrl)
        {
            foreach (var vp in activeVideoPlayers)
            {
                try
                {
                    ModLog.Verbose($"[BgaInjector] {vp.gameObject.name}에 BGA 설정 중...");
                    vp.source = VideoSource.Url;
                    vp.url = fileUrl;
                    vp.isLooping = true;
                    vp.Prepare();
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[BgaInjector] {vp.gameObject.name} BGA 설정 실패: {ex.Message}");
                }
            }
        }

        private static IEnumerator WaitForPlayersPreparedCoroutine(
            System.Collections.Generic.List<VideoPlayer> activeVideoPlayers,
            long fileSizeBytes)
        {
            // 기본 5초 + 파일 크기당 추가 시간 (100MB당 1초, 최대 60초).
            // 프레임이 아니라 실제 시간으로 셉니다. 프레임으로 세면 144Hz 화면에서 2.4배 짧아집니다(F3).
            float maxWaitSeconds = 5f;
            if (fileSizeBytes > 0)
            {
                var fileSizeMB = fileSizeBytes / (1024.0 * 1024.0);
                maxWaitSeconds = Math.Min(5f + (float)(fileSizeMB / 100.0), 60f);
            }

            float waitStart = Time.realtimeSinceStartup;
            float nextLogAt = waitStart + 1f;
            bool allPrepared = false;

            while (!allPrepared && Time.realtimeSinceStartup - waitStart < maxWaitSeconds)
            {
                // 매 프레임 LINQ All(vp => ...) 람다 할당을 피하기 위해 인덱서 루프로 검사(P21).
                allPrepared = AreAllVideoPlayersPrepared(activeVideoPlayers);

                if (!allPrepared && Time.realtimeSinceStartup >= nextLogAt)
                {
                    var elapsed = Time.realtimeSinceStartup - waitStart;
                    int preparedCount = CountPreparedVideoPlayers(activeVideoPlayers);
                    ModLog.Verbose($"[BgaInjector] BGA Prepare 대기 중... ({elapsed:F1}초 경과, {preparedCount}/{activeVideoPlayers.Count} 준비 완료)");
                    nextLogAt += 1f;
                }
                yield return null;
            }

            if (!allPrepared)
            {
                var elapsedSeconds = Time.realtimeSinceStartup - waitStart;
                MelonLogger.Warning($"[BgaInjector] 일부 BGA 준비 시간 초과 ({elapsedSeconds:F1}초 경과, 최대 {maxWaitSeconds:F1}초)");
            }
        }

        private static bool AreAllVideoPlayersPrepared(System.Collections.Generic.List<VideoPlayer> players)
        {
            for (int i = 0; i < players.Count; i++)
            {
                var vp = players[i];
                if (vp != null && !vp.isPrepared)
                    return false;
            }
            return true;
        }

        private static int CountPreparedVideoPlayers(System.Collections.Generic.List<VideoPlayer> players)
        {
            int count = 0;
            for (int i = 0; i < players.Count; i++)
            {
                var vp = players[i];
                if (vp != null && vp.isPrepared)
                    count++;
            }
            return count;
        }

        private static void StartPreparedVideoPlayers(System.Collections.Generic.List<VideoPlayer> activeVideoPlayers)
        {
            foreach (var vp in activeVideoPlayers)
            {
                try
                {
                    if (vp.isPrepared)
                    {
                        ModLog.Verbose($"[BgaInjector] {vp.gameObject.name} 재생 시작");
                        vp.Play();
                    }
                    else
                    {
                        MelonLogger.Warning($"[BgaInjector] {vp.gameObject.name} 준비되지 않아 재생 건너뜀");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[BgaInjector] {vp.gameObject.name} 재생 시작 실패: {ex.Message}");
                }
            }
        }

        private static VideoPlayer[] CollectPlayingVideoPlayers(System.Collections.Generic.List<VideoPlayer> activeVideoPlayers)
        {
            var playing = activeVideoPlayers.Where(vp => vp != null && vp.isPlaying).ToArray();
            foreach (var vp in playing)
                ModLog.Verbose($"[BgaInjector] {vp.gameObject.name} 재생 중 ✓");
            return playing;
        }

        private static void StopOriginalCrossfade()
        {
            // 게임 자체 BGA 크로스페이드 코루틴이 곡 막바지에 원본 클립으로 되돌리는 것을 방지
            try
            {
                var movieSceneManager = UnityEngine.Object.FindObjectOfType<cPlayMovieSceneManager>();
                if (movieSceneManager != null)
                    BgaCrossfadePatch.TryStopSwapCoroutine(movieSceneManager);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgaInjector] 크로스페이드 코루틴 정지 시도 오류: {ex.Message}");
            }
        }

        private static void StartBgmSync(VideoPlayer[] playingVideoPlayers)
        {
            ModLog.Verbose($"[BgaInjector] 동기화 시작 시도: {playingVideoPlayers.Length}개 VideoPlayer 재생 중");
            if (playingVideoPlayers.Length > 0)
            {
                ModLog.Verbose("[BgaInjector] BgaBgmSyncManager.StartSync 호출");
                BgaBgmSyncManager.StartSync(playingVideoPlayers);
            }
            else
            {
                MelonLogger.Warning("[BgaInjector] 재생 중인 VideoPlayer가 없어 동기화를 시작할 수 없습니다");
            }
        }
    }
}
