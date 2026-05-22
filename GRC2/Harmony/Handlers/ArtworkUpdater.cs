using MelonLoader;
using UnityEngine;
using System;
using System.Reflection;
using GRC2.Core;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// 아트워크 업데이트를 담당하는 클래스
    /// </summary>
    public static class ArtworkUpdater
    {
        /// <summary>
        /// 아트워크 강제 업데이트
        /// </summary>
        public static void UpdateArtwork(object instance, Type instanceType)
        {
            try
            {
                // 현재 선택된 MusicID 가져오기
                object currentMusicID = AudioClipPatch.GetCurrentMusicIDFromInstance(instance);
                if (currentMusicID != null)
                {
                    MelonLogger.Msg($"[ArtworkUpdater] 🔄 곡 변경 메서드 호출됨");
                    MelonLogger.Msg($"[ArtworkUpdater] 현재 선택된 MusicID: {currentMusicID}");
                    
                    // 커스텀 차트인지 확인
                    if (AlbumManager.IsCustomChartMusicID(currentMusicID))
                    {
                        MelonLogger.Msg($"[ArtworkUpdater] ✅ 커스텀 차트 선택 감지! (MusicID: {currentMusicID})");
                        MelonLogger.Msg($"[ArtworkUpdater] ✅ 커스텀 차트 선택됨 (MusicID: {currentMusicID})");
                        
                        // 현재 BGM 파일 확인
                        var currentBgmFile = AlbumManager.GetCurrentBgmFile();
                        if (!string.IsNullOrEmpty(currentBgmFile))
                        {
                            MelonLogger.Msg($"[ArtworkUpdater] 🔍 현재 BGM 파일: {currentBgmFile}");
                            MelonLogger.Msg($"[ArtworkUpdater] 🚀 BGM 주입 코루틴 시작 (ChangeMusicPostfix)");
                        }
                    }
                }
                
                // mArtWorkAndMusicDetail 필드 찾기
                FieldInfo artWorkField = instanceType.GetField("mArtWorkAndMusicDetail", 
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                
                if (artWorkField != null)
                {
                    object artWorkManager = artWorkField.GetValue(instance);
                    if (artWorkManager != null)
                    {
                        Type artWorkManagerType = artWorkManager.GetType();
                        MelonLogger.Msg($"[ArtworkUpdater] 🔍 artWorkManager 타입: {artWorkManagerType.FullName}");
                        
                        // cMusicSelectArtWork 타입 찾기
                        Type artWorkType = ReflectionHelper.FindType("IntiCreates.cMusicSelectArtWork");
                        object artWorkInstance = null;
                        
                        if (artWorkType != null)
                        {
                            // 방법 1: cMusicSelectArtWorkManager의 필드에서 찾기
                            artWorkInstance = FindArtWorkInstanceFromFields(artWorkManager, artWorkManagerType, artWorkType);
                            
                            // 방법 2: FindObjectsOfType으로 찾기
                            if (artWorkInstance == null)
                            {
                                artWorkInstance = FindArtWorkInstanceByType(artWorkType);
                            }
                            
                            // requestSetArtworkSprite 메서드 찾기 및 호출
                            if (artWorkInstance != null)
                            {
                                SetCustomArtworkSprite(artWorkInstance, artWorkType);
                            }
                            else
                            {
                                MelonLogger.Msg("[ArtworkUpdater] ⚠️ cMusicSelectArtWork 인스턴스를 찾을 수 없습니다.");
                            }
                        }
                        else
                        {
                            MelonLogger.Msg("[ArtworkUpdater] ⚠️ cMusicSelectArtWork 타입을 찾을 수 없습니다.");
                        }
                    }
                }
                else
                {
                    MelonLogger.Msg("[ArtworkUpdater] ⚠️ mArtWorkAndMusicDetail 필드를 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ArtworkUpdater] ⚠️ 아트워크 업데이트 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 필드에서 ArtWork 인스턴스를 찾습니다.
        /// </summary>
        private static object FindArtWorkInstanceFromFields(object artWorkManager, Type artWorkManagerType, Type artWorkType)
        {
            FieldInfo[] fields = artWorkManagerType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (field.FieldType == artWorkType || field.FieldType.IsSubclassOf(artWorkType))
                {
                    object artWorkInstance = field.GetValue(artWorkManager);
                    if (artWorkInstance != null)
                    {
                        MelonLogger.Msg($"[ArtworkUpdater] ✅ cMusicSelectArtWork 인스턴스 발견 (필드: {field.Name})");
                        return artWorkInstance;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// FindObjectsOfType으로 ArtWork 인스턴스를 찾습니다.
        /// </summary>
        private static object FindArtWorkInstanceByType(Type artWorkType)
        {
            UnityEngine.Object[] artWorkObjects = UnityEngine.Object.FindObjectsOfType(artWorkType);
            if (artWorkObjects != null && artWorkObjects.Length > 0)
            {
                MelonLogger.Msg($"[ArtworkUpdater] ✅ cMusicSelectArtWork 인스턴스 발견 (FindObjectsOfType)");
                return artWorkObjects[0];
            }
            return null;
        }

        /// <summary>
        /// 커스텀 아트워크 스프라이트를 설정합니다.
        /// </summary>
        private static void SetCustomArtworkSprite(object artWorkInstance, Type artWorkType)
        {
            MethodInfo requestSetArtworkMethod = artWorkType.GetMethod("requestSetArtworkSprite", 
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            
            if (requestSetArtworkMethod != null)
            {
                // 커스텀 아트워크 로드
                var imageFile = AlbumManager.GetCurrentImageFile();
                if (!string.IsNullOrEmpty(imageFile) && System.IO.File.Exists(imageFile))
                {
                    CustomAssetManager.LoadCustomArtwork(imageFile);
                    Sprite customSprite = CustomAssetManager.GetCustomArtwork();
                    
                    if (customSprite != null)
                    {
                        // requestSetArtworkSprite 호출 (isInstant = true)
                        requestSetArtworkMethod.Invoke(artWorkInstance, new object[] { customSprite, true });
                        MelonLogger.Msg($"[ArtworkUpdater] ✅ 아트워크 강제 업데이트 완료: '{customSprite.name}'");
                    }
                }
            }
            else
            {
                MelonLogger.Msg("[ArtworkUpdater] ⚠️ cMusicSelectArtWork.requestSetArtworkSprite 메서드를 찾을 수 없습니다.");
            }
        }
    }
}

