using System;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using GRC2.Core;
using GRC2.Helpers;

namespace GRC2.Injectors
{
    /// <summary>
    /// BGM 관련 아트워크 업데이트를 담당하는 클래스
    /// </summary>
    internal static class BgmArtworkUpdater
    {
        // 동일 씬/동일 스프라이트에 대해 중복 업데이트를 방지하기 위한 캐시 (성능 최적화)
        private static Sprite _lastAppliedSprite = null;
        private static int _lastAppliedSceneHash = 0;

        /// <summary>
        /// 플레이 씬에서 커스텀 아트워크를 업데이트합니다.
        /// </summary>
        public static void UpdateCustomArtworkInPlayScene()
        {
            try
            {
                if (!CustomAssetManager.IsCustomChartSelected())
                {
                    return;
                }
                
                var imageFile = Core.AlbumManager.GetCurrentImageFile();
                if (string.IsNullOrEmpty(imageFile) || !System.IO.File.Exists(imageFile))
                {
                    return;
                }
                
                // 커스텀 아트워크 로드
                CustomAssetManager.LoadCustomArtwork(imageFile);
                Sprite customSprite = CustomAssetManager.GetCustomArtwork();
                
                if (customSprite == null)
                {
                    return;
                }

                // 현재 씬/스프라이트 조합이 이미 적용되어 있다면 추가 작업 생략
                int currentSceneHash = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetHashCode();
                if (_lastAppliedSprite == customSprite && _lastAppliedSceneHash == currentSceneHash)
                {
                    return;
                }
                
                // cMusicSelectArtWork 타입 찾기 (음악 선택 씬)
                Type artWorkType = ReflectionHelper.FindType("IntiCreates.cMusicSelectArtWork");
                if (artWorkType != null)
                {
                    UnityEngine.Object[] artWorkObjects = UnityEngine.Object.FindObjectsOfType(artWorkType);
                    if (artWorkObjects != null && artWorkObjects.Length > 0)
                    {
                        foreach (var artWorkObj in artWorkObjects)
                        {
                            SetArtworkSprite(artWorkObj, artWorkType, customSprite);
                        }
                    }
                }
                
                // 플레이 씬의 아트워크 컴포넌트도 찾아서 업데이트
                string[] playSceneArtWorkTypes = new[]
                {
                    "IntiCreates.cRythmArtWork",
                    "IntiCreates.cRythmGameArtWork",
                    "IntiCreates.cGameArtWork",
                    "IntiCreates.cPlayArtWork"
                };
                
                foreach (var typeName in playSceneArtWorkTypes)
                {
                    Type playArtWorkType = ReflectionHelper.FindType(typeName);
                    if (playArtWorkType != null)
                    {
                        UnityEngine.Object[] playArtWorkObjects = UnityEngine.Object.FindObjectsOfType(playArtWorkType);
                        if (playArtWorkObjects != null && playArtWorkObjects.Length > 0)
                        {
                            foreach (var artWorkObj in playArtWorkObjects)
                            {
                                SetArtworkSprite(artWorkObj, playArtWorkType, customSprite);
                            }
                        }
                    }
                }
                
                // Image 컴포넌트 직접 찾기 (이름으로 필터링)
                UpdateArtworkImages(customSprite);

                // 캐시 업데이트: 동일 씬/동일 스프라이트에 대해서는 이후 호출 시 스킵
                _lastAppliedSprite = customSprite;
                _lastAppliedSceneHash = currentSceneHash;
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[BgmArtworkUpdater] ❌ 플레이 씬 아트워크 업데이트 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 아트워크 스프라이트를 설정합니다.
        /// </summary>
        private static void SetArtworkSprite(object artWorkInstance, Type artWorkType, Sprite customSprite)
        {
            try
            {
                // requestSetArtworkSprite 메서드 시도
                MethodInfo setArtworkMethod = artWorkType.GetMethod("requestSetArtworkSprite",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                
                if (setArtworkMethod != null)
                {
                    var parameters = setArtworkMethod.GetParameters();
                    if (parameters.Length == 2)
                    {
                        setArtworkMethod.Invoke(artWorkInstance, new object[] { customSprite, true });
                        return;
                    }
                    else if (parameters.Length == 1)
                    {
                        setArtworkMethod.Invoke(artWorkInstance, new object[] { customSprite });
                        return;
                    }
                }
                
                // setSprite 메서드 시도
                MethodInfo setSpriteMethod = artWorkType.GetMethod("setSprite",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                
                if (setSpriteMethod != null)
                {
                    setSpriteMethod.Invoke(artWorkInstance, new object[] { customSprite });
                    return;
                }
                
                // 필드 직접 설정 시도
                FieldInfo[] fields = artWorkType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(Sprite))
                    {
                        field.SetValue(artWorkInstance, customSprite);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[BgmArtworkUpdater] UpdateArtworkFields", "스프라이트 필드 반영 실패");
            }
        }
        
        /// <summary>
        /// Image 컴포넌트를 찾아서 아트워크를 업데이트합니다.
        /// </summary>
        private static void UpdateArtworkImages(Sprite customSprite)
        {
            try
            {
                var images = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Image>();
                
                if (images == null || images.Length == 0)
                {
                    return;
                }
                
                // 아트워크 관련 이름 패턴
                string[] artworkNames = new[] { "artwork", "jacket", "cover", "album" };
                
                foreach (var image in images)
                {
                    if (image == null || image.gameObject == null)
                    {
                        continue;
                    }
                    
                    string objName = image.gameObject.name.ToLower();
                    
                    foreach (var pattern in artworkNames)
                    {
                        if (objName.Contains(pattern))
                        {
                            image.sprite = customSprite;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[BgmArtworkUpdater] UpdateArtworkImages", "Image 검색/설정 실패");
            }
        }
    }
}





