using System;
using System.Collections;
using System.IO;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;


namespace GRC2.Injectors
{
    internal static partial class BgmLoader
    {
        private const int BaseTimeoutFrames = 600;       // 기본 10초 (60fps 기준)
        private const int MaxTimeoutFrames = 3600;       // 최대 60초
        private const int LogIntervalFrames = 120;       // 2초마다 로딩 진행 로그
        private const float FramesPerSecond = 60.0f;
        private const double FramesPerMbPerSec = 6.0;   // 10MB당 60프레임

        public static IEnumerator TryInjectViaSorceField(
            string bgmFilePath,
            Type bgmBeatManagerType,
            object bgmBeatManagerInstance,
            Action<bool> setInjectedCallback,
            Action<bool> setLogShownCallback)
        {
            var sorceField = TryGetSorceField(bgmBeatManagerType, setLogShownCallback);
            if (sorceField != null)
            {
                yield return InjectViaSorceField(bgmFilePath, sorceField, bgmBeatManagerInstance, setInjectedCallback, setLogShownCallback);
                yield break;
            }

            TryCallRequestLoadBgm(bgmBeatManagerType, bgmBeatManagerInstance, setLogShownCallback);
        }

        private static FieldInfo TryGetSorceField(Type type, Action<bool> setLogShownCallback)
        {
            try
            {
                return type.GetField("_sorce", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }
            catch (Exception ex)
            {
                WarnOnce(setLogShownCallback, $"[BgmLoader] _sorce 필드 찾기 실패: {ex.Message}");
                return null;
            }
        }

        private static IEnumerator InjectViaSorceField(
            string bgmFilePath,
            FieldInfo sorceField,
            object bgmBeatManagerInstance,
            Action<bool> setInjectedCallback,
            Action<bool> setLogShownCallback)
        {
            MelonLogger.Msg("[BgmLoader] _sorce 필드 발견, 직접 AudioSource.clip 설정 시도");

            long fileSizeBytes = GetFileSizeBytes(bgmFilePath);
            int maxWaitFrames = CalcTimeoutFrames(fileSizeBytes);

            var fileUrl = "file://" + bgmFilePath.Replace("\\", "/");
            UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(fileUrl, GetAudioType(bgmFilePath));
            request.SendWebRequest();

            yield return WaitForRequest(request, maxWaitFrames);

            if (!request.isDone)
            {
                MelonLogger.Error($"[BgmLoader] BGM 로딩 타임아웃 ({maxWaitFrames / FramesPerSecond:F1}초 경과)");
                request.Dispose();
                yield break;
            }

            if (request.result == UnityWebRequest.Result.Success)
                TrySetAudioSourceClip(request, sorceField, bgmBeatManagerInstance, setInjectedCallback, setLogShownCallback);
            else
                WarnOnce(setLogShownCallback, $"[BgmLoader] BGM 로드 실패: {request.error}");

            request.Dispose();
        }

        private static long GetFileSizeBytes(string bgmFilePath)
        {
            try
            {
                var fileInfo = new FileInfo(bgmFilePath);
                double fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);
                MelonLogger.Msg($"[BgmLoader] 오디오 파일: {Path.GetFileName(bgmFilePath)} ({fileSizeMB:F2} MB)");
                if (fileSizeMB > 50) MelonLogger.Warning($"[BgmLoader] 대용량 오디오 파일 감지 ({fileSizeMB:F2} MB)");
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

        private static void TrySetAudioSourceClip(
            UnityWebRequest request,
            FieldInfo sorceField,
            object bgmBeatManagerInstance,
            Action<bool> setInjectedCallback,
            Action<bool> setLogShownCallback)
        {
            try
            {
                var audioClip = DownloadHandlerAudioClip.GetContent(request);
                if (audioClip == null) return;

                var audioSource = sorceField.GetValue(bgmBeatManagerInstance) as AudioSource;
                if (audioSource == null) return;

                audioSource.clip = audioClip;
                audioSource.Play();
                MelonLogger.Msg("[BgmLoader] BGM 주입 성공 (_sorce 필드 직접 설정)");
                setInjectedCallback?.Invoke(true);
            }
            catch (Exception ex)
            {
                WarnOnce(setLogShownCallback, $"[BgmLoader] _sorce 필드 설정 실패: {ex.Message}");
            }
        }

        private static void TryCallRequestLoadBgm(Type bgmBeatManagerType, object bgmBeatManagerInstance, Action<bool> setLogShownCallback)
        {
            MethodInfo loadBgmMethod = null;
            try
            {
                loadBgmMethod = bgmBeatManagerType.GetMethod("requestLoadBGM",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }
            catch (Exception ex)
            {
                WarnOnce(setLogShownCallback, $"[BgmLoader] requestLoadBGM 메서드 찾기 실패: {ex.Message}");
            }

            if (loadBgmMethod == null) return;

            try
            {
                MelonLogger.Msg("[BgmLoader] requestLoadBGM 메서드 발견");
                loadBgmMethod.Invoke(bgmBeatManagerInstance, null);
            }
            catch (Exception ex)
            {
                WarnOnce(setLogShownCallback, $"[BgmLoader] requestLoadBGM 호출 실패: {ex.Message}");
            }
        }

        private static void WarnOnce(Action<bool> setLogShownCallback, string message)
        {
            if (BgmInjector.LogShown) return;
            MelonLogger.Warning(message);
            setLogShownCallback?.Invoke(true);
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

        
        private static void VerifyInjection(object bgmBeatManagerInstance, Type bgmBeatManagerType, AudioClip audioClip, string fileName)
        {
            // 주입 후 즉시 AudioClip 확인
            var getAudioClipMethod = bgmBeatManagerType.GetMethod("getAudioClip", 
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (getAudioClipMethod != null)
            {
                try
                {
                    var injectedClip = getAudioClipMethod.Invoke(bgmBeatManagerInstance, null) as AudioClip;
                    if (injectedClip != null)
                    {
                        var injectedClipName = string.IsNullOrEmpty(injectedClip.name) ? fileName : injectedClip.name;
                        MelonLogger.Msg($"[BgmLoader] ✓ 주입 후 getAudioClip() 결과: {injectedClipName}, 길이: {injectedClip.length:F3}초 ({injectedClip.samples} 샘플)");
                        if (injectedClip.length == audioClip.length)
                        {
                            MelonLogger.Msg("[BgmLoader] ✓✓ BGM 주입 확인: 주입된 클립과 일치합니다!");
                        }
                        else
                        {
                            var clipNameForLog = string.IsNullOrEmpty(audioClip.name) ? fileName : audioClip.name;
                            MelonLogger.Warning($"[BgmLoader] ⚠ BGM 주입 불일치: 주입한 클립({clipNameForLog})과 다른 클립({injectedClipName})이 설정되어 있습니다.");
                        }
                    }
                    else
                    {
                        MelonLogger.Error("[BgmLoader] ✗ 주입 후 getAudioClip() 결과: null - 주입 실패 가능성");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[BgmLoader] 주입 후 AudioClip 확인 실패: {ex.Message}");
                }
            }
            
            // AudioSource 필드에서도 확인
            var fields = bgmBeatManagerType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(AudioSource))
                {
                    try
                    {
                        var audioSource = field.GetValue(bgmBeatManagerInstance) as AudioSource;
                        if (audioSource != null)
                        {
                            if (audioSource.clip != null)
                            {
                                var sourceClipName = string.IsNullOrEmpty(audioSource.clip.name) ? fileName : audioSource.clip.name;
                                MelonLogger.Msg($"[BgmLoader] AudioSource({field.Name}).clip: {sourceClipName}, 길이: {audioSource.clip.length:F3}초");
                                if (audioSource.clip.length == audioClip.length)
                                {
                                    MelonLogger.Msg($"[BgmLoader] ✓✓ AudioSource({field.Name})에 주입된 클립이 설정되어 있습니다!");
                                }
                            }
                            else
                            {
                                MelonLogger.Warning($"[BgmLoader] ⚠ AudioSource({field.Name}).clip: null");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[BgmLoader] AudioSource({field.Name}) 확인 실패: {ex.Message}");
                    }
                }
            }
        }

        
        private static void RequestPlayAudio(object bgmBeatManagerInstance, Type bgmBeatManagerType)
        {
            try
            {
                var requestPlayMethod = bgmBeatManagerType.GetMethod("requestPlayAudio", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (requestPlayMethod != null)
                {
                    requestPlayMethod.Invoke(bgmBeatManagerInstance, null);
                    MelonLogger.Msg("[BgmLoader] BGM 재생 요청 완료");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgmLoader] requestPlayAudio 호출 실패: {ex.Message}");
            }
        }
}
}
