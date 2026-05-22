using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace GRC2.Core
{
    public static partial class ResultSceneInjector
    {
        private static bool TryApplyDifficultyToSongInfoObject(int[] lvArray)
        {
            GameObject songInfoObj = FindSongInfoObject();
            Transform musicLv = songInfoObj?.transform.Find(MusicLvObjectName);
            if (musicLv == null)
                return false;

            if (TryApplyDifficultyToComponents(musicLv.GetComponents<MonoBehaviour>(), lvArray, "楽曲情報/musicLV"))
                return true;

            return TryApplyDifficultyToComponents(musicLv.GetComponentsInChildren<MonoBehaviour>(true), lvArray, "楽曲情報/musicLV");
        }

        private static GameObject FindSongInfoObject()
        {
            GameObject songInfoObj = GameObject.Find(SongInfoObjectName);
            if (songInfoObj != null)
                return songInfoObj;

            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                Transform target = root?.transform.Find(SongInfoObjectName);
                if (target != null)
                    return target.gameObject;
            }

            return null;
        }

        private static bool TryApplyDifficultyToComponents(IEnumerable<MonoBehaviour> components, int[] lvArray, string logLabel)
        {
            if (components == null)
                return false;

            foreach (var comp in components)
            {
                if (comp != null && TrySetDifficultyOnObject(comp, comp.GetType(), lvArray, logLabel))
                    return true;
            }

            return false;
        }

        private static bool TryApplyDifficultyToUpdater(Type updaterType, int[] lvArray)
        {
            foreach (var updater in FindResultSceneUpdaters(updaterType))
            {
                if (TrySetDifficultyOnObject(updater, updater.GetType(), lvArray, "Updater"))
                    return true;

                object musicLvUi = updaterType.GetField("mMusicLVUI", InstanceFieldFlags)?.GetValue(updater);
                if (musicLvUi != null && TrySetDifficultyOnObject(musicLvUi, musicLvUi.GetType(), lvArray, "Updater.mMusicLVUI"))
                    return true;
            }

            return false;
        }

        private static bool TryApplyDifficultyToSceneLvObjects(int[] lvArray)
        {
            var allMb = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            if (allMb == null)
                return false;

            foreach (var mb in allMb)
            {
                if (mb == null)
                    continue;

                string goName = mb.gameObject != null ? mb.gameObject.name : "";
                if (!IsDifficultyDisplayObject(goName) && goName.IndexOf("LV", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (TrySetDifficultyOnObject(mb, mb.GetType(), lvArray, goName))
                {
                    MelonLogger.Msg($"[ResultSceneInjector] ✅ 결과 씬 난이도 적용: {goName}");
                    return true;
                }
            }

            return false;
        }

        private static bool TryApplyDifficultyTextFallback(Type updaterType, int[] lvArray)
        {
            string difficultyStr = FormatDifficultyString(lvArray);
            foreach (var updater in FindResultSceneUpdaters(updaterType))
            {
                if (TrySetDifficultyText(updater, updater.GetType(), difficultyStr))
                {
                    MelonLogger.Msg($"[ResultSceneInjector] ✅ 결과 씬 난이도 표시(텍스트): {difficultyStr}");
                    return true;
                }
            }

            return false;
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
