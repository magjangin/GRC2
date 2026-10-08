using System;
using System.Collections;
using System.IO;
using GRC2.Helpers;
using IntiCreates;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;

namespace GRC2.Injectors
{
    /// <summary>
    /// BGM 파일 로딩 및 주입 로직을 담당하는 클래스
    /// </summary>
    internal static class BgmLoader
    {
        // 시간은 프레임 수가 아니라 실제 시간(초)으로 셉니다. 프레임으로 세면 144Hz 화면에서 2.4배 짧아집니다(F3).
        private const float BaseTimeoutSeconds = 10f;     // 기본 10초
        private const float MaxTimeoutSeconds = 60f;      // 최대 60초
        private const float LogIntervalSeconds = 2f;      // 2초마다 로딩 진행 로그
        private const double SecondsPerMb = 0.1;          // 10MB당 1초

        /// <summary>
        /// AudioClip을 로드하고 cBGMBeatManager에 주입합니다.
        /// </summary>
        public static IEnumerator LoadAndInjectAudioClip(
            string bgmFilePath,
            cBGMBeatManager bgmBeatManager,
            Action<bool> setInjectedCallback)
        {
            long fileSizeBytes = GetFileSizeBytes(bgmFilePath);
            float maxWaitSeconds = CalcTimeoutSeconds(fileSizeBytes);

            // 문자열을 이어 붙이면 경로에 '#', '%', '?'가 있을 때 URL이 잘못 해석됩니다.
            // 곡 선택 프리뷰(CustomBgmPlayer)가 같은 파일을 이미 이 방식으로 읽습니다.
            var fileUrl = new Uri(Path.GetFullPath(bgmFilePath)).AbsoluteUri;
            UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(fileUrl, GetAudioType(bgmFilePath));

            // 로딩 중에 StopInjection으로 코루틴이 멈춰도 요청이 남지 않도록 finally에서 해제합니다(H7).
            try
            {
                request.SendWebRequest();

                yield return WaitForRequest(request, maxWaitSeconds);

                if (!request.isDone)
                {
                    MelonLogger.Error($"[BgmLoader] BGM 로딩 타임아웃 (최대 {maxWaitSeconds:F1}초)");
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
            }
            finally
            {
                request.Dispose();
            }
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
                    // UnityWebRequest로 만든 클립은 name이 비어 있을 수 있어 파일 이름으로 채웁니다.
                    audioClip.name = fileName;
                }

                var clipNameForLog = string.IsNullOrEmpty(audioClip.name) ? fileName : audioClip.name;
                MelonLogger.Msg($"[BgmLoader] 주입할 BGM: {clipNameForLog}, 길이: {audioClip.length:F3}초 ({audioClip.samples} 샘플)");

                // 노트 시계는 NoteSampleTime.SampleRate(48000Hz)로 계산합니다. 다른 샘플레이트의 BGM이면 노트가 그 비율만큼 밀립니다(B1).
                if (audioClip.frequency != NoteSampleTime.SampleRate)
                {
                    MelonLogger.Error($"[BgmLoader] ⚠ BGM 샘플레이트가 {audioClip.frequency}Hz입니다. 노트 타이밍이 맞으려면 48000Hz로 다시 저장해야 합니다.");
                }

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

        private static float CalcTimeoutSeconds(long fileSizeBytes)
        {
            if (fileSizeBytes <= 0) return BaseTimeoutSeconds;
            double fileSizeMB = fileSizeBytes / (1024.0 * 1024.0);
            double additional = fileSizeMB * SecondsPerMb;
            return (float)Math.Min(BaseTimeoutSeconds + additional, MaxTimeoutSeconds);
        }

        private static IEnumerator WaitForRequest(UnityWebRequest request, float maxWaitSeconds)
        {
            float start = Time.realtimeSinceStartup;
            float nextLogAt = start + LogIntervalSeconds;
            while (!request.isDone && Time.realtimeSinceStartup - start < maxWaitSeconds)
            {
                if (Time.realtimeSinceStartup >= nextLogAt)
                {
                    float elapsed = Time.realtimeSinceStartup - start;
                    float progress = request.downloadProgress * 100.0f;
                    MelonLogger.Msg($"[BgmLoader] BGM 로딩 중... ({elapsed:F1}초 경과, {progress:F1}%)");
                    nextLogAt += LogIntervalSeconds;
                }
                yield return null;
            }
        }

        public static AudioType GetAudioType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
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
