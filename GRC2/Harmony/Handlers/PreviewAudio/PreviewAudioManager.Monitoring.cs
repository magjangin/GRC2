using MelonLoader;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GRC2.Core;


namespace GRC2.Harmony.Handlers
{
    public static partial class PreviewAudioManager
    {
    

        private static IEnumerator MonitorAndMuteAudioCoroutine()
        {
            MelonLogger.Msg("[PreviewAudioManager] 🔍 프리뷰/환경음 모니터링 시작");
            float monitorDuration = 3f; // 3초 동안 모니터링
            float elapsed = 0f;
            float checkInterval = 0.1f; // 0.1초마다 체크
            
            while (elapsed < monitorDuration && CustomAssetManager.IsCustomChartSelected())
            {
                // 저장된 AudioSource들이 다시 재생되면 중지
                foreach (var kvp in _mutedAudioSourcesWithVolume)
                {
                    AudioSource audioSource = kvp.Key;
                    if (audioSource != null)
                    {
                        if (audioSource.isPlaying || audioSource.volume > 0f || !audioSource.mute)
                        {
                            audioSource.Stop();
                            audioSource.volume = 0f;
                            audioSource.mute = true;
                        }
                    }
                }
                
                // 새로 생성된 프리뷰/환경음 AudioSource도 찾아서 중지
                AudioSource[] allAudioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
                foreach (var audioSource in allAudioSources)
                {
                    if (audioSource == null || audioSource == CustomBgmPlayer.CurrentAudioSource)
                        continue;
                    
                    string clipName = audioSource.clip != null ? audioSource.clip.name ?? "" : "";
                    
                    if (!string.IsNullOrEmpty(clipName) && 
                        (clipName.Contains("PCD_PREVIEW_") || clipName.Contains("PREVIEW") ||
                         clipName.Contains("PCD_AMB_") || clipName.Contains("AMB")))
                    {
                        if (audioSource.isPlaying || audioSource.volume > 0f)
                        {
                            // 원래 볼륨 저장 (아직 저장 안 된 경우)
                            if (!_mutedAudioSourcesWithVolume.ContainsKey(audioSource))
                            {
                                _mutedAudioSourcesWithVolume[audioSource] = audioSource.volume;
                            }
                            
                            audioSource.Stop();
                            audioSource.volume = 0f;
                            audioSource.mute = true;
                        }
                    }
                }
                
                yield return new WaitForSeconds(checkInterval);
                elapsed += checkInterval;
            }
            
            _isMonitoringAudio = false;
            MelonLogger.Msg("[PreviewAudioManager] 🔍 프리뷰/환경음 모니터링 종료");
        }

