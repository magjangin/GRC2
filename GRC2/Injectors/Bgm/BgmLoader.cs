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
        
        /// <summary>
        /// _sorce 필드를 통해 직접 AudioSource.clip을 설정합니다.
        /// </summary>
        
        /// <summary>
        /// 파일 확장자에 따라 AudioType을 반환합니다.
        /// </summary>
    }
}

