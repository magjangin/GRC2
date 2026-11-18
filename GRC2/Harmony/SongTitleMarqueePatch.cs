using System;
using GRC2.Core;
using HarmonyLib;
using IntiCreates;
using MelonLoader;
using TMPro;

namespace GRC2.Harmony
{
    /// <summary>
    /// 곡 선택 화면에서 선택한 곡의 제목(큰 막대)이 칸보다 길면 옆으로 흘려 끝까지 보여 줍니다(<see cref="TextMarquee"/>).
    /// 제목을 쓰는 setDispMusicData 뒤에 한 번 붙여 두면, 이후 제목이 바뀔 때마다 컴포넌트가 스스로 다시 잽니다.
    /// 원본 곡에도 똑같이 적용됩니다(넘치지 않는 제목은 아무것도 바꾸지 않습니다).
    /// </summary>
    [HarmonyPatch(typeof(cMusicSelectSceneSelectingMusicUI), nameof(cMusicSelectSceneSelectingMusicUI.setDispMusicData))]
    public static class SongTitleMarqueePatch
    {
        private static readonly AccessTools.FieldRef<cMusicSelectSceneSelectingMusicUI, TextMeshProUGUI> MusicNameText =
            AccessTools.FieldRefAccess<cMusicSelectSceneSelectingMusicUI, TextMeshProUGUI>("mMusicNameText");

        private static void Postfix(cMusicSelectSceneSelectingMusicUI __instance)
        {
            try
            {
                TextMarquee.Attach(MusicNameText(__instance));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[제목 스크롤] 붙이기 실패: {ex.Message}");
            }
        }
    }
}
