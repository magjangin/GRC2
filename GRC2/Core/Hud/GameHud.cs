using IntiCreates.RythmGame;
using UnityEngine;

namespace GRC2.Core.Hud
{
    /// <summary>
    /// 판정바를 MelonLoader의 OnGUI(IMGUI)에서 직접 그립니다. 게임의 UI 캔버스/오브젝트 트리를
    /// 전혀 만들지 않고 화면 위에 얹어 그리기만 하므로, 게임 원본 UI에는 영향이 없습니다.
    /// </summary>
    public static class GameHud
    {
        private const float JudgmentBarRangeMs = 132f; // tap_badJudgeRange 기준 (판정 가능한 최대 오차)

        private static Texture2D _whiteTex;

        private static Texture2D _barTexture;
        private static int _barTextureW = -1;
        private static int _barTextureH = -1;
        private static bool _barTextureVertical;
        private static bool _barTextureCapsule;

        private static float? _latestJudgmentMs;

        private static void EnsureStyles()
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }
        }

        /// <summary>SceneDetector.OnGUI()에서 매 프레임 호출합니다. 플레이 씬이 아니면 아무것도 그리지 않습니다.</summary>
        public static void Draw(bool isPlayScene)
        {
            if (!isPlayScene)
                return;

            try
            {
                EnsureStyles();

                if (CustomKeySettings.EnableJudgmentBar)
                    DrawJudgmentBar();
            }
            catch (System.Exception ex)
            {
                MelonLoader.MelonLogger.Warning("[GameHud] OnGUI 드로우 오류: " + ex.Message);
            }
        }

        /// <summary>judgeType/subSample(샘플 단위 오차)을 받아 판정바 마커 위치를 갱신합니다.</summary>
        public static void ReportJudgment(JudgeType judgeType, int subSample, float samplesPerSecond)
        {
            if (!CustomKeySettings.EnableJudgmentBar)
                return;

            float ms = samplesPerSecond > 0f ? subSample / samplesPerSecond * 1000f : 0f;
            _latestJudgmentMs = Mathf.Clamp(ms, -JudgmentBarRangeMs, JudgmentBarRangeMs);
        }

        private static void DrawJudgmentBar()
        {
            bool vertical = CustomKeySettings.JudgmentBarVertical;

            Vector2 barSize = vertical ? new Vector2(28f, 320f) : new Vector2(320f, 28f);
            const float edgeMargin = 24f;
            Rect bar = vertical
                ? new Rect(
                    CustomKeySettings.JudgmentBarLeft ? edgeMargin : Screen.width - edgeMargin - barSize.x,
                    (Screen.height - barSize.y) / 2f,
                    barSize.x, barSize.y)
                : new Rect((Screen.width - barSize.x) / 2f, Screen.height / 2f - barSize.y / 2f, barSize.x, barSize.y);

            EnsureBarTexture((int)barSize.x, (int)barSize.y, vertical, CustomKeySettings.JudgmentBarCapsule);
            GUI.DrawTexture(bar, _barTexture);

            float normalized = _latestJudgmentMs.HasValue
                ? Mathf.Clamp(_latestJudgmentMs.Value / JudgmentBarRangeMs, -1f, 1f)
                : 0f; // 아직 판정이 없으면 정중앙에 마커를 둡니다.

            const float markerThickness = 3f;
            if (vertical)
            {
                float y = bar.y + bar.height / 2f - normalized * (bar.height / 2f);
                DrawColorRect(new Rect(bar.x, y - markerThickness / 2f, bar.width, markerThickness), Color.white);
            }
            else
            {
                float x = bar.x + bar.width / 2f + normalized * (bar.width / 2f);
                DrawColorRect(new Rect(x - markerThickness / 2f, bar.y, markerThickness, bar.height), Color.white);
            }
        }

        /// <summary>
        /// 알약(캡슐) 모양 또는 사각 바 배경에, 중앙(파랑)에서 바깥(남색)으로 갈수록 어두워지는 색 띠
        /// 4단계를 좌우/상하 대칭으로 구운 텍스처를 만듭니다. 설정이 바뀌지 않으면 캐시를 재사용합니다.
        /// </summary>
        private static void EnsureBarTexture(int width, int height, bool vertical, bool capsule)
        {
            if (_barTexture != null && _barTextureW == width && _barTextureH == height &&
                _barTextureVertical == vertical && _barTextureCapsule == capsule)
                return;

            if (_barTexture != null)
                Object.Destroy(_barTexture);

            _barTexture = BuildBarTexture(width, height, vertical, capsule);
            _barTextureW = width;
            _barTextureH = height;
            _barTextureVertical = vertical;
            _barTextureCapsule = capsule;
        }

        private static readonly Color[] BandColors =
        {
            new Color(0.29f, 0.42f, 0.55f, 0.95f), // 중앙: 스틸블루
            new Color(0.42f, 0.38f, 0.32f, 0.90f), // 회갈색
            new Color(0.30f, 0.28f, 0.12f, 0.90f), // 올리브
            new Color(0.08f, 0.09f, 0.14f, 0.90f)  // 가장 바깥: 다크 네이비
        };

        private static Texture2D BuildBarTexture(int width, int height, bool vertical, bool capsule)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };

            float shortAxis = vertical ? width : height;
            float longAxis = vertical ? height : width;
            float radius = capsule ? shortAxis / 2f : 0f; // 사각 바는 반지름 0 = 둥근 캡 없음

            Vector2 capA = vertical ? new Vector2(width / 2f, radius) : new Vector2(radius, height / 2f);
            Vector2 capB = vertical ? new Vector2(width / 2f, height - radius) : new Vector2(width - radius, height / 2f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float posOnLongAxis = vertical ? y : x;
                    float centerDist = Mathf.Abs(posOnLongAxis - longAxis / 2f);
                    float t = Mathf.Clamp01(centerDist / (longAxis / 2f));
                    int bandIndex = Mathf.Clamp(Mathf.FloorToInt(t * BandColors.Length), 0, BandColors.Length - 1);
                    Color color = BandColors[bandIndex];

                    bool visible = true;
                    if (vertical)
                    {
                        if (y < radius)
                            visible = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), capA) <= radius;
                        else if (y > height - radius)
                            visible = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), capB) <= radius;
                    }
                    else
                    {
                        if (x < radius)
                            visible = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), capA) <= radius;
                        else if (x > width - radius)
                            visible = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), capB) <= radius;
                    }

                    tex.SetPixel(x, y, visible ? color : new Color(0f, 0f, 0f, 0f));
                }
            }

            tex.Apply();
            return tex;
        }

        private static void DrawColorRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, _whiteTex);
            GUI.color = Color.white;
        }
    }
}
