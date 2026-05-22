using System;
using System.Reflection;
using GRC2.Core;
using MelonLoader;

namespace GRC2.Services
{
    /// <summary>
    /// 커스텀 차트 처리 서비스
    /// 성능 최적화: 필드 정보 캐싱
    /// </summary>
    internal static partial class CustomChartHandler
    {
        // ⚡ 성능 최적화: 필드 정보 캐시
        private static FieldInfo _dispDataFieldCache = null;
        private static FieldInfo _musicSelectDataFieldCache = null;
        private static FieldInfo _musicIdFieldCache = null;
        private static FieldInfo _songTitleFieldCache = null;
        
        /// <summary>
        /// 커스텀 차트 감지 및 아트워크/BGM 로드
        /// </summary>
        public static void UpdateCustomChartTitle(object instance)
        {
            try
            {
                // SoundPlayerScene / MoviePlayer_MovieSelect는 곡 리스트가 있지만 곡 선택 씬이 아님 → 커스텀 차트로 착각하지 않음
                if (CustomAssetManager.IsSceneWhereInjectionDisallowed())
                {
                    MelonLogger.Msg("[CustomChartHandler] 곡 선택 씬이 아님 - 커스텀 차트 감지 건너뜀");
                    return;
                }

                MelonLogger.Msg("===========================================");
                MelonLogger.Msg("[CustomChartHandler] 🔍 coOpen() - 커스텀 차트 감지 시작");
                
                Type instanceType = instance.GetType();
                
                // ⚡ 성능 최적화: 캐시된 필드 정보 사용
                FieldInfo dispDataField = _dispDataFieldCache ?? (_dispDataFieldCache = 
                    instanceType.GetField("mCurrentDispData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
                
                if (dispDataField == null)
                {
                    MelonLogger.Msg("[CustomChartHandler] ⚠️ mCurrentDispData 필드를 찾을 수 없습니다.");
                    return;
                }
                
                object dispData = dispDataField.GetValue(instance);
                if (dispData == null)
                {
                    MelonLogger.Msg("[CustomChartHandler] ⚠️ mCurrentDispData가 null입니다.");
                    return;
                }
                
                Type dispDataType = dispData.GetType();
                MelonLogger.Msg($"[CustomChartHandler] ✅ mCurrentDispData 타입: {dispDataType.Name}");
                
                // ⚡ 성능 최적화: 캐시된 필드 정보 사용
                FieldInfo musicSelectDataField = _musicSelectDataFieldCache ?? (_musicSelectDataFieldCache = 
                    dispDataType.GetField("mMusicSelectData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
                
                if (musicSelectDataField == null)
                {
                    MelonLogger.Msg("[CustomChartHandler] ⚠️ mMusicSelectData 필드를 찾을 수 없습니다.");
                    return;
                }
                
                object musicSelectData = musicSelectDataField.GetValue(dispData);
                if (musicSelectData == null)
                {
                    MelonLogger.Msg("[CustomChartHandler] ⚠️ mMusicSelectData가 null입니다.");
                    return;
                }
                
                Type musicSelectDataType = musicSelectData.GetType();
                
                // ⚡ 성능 최적화: 캐시된 필드 정보 사용
                FieldInfo musicIdField = _musicIdFieldCache ?? (_musicIdFieldCache = 
                    musicSelectDataType.GetField("musicID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
                
                if (musicIdField == null)
                {
                    MelonLogger.Msg("[CustomChartHandler] ⚠️ musicID 필드를 찾을 수 없습니다.");
                    return;
                }
                
                object musicID = musicIdField.GetValue(musicSelectData);
                MelonLogger.Msg($"[CustomChartHandler] 📌 현재 MusicID: {musicID}");
                
                // ⚡ 성능 최적화: 캐시된 필드 정보 사용
                FieldInfo songTitleField = _songTitleFieldCache ?? (_songTitleFieldCache = 
                    musicSelectDataType.GetField("songTitle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
                
                string songTitle = "제목 없음";
                if (songTitleField != null)
                {
                    object titleObj = songTitleField.GetValue(musicSelectData);
                    songTitle = titleObj?.ToString() ?? "제목 없음";
                }
                MelonLogger.Msg($"[CustomChartHandler] 🎵 현재 곡 이름: {songTitle}");
                
                // 곡 제목으로 커스텀 차트인지 확인
                bool isCustom = IsCustomChart(musicID, songTitle);
                
                MelonLogger.Msg($"[CustomChartHandler] 🔍 최종 커스텀 차트 여부: {isCustom}");
                
                if (!isCustom)
                {
                    HandleNonCustomChart();
                    return;
                }
                
                HandleCustomChart(instance, musicID, songTitle);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomChartHandler] UpdateCustomChartTitle 오류: {ex.Message}");
                MelonLogger.Warning($"[CustomChartHandler] 스택 트레이스: {ex.StackTrace}");
            }
        }

    }
}













