using System.Collections.Generic;
using UnityEngine;

namespace GRC2.Core
{
    /// <summary>
    /// 판정바를 MelonLoader의 OnGUI(IMGUI)에서 직접 그립니다. 게임의 UI 캔버스/오브젝트 트리를
    /// 전혀 만들지 않고 화면 위에 얹어 그리기만 하므로, 게임 원본 UI에는 영향이 없습니다.
    ///
    /// 구성은 sxtg2 판정바와 같습니다: 게임에서 읽은 실제 판정 범위 박스, 등급 색으로 찍혀
    /// HitFadeSeconds 동안 서서히 사라지는 틱, 마지막 판정의 오차(ms)·등급·FAST/LATE 라벨.
    /// </summary>
    public static class GameHud
    {
        private struct HitTick
        {
            public float OffsetMs;
            public float TimeAdded;
            public Color Color;
        }

        /// <summary>틱과 라벨이 서서히 사라지는 시간(초).</summary>
        private const float HitFadeSeconds = 1.5f;

        // JudgeType(PERFECT/GREAT/GOOD/BAD/MISS)과 같은 인덱스를 씁니다.
        private static readonly string[] JudgeNames = { "PERFECT", "GREAT", "GOOD", "BAD", "MISS" };
        private const int MissIndex = 4;

        private static readonly Color[] JudgeColors =
        {
            new Color(0.35f, 0.80f, 1.00f), // PERFECT
            new Color(0.45f, 0.90f, 0.45f), // GREAT
            new Color(1.00f, 0.85f, 0.25f), // GOOD
            new Color(1.00f, 0.55f, 0.20f), // BAD
            new Color(0.95f, 0.25f, 0.25f)  // MISS
        };

        // 탭 노트 판정 범위(ms, PERFECT/GREAT/GOOD/BAD 경계). 게임에서 읽기 전에는
        // 원본 기본값(cFairyModeNotesManager.tap_*JudgeRange)을 씁니다.
        private static readonly float[] RangeMs = { 33f, 66f, 99f, 132f };

        private static readonly List<HitTick> HitHistory = new List<HitTick>();
        private static float _lastHitTime = -999f;
        private static string _lastHitText;
        private static Color _lastHitColor = Color.white;

        private static Texture2D _whiteTex;
        private static GUIStyle _labelStyle;

        private static string _warningText;
        private static float _warningUntil = -1f;
        private static GUIStyle _warningStyle;
        private static readonly Dictionary<long, Texture2D> CapsuleTexCache = new Dictionary<long, Texture2D>();

        /// <summary>SceneDetector.OnGUI()에서 매 프레임 호출합니다. 플레이 씬이 아니면 아무것도 그리지 않습니다.</summary>
        public static void Draw(bool isPlayScene)
        {
            if (!isPlayScene)
            {
                // 다음 곡에 이전 곡의 틱/라벨/경고가 남지 않도록 플레이 씬을 벗어나면 비웁니다.
                ClearHits();
                ClearWarning();
                return;
            }

            // OnGUI는 프레임당 Layout/Repaint 등 여러 이벤트로 불립니다. 실제로 그려지는 건 Repaint뿐입니다.
            Event guiEvent = Event.current;
            if (guiEvent == null || guiEvent.type != EventType.Repaint)
                return;

            try
            {
                // 경고는 판정바 설정과 관계없이 보입니다. 주입이 취소됐는데 아무 표시가 없으면 원인을 알 수 없습니다(H2).
                DrawWarning();

                if (CustomKeySettings.EnableJudgmentBar)
                {
                    EnsureWhiteTexture();
                    DrawJudgmentBar();
                }
            }
            catch (System.Exception ex)
            {
                MelonLoader.MelonLogger.Warning("[GameHud] OnGUI 드로우 오류: " + ex.Message);
            }
        }

        /// <summary>게임의 실제 탭 판정 범위(ms)를 반영합니다. 값이 이상하면 기존 값을 유지합니다.</summary>
        public static void SetJudgeRanges(float perfectMs, float greatMs, float goodMs, float badMs)
        {
            if (perfectMs <= 0f || greatMs < perfectMs || goodMs < greatMs || badMs < goodMs)
                return;

            RangeMs[0] = perfectMs;
            RangeMs[1] = greatMs;
            RangeMs[2] = goodMs;
            RangeMs[3] = badMs;
        }

        /// <summary>
        /// 판정 하나를 등록합니다. subSample은 원본 OnJudgeParam.subSample(perfectSample - 입력 시점)이라
        /// 양수가 FAST, 음수가 LATE입니다. showTick이 false면 라벨만 갱신하고 막대에 틱은 찍지 않습니다.
        /// </summary>
        public static void ReportHit(int judgeIndex, int subSample, float samplesPerSecond, bool showTick)
        {
            if (!CustomKeySettings.EnableJudgmentBar || judgeIndex < 0 || judgeIndex >= MissIndex)
                return;

            float ms = samplesPerSecond > 0f ? subSample / samplesPerSecond * 1000f : 0f;
            Color color = JudgeColors[judgeIndex];
            float now = Time.unscaledTime;

            if (showTick)
                HitHistory.Add(new HitTick { OffsetMs = ms, TimeAdded = now, Color = color });

            string sign = ms > 0f ? "+" : "";
            string tag = ms > 0f ? " (FAST)" : (ms < 0f ? " (LATE)" : "");
            SetLabel(sign + ms.ToString("F1") + " ms · " + JudgeNames[judgeIndex] + tag, color, now);
        }

