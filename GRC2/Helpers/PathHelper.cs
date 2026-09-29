using System.IO;

namespace GRC2.Helpers
{
    /// <summary>
    /// 사용자가 준 파일 경로를 절대 경로로 정규화합니다. 잘못된 경로는 예외 대신 null입니다.
    /// (<c>CustomAssetManager</c>와 <c>CustomBgmPlayer</c>가 각자 갖고 있던 같은 로직을 모았습니다.)
    /// </summary>
    public static class PathHelper
    {
        /// <summary>비어 있거나 잘못된 경로면 null, 아니면 절대 경로(파일이 없어도 됨).</summary>
        public static string GetFullPathOrNull(string path)
        {
            try
            {
                return string.IsNullOrWhiteSpace(path)
                    ? null
                    : Path.GetFullPath(path);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>파일이 실제로 있을 때만 절대 경로, 아니면 null.</summary>
        public static string GetExistingFullPathOrNull(string path)
        {
            string fullPath = GetFullPathOrNull(path);
            return fullPath != null && File.Exists(fullPath)
                ? fullPath
                : null;
        }
    }
}
