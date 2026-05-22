using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GRC2.Core;
using GRC2.Helpers;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// 오디오 소스 및 클립 탐색을 담당하는 클래스
    /// </summary>
    public static partial class AudioSourceFinder
    {
        private const BindingFlags InstanceLookupFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        private const BindingFlags MemberLookupFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static readonly string[] SingletonPropertyNames = { "Instance", "instance" };
        private static readonly string[] SingletonFieldNames = { "_instance", "Instance" };

        /// <summary>
        /// sSoundManager2D를 탐색하고 프리뷰 BGM을 교체하는 코루틴
        /// </summary>
        public static IEnumerator LoadAndReplacePreviewBGMCoroutineForSoundManager2D(string musicOggPath)
        {
            MelonLogger.Msg("[AudioSourceFinder] 🔍 sSoundManager2D 탐색 및 프리뷰 BGM 교체 시작");
            
            // sSoundManager2D 타입 찾기
            Type soundManagerType = null;
            try
            {
                soundManagerType = ReflectionHelper.FindType("sSoundManager2D");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[AudioSourceFinder] ❌ sSoundManager2D 타입 찾기 오류: {ex.Message}");
                yield break;
            }
            
            if (soundManagerType == null)
            {
                MelonLogger.Msg("[AudioSourceFinder] ⚠️ sSoundManager2D 타입을 찾을 수 없습니다");
                yield break;
            }
            
            MelonLogger.Msg($"[AudioSourceFinder] ✅ sSoundManager2D 타입 발견: {soundManagerType.FullName}");
            
            object soundManagerInstance = FindSoundManagerInstance(soundManagerType);
            if (soundManagerInstance == null)
            {
                MelonLogger.Msg("[AudioSourceFinder] ⚠️ sSoundManager2D 인스턴스를 찾을 수 없습니다");
                yield break;
            }
            
            // 잠시 대기 (인스턴스가 완전히 초기화될 때까지)
            yield return new WaitForSeconds(0.3f);
            
            // AudioSource 찾기
            List<AudioSource> previewAudioSources = FindPreviewAudioSources(soundManagerType, soundManagerInstance);
            
            MelonLogger.Msg($"[AudioSourceFinder] 🔍 총 {previewAudioSources.Count}개의 AudioSource 후보 발견");
            
            if (previewAudioSources.Count == 0)
            {
                MelonLogger.Msg("[AudioSourceFinder] ⚠️ sSoundManager2D에서 프리뷰 AudioSource를 찾을 수 없습니다");
                yield break;
            }
                
            // music.ogg 로드 및 교체
            yield return LoadAndReplaceBGMForAudioSources(musicOggPath, previewAudioSources);
        }

    }
}