        /// <summary>MISS는 원본이 오차를 0으로 넘기므로 틱 없이 라벨만 띄웁니다.</summary>
        public static void ReportMiss()
        {
            if (!CustomKeySettings.EnableJudgmentBar)
                return;

            SetLabel(JudgeNames[MissIndex], JudgeColors[MissIndex], Time.unscaledTime);
        }

        /// <summary>플레이 화면 위쪽에 경고 문구를 seconds초 동안 보입니다. 로그에만 남으면 플레이 중에는 알 수 없습니다(H2).</summary>
        public static void ShowWarning(string text, float seconds)
        {
            _warningText = text;
            _warningUntil = Time.unscaledTime + seconds;
        }

        private static void ClearWarning()
        {
            _warningText = null;
            _warningUntil = -1f;
        }

        private static void DrawWarning()
        {
            if (string.IsNullOrEmpty(_warningText) || Time.unscaledTime >= _warningUntil)
                return;

            if (_warningStyle == null)
                _warningStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 20, alignment = TextAnchor.UpperCenter };

            _warningStyle.normal.textColor = new Color(1f, 0.35f, 0.35f, 1f);
            GUI.Label(new Rect(0f, 40f, Screen.width, 40f), _warningText, _warningStyle);
        }

        private static void ClearHits()
        {
            if (HitHistory.Count > 0)
                HitHistory.Clear();
            _lastHitTime = -999f;
        }

        private static void SetLabel(string text, Color color, float now)
        {
            _lastHitText = text;
            _lastHitColor = color;
            _lastHitTime = now;
        }

        private static void EnsureWhiteTexture()
        {
            if (_whiteTex != null)
                return;

            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();
        }

        private static void DrawJudgmentBar()
        {
            float now = Time.unscaledTime;
            for (int i = HitHistory.Count - 1; i >= 0; i--)
            {
                if (now - HitHistory[i].TimeAdded > HitFadeSeconds)
                    HitHistory.RemoveAt(i);
            }

            bool vertical = CustomKeySettings.JudgmentBarVertical;
            bool capsule = CustomKeySettings.JudgmentBarCapsule;

            Vector2 barSize = vertical ? new Vector2(28f, 320f) : new Vector2(320f, 28f);
            const float edgeMargin = 24f;
            Rect bar = vertical
                ? new Rect(
                    CustomKeySettings.JudgmentBarLeft ? edgeMargin : Screen.width - edgeMargin - barSize.x,
                    (Screen.height - barSize.y) / 2f,
                    barSize.x, barSize.y)
                : new Rect((Screen.width - barSize.x) / 2f, Screen.height / 2f - barSize.y / 2f, barSize.x, barSize.y);

            // 막대 끝 = BAD 경계. 그보다 큰 오차는 끝에 붙여 그립니다.
            float maxMs = RangeMs[3];
            float scale = (vertical ? bar.height : bar.width) / 2f / maxMs;
            Vector2 center = bar.center;

            // 배경 트랙(±BAD)과 넓은 등급부터 겹쳐 그리는 판정 범위 박스.
            DrawShape(bar, new Color(0.08f, 0.08f, 0.10f, 0.70f), capsule);
            DrawRangeBox(bar, center, vertical, RangeMs[2] * scale, Tint(JudgeColors[2], 0.22f), capsule); // GOOD
            DrawRangeBox(bar, center, vertical, RangeMs[1] * scale, Tint(JudgeColors[1], 0.24f), capsule); // GREAT
            DrawRangeBox(bar, center, vertical, RangeMs[0] * scale, Tint(JudgeColors[0], 0.36f), capsule); // PERFECT

            // 0ms 기준선.
            if (vertical)
                DrawColorRect(new Rect(bar.x - 2f, center.y - 1f, bar.width + 4f, 2f), Color.white);
            else
                DrawColorRect(new Rect(center.x - 1f, bar.y - 2f, 2f, bar.height + 4f), Color.white);

            // 틱: 세로는 위쪽 = FAST, 가로는 오른쪽 = FAST.
            foreach (HitTick tick in HitHistory)
            {
                float ms = Mathf.Clamp(tick.OffsetMs, -maxMs, maxMs);
                float alpha = Mathf.Clamp01(1f - (now - tick.TimeAdded) / HitFadeSeconds);
                Color color = new Color(tick.Color.r, tick.Color.g, tick.Color.b, alpha * 0.9f);

                if (vertical)
                    DrawColorRect(new Rect(bar.x - 1f, center.y - ms * scale - 1.5f, bar.width + 2f, 3f), color);
                else
                    DrawColorRect(new Rect(center.x + ms * scale - 1.5f, bar.y - 1f, 3f, bar.height + 2f), color);
            }

            DrawHitLabel(bar, vertical, now);
        }

