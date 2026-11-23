using UnityEngine.SceneManagement;

namespace GRC2.Helpers
{
    /// <summary>
    /// 활성 씬 이름을 씬 핸들 기준으로 캐시합니다.
    ///
    /// <c>Scene.name</c>은 네이티브 호출이라 부를 때마다 새 문자열을 만듭니다. <c>CustomAssetManager.ShouldInjectCustomContent</c>는
    /// 곡 종료 대기 코루틴에서 매 프레임, 크레딧 게터·주입 루프에서 수시로 불리므로 그때마다 문자열이 버려졌습니다(알려진_문제 P11).
    /// <c>Scene.handle</c>은 할당 없는 정수이고 씬을 다시 로드하면 새 값이 되므로, 핸들이 바뀔 때만 이름을 다시 읽습니다.
    /// 씬 로드 이벤트로 이름을 미리 저장하는 방식은 쓰지 않습니다 — 추가(additive) 로드된 씬이 활성 씬과 다를 수 있어
    /// 예전 <c>GetActiveScene().name</c> 판정과 결과가 달라질 수 있습니다.
    /// </summary>
    public static class ActiveSceneName
    {
        private static int _cachedHandle;
        private static bool _hasCache;
        private static string _cachedName = string.Empty;

        /// <summary>지금 활성 씬의 이름. 활성 씬이 없거나 유효하지 않으면 빈 문자열입니다.</summary>
        public static string Current
        {
            get
            {
                Scene scene = SceneManager.GetActiveScene();
                if (!scene.IsValid())
                    return string.Empty;

                int handle = scene.handle;
                if (!_hasCache || handle != _cachedHandle)
                {
                    _cachedName = scene.name ?? string.Empty;
                    _cachedHandle = handle;
                    _hasCache = true;
                }

                return _cachedName;
            }
        }

        public static bool Is(string sceneName) => string.Equals(Current, sceneName, System.StringComparison.Ordinal);
    }
}
