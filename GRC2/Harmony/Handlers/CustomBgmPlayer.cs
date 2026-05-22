using MelonLoader;
using UnityEngine;
using System;
using System.Collections;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// 커스텀 BGM 재생 관리 클래스
    /// - BGM 주입 및 정리
    /// - AudioSource 생성 및 관리
    /// </summary>
    public static class CustomBgmPlayer
    {
        private static AudioSource _customBgmAudioSource = null;
        private static GameObject _customBgmGameObject = null;
        
        /// <summary>
        /// 현재 커스텀 BGM AudioSource (모니터링에서 제외용)
        /// </summary>
        public static AudioSource CurrentAudioSource => _customBgmAudioSource;
        
        /// <summary>
        /// BGM이 재생 중인지 확인
        /// </summary>
        public static bool IsPlaying => _customBgmAudioSource != null && _customBgmAudioSource.isPlaying;

        /// <summary>
        /// 새로운 AudioSource에 커스텀 BGM 주입
        /// </summary>
        public static IEnumerator InjectCustomBgm(string bgmFilePath)
        {
            Cleanup();

            // 새로운 GameObject 생성
            _customBgmGameObject = new GameObject("CustomPreviewBGM");
            UnityEngine.Object.DontDestroyOnLoad(_customBgmGameObject);

            // AudioSource 추가
            _customBgmAudioSource = _customBgmGameObject.AddComponent<AudioSource>();
            _customBgmAudioSource.playOnAwake = false;
            _customBgmAudioSource.loop = true;
            _customBgmAudioSource.volume = 1f;
            _customBgmAudioSource.priority = 0; // 최우선 재생

            MelonLogger.Msg("[CustomBgmPlayer] ✅ 커스텀 BGM용 AudioSource 생성 완료");
            MelonLogger.Msg($"[CustomBgmPlayer]   - volume: {_customBgmAudioSource.volume}");
            MelonLogger.Msg($"[CustomBgmPlayer]   - enabled: {_customBgmAudioSource.enabled}");
            MelonLogger.Msg($"[CustomBgmPlayer]   - priority: {_customBgmAudioSource.priority}");

            using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(
                "file://" + bgmFilePath, AudioType.UNKNOWN))
            {
                MelonLogger.Msg("[CustomBgmPlayer] 📥 커스텀 BGM 파일 로드 시작...");
                yield return www.SendWebRequest();

                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    AudioClip customBgm = null;
                    try
                    {
                        customBgm = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Msg($"[CustomBgmPlayer] ❌ 커스텀 BGM 주입 중 오류: {ex.Message}");
                        MelonLogger.Msg($"[CustomBgmPlayer] 스택 트레이스: {ex.StackTrace}");
                    }

                    if (customBgm != null && _customBgmAudioSource != null)
                    {
                        MelonLogger.Msg($"[CustomBgmPlayer] ✅ AudioClip 로드 완료: {customBgm.name} (길이: {customBgm.length:F2}초)");

                        _customBgmAudioSource.clip = customBgm;
                        _customBgmAudioSource.time = 0f;

                        // 재생 시작
                        MelonLogger.Msg("[CustomBgmPlayer] ▶️ AudioSource.Play() 호출...");
                        _customBgmAudioSource.Play();

                        // 재생 확인 (약간의 지연 후)
                        yield return new WaitForSeconds(0.1f);

                        if (_customBgmAudioSource.isPlaying)
                        {
                            MelonLogger.Msg($"[CustomBgmPlayer] ✅ 커스텀 프리뷰 BGM 재생 시작 성공: {System.IO.Path.GetFileName(bgmFilePath)}");
                            MelonLogger.Msg($"[CustomBgmPlayer]   - 현재 재생 시간: {_customBgmAudioSource.time:F2}초");
                            MelonLogger.Msg($"[CustomBgmPlayer]   - clip 길이: {_customBgmAudioSource.clip.length:F2}초");
                        }
                        else
                        {
                            MelonLogger.Msg("[CustomBgmPlayer] ⚠️ AudioSource.Play() 호출했지만 재생되지 않습니다.");
                            MelonLogger.Msg($"[CustomBgmPlayer]   - enabled: {_customBgmAudioSource.enabled}");
                            MelonLogger.Msg($"[CustomBgmPlayer]   - volume: {_customBgmAudioSource.volume}");
                            MelonLogger.Msg($"[CustomBgmPlayer]   - clip: {((_customBgmAudioSource.clip != null) ? _customBgmAudioSource.clip.name : "null")}");
                        }
                    }
                    else
                    {
                        MelonLogger.Msg("[CustomBgmPlayer] ❌ 커스텀 BGM 로드 실패: AudioClip이 null이거나 AudioSource가 null입니다.");
                    }
                }
                else
                {
                    MelonLogger.Msg($"[CustomBgmPlayer] ❌ 커스텀 BGM 로드 실패: {www.error}");
                }
            }
        }
        
        /// <summary>
        /// 커스텀 BGM 정리
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                if (_customBgmAudioSource != null)
                {
                    if (_customBgmAudioSource.isPlaying)
                    {
                        _customBgmAudioSource.Stop();
                    }
                    _customBgmAudioSource = null;
                }
                
                if (_customBgmGameObject != null)
                {
                    UnityEngine.Object.Destroy(_customBgmGameObject);
                    _customBgmGameObject = null;
                }
                
                MelonLogger.Msg("[CustomBgmPlayer] ✅ 커스텀 BGM 정리 완료");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CustomBgmPlayer] ⚠️ 커스텀 BGM 정리 중 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 정리 및 프리뷰/환경음 복원
        /// </summary>
        public static void CleanupAndRestore()
        {
            Cleanup();
            PreviewAudioManager.RestoreMutedAudioSources();
        }
        
        /// <summary>
        /// 상태 초기화 (씬 변경 시 호출)
        /// </summary>
        public static void ResetState()
        {
            CleanupAndRestore();
            MelonLogger.Msg("[CustomBgmPlayer] 상태 초기화됨");
        }
    }
}
























