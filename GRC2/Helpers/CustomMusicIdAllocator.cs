using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GRC2.Helpers
{
    /// <summary>
    /// 커스텀 곡(앨범 폴더)마다 MusicID를 정하고 UserData/GRC2/musicid_map.txt에 기억합니다(Unity 없이 테스트 가능).
    ///
    /// 예전에는 앨범을 이름순으로 세워 54, 55, …를 붙였기 때문에 폴더를 하나 추가·삭제하면 뒤 곡들의 ID가 밀려
    /// 저장된 기록과 마지막 선택 곡이 다른 곡으로 옮겨 갔습니다(알려진 문제 C4). 이제는 한 번 받은 ID를 파일에
    /// 남겨 두고, 새 폴더만 비어 있는 가장 작은 ID를 받습니다. 지운 폴더의 ID도 지우지 않아 다른 곡이
    /// 그 기록을 물려받지 않습니다.
    ///
    /// 맵 파일이 없을 때의 첫 배정은 예전 방식(이름순으로 54부터)과 같으므로 기존 기록이 그대로 이어집니다.
    /// </summary>
    public static class CustomMusicIdAllocator
    {
        public const string MapFileName = "musicid_map.txt";

        public sealed class Result
        {
            /// <summary>앨범 키 → MusicID 값. 이번 목록에 있는 앨범만 들어 있습니다.</summary>
            public Dictionary<string, int> Assigned { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            /// <summary>파일에 쓸 전체 맵(지금 없는 앨범의 예약 ID 포함).</summary>
            public Dictionary<string, int> FullMap { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            /// <summary>ID가 모자라 받지 못한 앨범 키.</summary>
            public List<string> Skipped { get; } = new List<string>();

            /// <summary>FullMap이 기존 맵과 달라 파일을 다시 써야 하는지.</summary>
            public bool Changed { get; set; }
        }

        /// <summary>
        /// 앨범 키 목록에 ID를 배정합니다.
        /// </summary>
        /// <param name="albumKeys">이번에 발견한 앨범 키(순서 무관, 이름순으로 정렬해 씀).</param>
        /// <param name="existingMap">파일에서 읽은 키 → ID(없으면 빈 맵).</param>
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
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
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

        public static List<string> Format(IDictionary<string, int> map)
        {
            var lines = new List<string>
            {
                "# GRC2 커스텀 곡 MusicID 맵. 형식: ID=hwa 안의 앨범 폴더 키",
                "# 한 번 받은 ID는 폴더를 추가·삭제해도 바뀌지 않습니다. 저장된 기록(BlockSave=0)이 이 ID에 붙습니다.",
                "# 지운 폴더의 줄도 남겨 두면 다른 곡이 그 기록을 물려받지 않습니다. 직접 고칠 때는 게임을 끈 상태에서 고치세요.",
            };
            lines.AddRange(map.OrderBy(p => p.Value).Select(p => p.Value.ToString(CultureInfo.InvariantCulture) + "=" + p.Key));
            return lines;
        }

        /// <summary>파일을 읽습니다. 없거나 못 읽으면 빈 맵.</summary>
        public static Dictionary<string, int> Load(string path)
        {
            try
            {
                return File.Exists(path)
                    ? Parse(File.ReadAllLines(path, Encoding.UTF8))
                    : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>임시 파일에 쓴 뒤 바꿔 끼워, 쓰다가 게임이 꺼져도 맵이 반쯤 남지 않게 합니다.</summary>
        public static void Save(string path, IDictionary<string, int> map)
        {
            var tmp = path + ".tmp";
            File.WriteAllLines(tmp, Format(map), new UTF8Encoding(false));
            if (File.Exists(path))
                File.Replace(tmp, path, null);
            else
                File.Move(tmp, path);
        }
    }
}
