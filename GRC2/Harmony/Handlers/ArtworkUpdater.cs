using GRC2.Core;
using MelonLoader;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// 비동기로 준비된 커스텀 아트워크를 현재 곡 선택 UI에 반영합니다.
    /// </summary>
    public static class ArtworkUpdater
    {
        public static void UpdateArtwork(object instance, Type instanceType)
        {
            if (instance == null ||
                instanceType == null ||
                !CustomAssetManager.IsCustomChartSelected())
            {
                return;
            }

            string imagePath = AlbumManager.GetCurrentImageFile();
            if (string.IsNullOrEmpty(imagePath))
            {
                return;
            }

            if (CustomAssetManager.TryGetCustomArtwork(
                imagePath,
                out Sprite cachedSprite))
            {
                ApplyArtwork(instance, instanceType, cachedSprite);
                return;
            }

            string requestedPath = Path.GetFullPath(imagePath);
            CustomAssetManager.RequestCustomArtwork(
                requestedPath,
                sprite =>
                {
                    string currentPath = AlbumManager.GetCurrentImageFile();
                    if (!CustomAssetManager.IsCustomChartSelected() ||
                        string.IsNullOrEmpty(currentPath) ||
                        !string.Equals(
                            Path.GetFullPath(currentPath),
                            requestedPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    ApplyArtwork(instance, instanceType, sprite);
                });
        }

        private static void ApplyArtwork(
            object sceneUpdater,
            Type sceneUpdaterType,
            Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            try
            {
                const BindingFlags flags =
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance;

                FieldInfo managerField =
                    sceneUpdaterType.GetField("mArtWorkAndMusicDetail", flags);
                object artworkManager = managerField?.GetValue(sceneUpdater);
                if (artworkManager == null)
                {
                    return;
                }

                Type artworkType =
                    ReflectionHelper.FindType("IntiCreates.cMusicSelectArtWork");
                if (artworkType == null)
                {
                    return;
                }

                object artworkInstance = FindArtworkInstance(
                    artworkManager,
                    artworkManager.GetType(),
                    artworkType,
                    flags);
                if (artworkInstance == null)
                {
                    return;
                }

                MethodInfo requestSetArtwork = artworkType.GetMethod(
                    "requestSetArtworkSprite",
                    flags);
                requestSetArtwork?.Invoke(
                    artworkInstance,
                    new object[] { sprite, true });
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    $"[ArtworkUpdater] 아트워크 UI 반영 실패: {ex.Message}");
            }
        }

        private static object FindArtworkInstance(
            object manager,
            Type managerType,
            Type artworkType,
            BindingFlags flags)
        {
            FieldInfo[] fields = managerType.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (artworkType.IsAssignableFrom(field.FieldType))
                {
                    object value = field.GetValue(manager);
                    if (value != null)
                    {
                        return value;
                    }
                }
            }

            UnityEngine.Object[] objects =
                UnityEngine.Object.FindObjectsOfType(artworkType);
            return objects != null && objects.Length > 0 ? objects[0] : null;
        }
    }
}
