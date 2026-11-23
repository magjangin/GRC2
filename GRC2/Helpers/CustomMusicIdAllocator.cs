using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GRC2.Helpers
{
    /// <summary>
    /// 커스텀 곡(앨범 폴더)마다 MusicID를 정합니다(Unity 없이 테스트 가능).
    ///
    /// 외부 맵 파일을 만들거나 읽지 않고 메모리에서만 동작합니다(<see cref="GRC2.Core.CustomMusicIdRegistry"/>).
    /// 처음 실행할 때는 앨범 키를 이름순으로 세워 54부터 배정하며, 같은 실행 중에 <c>hwa</c> 폴더에 곡을 추가·삭제해
    /// 다시 스캔할 때는 이번 세션에서 이미 받은 ID를 그대로 유지하고 새 폴더만 비어 있는 가장 작은 ID를 받습니다.
    /// </summary>
    public static class CustomMusicIdAllocator
    {
        /// <summary>0.6.0까지 맵을 따로 두던 파일 이름(시작 시 삭제용).</summary>
        public const string LegacyMapFileName = "musicid_map.txt";

        public sealed class Result
        {
            /// <summary>앨범 키 → MusicID 값. 이번 목록에 있는 앨범만 들어 있습니다.</summary>
            public Dictionary<string, int> Assigned { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            /// <summary>이번 세션에서 기억하는 전체 맵(지운 앨범의 예약 ID 포함).</summary>
            public Dictionary<string, int> FullMap { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            /// <summary>ID가 모자라 받지 못한 앨범 키.</summary>
            public List<string> Skipped { get; } = new List<string>();

            /// <summary>FullMap이 기존 맵과 달라졌는지.</summary>
            public bool Changed { get; set; }
        }

        /// <summary>
        /// 앨범 키 목록에 ID를 배정합니다.
        /// </summary>
        /// <param name="albumKeys">이번에 발견한 앨범 키(순서 무관, 이름순으로 정렬해 씀).</param>
        /// <param name="existingMap">기존에 배정된 키 → ID(없으면 빈 맵).</param>
        /// <param name="startInclusive">첫 커스텀 ID(원본 곡 다음 값).</param>
        /// <param name="endInclusive">마지막 커스텀 ID.</param>
        /// <param name="isReserved">게임이 이름을 붙여 쓰는 값(예: SAVEABLE_ID_END=512)이면 true. 그런 값은 건너뜁니다.</param>
        public static Result Allocate(
            IEnumerable<string> albumKeys,
            IDictionary<string, int> existingMap,
            int startInclusive,
            int endInclusive,
            Func<int, bool> isReserved)
        {
            var result = new Result();
            var used = new HashSet<int>();

            // 1) 기존 맵에서 아직 쓸 수 있는 항목을 옮깁니다. 범위를 벗어났거나 예약 값이거나 다른 키와 겹친 항목은 버립니다.
            if (existingMap != null)
            {
                foreach (var pair in existingMap.OrderBy(p => p.Value).ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) ||
                        pair.Value < startInclusive || pair.Value > endInclusive ||
                        isReserved(pair.Value) || !used.Add(pair.Value) ||
                        result.FullMap.ContainsKey(pair.Key))
                    {
                        result.Changed = true;
                        continue;
                    }
                    result.FullMap[pair.Key] = pair.Value;
                }
            }

            // 2) 이번 목록의 앨범: 맵에 있으면 그 ID, 없으면 비어 있는 가장 작은 ID.
            int next = startInclusive;
            foreach (var key in albumKeys
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                if (result.FullMap.TryGetValue(key, out int existing))
                {
                    result.Assigned[key] = existing;
                    continue;
                }

                while (next <= endInclusive && (used.Contains(next) || isReserved(next)))
                    next++;

                if (next > endInclusive)
                {
                    result.Skipped.Add(key);
                    continue;
                }

                used.Add(next);
                result.FullMap[key] = next;
                result.Assigned[key] = next;
                result.Changed = true;
                next++;
            }

            return result;
        }

        /// <summary>"ID=앨범 키" 줄을 읽습니다. 키에 '='가 들어 있어도 첫 '='에서 나누므로 괜찮습니다.</summary>
        public static Dictionary<string, int> Parse(IEnumerable<string> lines)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (lines == null)
                return map;

            foreach (var raw in lines)
            {
                var line = raw?.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                int idx = line.IndexOf('=');
                if (idx <= 0)
                    continue;

                if (!int.TryParse(line.Substring(0, idx).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                    continue;

                var key = line.Substring(idx + 1).Trim();
                if (key.Length == 0 || map.ContainsKey(key))
                    continue;

                map[key] = id;
            }
            return map;
        }

        /// <summary>ID 순서로 "ID=키" 줄을 만듭니다.</summary>
        public static List<string> Format(IDictionary<string, int> map)
        {
            return map.OrderBy(p => p.Value)
                .Select(p => p.Value.ToString(CultureInfo.InvariantCulture) + "=" + p.Key)
                .ToList();
        }

        /// <summary>
        /// INI/TOML 형식 줄 목록에서 지정한 <c>[sectionName]</c> 섹션(다음 <c>[...]</c> 헤더 전까지)을 걷어 냅니다.
        /// 예전 <c>MelonPreferences.cfg</c>의 <c>[GRC2_MusicID]</c> 섹션을 정리할 때 씁니다.
        /// </summary>
        public static List<string> StripIniSection(IEnumerable<string> lines, string sectionName, out bool removed)
        {
            removed = false;
            var output = new List<string>();
            if (lines == null || string.IsNullOrWhiteSpace(sectionName))
                return output;

            string header = "[" + sectionName.Trim() + "]";
            bool skipping = false;

            foreach (var line in lines)
            {
                var trimmed = line?.Trim() ?? string.Empty;
                if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
                {
                    if (string.Equals(trimmed, header, StringComparison.OrdinalIgnoreCase))
                    {
                        skipping = true;
                        removed = true;
                        continue;
                    }
                    skipping = false;
                }

                if (!skipping)
                    output.Add(line);
            }

            while (removed && output.Count > 0 && string.IsNullOrWhiteSpace(output[output.Count - 1]))
                output.RemoveAt(output.Count - 1);

            return output;
        }
    }
}
