using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using GRC2.Helpers;
using GRC2.Injectors;

namespace GRC2.Core
{
    public static partial class ResultSceneInjector
    {
        /// <summary>
        /// 결과 씬에서 ResultSceneCanvas/楽曲名下地/アートワーク 경로 존재 여부를 로그로 출력합니다.
        /// </summary>
        private static void LogResultSceneArtworkPathCheck()
        {
            try
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                MelonLogger.Msg($"[ResultSceneInjector] 🔍 결과 씬 아트워크 경로 확인 (씬: {scene.name})");

                ReadResultSceneArtworkPathStatus(
                    out bool hasResultSceneCanvas,
                    out Transform songNameBg,
                    out Transform artWork);

                MelonLogger.Msg($"[ResultSceneInjector]   ResultSceneCanvas: {(hasResultSceneCanvas ? "있음" : "없음")}");
                MelonLogger.Msg($"[ResultSceneInjector]   → 楽曲名下地: {(songNameBg != null ? "있음" : "없음")}");
                MelonLogger.Msg($"[ResultSceneInjector]   → アートワーク: {(artWork != null ? "있음" : "없음")}");

                GameObject found = FindArtWorkObject();
                MelonLogger.Msg(found != null
                    ? $"[ResultSceneInjector]   FindArtWorkObject() 반환: {found.name} (경로: {GetGameObjectPath(found.transform)})"
                    : "[ResultSceneInjector]   FindArtWorkObject() 반환: null");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ResultSceneInjector] 경로 확인 오류: {ex.Message}");
            }
        }

        private static void ReadResultSceneArtworkPathStatus(out bool hasResultSceneCanvas, out Transform songNameBg, out Transform artWork)
        {
            hasResultSceneCanvas = false;
            songNameBg = null;
            artWork = null;

            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                if (root == null || root.name != ResultSceneCanvasName)
                    continue;

                hasResultSceneCanvas = true;
                songNameBg = root.transform.Find(SongNameBackgroundPath);
                artWork = songNameBg?.Find(ArtworkObjectName);
                return;
            }
        }

        private static string GetGameObjectPath(Transform t)
        {
            if (t == null) return "";
            var list = new List<string>();
            while (t != null)
            {
                list.Add(t.name);
                t = t.parent;
            }
            list.Reverse();
            return string.Join("/", list);
        }

        /// <summary>
        /// 현재 씬에서 "アートワーク" 오브젝트를 찾아 스프라이트를 설정합니다.
        /// (결과 씬, 곡 시작 전 윈도우 등 공통 사용)
        /// </summary>
        public static bool ApplyArtworkToArtWorkObject(Sprite customSprite)
        {
            if (customSprite == null) return false;
            GameObject artWorkObj = FindArtWorkObject();
            return TryApplySpriteToGameObject(artWorkObj, customSprite);
        }

        private static GameObject FindArtWorkObject()
        {
            GameObject artWorkObj = FindByGlobalPath(ArtworkSearchPaths[0]);
            if (artWorkObj != null)
                return artWorkObj;

            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                artWorkObj = FindInRoot(root, ArtworkSearchPaths);
                if (artWorkObj != null)
                    return artWorkObj;
            }

            return FindByGlobalPath(ArtworkObjectName);
        }

        private static GameObject FindByGlobalPath(string path)
        {
            return string.IsNullOrEmpty(path) ? null : GameObject.Find(path);
        }

        private static GameObject FindInRoot(GameObject root, IEnumerable<string> paths)
        {
            if (root == null)
                return null;

            foreach (string path in paths)
            {
                Transform target = root.transform.Find(path);
                if (target != null)
                    return target.gameObject;
            }

            return null;
        }

        private static bool ApplyArtworkToResultScene(Sprite customSprite)
        {
            try
            {
                if (ApplyArtworkToArtWorkObject(customSprite))
                    return true;

                return TryApplyArtworkToGenericArtWorkObject(customSprite) ||
                    TryApplyArtworkToResultSceneUpdater(customSprite);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ResultSceneInjector] 아트워크 적용 오류: {ex.Message}");
                return false;
            }
        }

        private static bool TryApplyArtworkToGenericArtWorkObject(Sprite customSprite)
        {
            GameObject artWorkObj = GameObject.Find(GenericArtworkObjectName);
            if (artWorkObj == null || IsDifficultyDisplayObject(artWorkObj.name))
                return false;

            return TryApplySpriteToGameObject(artWorkObj, customSprite);
        }

        private static bool TryApplyArtworkToResultSceneUpdater(Sprite customSprite)
        {
            foreach (var updater in FindResultSceneUpdaters(GameTypeSearcher.RythmGameResultSceneUpdaterType))
            {
                var image = GetArtWorkImageFromField(updater, updater.GetType());
                if (TryApplySpriteToImage(image, customSprite))
                    return true;
            }

            return false;
        }

        private static UnityEngine.UI.Image GetArtWorkImageFromField(object target, Type type)
        {
            if (target == null || type == null) return null;
            try
            {
                var f = type.GetField("mArtWorkImage", InstanceFieldFlags);
                if (f != null && f.FieldType == typeof(UnityEngine.UI.Image))
                    return f.GetValue(target) as UnityEngine.UI.Image;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[ResultSceneInjector] TryGetArtWorkImageFromField", "리플렉션 실패");
            }
            return null;
        }

        private static bool TryApplySpriteToGameObject(GameObject target, Sprite sprite)
        {
            if (target == null)
                return false;

            return TryApplySpriteToImage(target.GetComponent<UnityEngine.UI.Image>(), sprite);
        }

        private static bool TryApplySpriteToImage(UnityEngine.UI.Image image, Sprite sprite)
        {
            if (image == null || sprite == null)
                return false;

            image.sprite = sprite;
            return true;
        }
    }
}
