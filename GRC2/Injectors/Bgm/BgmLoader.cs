using System;
using System.Collections;
using System.IO;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;
using GRC2.Helpers;

namespace GRC2.Injectors
{
    /// <summary>
    /// BGM 파일 로딩 및 주입 로직을 담당하는 클래스
    /// </summary>
    internal static partial class BgmLoader
    {
        /// <summary>
        /// AudioClip을 로드하고 cBGMBeatManager에 주입합니다.
        /// </summary>
        public static IEnumerator LoadAndInjectAudioClip(
            string bgmFilePath, 
            MethodInfo setClipMethod, 
            ParameterInfo[] parameters, 
            Type bgmBeatManagerType,
            object bgmBeatManagerInstance,
            Action<bool> setInjectedCallback)
        {
            // 파일 크기 확인 (대용량 지원)
            long fileSizeBytes = 0;
            try
            {
                var fileInfo = new FileInfo(bgmFilePath);
                fileSizeBytes = fileInfo.Length;
                var fileSizeMB = fileSizeBytes / (1024.0 * 1024.0);
                MelonLogger.Msg($"[BgmLoader] setClip은 AudioClip을 받습니다. 오디오 파일 로드 시도: {Path.GetFileName(bgmFilePath)} ({fileSizeMB:F2} MB)");
                
                // 대용량 파일 경고
                if (fileSizeMB > 50)
                {
                    MelonLogger.Warning($"[BgmLoader] 대용량 오디오 파일 감지 ({fileSizeMB:F2} MB). 메모리 사용량이 높을 수 있습니다.");
                }
                if (fileSizeMB > 200)
                {
                    MelonLogger.Error($"[BgmLoader] 매우 큰 오디오 파일 ({fileSizeMB:F2} MB). 메모리 부족 가능성이 있습니다. WAV 대신 OGG/MP3 사용을 권장합니다.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgmLoader] 파일 크기 확인 실패: {ex.Message}");
            }
            
            // 파일 경로를 file:// URL로 변환
            var fileUrl = "file://" + bgmFilePath.Replace("\\", "/");
            
            // UnityWebRequestMultimedia.GetAudioClip 사용
            UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(fileUrl, GetAudioType(bgmFilePath));
            request.SendWebRequest();
            
            // 로딩 타임아웃 설정 (파일 크기에 따라 동적 조정)
            int maxWaitFrames = 600; // 기본 10초 (60fps 기준)
            if (fileSizeBytes > 0)
            {
                var fileSizeMB = fileSizeBytes / (1024.0 * 1024.0);
                var additionalFrames = (int)(fileSizeMB / 10.0 * 60.0); // 10MB당 1초
                maxWaitFrames = Math.Min(600 + additionalFrames, 3600); // 최대 60초
            }
            
            int waitCount = 0;
            int lastLogFrame = 0;
            while (!request.isDone && waitCount < maxWaitFrames)
            {
                waitCount++;
                // 2초마다 진행 상황 로깅 (대용량 파일용)
                if (waitCount - lastLogFrame >= 120)
                {
                    var elapsedSeconds = waitCount / 60.0f;
                    var progress = request.downloadProgress * 100.0f;
                    MelonLogger.Msg($"[BgmLoader] BGM 로딩 중... ({elapsedSeconds:F1}초 경과, {progress:F1}%)");
                    lastLogFrame = waitCount;
                }
                yield return null;
            }
            
            if (!request.isDone)
            {
                var elapsedSeconds = waitCount / 60.0f;
                MelonLogger.Error($"[BgmLoader] BGM 로딩 타임아웃 ({elapsedSeconds:F1}초 경과, 최대 {maxWaitFrames / 60.0f:F1}초)");
                request.Dispose();
                yield break;
            }
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var audioClip = DownloadHandlerAudioClip.GetContent(request);
                    if (audioClip != null)
                    {
                        // AudioClip의 name이 비어있으면 파일 이름으로 설정 시도
                        var fileName = Path.GetFileNameWithoutExtension(bgmFilePath);
                        if (string.IsNullOrEmpty(audioClip.name))
                        {
                            try
                            {
                                // Reflection으로 name 설정 시도
                                var nameField = typeof(AudioClip).GetField("m_Name", 
                                    BindingFlags.NonPublic | BindingFlags.Instance);
                                if (nameField != null)
                                {
                                    nameField.SetValue(audioClip, fileName);
                                }
                            }
                            catch (Exception ex)
                            {
                                ErrorLogger.LogWarning(ex, "[BgmLoader] LoadAndInjectAudioClip", "AudioClip.m_Name 설정 실패(무시)");
                            }
                        }
                        
                        // 로그 출력용 이름
                        var clipNameForLog = string.IsNullOrEmpty(audioClip.name) ? fileName : audioClip.name;
                        
                        // 주입할 BGM 파일의 길이 확인
                        MelonLogger.Msg($"[BgmLoader] 주입할 BGM 파일 길이: {audioClip.length:F3}초 ({audioClip.samples} 샘플)");
                        
                        // setClip 메서드 호출
                        MelonLogger.Msg($"[BgmLoader] setClip 호출 시작 - AudioClip: {clipNameForLog}, 길이: {audioClip.length:F3}초");
                        if (parameters.Length == 2 && parameters[1].ParameterType == typeof(bool))
                        {
                            setClipMethod.Invoke(bgmBeatManagerInstance, new object[] { audioClip, false });
                            MelonLogger.Msg("[BgmLoader] setClip(audioClip, false) 호출 완료");
                        }
                        else
                        {
                            setClipMethod.Invoke(bgmBeatManagerInstance, new object[] { audioClip });
                            MelonLogger.Msg("[BgmLoader] setClip(audioClip) 호출 완료");
                        }
                        
                        // 주입 후 확인
                        VerifyInjection(bgmBeatManagerInstance, bgmBeatManagerType, audioClip, fileName);
                        
                        // requestPlayAudio 메서드 호출 (재생 시작)
                        RequestPlayAudio(bgmBeatManagerInstance, bgmBeatManagerType);
                        
                        MelonLogger.Msg("[BgmLoader] BGM 주입 성공 (setClip with AudioClip)");
                        
                        // 주입된 BGM 길이로 게임 종료 시간 설정
                        BgmFinishTimeManager.SetFinishTime(audioClip.length, bgmBeatManagerType);
                        
                        setInjectedCallback?.Invoke(true);
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[BgmLoader] setClip 호출 실패: {ex.Message}");
                }
            }
            else
            {
                MelonLogger.Warning($"[BgmLoader] BGM 로드 실패: {request.error}");
            }
            
            request.Dispose();
        }
            }

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
