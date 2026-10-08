using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GRC2.Helpers;
using MelonLoader;

namespace GRC2.Core
{
    /// <summary>
    /// savecustomkey/config.txt에서 AutoPlay/판정조작/기록차단/판정바/노트 흔들림 설정을 읽습니다.
    /// 게임 로드 시 한 번 읽고, 이후에는 씬이 바뀔 때마다 <see cref="Reload"/>로 다시 읽습니다.
    /// 값이 바뀌는 시점이 씬 경계뿐이라 곡이 진행되는 도중에는 절대 흔들리지 않습니다. 즉 파일을
    /// 고치면 게임 재시작 없이 다음 플레이부터 적용됩니다.
    /// </summary>
    public static class CustomKeySettings
    {
        private const string FolderName = "savecustomkey";
        private const string FileName = "config.txt";

        // 기본값은 이 한 곳에만 둡니다. 프로퍼티 초기값, 키가 없을 때의 대체값, 새로 만드는 설정 파일이 모두 이 값을 씁니다(H3).
        private const bool DefaultAutoPlay = false;
        private const bool DefaultAllPerfect = false;
        private const bool DefaultBlockSave = true;
        private const bool DefaultEnableJudgmentBar = true;
        private const bool DefaultJudgmentBarVertical = true;
        private const bool DefaultJudgmentBarCapsule = false;
        private const bool DefaultJudgmentBarLeft = true;
        private const bool DefaultNoteSway = false;
        private const float DefaultNoteSwayAmplitude = 20f;
        private const float DefaultNoteSwaySpeed = 0.8f;
        private const bool DefaultNoteSwayDamping = true;
        private const float DefaultNoteSwayDampingTime = 0.4f;
        private const bool DefaultNoteSpeedChaos = false;
        private const float DefaultNoteSpeedChaosMin = 0.6f;
        private const float DefaultNoteSpeedChaosMax = 1.8f;
        private const bool DefaultNoteSpeedChaosPerLane = true;

        // 허용 범위. 범위를 벗어난 값은 가장 가까운 한계로 맞추고 경고를 남깁니다. 0이면 노트가 멈추고 음수면 거꾸로 가므로 하한을 둡니다(H3).
        private const float MinNoteSwayAmplitude = 0f;
        private const float MaxNoteSwayAmplitude = 200f;
        private const float MinNoteSwaySpeed = 0f;
        private const float MaxNoteSwaySpeed = 10f;
        private const float MinNoteSwayDampingTime = 0.01f;
        private const float MaxNoteSwayDampingTime = 5f;
        private const float MinNoteSpeedChaos = 0.1f;
        private const float MaxNoteSpeedChaos = 10f;

        /// <summary>이 모드가 아는 설정 키입니다. 여기 없는 키는 오타일 수 있어 경고합니다.</summary>
        private static readonly HashSet<string> KnownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AutoPlay", "AllPerfect", "BlockSave",
            "EnableJudgmentBar", "JudgmentBarVertical", "JudgmentBarCapsule", "JudgmentBarLeft",
            "NoteSway", "NoteSwayAmplitude", "NoteSwaySpeed", "NoteSwayDamping", "NoteSwayDampingTime",
            "NoteSpeedChaos", "NoteSpeedChaosMin", "NoteSpeedChaosMax", "NoteSpeedChaosPerLane"
        };

        /// <summary>Initialize에서 확정한 설정 파일 경로. Reload/PollFileChange는 이 경로만 사용합니다.</summary>
        private static string _filePath;

        /// <summary>마지막으로 "값까지 반영한" 파일 수정 시각.</summary>
        private static DateTime _loadedWriteTimeUtc = DateTime.MinValue;

        /// <summary>마지막으로 "변경 감지 로그를 띄운" 파일 수정 시각. 같은 수정에 로그가 반복되지 않게 합니다.</summary>
        private static DateTime _announcedWriteTimeUtc = DateTime.MinValue;

        public static bool AutoPlay { get; private set; } = DefaultAutoPlay;
        public static bool AllPerfect { get; private set; } = DefaultAllPerfect;
        public static bool BlockSave { get; private set; } = DefaultBlockSave;

