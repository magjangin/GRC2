using System;
using System.Collections;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using System.Collections.Generic;
using GRC2.Helpers;
using GRC2.Injectors;

namespace GRC2.Core
{
    /// <summary>
    /// 결과 씬(RythmGameResultScene)에서 커스텀 아트워크와 난이도 표시를 주입합니다.
    /// </summary>
    public static partial class ResultSceneInjector
    {
        private const BindingFlags InstanceFieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const int ArtworkApplyMaxAttempts = 10;
        private const float ArtworkApplyRetryInterval = 0.25f;
        private const string ResultSceneCanvasName = "ResultSceneCanvas";
        private const string SongNameBackgroundPath = "楽曲名下地";
        private const string ArtworkObjectName = "アートワーク";
        private const string GenericArtworkObjectName = "ArtWork";
        private const string SongInfoObjectName = "楽曲情報";
        private const string MusicLvObjectName = "musicLV";

        private static readonly string[] ArtworkSearchPaths =
        {
            "ResultSceneCanvas/楽曲名下地/アートワーク",
            "楽曲名下地/アートワーク",
            "アートワーク",
            "楽曲情報/アートワーク",
            "PreMusicStartWindow/ウインドウ/楽曲情報/アートワーク"
        };

        private static readonly string[] DifficultyOrder = { "easy", "normal", "hard", "expert" };

        private static readonly string[] DifficultyArrayFieldNames =
        {
            "musicLVArray",
            "mMusicLVArray",
            "levelArray",
            "mLevelArray",
            "musicLevelArray",
            "mLvArray",
            "lvArray",
            "mMusicLvArray",
            "levels",
            "mLevels"
        };

        private static object _coroutineRef;

        /// <summary>
        /// 결과 씬 로드 시 호출. 커스텀 차트이면 아트워크·난이도 주입 코루틴을 시작합니다.
        /// </summary>
        public static void StartInjection()
        {
            if (!CustomAssetManager.IsCustomChartSelected())
                return;

            if (_coroutineRef != null)
            {
                MelonLoader.MelonCoroutines.Stop(_coroutineRef);
                _coroutineRef = null;
            }

            _coroutineRef = MelonLoader.MelonCoroutines.Start(InjectArtworkAndDifficultyCoroutine());
        }

        private static IEnumerator InjectArtworkAndDifficultyCoroutine()
        {
            yield return new WaitForSeconds(0.15f);

            Sprite customSprite = null;
            try
            {
                LogResultSceneArtworkPathCheck();
                HideLvPartsInResultScene();
                var imageFile = AlbumManager.GetCurrentImageFile();
                if (!string.IsNullOrEmpty(imageFile) && System.IO.File.Exists(imageFile))
                {
                    CustomAssetManager.LoadCustomArtwork(imageFile);
                    customSprite = CustomAssetManager.GetCustomArtwork();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ResultSceneInjector] 주입 준비 오류: {ex.Message}");
            }

            if (customSprite != null)
            {
                for (int attempt = 1; attempt <= ArtworkApplyMaxAttempts; attempt++)
                {
                    try
                    {
                        bool applied = ApplyArtworkToResultScene(customSprite);
                        if (applied)
                            MelonLogger.Msg($"[ResultSceneInjector] ✅ 결과 씬 아트워크 적용 (시도 {attempt}/{ArtworkApplyMaxAttempts})");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[ResultSceneInjector] 아트워크 적용 오류: {ex.Message}");
                    }

                    if (attempt < ArtworkApplyMaxAttempts)
                        yield return new WaitForSeconds(ArtworkApplyRetryInterval);
                }
            }

            // 난이도 표시는 ResultSceneUpdaterPatch(initializePreFade 후킹)에서
            // 재생한 난이도 하나만 정확히 덮어쓰므로 여기서는 더 이상 처리하지 않습니다.
            // (기존 폴백은 4개 난이도를 모두 이어붙인 문자열을 mDifficultyText에 덮어쓰는 버그가 있었음)
            _coroutineRef = null;
        }
    }

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

    public static partial class ResultSceneInjector
    {
        /// <summary>
        /// 결과 씬에서 이름에 "LV"가 포함된 오브젝트(LV_Part, MusicLV 등)를 비활성화합니다.
        /// 커스텀 차트 결과 화면에서 원본 난이도 UI를 숨길 때 사용합니다.
        /// </summary>
        private static void HideLvPartsInResultScene()
        {
            try
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                var roots = scene.GetRootGameObjects();
                var toHide = new List<GameObject>();
                foreach (var root in roots)
                {
                    if (root == null) continue;
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (t != null && t.gameObject != null && IsLvPartName(t.gameObject.name))
                            toHide.Add(t.gameObject);
                    }
                }
                foreach (var go in toHide)
                {
                    if (go != null && go.activeSelf)
                        go.SetActive(false);
                }
                if (toHide.Count > 0)
                    MelonLogger.Msg($"[ResultSceneInjector] ✅ LV 포함 오브젝트 비활성화: {toHide.Count}개");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ResultSceneInjector] HideLvParts 오류: {ex.Message}");
            }
        }

        private static bool IsLvPartName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return false;
            return objectName.IndexOf("lv", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsDifficultyDisplayObject(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return false;
            string n = objectName.ToLower();
            return n.Contains("lv_part") || n.Contains("musiclv") || n.Contains("_part") && n.Contains("lv");
        }

        private static IEnumerable<object> FindResultSceneUpdaters(Type updaterType)
        {
            if (updaterType == null)
                yield break;

            var updaters = UnityEngine.Object.FindObjectsOfType(updaterType);
            if (updaters == null)
                yield break;

            foreach (var updater in updaters)
            {
                if (updater != null)
                    yield return updater;
            }
        }
    }
}
