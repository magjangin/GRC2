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
    /// 게임 로드 시 한 번만 읽으며, 값을 바꾸려면 파일을 수정한 뒤 게임을 재시작해야 합니다.
    /// </summary>
    public static class CustomKeySettings
    {
        private const string FolderName = "savecustomkey";
        private const string FileName = "config.txt";

        public static bool AutoPlay { get; private set; }
        public static bool AllPerfect { get; private set; }
        public static bool BlockSave { get; private set; } = true;

        public static bool EnableJudgmentBar { get; private set; } = true;
        public static bool JudgmentBarVertical { get; private set; } = true;
        public static bool JudgmentBarCapsule { get; private set; }
        public static bool JudgmentBarLeft { get; private set; } = true;

        public static bool NoteSway { get; private set; }
        public static float NoteSwayAmplitude { get; private set; } = 20f;
        public static float NoteSwaySpeed { get; private set; } = 0.8f;
        public static bool NoteSwayDamping { get; private set; } = true;
        public static float NoteSwayDampingTime { get; private set; } = 0.4f;

        public static bool NoteSpeedChaos { get; private set; }
        public static float NoteSpeedChaosMin { get; private set; } = 0.6f;
        public static float NoteSpeedChaosMax { get; private set; } = 1.8f;
        public static bool NoteSpeedChaosPerLane { get; private set; } = true;

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

                Load(filePath);
                MelonLogger.Msg(
                    "[CustomKeySettings] 로드 완료: " +
                    $"AutoPlay={AutoPlay}, AllPerfect={AllPerfect}, BlockSave={BlockSave}, " +
                    $"EnableJudgmentBar={EnableJudgmentBar}(Vertical={JudgmentBarVertical}, Capsule={JudgmentBarCapsule}, Left={JudgmentBarLeft}), " +
                    $"NoteSway={NoteSway}(Amplitude={NoteSwayAmplitude}, Speed={NoteSwaySpeed}, " +
                    $"Damping={NoteSwayDamping}, DampingTime={NoteSwayDampingTime}), " +
                    $"NoteSpeedChaos={NoteSpeedChaos}(Min={NoteSpeedChaosMin}, Max={NoteSpeedChaosMax}, " +
                    $"PerLane={NoteSpeedChaosPerLane})");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[CustomKeySettings]", "초기화 오류");
            }
        }

        private static readonly string[] DefaultLines =
        {
            "# 지원 형식: 1/0, true/false, 참/거짓, 켜기/끄기(켜짐/꺼짐), 활성화/비활성화, on/off, enable/disable, enabled/disabled, y/n, yes/no, 트루/폴스",
            "",
            "# 오토 플레이 (1 = 켜짐, 0 = 꺼짐)",
            "AutoPlay=0",
            "",
            "# 올 퍼펙트 판정 조작 - BLUESTAR (1 = 켜짐, 0 = 꺼짐)",
            "AllPerfect=1",
            "",
            "# 베스트 스코어 / 랭킹 저장 차단 (1 = 켜짐, 0 = 꺼짐)",
            "BlockSave=1",
            "",
            "# 실시간 판정바 표시 (1 = 켜짐, 0 = 꺼짐)",
            "EnableJudgmentBar=1",
            "",
            "# 판정바 형태 (1 = 세로 판정바, 0 = 가로 판정바)",
            "JudgmentBarVertical=1",
            "",
            "# 판정바 모양 (1 = 알약(캡슐) 모양, 0 = 사각 바)",
            "JudgmentBarCapsule=0",
            "",
            "# 세로 판정바를 화면 어느 쪽에 둘지 (1 = 왼쪽, 0 = 오른쪽). 가로 판정바에는 영향 없음(항상 중앙).",
            "JudgmentBarLeft=1",
            "",
            "# 노트가 눈송이처럼 좌우로 흔들리며 내려오는 연출 (1 = 켜짐, 0 = 꺼짐)",
            "# 판정에는 전혀 영향이 없는 순수 시각 효과입니다.",
            "NoteSway=0",
            "",
            "# 흔들림 폭 (픽셀). 너무 크면 레인 밖으로 나가 잘릴 수 있습니다.",
            "NoteSwayAmplitude=20",
            "",
            "# 흔들림 속도 (초당 왕복 횟수)",
            "NoteSwaySpeed=0.8",
            "",
            "# 판정선에 가까워지면 흔들림을 잦아들게 함 (1 = 켜짐, 0 = 꺼짐)",
            "# 끄면 판정선에 닿는 순간까지 계속 흔들립니다.",
            "NoteSwayDamping=1",
            "",
            "# 판정선 도달 몇 초 전부터 흔들림이 잦아들지",
            "NoteSwayDampingTime=0.4",
            "",
            "# [챌린지] 노트마다 낙하 속도를 제각각으로 (1 = 켜짐, 0 = 꺼짐)",
            "# 노트끼리 서로 추월하므로 읽기가 매우 어려워집니다. 판정에는 영향이 없습니다.",
            "NoteSpeedChaos=0",
            "",
            "# 속도 배율 범위 (1 = 원래 속도). 예: 0.6 ~ 1.8",
            "NoteSpeedChaosMin=0.6",
            "NoteSpeedChaosMax=1.8",
            "",
            "# 1 = 레인마다 속도가 다름(같은 레인 안에서는 순서 유지, 읽을 수는 있음)",
            "# 0 = 노트마다 속도가 다름(완전 카오스)",
            "NoteSpeedChaosPerLane=1"
        };

        private static void Load(string filePath)
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

            AutoPlay = ParseBool(values, "AutoPlay", false);
            AllPerfect = ParseBool(values, "AllPerfect", false);
            BlockSave = ParseBool(values, "BlockSave", true);
            EnableJudgmentBar = ParseBool(values, "EnableJudgmentBar", true);
            JudgmentBarVertical = ParseBool(values, "JudgmentBarVertical", true);
            JudgmentBarCapsule = ParseBool(values, "JudgmentBarCapsule", false);
            JudgmentBarLeft = ParseBool(values, "JudgmentBarLeft", true);
            NoteSway = ParseBool(values, "NoteSway", false);
            NoteSwayAmplitude = ParseFloat(values, "NoteSwayAmplitude", 20f);
            NoteSwaySpeed = ParseFloat(values, "NoteSwaySpeed", 0.8f);
            NoteSwayDamping = ParseBool(values, "NoteSwayDamping", true);
            NoteSwayDampingTime = ParseFloat(values, "NoteSwayDampingTime", 0.4f);
            NoteSpeedChaos = ParseBool(values, "NoteSpeedChaos", false);
            NoteSpeedChaosMin = ParseFloat(values, "NoteSpeedChaosMin", 0.6f);
            NoteSpeedChaosMax = ParseFloat(values, "NoteSpeedChaosMax", 1.8f);
            NoteSpeedChaosPerLane = ParseBool(values, "NoteSpeedChaosPerLane", true);
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

            return ParseBool(raw, fallback);
        }

        public static bool ParseBool(string raw, bool fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;

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
                    return false;

                default:
                    return fallback;
            }
        }

        private static float ParseFloat(Dictionary<string, string> values, string key, float fallback)
        {
            if (values.TryGetValue(key, out var raw) &&
                float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }
}
