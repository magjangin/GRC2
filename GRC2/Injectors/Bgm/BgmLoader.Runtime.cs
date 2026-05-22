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
    

        public static IEnumerator TryInjectViaSorceField(
            string bgmFilePath, 
            Type bgmBeatManagerType,
            object bgmBeatManagerInstance,
            Action<bool> setInjectedCallback,
            Action<bool> setLogShownCallback)
        {
            FieldInfo sorceField = null;
            try
            {
                sorceField = bgmBeatManagerType.GetField("_sorce", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }
            catch (Exception ex)
            {
                if (setLogShownCallback != null && !BgmInjector.LogShown)
                {
                    MelonLogger.Warning($"[BgmLoader] _sorce 필드 찾기 실패: {ex.Message}");
                    setLogShownCallback(true);
                }
            }
            
            if (sorceField != null)
            {
                MelonLogger.Msg("[BgmLoader] _sorce 필드 발견, 직접 AudioSource.clip 설정 시도");
                
                // 파일 크기 확인
                long fileSizeBytes = 0;
                try
                {
                    var fileInfo = new FileInfo(bgmFilePath);
                    fileSizeBytes = fileInfo.Length;
                    var fileSizeMB = fileSizeBytes / (1024.0 * 1024.0);
                    MelonLogger.Msg($"[BgmLoader] 오디오 파일: {Path.GetFileName(bgmFilePath)} ({fileSizeMB:F2} MB)");
                    
                    if (fileSizeMB > 50)
                    {
                        MelonLogger.Warning($"[BgmLoader] 대용량 오디오 파일 감지 ({fileSizeMB:F2} MB)");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[BgmLoader] 파일 크기 확인 실패: {ex.Message}");
                }
                
                var fileUrl = "file://" + bgmFilePath.Replace("\\", "/");
                UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(fileUrl, GetAudioType(bgmFilePath));
                request.SendWebRequest();
                
                // 로딩 타임아웃 설정
                int maxWaitFrames = 600; // 기본 10초
                if (fileSizeBytes > 0)
                {
                    var fileSizeMB = fileSizeBytes / (1024.0 * 1024.0);
                    var additionalFrames = (int)(fileSizeMB / 10.0 * 60.0);
                    maxWaitFrames = Math.Min(600 + additionalFrames, 3600);
                }
                
                int waitCount = 0;
                int lastLogFrame = 0;
                while (!request.isDone && waitCount < maxWaitFrames)
                {
                    waitCount++;
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
                    MelonLogger.Error($"[BgmLoader] BGM 로딩 타임아웃 ({elapsedSeconds:F1}초 경과)");
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
                            var audioSource = sorceField.GetValue(bgmBeatManagerInstance) as AudioSource;
                            if (audioSource != null)
                            {
                                audioSource.clip = audioClip;
                                audioSource.Play();
                                MelonLogger.Msg("[BgmLoader] BGM 주입 성공 (_sorce 필드 직접 설정)");
                                setInjectedCallback?.Invoke(true);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (setLogShownCallback != null && !BgmInjector.LogShown)
                        {
                            MelonLogger.Warning($"[BgmLoader] _sorce 필드 설정 실패: {ex.Message}");
                            setLogShownCallback(true);
                        }
                    }
                }
                else
                {
                    if (setLogShownCallback != null && !BgmInjector.LogShown)
                    {
                        MelonLogger.Warning($"[BgmLoader] BGM 로드 실패: {request.error}");
                        setLogShownCallback(true);
                    }
                }
                
                request.Dispose();
                yield break;
            }
            
            // requestLoadBGM 메서드 찾기
            MethodInfo loadBgmMethod = null;
            try
            {
                loadBgmMethod = bgmBeatManagerType.GetMethod("requestLoadBGM", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }
            catch (Exception ex)
            {
                if (setLogShownCallback != null && !BgmInjector.LogShown)
                {
                    MelonLogger.Warning($"[BgmLoader] requestLoadBGM 메서드 찾기 실패: {ex.Message}");
                    setLogShownCallback(true);
                }
            }
            
            if (loadBgmMethod != null)
            {
                try
                {
                    MelonLogger.Msg("[BgmLoader] requestLoadBGM 메서드 발견");
                    loadBgmMethod.Invoke(bgmBeatManagerInstance, null);
                }
                catch (Exception ex)
                {
                    if (setLogShownCallback != null && !BgmInjector.LogShown)
                    {
                        MelonLogger.Warning($"[BgmLoader] requestLoadBGM 호출 실패: {ex.Message}");
                        setLogShownCallback(true);
                    }
                }
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
