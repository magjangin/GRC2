using System;
using System.Reflection;
using MelonLoader;
using GRC2.Core;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// 플레이/결과 씬에서 모델/보이스 로딩에 사용되는 "MainCharactor charactor" 인자를 커스텀 곡 기준으로 강제 교체합니다.
    /// 핵심 아이디어:
    /// - 게임은 (charactor, musicData) 형태로 에셋을 로드할 수 있음 (예: coLoadAssets(MainCharactor charactor, MusicData musicData))
    /// - musicData의 charactorID 필드를 바꿔도 실제 로딩 인자가 고정이면 반영되지 않음
    /// - 그래서 Harmony로 첫 번째 인자를 musicData.charactorID(또는 유사 필드) 기반으로 덮어씌움
    /// </summary>
    public static class CharactorLoadPatch
    {
        // 호출 확인용: musicID별 1회만 로그
        private static readonly System.Collections.Generic.HashSet<string> _loggedMusicIds = new System.Collections.Generic.HashSet<string>();

        /// <summary>
        /// PatchApplier에서 생성한 DynamicMethod가 호출하는 헬퍼.
        /// musicData가 "커스텀 곡"이면, musicData.charactorID(또는 유사 필드) 기반으로 targetCharactorType 값을 반환합니다.
        /// </summary>
        public static object ComputePatchedCharactor(object musicData, Type targetCharactorType)
        {
            try
            {
                if (musicData == null || targetCharactorType == null)
                    return null;
                
                // 커스텀 곡인지 확인
                var musicId = TryExtractMusicId(musicData);
                if (!CustomAssetManager.IsCustomChart(musicId, musicData))
                    return null;
                
                // musicData에서 원하는 charactor 값 추출
                var desired = TryExtractCharactorValue(musicData);
                
                if (desired == null)
                    return null;
                
                var converted = ConvertToTargetType(desired, targetCharactorType);
                if (converted == null)
                    return null;
                
                // 성공 로그 (musicID별 1회)
                var key = musicId?.ToString() ?? "(null)";
                lock (_loggedMusicIds)
                {
                    if (!_loggedMusicIds.Contains(key))
                    {
                        _loggedMusicIds.Add(key);
                        MelonLogger.Msg($"[CharactorLoadPatch] ✅ charactor 로딩 인자 치환 준비됨 (MusicID={key}, desired={desired}, targetType={targetCharactorType.Name})");
                    }
                }
                
                return converted;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharactorLoadPatch] ComputePatchedCharactor 오류: {ex.Message}");
                return null;
            }
        }
        
        private static object TryExtractMusicId(object musicData)
        {
            try
            {
                var t = musicData.GetType();
                // 흔한 필드명 후보들
                var f =
                    t.GetField("musicID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                    t.GetField("mMusicID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                    t.GetField("MusicID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                return f?.GetValue(musicData);
            }
            catch { return null; }
        }
        
        private static object TryExtractCharactorValue(object musicData)
        {
            try
            {
                var t = musicData.GetType();
                
                // 1) 명시 필드명 우선 (유저가 말한 charactorID)
                var f =
                    t.GetField("charactorID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                    t.GetField("mCharactorID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                    t.GetField("mainCharactorID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                    t.GetField("mMainCharactorID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (f != null)
                {
                    var v = f.GetValue(musicData);
                    if (v != null) return v;
                }
                
                // 2) 폴백: charactor/character가 들어간 필드 중 enum/정수 후보
                var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var cand in fields)
                {
                    var name = (cand.Name ?? "").ToLowerInvariant();
                    if (!name.Contains("charactor") && !name.Contains("character"))
                        continue;
                    
                    var v = cand.GetValue(musicData);
                    if (v == null) continue;
                    
                    var vt = v.GetType();
                    if (vt.IsEnum || v is int || v is short || v is byte || v is string)
                        return v;
                }
                
                return null;
            }
            catch { return null; }
        }
        
        private static object ConvertToTargetType(object desired, Type targetType)
        {
            try
            {
                if (desired == null || targetType == null)
                    return null;
                
                // 이미 같은 타입이면 그대로
                if (targetType.IsInstanceOfType(desired))
                    return desired;
                
                // enum 타겟
                if (targetType.IsEnum)
                {
                    if (desired is string s)
                    {
                        return Enum.Parse(targetType, s, ignoreCase: true);
                    }
                    
                    // enum/정수 → enum
                    var intVal = Convert.ToInt32(desired);
                    return Enum.ToObject(targetType, intVal);
                }
                
                // 정수 타겟
                if (targetType == typeof(int) || targetType == typeof(short) || targetType == typeof(byte))
                {
                    return Convert.ChangeType(desired, targetType);
                }
                
                // object 등은 그대로 넘겨보기
                if (targetType == typeof(object))
                    return desired;
                
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}


