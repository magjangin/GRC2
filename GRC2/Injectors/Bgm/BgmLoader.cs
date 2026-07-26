using System;
using System.Collections;
using System.IO;
using System.Reflection;
using IntiCreates;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;
using GRC2.Helpers;

namespace GRC2.Injectors
{
    /// <summary>
    /// BGM 파일 로딩 및 주입 로직을 담당하는 클래스
    /// </summary>
    internal static class BgmLoader
    {
        private const int BaseTimeoutFrames = 600;       // 기본 10초 (60fps 기준)
        private const int MaxTimeoutFrames = 3600;       // 최대 60초
        private const int LogIntervalFrames = 120;       // 2초마다 로딩 진행 로그
        private const float FramesPerSecond = 60.0f;
        private const double FramesPerMbPerSec = 6.0;    // 10MB당 60프레임

        /// <summary>
        /// AudioClip을 로드하고 cBGMBeatManager에 주입합니다.
        /// </summary>
        public static IEnumerator LoadAndInjectAudioClip(
            string bgmFilePath,
            cBGMBeatManager bgmBeatManager,
            Action<bool> setInjectedCallback)
        {
            long fileSizeBytes = GetFileSizeBytes(bgmFilePath);
            int maxWaitFrames = CalcTimeoutFrames(fileSizeBytes);

            var fileUrl = "file://" + bgmFilePath.Replace("\\", "/");
            UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(fileUrl, GetAudioType(bgmFilePath));
            request.SendWebRequest();

            yield return WaitForRequest(request, maxWaitFrames);

            if (!request.isDone)
            {
                MelonLogger.Error($"[BgmLoader] BGM 로딩 타임아웃 (최대 {maxWaitFrames / FramesPerSecond:F1}초)");
                request.Dispose();
                yield break;
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                InjectLoadedClip(request, bgmFilePath, bgmBeatManager, setInjectedCallback);
            }
            else
            {
                MelonLogger.Warning($"[BgmLoader] BGM 로드 실패: {request.error}");
            }

            request.Dispose();
        }

        private static void InjectLoadedClip(
            UnityWebRequest request,
            string bgmFilePath,
            cBGMBeatManager bgmBeatManager,
            Action<bool> setInjectedCallback)
        {
            try
            {
                var audioClip = DownloadHandlerAudioClip.GetContent(request);
                if (audioClip == null)
                {
                    return;
                }

                var fileName = Path.GetFileNameWithoutExtension(bgmFilePath);
                if (string.IsNullOrEmpty(audioClip.name))
                {
                    TrySetClipName(audioClip, fileName);
                }

                var clipNameForLog = string.IsNullOrEmpty(audioClip.name) ? fileName : audioClip.name;
                MelonLogger.Msg($"[BgmLoader] 주입할 BGM: {clipNameForLog}, 길이: {audioClip.length:F3}초 ({audioClip.samples} 샘플)");

                bgmBeatManager.setClip(audioClip, false);
                VerifyInjection(bgmBeatManager, audioClip, fileName);
                bgmBeatManager.requestPlayAudio();

                MelonLogger.Msg("[BgmLoader] BGM 주입 성공");

                // 주입된 BGM 길이로 게임 종료 시간 설정
                BgmFinishTimeManager.SetFinishTime(audioClip.length);

                setInjectedCallback?.Invoke(true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgmLoader] BGM 주입 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// UnityWebRequest로 만든 AudioClip은 name이 비어있을 수 있어 파일 이름으로 채웁니다.
        /// AudioClip.name setter는 런타임 생성 클립에 반영되지 않으므로 내부 필드를 사용합니다.
        /// </summary>
        private static void TrySetClipName(AudioClip audioClip, string fileName)
        {
            try
            {
                var nameField = typeof(AudioClip).GetField("m_Name", BindingFlags.NonPublic | BindingFlags.Instance);
                nameField?.SetValue(audioClip, fileName);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[BgmLoader] LoadAndInjectAudioClip", "AudioClip.m_Name 설정 실패(무시)");
            }
        }

        private static long GetFileSizeBytes(string bgmFilePath)
        {
            try
            {
                var fileInfo = new FileInfo(bgmFilePath);
                double fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);
                MelonLogger.Msg($"[BgmLoader] 오디오 파일: {Path.GetFileName(bgmFilePath)} ({fileSizeMB:F2} MB)");

                if (fileSizeMB > 200)
                {
                    MelonLogger.Error($"[BgmLoader] 매우 큰 오디오 파일 ({fileSizeMB:F2} MB). 메모리 부족 가능성이 있습니다. WAV 대신 OGG/MP3 사용을 권장합니다.");
                }
                else if (fileSizeMB > 50)
                {
                    MelonLogger.Warning($"[BgmLoader] 대용량 오디오 파일 감지 ({fileSizeMB:F2} MB). 메모리 사용량이 높을 수 있습니다.");
                }

                return fileInfo.Length;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgmLoader] 파일 크기 확인 실패: {ex.Message}");
                return 0;
            }
        }

        private static int CalcTimeoutFrames(long fileSizeBytes)
        {
            if (fileSizeBytes <= 0) return BaseTimeoutFrames;
            double fileSizeMB = fileSizeBytes / (1024.0 * 1024.0);
            int additional = (int)(fileSizeMB * FramesPerMbPerSec);
            return Math.Min(BaseTimeoutFrames + additional, MaxTimeoutFrames);
        }

        private static IEnumerator WaitForRequest(UnityWebRequest request, int maxWaitFrames)
        {
            int waitCount = 0;
            int lastLogFrame = 0;
            while (!request.isDone && waitCount < maxWaitFrames)
            {
                waitCount++;
                if (waitCount - lastLogFrame >= LogIntervalFrames)
                {
                    float elapsed = waitCount / FramesPerSecond;
                    float progress = request.downloadProgress * 100.0f;
                    MelonLogger.Msg($"[BgmLoader] BGM 로딩 중... ({elapsed:F1}초 경과, {progress:F1}%)");
                    lastLogFrame = waitCount;
                }
                yield return null;
            }
        }

        public static AudioType GetAudioType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            switch (extension)
            {
                case ".mp3":
                    return AudioType.MPEG;
                case ".wav":
                    return AudioType.WAV;
                case ".ogg":
                    return AudioType.OGGVORBIS;
                default:
                    return AudioType.UNKNOWN;
            }
        }

        private static void VerifyInjection(cBGMBeatManager bgmBeatManager, AudioClip audioClip, string fileName)
        {
            var injectedClip = bgmBeatManager.getAudioClip();
            if (injectedClip == null)
            {
                MelonLogger.Error("[BgmLoader] ✗ 주입 후 getAudioClip() 결과: null - 주입 실패 가능성");
                return;
            }

            var injectedClipName = string.IsNullOrEmpty(injectedClip.name) ? fileName : injectedClip.name;
            if (injectedClip.length == audioClip.length)
            {
                MelonLogger.Msg($"[BgmLoader] ✓ BGM 주입 확인: {injectedClipName}, 길이: {injectedClip.length:F3}초 ({injectedClip.samples} 샘플)");
            }
            else
            {
                var clipNameForLog = string.IsNullOrEmpty(audioClip.name) ? fileName : audioClip.name;
                MelonLogger.Warning($"[BgmLoader] ⚠ BGM 주입 불일치: 주입한 클립({clipNameForLog})과 다른 클립({injectedClipName})이 설정되어 있습니다.");
            }
        }
    }
}
