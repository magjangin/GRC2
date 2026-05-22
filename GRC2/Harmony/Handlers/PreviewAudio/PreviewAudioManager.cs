using MelonLoader;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GRC2.Core;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// 프리뷰 BGM 및 환경음 관리 클래스
    /// - 음소거/복원 기능
    /// - 지속적인 모니터링
    /// </summary>
    public static partial class PreviewAudioManager
    {
        // 음소거할 AudioSource 참조 및 원래 볼륨 저장
        private static Dictionary<AudioSource, float> _mutedAudioSourcesWithVolume = new Dictionary<AudioSource, float>();
        private static bool _isMonitoringAudio = false;
        
        /// <summary>
        /// 모니터링 상태 확인
        /// </summary>
        public static bool IsMonitoring => _isMonitoringAudio;
        
        /// <summary>
        /// 음소거된 AudioSource 수
        /// </summary>
        public static int MutedCount => _mutedAudioSourcesWithVolume.Count;

        /// <summary>
        /// 프리뷰와 환경음 확실하게 중지
        /// </summary>
        public static void StopPreviewAndAmbient()
        {
            try
            {
                int stoppedCount = 0;
                _mutedAudioSourcesWithVolume.Clear();
                
                // 모든 AudioSource에서 프리뷰와 환경음 찾아서 확실하게 중지
                AudioSource[] allAudioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
                if (allAudioSources != null && allAudioSources.Length > 0)
                {
                    foreach (var audioSource in allAudioSources)
                    {
                        if (audioSource == null)
                            continue;
                        
                        string clipName = audioSource.clip != null ? (string.IsNullOrEmpty(audioSource.clip.name) ? "" : audioSource.clip.name) : "";
                        
                        // 프리뷰 BGM 또는 환경음인지 확인
                        bool shouldStop = false;
                        string audioType = "";
                        
                        if (!string.IsNullOrEmpty(clipName))
                        {
                            if (clipName.Contains("PCD_PREVIEW_") || clipName.Contains("PREVIEW"))
                            {
                                shouldStop = true;
                                audioType = "프리뷰 BGM";
                            }
                            else if (clipName.Contains("PCD_AMB_") || clipName.Contains("AMB") || clipName.Contains("Ambient"))
                            {
                                shouldStop = true;
                                audioType = "환경음";
                            }
                        }
                        
                        if (shouldStop)
                        {
                            // 1. 원래 볼륨 저장
                            float originalVolume = audioSource.volume;
                            if (!_mutedAudioSourcesWithVolume.ContainsKey(audioSource))
                            {
                                _mutedAudioSourcesWithVolume[audioSource] = originalVolume;
                            }
                            
                            // 2. 재생 중이면 중지
                            if (audioSource.isPlaying)
                            {
                                audioSource.Stop();
                            }
                            // 3. 볼륨을 0으로 설정 (재시작되어도 안 들림)
                            audioSource.volume = 0f;
                            // 4. 음소거 설정
                            audioSource.mute = true;
                            
                            string goName = audioSource.gameObject != null ? audioSource.gameObject.name : "?";
                            MelonLogger.Msg($"[PreviewAudioManager] 🛑 {audioType} 중지/음소거: clip={clipName}, gameObject={goName} (원래 볼륨: {originalVolume:F2})");
                            stoppedCount++;
                        }
                    }
                }
                
                // cSoundManager의 mPreviewAudioSorce도 확실하게 중지
                Type soundManagerType = ReflectionHelper.FindType("IntiCreates.cSoundManager");
                if (soundManagerType != null)
                {
                    UnityEngine.Object[] soundManagers = UnityEngine.Object.FindObjectsOfType(soundManagerType);
                    if (soundManagers != null && soundManagers.Length > 0)
                    {
                        object soundManagerInstance = soundManagers[0];
                        Type managerType = soundManagerInstance.GetType();
                        
                        // mPreviewAudioSorce 중지
                        FieldInfo previewSourceField = managerType.GetField("mPreviewAudioSorce", 
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        
                        if (previewSourceField != null)
                        {
                            object sourceValue = previewSourceField.GetValue(soundManagerInstance);
                            if (sourceValue is AudioSource audioSource && audioSource != null)
                            {
                                float originalVolume = audioSource.volume;
                                if (!_mutedAudioSourcesWithVolume.ContainsKey(audioSource))
                                {
                                    _mutedAudioSourcesWithVolume[audioSource] = originalVolume;
                                }
                                
                                if (audioSource.isPlaying)
                                {
                                    audioSource.Stop();
                                }
                                audioSource.volume = 0f;
                                audioSource.mute = true;
                                string goNameP = audioSource.gameObject != null ? audioSource.gameObject.name : "?";
                                string clipP = audioSource.clip != null ? audioSource.clip.name : "(null)";
                                MelonLogger.Msg($"[PreviewAudioManager] 🛑 프리뷰 AudioSource 중지/음소거 (mPreviewAudioSorce [게임 필드명]): gameObject={goNameP}, clip={clipP}, 원래 볼륨: {originalVolume:F2}");
                                stoppedCount++;
                            }
                        }
                        
                        // mAmbientAudioSorce도 음소거 (환경음 전용)
                        FieldInfo ambientSourceField = managerType.GetField("mAmbientAudioSorce", 
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        
                        if (ambientSourceField != null)
                        {
                            object sourceValue = ambientSourceField.GetValue(soundManagerInstance);
                            if (sourceValue is AudioSource ambientSource && ambientSource != null)
                            {
                                float originalVolume = ambientSource.volume;
                                if (!_mutedAudioSourcesWithVolume.ContainsKey(ambientSource))
                                {
                                    _mutedAudioSourcesWithVolume[ambientSource] = originalVolume;
                                }
                                
                                if (ambientSource.isPlaying)
                                {
                                    ambientSource.Stop();
                                }
                                ambientSource.volume = 0f;
                                ambientSource.mute = true;
                                string goNameA = ambientSource.gameObject != null ? ambientSource.gameObject.name : "?";
                                string clipA = ambientSource.clip != null ? ambientSource.clip.name : "(null)";
                                MelonLogger.Msg($"[PreviewAudioManager] 🛑 환경음 AudioSource 중지/음소거 (mAmbientAudioSorce [게임 필드명]): gameObject={goNameA}, clip={clipA}, 원래 볼륨: {originalVolume:F2}");
                                stoppedCount++;
                            }
                        }
                    }
                }
                
                if (stoppedCount > 0)
                {
                    MelonLogger.Msg($"[PreviewAudioManager] ✅ 총 {stoppedCount}개의 오디오 소스 중지/음소거 완료");
                }
                
                // 지속적인 모니터링 시작 (재시작 방지)
                if (!_isMonitoringAudio)
                {
                    _isMonitoringAudio = true;
                    MelonLoader.MelonCoroutines.Start(MonitorAndMuteAudioCoroutine());
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[PreviewAudioManager] ⚠️ 프리뷰/환경음 중지 중 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 프리뷰/환경음이 다시 재생되는 것을 지속적으로 모니터링하고 음소거
        /// </summary>
        
        /// <summary>
        /// 음소거했던 AudioSource들 복원 (일반 곡 선택 시)
        /// </summary>
        
        /// <summary>
        /// 상태 초기화
        /// </summary>
        /// <summary>
        /// 디버깅: 씬 내 모든 AudioSource 상태 로그 (키음 버그 추적용)
        /// </summary>
    }
}