        public static bool EnableJudgmentBar { get; private set; } = DefaultEnableJudgmentBar;
        public static bool JudgmentBarVertical { get; private set; } = DefaultJudgmentBarVertical;
        public static bool JudgmentBarCapsule { get; private set; } = DefaultJudgmentBarCapsule;
        public static bool JudgmentBarLeft { get; private set; } = DefaultJudgmentBarLeft;

        public static bool NoteSway { get; private set; } = DefaultNoteSway;
        public static float NoteSwayAmplitude { get; private set; } = DefaultNoteSwayAmplitude;
        public static float NoteSwaySpeed { get; private set; } = DefaultNoteSwaySpeed;
        public static bool NoteSwayDamping { get; private set; } = DefaultNoteSwayDamping;
        public static float NoteSwayDampingTime { get; private set; } = DefaultNoteSwayDampingTime;

        public static bool NoteSpeedChaos { get; private set; } = DefaultNoteSpeedChaos;
        public static float NoteSpeedChaosMin { get; private set; } = DefaultNoteSpeedChaosMin;
        public static float NoteSpeedChaosMax { get; private set; } = DefaultNoteSpeedChaosMax;
        public static bool NoteSpeedChaosPerLane { get; private set; } = DefaultNoteSpeedChaosPerLane;

