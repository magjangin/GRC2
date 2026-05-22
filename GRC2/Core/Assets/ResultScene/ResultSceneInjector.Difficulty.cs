using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using GRC2.Injectors;

namespace GRC2.Core
{
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

        private static void ApplyDifficultyToResultScene()
        {
            try
            {
                int[] lvArray = BuildCurrentDifficultyArray();
                if (lvArray == null)
                    return;

                Type updaterType = GameTypeSearcher.RythmGameResultSceneUpdaterType;

                if (TryApplyDifficultyToSongInfoObject(lvArray))
                    return;
                if (TryApplyDifficultyToUpdater(updaterType, lvArray))
                    return;
                if (TryApplyDifficultyToSceneLvObjects(lvArray))
                    return;

                if (!TryApplyDifficultyTextFallback(updaterType, lvArray))
                    MelonLogger.Msg("[ResultSceneInjector] 결과 씬에서 난이도 필드를 찾지 못했습니다 (표시는 게임 기본값 유지).");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ResultSceneInjector] 난이도 적용 오류: {ex.Message}");
            }
        }

        private static int[] BuildCurrentDifficultyArray()
        {
            var songInfo = AlbumManager.GetCurrentSongInfo();
            if (songInfo?.DifficultyNumbers == null || songInfo.DifficultyNumbers.Count == 0)
                return null;

            int[] lvArray = new int[DifficultyOrder.Length];
            for (int i = 0; i < DifficultyOrder.Length && i < lvArray.Length; i++)
            {
                if (songInfo.DifficultyNumbers.TryGetValue(DifficultyOrder[i], out int num))
                    lvArray[i] = num;
            }
            return lvArray;
        }

    }
}
