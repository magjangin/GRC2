using System;
using System.Collections;
using System.IO;
using System.Linq;
using MelonLoader;
using UnityEngine;
using UnityEngine.Video;

namespace GRC2.Injectors
{
    internal static class BgaInjector
    {
        private static bool _bgaInjected = false;

        public static bool IsInjected => _bgaInjected;

        public static void Reset()
        {
            _bgaInjected = false;
            BgaBgmSyncManager.Reset();
        }

        public static IEnumerator TryInjectBgaCoroutine(string bgaFilePath)
        {
            VideoPlayer videoPlayer = null;
            VideoPlayer[] videoPlayers = null;
            try
            {
                videoPlayers = UnityEngine.Object.FindObjectsOfType<VideoPlayer>();
                if (videoPlayers == null || videoPlayers.Length == 0)
                {
                    // VideoPlayer를 찾을 수 없으면 조용히 종료 (플레이 씬이 아닐 수 있음)
                    yield break;
                }

            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgaInjector] VideoPlayer 찾기 실패: {ex.Message}");
                MelonLogger.Warning($"[BgaInjector] 스택 트레이스: {ex.StackTrace}");
                yield break;
            }

            // 활성화된 VideoPlayer 목록 준비
            var activeVideoPlayers = new System.Collections.Generic.List<VideoPlayer>();
            foreach (var vp in videoPlayers)
            {
                if (vp != null && vp.gameObject.activeInHierarchy)
                {
                    activeVideoPlayers.Add(vp);
                    // VideoPlayer 활성화 확인
                    if (!vp.gameObject.activeInHierarchy)
                    {
                        MelonLogger.Msg($"[BgaInjector] {vp.gameObject.name} 활성화");
                        vp.gameObject.SetActive(true);
                    }
                }
            }

            if (activeVideoPlayers.Count == 0)
            {
                // 활성화된 VideoPlayer가 없으면 조용히 종료 (플레이 씬이 아닐 수 있음)
                yield break;
            }

            // videoPlayer 변수는 첫 번째로 설정 (하위 호환성)
            videoPlayer = activeVideoPlayers[0];

            // 파일 경로를 file:// URL로 변환
            var fileUrl = "file://" + bgaFilePath.Replace("\\", "/");
            