        public static void Initialize(string gameFolder)
        {
            try
            {
                var folder = Path.Combine(gameFolder, FolderName);
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                    MelonLogger.Msg($"[CustomKeySettings] {FolderName} 폴더 생성 완료: {folder}");
                }

                var filePath = Path.Combine(folder, FileName);
                if (!File.Exists(filePath))
                {
                    File.WriteAllLines(filePath, DefaultLines);
                    MelonLogger.Msg($"[CustomKeySettings] 기본 설정 파일 생성: {filePath}");
                }

                _filePath = filePath;

                if (!Load(filePath))
                {
                    MelonLogger.Warning("[CustomKeySettings] 설정 파일에서 유효한 항목을 찾지 못했습니다. 기본값으로 진행합니다.");
                }

                _loadedWriteTimeUtc = GetWriteTimeUtcOrDefault(filePath);
                _announcedWriteTimeUtc = _loadedWriteTimeUtc;
                MelonLogger.Msg("[CustomKeySettings] 로드 완료: " + DescribeSettings());
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[CustomKeySettings]", "초기화 오류");
            }
        }

        /// <summary>
        /// 설정 파일을 다시 읽습니다. 씬 로드 시점에만 호출되므로 곡이 진행되는 도중에는 값이 바뀌지 않습니다.
        /// 에디터가 파일을 쓰는 도중에 읽히면 항목이 하나도 안 잡혀 전부 기본값으로 떨어질 수 있으므로,
        /// 그런 경우에는 반영하지 않고 이전 값을 그대로 유지합니다.
        /// </summary>
        public static void Reload()
        {
            if (string.IsNullOrEmpty(_filePath))
                return;

            try
            {
                if (!File.Exists(_filePath))
                {
                    MelonLogger.Warning($"[CustomKeySettings] 설정 파일이 없어 다시 읽기를 건너뜁니다: {_filePath}");
                    return;
                }

                var writeTimeUtc = GetWriteTimeUtcOrDefault(_filePath);
                if (writeTimeUtc == _loadedWriteTimeUtc)
                    return; // 파일이 그대로면 다시 읽을 이유가 없습니다.

                // Load는 파일을 전부 읽어 딕셔너리를 만든 뒤에야 프로퍼티에 대입하므로,
                // ReadAllLines가 던지면 값이 반쯤 바뀐 상태로 남지 않습니다.
                if (!Load(_filePath))
                {
                    MelonLogger.Warning("[CustomKeySettings] 설정 파일에서 유효한 항목을 찾지 못해 이전 값을 유지합니다.");
                    return;
                }

                _loadedWriteTimeUtc = writeTimeUtc;
                _announcedWriteTimeUtc = writeTimeUtc;
                MelonLogger.Msg("[CustomKeySettings] 설정 다시 읽음: " + DescribeSettings());
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[CustomKeySettings]", "설정 다시 읽기 실패 (이전 값 유지)");
            }
        }

        /// <summary>
        /// 설정 파일이 수정됐는지 확인해 한 번만 알립니다. 값은 건드리지 않습니다.
        /// 실제 반영은 다음 플레이 씬에 진입할 때 <see cref="Reload"/>에서 이뤄집니다.
        /// </summary>
        public static void PollFileChange()
        {
            if (string.IsNullOrEmpty(_filePath))
                return;

            try
            {
                if (!File.Exists(_filePath))
                    return;

                var writeTimeUtc = GetWriteTimeUtcOrDefault(_filePath);
                if (writeTimeUtc == _loadedWriteTimeUtc || writeTimeUtc == _announcedWriteTimeUtc)
                    return;

                _announcedWriteTimeUtc = writeTimeUtc;
                MelonLogger.Msg("[CustomKeySettings] 설정 파일 변경 감지 - 다음 플레이부터 적용됩니다.");
            }
            catch
            {
                // 에디터가 저장하는 순간에는 접근이 막힐 수 있습니다. 다음 폴링에서 다시 확인합니다.
            }
        }

        private static DateTime GetWriteTimeUtcOrDefault(string filePath)
        {
            try
            {
                return File.GetLastWriteTimeUtc(filePath);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private static string DescribeSettings()
        {
            return
                $"AutoPlay={AutoPlay}, AllPerfect={AllPerfect}, BlockSave={BlockSave}, " +
                $"EnableJudgmentBar={EnableJudgmentBar}(Vertical={JudgmentBarVertical}, Capsule={JudgmentBarCapsule}, Left={JudgmentBarLeft}), " +
                $"NoteSway={NoteSway}(Amplitude={NoteSwayAmplitude}, Speed={NoteSwaySpeed}, " +
                $"Damping={NoteSwayDamping}, DampingTime={NoteSwayDampingTime}), " +
                $"NoteSpeedChaos={NoteSpeedChaos}(Min={NoteSpeedChaosMin}, Max={NoteSpeedChaosMax}, " +
                $"PerLane={NoteSpeedChaosPerLane})";
        }

        private static string Bit(bool value) => value ? "1" : "0";

        private static string Num(float value) => value.ToString(CultureInfo.InvariantCulture);

        private static readonly string[] DefaultLines =
        {
            "# 이 파일은 게임을 재시작하지 않아도 반영됩니다. 저장해두면 다음 플레이(리트라이 포함)부터 적용됩니다.",
            "# 곡이 진행되는 도중에는 값이 바뀌지 않습니다.",
            "",
            "# 지원 형식: 1/0, true/false, 참/거짓, 켜기/끄기(켜짐/꺼짐), 활성화/비활성화, on/off, enable/disable, enabled/disabled, y/n, yes/no, 트루/폴스",
            "",
            "# 오토 플레이 (1 = 켜짐, 0 = 꺼짐)",
            $"AutoPlay={Bit(DefaultAutoPlay)}",
            "",
            "# 올 퍼펙트 판정 조작 - BLUESTAR (1 = 켜짐, 0 = 꺼짐). 기본은 꺼짐입니다.",
            $"AllPerfect={Bit(DefaultAllPerfect)}",
            "",
            "# 베스트 스코어 / 랭킹 저장 차단 (1 = 켜짐, 0 = 꺼짐). 결과 화면에서만 막습니다.",
            $"BlockSave={Bit(DefaultBlockSave)}",
            "",
            "# 실시간 판정바 표시 (1 = 켜짐, 0 = 꺼짐)",
            $"EnableJudgmentBar={Bit(DefaultEnableJudgmentBar)}",
            "",
            "# 판정바 형태 (1 = 세로 판정바, 0 = 가로 판정바)",
            $"JudgmentBarVertical={Bit(DefaultJudgmentBarVertical)}",
            "",
            "# 판정바 모양 (1 = 알약(캡슐) 모양, 0 = 사각 바)",
            $"JudgmentBarCapsule={Bit(DefaultJudgmentBarCapsule)}",
            "",
            "# 세로 판정바를 화면 어느 쪽에 둘지 (1 = 왼쪽, 0 = 오른쪽). 가로 판정바에는 영향 없음(항상 중앙).",
            $"JudgmentBarLeft={Bit(DefaultJudgmentBarLeft)}",
            "",
            "# 노트가 눈송이처럼 좌우로 흔들리며 내려오는 연출 (1 = 켜짐, 0 = 꺼짐)",
            "# 판정에는 전혀 영향이 없는 순수 시각 효과입니다.",
            $"NoteSway={Bit(DefaultNoteSway)}",
            "",
            "# 흔들림 폭. 대략 픽셀 단위지만 실제로는 값 × 0.01을 로컬 좌표에 더합니다. 너무 크면 레인 밖으로 나가 잘릴 수 있습니다.",
            $"NoteSwayAmplitude={Num(DefaultNoteSwayAmplitude)}",
            "",
            "# 흔들림 속도 (초당 왕복 횟수)",
            $"NoteSwaySpeed={Num(DefaultNoteSwaySpeed)}",
            "",
            "# 판정선에 가까워지면 흔들림을 잦아들게 함 (1 = 켜짐, 0 = 꺼짐)",
            "# 끄면 판정선에 닿는 순간까지 계속 흔들립니다.",
            $"NoteSwayDamping={Bit(DefaultNoteSwayDamping)}",
            "",
            "# 판정선 도달 몇 초 전부터 흔들림이 잦아들지",
            $"NoteSwayDampingTime={Num(DefaultNoteSwayDampingTime)}",
            "",
            "# [챌린지] 노트마다 낙하 속도를 제각각으로 (1 = 켜짐, 0 = 꺼짐)",
            "# 노트끼리 서로 추월하므로 읽기가 매우 어려워집니다. 판정에는 영향이 없습니다.",
            $"NoteSpeedChaos={Bit(DefaultNoteSpeedChaos)}",
            "",
            "# 속도 배율 범위 (1 = 원래 속도). 예: 0.6 ~ 1.8",
            $"NoteSpeedChaosMin={Num(DefaultNoteSpeedChaosMin)}",
            $"NoteSpeedChaosMax={Num(DefaultNoteSpeedChaosMax)}",
            "",
            "# 1 = 레인마다 속도가 다름(같은 레인 안에서는 순서 유지, 읽을 수는 있음)",
            "# 0 = 노트마다 속도가 다름(완전 카오스)",
            $"NoteSpeedChaosPerLane={Bit(DefaultNoteSpeedChaosPerLane)}"
        };

        /// <summary>
        /// 설정 파일을 읽어 프로퍼티에 반영합니다. 유효한 "키=값" 항목을 하나도 찾지 못하면
        /// (파일이 비었거나 저장 도중이라 반쯤 읽힌 경우) 아무것도 대입하지 않고 false를 돌려줍니다.
        /// </summary>
        private static bool Load(string filePath)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadAllLines(filePath))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                    continue;

                var idx = trimmed.IndexOf('=');
                if (idx <= 0)
                    continue;

                values[trimmed.Substring(0, idx).Trim()] = trimmed.Substring(idx + 1).Trim();
            }

            if (values.Count == 0)
                return false;

            WarnUnknownKeys(values);

            AutoPlay = ParseBool(values, "AutoPlay", DefaultAutoPlay);
            AllPerfect = ParseBool(values, "AllPerfect", DefaultAllPerfect);
            BlockSave = ParseBool(values, "BlockSave", DefaultBlockSave);
            EnableJudgmentBar = ParseBool(values, "EnableJudgmentBar", DefaultEnableJudgmentBar);
            JudgmentBarVertical = ParseBool(values, "JudgmentBarVertical", DefaultJudgmentBarVertical);
            JudgmentBarCapsule = ParseBool(values, "JudgmentBarCapsule", DefaultJudgmentBarCapsule);
            JudgmentBarLeft = ParseBool(values, "JudgmentBarLeft", DefaultJudgmentBarLeft);
            NoteSway = ParseBool(values, "NoteSway", DefaultNoteSway);
            NoteSwayAmplitude = ParseRangedFloat(values, "NoteSwayAmplitude", DefaultNoteSwayAmplitude, MinNoteSwayAmplitude, MaxNoteSwayAmplitude);
            NoteSwaySpeed = ParseRangedFloat(values, "NoteSwaySpeed", DefaultNoteSwaySpeed, MinNoteSwaySpeed, MaxNoteSwaySpeed);
            NoteSwayDamping = ParseBool(values, "NoteSwayDamping", DefaultNoteSwayDamping);
            NoteSwayDampingTime = ParseRangedFloat(values, "NoteSwayDampingTime", DefaultNoteSwayDampingTime, MinNoteSwayDampingTime, MaxNoteSwayDampingTime);
            NoteSpeedChaos = ParseBool(values, "NoteSpeedChaos", DefaultNoteSpeedChaos);
            NoteSpeedChaosMin = ParseRangedFloat(values, "NoteSpeedChaosMin", DefaultNoteSpeedChaosMin, MinNoteSpeedChaos, MaxNoteSpeedChaos);
            NoteSpeedChaosMax = ParseRangedFloat(values, "NoteSpeedChaosMax", DefaultNoteSpeedChaosMax, MinNoteSpeedChaos, MaxNoteSpeedChaos);
            NoteSpeedChaosPerLane = ParseBool(values, "NoteSpeedChaosPerLane", DefaultNoteSpeedChaosPerLane);
            return true;
        }

        /// <summary>오타나 옛 키를 조용히 무시하면 설정이 안 먹는 이유를 알 수 없으므로 한 번씩 알립니다(H3).</summary>
        private static void WarnUnknownKeys(Dictionary<string, string> values)
        {
            foreach (var key in values.Keys)
            {
                if (!KnownKeys.Contains(key))
                    MelonLogger.Warning($"[CustomKeySettings] 알 수 없는 설정 키를 무시합니다: {key}");
            }
        }

        /// <summary>
        /// 참: true, 트루, 참, 켜기, 켜짐, 활성화, on, 1, enable, enabled, y, yes
        /// 거짓: false, 폴스, 거짓, 비활성화, 끄기, 꺼짐, off, 0, disable, disabled, n, no
        /// 라벨 표현식 파싱
        /// </summary>
        public static bool ParseBool(Dictionary<string, string> values, string key, bool fallback)
        {
            if (!values.TryGetValue(key, out var raw))
                return fallback;

            if (!string.IsNullOrWhiteSpace(raw) && !TryParseBoolKeyword(raw, out _))
                MelonLogger.Warning($"[CustomKeySettings] {key}={raw} 은(는) 알 수 없는 값이라 기본값 {fallback}을 씁니다.");

            return ParseBool(raw, fallback);
        }

        public static bool ParseBool(string raw, bool fallback)
        {
            return TryParseBoolKeyword(raw, out bool value) ? value : fallback;
        }

        private static bool TryParseBoolKeyword(string raw, out bool value)
        {
            value = false;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                case "트루":
                case "참":
                case "켜기":
                case "켜짐":
                case "활성화":
                case "on":
                case "enable":
                case "enabled":
                case "y":
                case "yes":
                    value = true;
                    return true;

                case "0":
                case "false":
                case "폴스":
                case "거짓":
                case "비활성화":
                case "끄기":
                case "꺼짐":
                case "off":
                case "disable":
                case "disabled":
                case "n":
                case "no":
                    value = false;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 실수를 읽고 허용 범위로 맞춥니다. NaN과 무한대는 읽지 않은 것으로 보고 기본값을 씁니다(H3).
        /// 키가 없으면 조용히 기본값을 씁니다.
        /// </summary>
        private static float ParseRangedFloat(Dictionary<string, string> values, string key, float fallback, float min, float max)
        {
            if (!values.TryGetValue(key, out var raw))
                return fallback;

            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ||
                float.IsNaN(parsed) || float.IsInfinity(parsed))
            {
                MelonLogger.Warning($"[CustomKeySettings] {key}={raw} 은(는) 숫자가 아니라 기본값 {Num(fallback)}을 씁니다.");
                return fallback;
            }

            if (parsed < min || parsed > max)
            {
                float clamped = Math.Max(min, Math.Min(max, parsed));
                MelonLogger.Warning($"[CustomKeySettings] {key}={raw} 은(는) 허용 범위 {Num(min)}~{Num(max)}를 벗어나 {Num(clamped)}로 맞춥니다.");
                return clamped;
            }

            return parsed;
        }
    }
}
