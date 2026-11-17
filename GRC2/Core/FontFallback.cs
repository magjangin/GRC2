using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GRC2.Helpers;
using HarmonyLib;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace GRC2.Core
{
    /// <summary>
    /// 커스텀 곡 제목의 □ 깨짐을 고칩니다.
    ///
    /// 게임은 언어마다 정적(Static) TMP 폰트 아틀라스를 씁니다(cLocalizedTMPFontEvent, PCDMainFontAssetTable).
    /// 아틀라스에는 게임에 나오는 글자만 구워져 있어서 커스텀 곡의 간체 한자(麦, 盖 등)는 □로 그려집니다.
    /// 여기서는 윈도우(또는 hwa/fonts) 폰트 파일로 Dynamic TMP 폰트를 만들어 TMP 전역 fallback
    /// (TMP_Settings.fallbackFontAssets)에 붙입니다. TMP는 글자를 "현재 폰트 → 그 폰트의 fallback →
    /// 전역 fallback" 순으로 찾으므로(TextMeshProUGUI.SetArraySizes), 게임 폰트에 있는 글자는 그대로이고
    /// 없는 글자만 이 폰트로 그 자리에서 그려 넣습니다. 언어를 바꿔 게임 폰트가 교체돼도 전역 목록은 그대로라
    /// 따로 훅이 필요 없습니다.
    ///
    /// 한계: 게임 폰트가 "있다"고 답하는 글자는 fallback으로 넘어가지 않습니다. 그런 글자는
    /// <see cref="LogCoverageDiagnostics"/> 로그로 어느 폰트가 가져가는지 확인합니다.
    /// </summary>
    public static class FontFallback
    {
        private const string LogPrefix = "[폰트]";

        // TMP 기본값(90/9)보다 작은 샘플 크기로 아틀라스 하나에 더 많은 글자를 담습니다. 곡 목록 글자는
        // 화면에서 30px 안팎이라 72면 충분하고, 패딩은 게임 머티리얼의 외곽선이 잘리지 않게 TMP 기본값을 둡니다.
        private const int SamplingPointSize = 72;
        private const int AtlasPadding = 9;
        private const int AtlasSize = 2048;

        private static readonly List<TMP_FontAsset> _fallbackFonts = new List<TMP_FontAsset>();
        private static bool _created;
        private static bool _diagnosticsLogged;

        /// <summary>
        /// 폰트를 (처음 한 번) 만들고 전역 fallback에 붙어 있는지 확인합니다. 씬이 로드될 때마다 불러도 됩니다.
        /// </summary>
        public static void EnsureInstalled(string gameFolder)
        {
            try
            {
                if (!_created)
                {
                    _created = true;
                    CreateFallbackFonts(gameFolder);
                }

                if (_fallbackFonts.Count == 0)
                    return;

                if (!TryAttachToGlobalFallback())
                    AttachToLoadedFontAssets();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, LogPrefix, "fallback 폰트 설치 실패");
            }
        }

        private static void CreateFallbackFonts(string gameFolder)
        {
            string userFolder = string.IsNullOrEmpty(gameFolder)
                ? null
                : Path.Combine(Path.Combine(gameFolder, "hwa"), FallbackFontCandidates.UserFontFolderName);
            string systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

            var paths = FallbackFontCandidates.Resolve(
                userFolder,
                systemFolder,
                dir => Directory.Exists(dir) ? Directory.GetFiles(dir) : Array.Empty<string>(),
                File.Exists);

            if (paths.Count == 0)
            {
                MelonLogger.Warning($"{LogPrefix} fallback으로 쓸 폰트 파일을 찾지 못했습니다(시스템 폰트 폴더: {systemFolder}). 게임 폰트에 없는 글자는 □로 보입니다.");
                return;
            }

            foreach (var path in paths)
            {
                var asset = CreateDynamicFontAsset(path);
                if (asset != null)
                    _fallbackFonts.Add(asset);
            }

            MelonLogger.Msg($"{LogPrefix} fallback 폰트 {_fallbackFonts.Count}/{paths.Count}개 준비: {string.Join(", ", _fallbackFonts.Select(f => f.name))}");
        }

        private static TMP_FontAsset CreateDynamicFontAsset(string path)
        {
            try
            {
                // Font(string)는 경로에 폴더가 있으면 파일에서 폰트 데이터를 읽습니다(Internal_CreateFontFromPath).
                // CreateDynamicFontFromOSFont로 만든 폰트는 TMP의 FontEngine.LoadFontFace가 못 읽을 수 있어 쓰지 않습니다.
                var font = new Font(path);
                font.name = Path.GetFileNameWithoutExtension(path);
                font.hideFlags = HideFlags.DontUnloadUnusedAsset;

                var asset = TMP_FontAsset.CreateFontAsset(
                    font, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
                    AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);

                if (asset == null)
                {
                    MelonLogger.Warning($"{LogPrefix} 폰트를 읽지 못했습니다: {path}");
                    return null;
                }

                asset.name = "GRC2 Fallback - " + font.name;
                // 씬 전환의 Resources.UnloadUnusedAssets에 아틀라스·머티리얼이 내려가지 않게 붙잡아 둡니다.
                asset.hideFlags = HideFlags.DontUnloadUnusedAsset;
                if (asset.material != null) asset.material.hideFlags = HideFlags.DontUnloadUnusedAsset;
                if (asset.atlasTextures != null)
                {
                    foreach (var tex in asset.atlasTextures)
                        if (tex != null) tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
                }
                return asset;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"{LogPrefix} 폰트 생성 실패({path}): {ex.Message}");
                return null;
            }
        }

        /// <summary>TMP 전역 fallback 목록 끝에 붙입니다. 게임이 원래 넣어 둔 fallback이 먼저 검색되게 끝에 둡니다.</summary>
        private static bool TryAttachToGlobalFallback()
        {
            var settings = TMP_Settings.instance;
            if (settings == null)
            {
                MelonLogger.Warning($"{LogPrefix} TMP Settings가 없어 게임 폰트마다 fallback을 붙입니다.");
                return false;
            }

            var list = TMP_Settings.fallbackFontAssets;
            if (list == null)
            {
                list = new List<TMP_FontAsset>();
                AccessTools.Field(typeof(TMP_Settings), "m_fallbackFontAssets")?.SetValue(settings, list);
                if (TMP_Settings.fallbackFontAssets == null)
                    return false;
            }

            int added = 0;
            foreach (var font in _fallbackFonts)
            {
                if (!list.Contains(font))
                {
                    list.Add(font);
                    added++;
                }
            }

            if (added > 0)
                MelonLogger.Msg($"{LogPrefix} TMP 전역 fallback에 {added}개 추가(전체 {list.Count}개)");
            return true;
        }

        /// <summary>전역 목록을 못 쓸 때의 대안: 지금 메모리에 있는 게임 폰트마다 fallback을 붙입니다.</summary>
        private static void AttachToLoadedFontAssets()
        {
            int touched = 0;
            foreach (var gameFont in GetLoadedGameFonts())
            {
                if (gameFont.fallbackFontAssetTable == null)
                    gameFont.fallbackFontAssetTable = new List<TMP_FontAsset>();

                bool changed = false;
                foreach (var font in _fallbackFonts)
                {
                    if (!gameFont.fallbackFontAssetTable.Contains(font))
                    {
                        gameFont.fallbackFontAssetTable.Add(font);
                        changed = true;
                    }
                }
                if (changed) touched++;
            }

            if (touched > 0)
                MelonLogger.Msg($"{LogPrefix} 게임 폰트 {touched}개에 fallback 추가");
        }

        private static IEnumerable<TMP_FontAsset> GetLoadedGameFonts()
        {
            return Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
                .Where(f => f != null && !_fallbackFonts.Contains(f));
        }

        /// <summary>
        /// 곡 선택 씬에 처음 들어갈 때 한 번, 커스텀 곡 문자열의 비ASCII 글자가 어느 폰트에서 그려지는지 기록합니다.
        /// 게임 폰트가 엉뚱한 글리프로 "가지고 있는" 글자(예: 吉이 다른 모양으로 보임)를 찾는 용도입니다.
        /// </summary>
        public static void LogCoverageDiagnostics()
        {
            if (_diagnosticsLogged) return;
            _diagnosticsLogged = true;

            try
            {
                var texts = AlbumManager.GetAllAlbums().Values
                    .Select(a => a.SongInfo)
                    .Where(s => s != null)
                    .SelectMany(s => new[] { s.Title, s.Artist, s.Lyricist, s.Composer, s.Arranger, s.Charter, s.CdTitle, s.GameTitle });
                var codePoints = FallbackFontCandidates.CollectNonAsciiCodePoints(texts);
                if (codePoints.Count == 0) return;

                var gameFonts = GetLoadedGameFonts().ToList();
                MelonLogger.Msg($"{LogPrefix} 로드된 게임 폰트 {gameFonts.Count}개: {string.Join(", ", gameFonts.Select(f => $"{f.name}({f.atlasPopulationMode}, {f.characterLookupTable?.Count ?? 0}자)"))}");

                var byFont = new Dictionary<string, List<string>>();
                foreach (var cp in codePoints)
                {
                    uint unicode = (uint)cp;
                    string owner = gameFonts.FirstOrDefault(f => f.characterLookupTable != null && f.characterLookupTable.ContainsKey(unicode))?.name;
                    if (owner == null)
                    {
                        // 게임 폰트에 없으면 fallback 폰트가 실제로 그릴 수 있는지 확인합니다(Dynamic이라 이때 아틀라스에 추가됨).
                        owner = _fallbackFonts.FirstOrDefault(f => f.HasCharacter((char)Math.Min(cp, 0xFFFF), searchFallbacks: false, tryAddCharacter: true))?.name
                                ?? "없음(□)";
                    }

                    if (!byFont.TryGetValue(owner, out var list))
                        byFont[owner] = list = new List<string>();
                    list.Add(char.ConvertFromUtf32(cp));
                }

                foreach (var pair in byFont.OrderBy(p => p.Key))
                    MelonLogger.Msg($"{LogPrefix} {pair.Key}: {string.Join("", pair.Value)}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"{LogPrefix} 글자 진단 실패: {ex.Message}");
            }
        }
    }
}