            // 파일 크기 확인 (대용량 지원)
            long fileSizeBytes = 0;
            try
            {
                var fileInfo = new FileInfo(bgaFilePath);
                fileSizeBytes = fileInfo.Length;
                var fileSizeMB = fileSizeBytes / (1024.0 * 1024.0);
                MelonLogger.Msg($"[BgaInjector] BGA 주입 시도: {Path.GetFileName(bgaFilePath)} ({fileSizeMB:F2} MB)");
                
                if (fileSizeMB > 500)
                {
                    MelonLogger.Warning($"[BgaInjector] 대용량 비디오 파일 감지 ({fileSizeMB:F2} MB). Prepare에 시간이 걸릴 수 있습니다.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgaInjector] 파일 크기 확인 실패: {ex.Message}");
            }

            MelonLogger.Msg($"[BgaInjector] 커스텀 BGA를 {activeVideoPlayers.Count}개 VideoPlayer에 설정");

            // 모든 VideoPlayer에 BGA 설정
            foreach (var vp in activeVideoPlayers)
            {
                try
                {
                    MelonLogger.Msg($"[BgaInjector] {vp.gameObject.name}에 BGA 설정 중...");
                    vp.source = VideoSource.Url;
                    vp.url = fileUrl;
                    vp.isLooping = true; // 반복 재생
                    vp.Prepare();
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[BgaInjector] {vp.gameObject.name} BGA 설정 실패: {ex.Message}");
                }
            }

            // Prepare 완료 대기 (파일 크기에 따라 타임아웃 동적 조정)
            // 기본 5초 + 파일 크기당 추가 시간 (100MB당 1초, 최대 60초)
            int maxWaitFrames = 300; // 기본 5초 (60fps 기준)
            if (fileSizeBytes > 0)
            {
                var fileSizeMB = fileSizeBytes / (1024.0 * 1024.0);
                var additionalFrames = (int)(fileSizeMB / 100.0 * 60.0); // 100MB당 1초
                maxWaitFrames = Math.Min(300 + additionalFrames, 3600); // 최대 60초
            }
            
            int waitCount = 0;
            int lastLogFrame = 0;
            bool allPrepared = false;
            
            while (!allPrepared && waitCount < maxWaitFrames)
            {
                waitCount++;
                allPrepared = true;
                
                foreach (var vp in activeVideoPlayers)
                {
                    if (!vp.isPrepared)
                    {
                        allPrepared = false;
                        break;
                    }
                }
                
                // 1초마다 진행 상황 로깅 (대용량 파일용)
                if (waitCount - lastLogFrame >= 60)
                {
                    var elapsedSeconds = waitCount / 60.0f;
                    int preparedCount = 0;
                    foreach (var vp in activeVideoPlayers)
                    {
                        if (vp.isPrepared) preparedCount++;
                    }
                    MelonLogger.Msg($"[BgaInjector] BGA Prepare 대기 중... ({elapsedSeconds:F1}초 경과, {preparedCount}/{activeVideoPlayers.Count} 준비 완료)");
                    lastLogFrame = waitCount;
                }
                yield return null;
            }

            if (!allPrepared)
            {
                var elapsedSeconds = waitCount / 60.0f;
                MelonLogger.Warning($"[BgaInjector] 일부 BGA 준비 시간 초과 ({elapsedSeconds:F1}초 경과, 최대 {maxWaitFrames / 60.0f:F1}초)");
                // 일부만 준비되어도 계속 진행
            }

            MelonLogger.Msg("[BgaInjector] BGA 준비 완료, 재생 시작");
            
            // 모든 VideoPlayer 재생
            int playingCount = 0;
            foreach (var vp in activeVideoPlayers)
            {
                try
                {
                    if (vp.isPrepared)
                    {
                        MelonLogger.Msg($"[BgaInjector] {vp.gameObject.name} 재생 시작");
                        vp.Play();
                        playingCount++;
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

            // 재생 확인
            yield return new WaitForSeconds(0.5f);
            
            int actuallyPlayingCount = 0;
            foreach (var vp in activeVideoPlayers)
            {
                if (vp != null && vp.isPlaying)
                {
                    actuallyPlayingCount++;
                    MelonLogger.Msg($"[BgaInjector] {vp.gameObject.name} 재생 중 ✓");
                }
            }
            
            if (actuallyPlayingCount > 0)
            {
                var bgaFileName = Path.GetFileName(bgaFilePath);
                MelonLogger.Msg($"[BGAPlayerHook] BGA 교체 완료 및 재생 시작: {bgaFileName}");
                MelonLogger.Msg($"[BgaInjector] BGA 재생 중 ({actuallyPlayingCount}/{activeVideoPlayers.Count}개 VideoPlayer)");
                _bgaInjected = true;

                // BGA와 BGM 동기화 시작
                var playingVideoPlayers = activeVideoPlayers.Where(vp => vp != null && vp.isPlaying).ToArray();
                MelonLogger.Msg($"[BgaInjector] 동기화 시작 시도: {playingVideoPlayers.Length}개 VideoPlayer 재생 중");
                if (playingVideoPlayers.Length > 0)
                {
                    MelonLogger.Msg("[BgaInjector] BgaBgmSyncManager.StartSync 호출");
                    BgaBgmSyncManager.StartSync(playingVideoPlayers);
                }
                else
                {
                    MelonLogger.Warning("[BgaInjector] 재생 중인 VideoPlayer가 없어 동기화를 시작할 수 없습니다");
                }
            }
            else
            {
                MelonLogger.Warning("[BgaInjector] BGA 재생 실패 - VideoPlayer가 재생되지 않습니다");
            }
        }

        public static void ResetSync()
        {
            BgaBgmSyncManager.Reset();
        }
    }
}