        public static void RestoreMutedAudioSources()
        {
            try
            {
                _isMonitoringAudio = false; // 모니터링 중지
                
                int restoredCount = 0;
                HashSet<AudioSource> restoredSources = new HashSet<AudioSource>();
                
                // 1. cSoundManager에서 직접 mPreviewAudioSorce와 mAmbientAudioSorce 찾아서 복원 (우선)
                Type soundManagerType = ReflectionHelper.FindType("IntiCreates.cSoundManager");
                AudioSource previewSource = null;
                AudioSource ambientSource = null;
                
                if (soundManagerType != null)
                {
                    UnityEngine.Object[] soundManagers = UnityEngine.Object.FindObjectsOfType(soundManagerType);
                    if (soundManagers != null && soundManagers.Length > 0)
                    {
                        object soundManagerInstance = soundManagers[0];
                        Type managerType = soundManagerInstance.GetType();
                        
                        // mPreviewAudioSorce 찾기
                        FieldInfo previewSourceField = managerType.GetField("mPreviewAudioSorce", 
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        
                        if (previewSourceField != null)
                        {
                            object sourceValue = previewSourceField.GetValue(soundManagerInstance);
                            if (sourceValue is AudioSource audioSource && audioSource != null)
                            {
                                previewSource = audioSource;
                            }
                        }
                        
                        // mAmbientAudioSorce 찾기
                        FieldInfo ambientSourceField = managerType.GetField("mAmbientAudioSorce", 
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        
                        if (ambientSourceField != null)
                        {
                            object sourceValue = ambientSourceField.GetValue(soundManagerInstance);
                            if (sourceValue is AudioSource audioSource && audioSource != null)
                            {
                                ambientSource = audioSource;
                            }
                        }
                    }
                }
                
                // 2. cSoundManager의 AudioSource 먼저 복원
                if (previewSource != null)
                {
                    previewSource.mute = false;
                    previewSource.volume = 1.0f;
                    restoredSources.Add(previewSource);
                    string goName = previewSource.gameObject != null ? previewSource.gameObject.name : "?";
                    string curClip = previewSource.clip != null ? previewSource.clip.name : "(null)";
                    MelonLogger.Msg($"[PreviewAudioManager] 🔊 mPreviewAudioSorce 복원 [게임 필드명]: gameObject={goName}, clip={curClip}, 볼륨 1.00");
                    restoredCount++;
                }
                
                if (ambientSource != null)
                {
                    ambientSource.mute = false;
                    ambientSource.volume = 1.0f;
                    restoredSources.Add(ambientSource);
                    string goName = ambientSource.gameObject != null ? ambientSource.gameObject.name : "?";
                    string curClip = ambientSource.clip != null ? ambientSource.clip.name : "(null)";
                    MelonLogger.Msg($"[PreviewAudioManager] 🔊 mAmbientAudioSorce 복원 [게임 필드명]: gameObject={goName}, clip={curClip}, 볼륨 1.00");
                    restoredCount++;
                }
                
                // 3. 딕셔너리에 저장된 다른 AudioSource 복원 (cSoundManager의 것 제외)
                foreach (var kvp in _mutedAudioSourcesWithVolume)
                {
                    AudioSource audioSource = kvp.Key;
                    
                    if (audioSource != null && !restoredSources.Contains(audioSource))
                    {
                        // cSoundManager의 AudioSource가 아닌 경우만 복원
                        if (audioSource != previewSource && audioSource != ambientSource)
                        {
                            audioSource.mute = false;
                            audioSource.volume = 1.0f;
                            restoredSources.Add(audioSource);
                            string goName = audioSource.gameObject != null ? audioSource.gameObject.name : "?";
                            string curClip = audioSource.clip != null ? audioSource.clip.name : "(null)";
                            MelonLogger.Msg($"[PreviewAudioManager] 🔊 AudioSource 복원: gameObject={goName}, clip={curClip}, 볼륨 1.00");
                            restoredCount++;
                        }
                    }
                }
                
                _mutedAudioSourcesWithVolume.Clear();
                MelonLogger.Msg($"[PreviewAudioManager] ✅ 총 {restoredCount}개 AudioSource 음소거 해제 및 볼륨 1.00 복원 완료");

                // 4. 키음(SE)용 AudioSource 강제 음소거 해제
                // - SE 클립 이름인 뮤트 소스 복구
                // - clip=null인 뮤트 소스도 복구 (게임이 키음용 풀으로 사용)
                int sePoolRestored = 0;
                AudioSource[] allForSe = UnityEngine.Object.FindObjectsOfType<AudioSource>();
                if (allForSe != null)
                {
                    foreach (var a in allForSe)
                    {
                        if (a == null || !a.mute) continue;
                        string goName = a.gameObject != null ? a.gameObject.name : "?";
                        if (goName == "CustomPreviewBGM") continue; // 모드가 만든 BGM 소스는 건드리지 않음
                        string clipName = a.clip != null ? a.clip.name : "";
                        bool isSeClip = !string.IsNullOrEmpty(clipName) &&
                            (clipName.Contains("SE_") || clipName.Contains("_SE") ||
                             clipName.IndexOf("SFX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             clipName.IndexOf("Effect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             clipName.IndexOf("UI", StringComparison.OrdinalIgnoreCase) >= 0);
                        bool isPoolSlot = string.IsNullOrEmpty(clipName); // clip 없음 = 풀 슬롯
                        if (isSeClip || isPoolSlot)
                        {
                            a.mute = false;
                            if (a.volume <= 0f) a.volume = 1f;
                            sePoolRestored++;
                        }
                    }
                    if (sePoolRestored > 0)
                        MelonLogger.Msg($"[PreviewAudioManager] 🔊 SE/키음풀 {sePoolRestored}개 음소거 해제");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[PreviewAudioManager] ⚠️ AudioSource 복원 중 오류: {ex.Message}");
            }
        }

        public static void Reset()
        {
            _isMonitoringAudio = false;
            _mutedAudioSourcesWithVolume.Clear();
        }

        public static void DebugDumpAllAudioSources(string label)
        {
            try
            {
                AudioSource[] all = UnityEngine.Object.FindObjectsOfType<AudioSource>();
                if (all == null || all.Length == 0)
                {
                    MelonLogger.Msg($"[PreviewAudioManager] [DEBUG {label}] AudioSource 0개");
                    return;
                }
                MelonLogger.Msg($"[PreviewAudioManager] [DEBUG {label}] === 씬 내 AudioSource 총 {all.Length}개 ===");
                for (int i = 0; i < all.Length; i++)
                {
                    var a = all[i];
                    if (a == null) continue;
                    string goName = a.gameObject != null ? a.gameObject.name : "?";
                    string clipName = a.clip != null ? a.clip.name : "(null)";
                    string muted = a.mute ? "MUTE" : "on";
                    bool looksLikeSE = !string.IsNullOrEmpty(clipName) &&
                        (clipName.Contains("SE_") || clipName.Contains("_SE") || clipName.IndexOf("SFX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         clipName.IndexOf("Effect", StringComparison.OrdinalIgnoreCase) >= 0 || clipName.IndexOf("UI", StringComparison.OrdinalIgnoreCase) >= 0);
                    string seTag = looksLikeSE ? " [SE/효과음 후보]" : "";
                    MelonLogger.Msg($"[PreviewAudioManager] [DEBUG {label}]   [{i}] go={goName}, clip={clipName}, volume={a.volume:F2}, mute={muted}{seTag}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[PreviewAudioManager] [DEBUG {label}] 덤프 오류: {ex.Message}");
            }
        }
}
}