        /// <summary>마지막 판정의 오차(ms)·등급 텍스트. 그림자 4방향 + 메인 텍스트.</summary>
        private static void DrawHitLabel(Rect bar, bool vertical, float now)
        {
            float elapsed = now - _lastHitTime;
            if (elapsed >= HitFadeSeconds || string.IsNullOrEmpty(_lastHitText))
                return;

            if (_labelStyle == null)
                _labelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 16 };

            const float labelWidth = 260f;
            const float labelHeight = 24f;
            Rect labelRect;
            if (!vertical)
            {
                _labelStyle.alignment = TextAnchor.LowerCenter;
                labelRect = new Rect(bar.center.x - labelWidth / 2f, bar.y - labelHeight - 6f, labelWidth, labelHeight);
            }
            else if (CustomKeySettings.JudgmentBarLeft)
            {
                _labelStyle.alignment = TextAnchor.MiddleLeft;
                labelRect = new Rect(bar.xMax + 10f, bar.center.y - labelHeight / 2f, labelWidth, labelHeight);
            }
            else
            {
                // 오른쪽 세로 막대는 라벨이 화면 밖으로 나가지 않게 막대 왼쪽에 붙입니다.
                _labelStyle.alignment = TextAnchor.MiddleRight;
                labelRect = new Rect(bar.x - 10f - labelWidth, bar.center.y - labelHeight / 2f, labelWidth, labelHeight);
            }

            float alpha = Mathf.Clamp01(1f - elapsed / HitFadeSeconds);
            const float shadow = 1.5f;
            _labelStyle.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.8f);
            GUI.Label(new Rect(labelRect.x - shadow, labelRect.y - shadow, labelRect.width, labelRect.height), _lastHitText, _labelStyle);
            GUI.Label(new Rect(labelRect.x + shadow, labelRect.y - shadow, labelRect.width, labelRect.height), _lastHitText, _labelStyle);
            GUI.Label(new Rect(labelRect.x - shadow, labelRect.y + shadow, labelRect.width, labelRect.height), _lastHitText, _labelStyle);
            GUI.Label(new Rect(labelRect.x + shadow, labelRect.y + shadow, labelRect.width, labelRect.height), _lastHitText, _labelStyle);

            _labelStyle.normal.textColor = new Color(_lastHitColor.r, _lastHitColor.g, _lastHitColor.b, alpha);
            GUI.Label(labelRect, _lastHitText, _labelStyle);
        }

        /// <summary>막대 중심에서 ±halfLength(px)만큼의 판정 범위 박스.</summary>
        private static void DrawRangeBox(Rect bar, Vector2 center, bool vertical, float halfLength, Color color, bool capsule)
        {
            Rect rect = vertical
                ? new Rect(bar.x + 1f, center.y - halfLength, bar.width - 2f, halfLength * 2f)
                : new Rect(center.x - halfLength, bar.y + 1f, halfLength * 2f, bar.height - 2f);

            DrawShape(rect, color, capsule);
        }

        private static Color Tint(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        private static void DrawColorRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, _whiteTex);
            GUI.color = Color.white;
        }

        /// <summary>사각 또는 알약(캡슐) 모양으로 rect를 칠합니다.</summary>
        private static void DrawShape(Rect rect, Color color, bool capsule)
        {
            int w = Mathf.RoundToInt(rect.width);
            int h = Mathf.RoundToInt(rect.height);
            if (!capsule || w < 2 || h < 2)
            {
                DrawColorRect(rect, color);
                return;
            }

            GUI.color = color;
            GUI.DrawTexture(rect, GetCapsuleTexture(w, h));
            GUI.color = Color.white;
        }

        /// <summary>양끝이 반원인 알약 모양의 알파 마스크 텍스처를 만들어 크기별로 캐시합니다.</summary>
        private static Texture2D GetCapsuleTexture(int w, int h)
        {
            long key = ((long)w << 32) | (uint)h;
            Texture2D cached;
            if (CapsuleTexCache.TryGetValue(key, out cached) && cached != null)
                return cached;

            // 판정 범위가 바뀔 때마다 크기가 달라지므로 캐시가 끝없이 쌓이지 않게 막습니다.
            if (CapsuleTexCache.Count > 32)
            {
                foreach (Texture2D tex in CapsuleTexCache.Values)
                    if (tex != null)
                        Object.Destroy(tex);
                CapsuleTexCache.Clear();
            }

            var texture = new Texture2D(w, h, TextureFormat.ARGB32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float radius = Mathf.Min(w, h) / 2f;
            Vector2 a = w >= h ? new Vector2(radius, h / 2f) : new Vector2(w / 2f, radius);
            Vector2 b = w >= h ? new Vector2(w - radius, h / 2f) : new Vector2(w / 2f, h - radius);

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float signedDist = DistanceToSegment(new Vector2(x + 0.5f, y + 0.5f), a, b) - radius;
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - signedDist) * 255f);
                    pixels[y * w + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            CapsuleTexCache[key] = texture;
            return texture;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            float t = lenSq > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq) : 0f;
            return Vector2.Distance(p, a + t * ab);
        }
    }
}
