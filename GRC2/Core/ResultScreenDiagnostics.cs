using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MelonLoader;
using TMPro;
using UnityEngine;

namespace GRC2.Core
{
    /// <summary>
    /// 결과 화면에서 커스텀 곡 제목 한자가 □로 보인다는 보고(2026-11-24, 알려진_문제 L2)를 조사하는 임시 진단입니다.
    /// TMP는 글자를 fallback 폰트(msyh)로 그렸다고 답하는데 화면에는 □가 보여서, 화면에 있는 모든 글자 칸의
    /// 글자별 폰트·머티리얼·아틀라스 상태를 남기고 결과 화면을 캡처합니다. 원인을 고치면 지웁니다.
    /// </summary>
    internal static class ResultScreenDiagnostics
    {
        private const string Prefix = "[결과 화면 진단]";
        private static bool _running;

        public static void Run(TextMeshProUGUI title)
        {
            if (_running) return;
            _running = true;
            MelonCoroutines.Start(RunCoroutine(title));
        }

        private static IEnumerator RunCoroutine(TextMeshProUGUI title)
        {
            // 제목이 페이드 연출 뒤에 켜질 수 있어 최대 5초까지 활성화를 기다린 뒤, 결과 연출이 자리 잡도록 3초 더 기다립니다.
            float deadline = Time.realtimeSinceStartup + 5f;
            do { yield return null; }
            while (title != null && !title.isActiveAndEnabled && Time.realtimeSinceStartup < deadline);

            float captureAt = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < captureAt)
                yield return null;

            try
            {
                LogAllTexts();
                CaptureScreen();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"{Prefix} 실패: {ex.Message}");
            }
            finally
            {
                _running = false;
            }
        }

        private static void LogAllTexts()
        {
            int scanned = 0;
            foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (text == null || !text.gameObject.scene.isLoaded || !text.isActiveAndEnabled)
                    continue;
                string s = text.text;
                if (string.IsNullOrEmpty(s) || !HasNonAscii(s))
                    continue;

                scanned++;
                text.ForceMeshUpdate();
                MelonLogger.Msg($"{Prefix} 글자 칸 {PathOf(text.transform)}: '{s}' 폰트 {text.font?.name}, 머티리얼 {text.fontSharedMaterial?.name}");

                var info = text.textInfo;
                for (int i = 0; info != null && i < info.characterCount; i++)
                {
                    var ch = info.characterInfo[i];
                    if (ch.character < 0x80) continue;
                    MelonLogger.Msg($"{Prefix}   {DescribeChar(ch)}");
                }
            }

            foreach (var legacy in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Text>())
            {
                if (legacy == null || !legacy.gameObject.scene.isLoaded || !legacy.isActiveAndEnabled)
                    continue;
                if (string.IsNullOrEmpty(legacy.text) || !HasNonAscii(legacy.text))
                    continue;
                scanned++;
                MelonLogger.Msg($"{Prefix} 일반 Text {PathOf(legacy.transform)}: '{legacy.text}' 폰트 {legacy.font?.name}(dynamic {legacy.font?.dynamic})");
            }

            MelonLogger.Msg($"{Prefix} 비ASCII 글자가 있는 활성 글자 칸 {scanned}개를 기록했습니다.");
        }

        private static string DescribeChar(TMP_CharacterInfo ch)
        {
            var sb = new StringBuilder();
            sb.Append(ch.character).Append("(U+").Append(((int)ch.character).ToString("X4")).Append(") ");
            sb.Append("보임 ").Append(ch.isVisible).Append(", 폰트 ").Append(ch.fontAsset != null ? ch.fontAsset.name : "없음");

            var glyph = ch.textElement?.glyph;
            if (glyph != null)
            {
                var r = glyph.glyphRect;
                sb.Append(", 글리프 #").Append(glyph.index).Append(" 아틀라스 ").Append(glyph.atlasIndex)
                  .Append(" 칸 ").Append(r.x).Append(',').Append(r.y).Append(' ').Append(r.width).Append('x').Append(r.height);

                var atlas = ch.fontAsset != null && ch.fontAsset.atlasTextures != null && glyph.atlasIndex < ch.fontAsset.atlasTextures.Length
                    ? ch.fontAsset.atlasTextures[glyph.atlasIndex] : null;
                sb.Append(", 아틀라스 텍스처 ").Append(atlas != null ? $"{atlas.name}#{atlas.GetInstanceID()} {atlas.width}x{atlas.height} {atlas.format}" : "없음");
                sb.Append(", 칸 알파 ").Append(DescribeAlpha(atlas, r));
            }

            var mat = ch.material;
            if (mat != null)
            {
                var tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                sb.Append(", 그린 머티리얼 ").Append(mat.name).Append(" 셰이더 ").Append(mat.shader?.name)
                  .Append(" _MainTex ").Append(tex != null ? $"{tex.name}#{tex.GetInstanceID()}" : "없음")
                  .Append(" _GradientScale ").Append(GetFloat(mat, "_GradientScale"))
                  .Append(" _UnderlayDilate ").Append(GetFloat(mat, "_UnderlayDilate"))
                  .Append(" _UnderlaySoftness ").Append(GetFloat(mat, "_UnderlaySoftness"))
                  .Append(" _ScaleRatioC ").Append(GetFloat(mat, "_ScaleRatioC"))
                  .Append(" 키워드 ").Append(string.Join("|", mat.shaderKeywords ?? Array.Empty<string>()));
            }
            return sb.ToString();
        }

        private static string GetFloat(Material mat, string name)
            => mat.HasProperty(name) ? mat.GetFloat(name).ToString("0.###") : "-";

        /// <summary>글리프 칸의 알파 최댓값·평균. 0이면 아틀라스에 글자가 안 그려진 것.</summary>
        private static string DescribeAlpha(Texture2D atlas, UnityEngine.TextCore.GlyphRect r)
        {
            if (atlas == null || r.width <= 0 || r.height <= 0) return "-";
            try
            {
                if (!atlas.isReadable) return "읽기 불가";
                var pixels = atlas.GetPixels(r.x, r.y, r.width, r.height);
                float max = 0f, sum = 0f;
                foreach (var p in pixels)
                {
                    if (p.a > max) max = p.a;
                    sum += p.a;
                }
                return $"최대 {max:0.00} 평균 {sum / pixels.Length:0.00}";
            }
            catch (Exception ex)
            {
                return "실패(" + ex.Message + ")";
            }
        }

        private static void CaptureScreen()
        {
            string folder = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "GRC2");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"result_screen_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            ScreenCapture.CaptureScreenshot(path);
            MelonLogger.Msg($"{Prefix} 결과 화면 캡처: {path}");
        }

        private static bool HasNonAscii(string s)
        {
            foreach (char c in s)
                if (c >= 0x80) return true;
            return false;
        }

        private static string PathOf(Transform t)
        {
            var names = new List<string>();
            for (; t != null && names.Count < 6; t = t.parent)
                names.Add(t.name);
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
