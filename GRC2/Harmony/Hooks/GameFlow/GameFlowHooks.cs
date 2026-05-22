using System.Reflection;

namespace GRC2.Harmony.Hooks
{
    /// <summary>
    /// 게임 흐름 관련 후킹 - 곡 선택 → 게임 시작 → 이전 화면 돌아가기
    /// cMusicSelectSceneUIUpdater의 메서드들을 후킹
    /// </summary>
    public static partial class GameFlowHooks
    {
        private const BindingFlags InstanceFieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly string[] StartRythmGameImportantFields =
        {
            "mCurrentDispData",
            "mMusicSelectData",
            "mSelectedMusicID",
            "mIsCurrentAutoPlay",
            "mCurrentMusicID",
            "mMusicID"
        };
    }
}
