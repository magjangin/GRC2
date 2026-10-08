using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GRC2.Core;
using GRC2.Injectors;
using GRC2.Parsers;
using HarmonyLib;
using IntiCreates;
using MelonLoader;

namespace GRC2.Harmony
{
    using MusicData = soRythmGameMusicDataMap.MusicData;
    using MusicID = soRythmGameMusicDataMap.MusicID;

    /// <summary>
    /// 곡 정보 크레딧(아티스트·작사·작곡·편곡·채보·수록 CD·원작 게임)을 커스텀 곡의 info.txt 값으로 바꿉니다.
    ///
    /// 게임이 이 텍스트를 그리는 곳은 두 군데이고, 둘 다 MusicData의 get*Text() 게터로 읽습니다.
    /// - 곡 선택 상세 패널: cMusicSelectArtWorkManager.requestSetMusicData(musicID)가 getMusicDataFromId(musicID)의
    ///   게터 7개를 부릅니다. 커스텀 MusicID는 원본 목록에 없어 id만 Invalid이고 나머지는 기본값인 MusicData가 오는데,
    ///   크레딧 enum이 전부 0이라 각 표의 첫 항목(실제 사람 이름)이 커스텀 곡의 크레딧으로 표시됐습니다.
    ///   이 MusicData로는 어느 커스텀 곡인지 알 수 없으므로 requestSetMusicData가 실행되는 동안만 그 musicID로 정합니다.
    /// - 곡 시작 화면: cPreRythmnGameStartScreen.coInitialize가 작사·작곡·편곡 게터를 부릅니다. 이때 MusicData는
    ///   곡 시작 창이 빌려 온 템플릿 곡의 것이라 템플릿 곡의 크레딧이 나왔습니다. 플레이 씬에서 커스텀 곡이
    ///   선택되어 있으면 현재 앨범 값을 씁니다(플레이 씬에서 이 게터를 부르는 곳은 곡 시작 화면뿐입니다).
    ///
    /// 게터만 바꾸므로 칸 배치, 라벨, 언어별 구분자는 원본 그대로입니다. info.txt에 없는 칸은 게임이 정보가 없을 때
    /// 쓰는 "-"(INVALID_MUSIC_INFO_DISP)로 보입니다.
    /// </summary>
    public static class CreditTextPatch
    {
        private static readonly Dictionary<string, Func<SongInfo, string>> CreditByGetter =
            new Dictionary<string, Func<SongInfo, string>>
            {
                { "getArtistText", info => info.Artist },
                { "getLylicistText", info => info.Lyricist },
                { "getComposerText", info => info.Composer },
                { "getArrangerText", info => info.Arranger },
                { "getNoteCreaterText", info => info.Charter },
                { "getCDTitleText", info => info.CdTitle },
                { "getGameTitleText", info => info.GameTitle }
            };

        /// <summary>info.txt가 없는 앨범용입니다. 모든 칸이 "-"로 보입니다.</summary>
        private static readonly SongInfo EmptySongInfo = new SongInfo();

        /// <summary>requestSetMusicData가 실행되는 동안만 true입니다.</summary>
        private static bool _inMusicSelectPanel;

        /// <summary>requestSetMusicData가 그리는 곡의 앨범. 원본 곡이면 null입니다.</summary>
        private static AlbumInfo _panelAlbum;

        /// <summary>지금 그리는 크레딧이 커스텀 곡의 것이면 그 곡 정보를, 아니면 null을 돌려줍니다.</summary>
        private static SongInfo ResolveCustomSongInfo()
        {
            AlbumInfo album;
            if (_inMusicSelectPanel)
            {
                // 상세 패널은 그리는 곡의 MusicID로만 정합니다. 커서가 원본 곡이면 커스텀 선택 상태와 무관하게 원본을 씁니다.
                album = _panelAlbum;
            }
            else if (BgmBgaInjector.IsPlayScene() && CustomAssetManager.ShouldInjectCustomContent())
            {
                album = AlbumManager.GetCurrentAlbum();
            }
            else
            {
                return null;
            }

            return album == null ? null : album.SongInfo ?? EmptySongInfo;
        }

        [HarmonyPatch(typeof(cMusicSelectArtWorkManager), "requestSetMusicData")]
        private static class MusicSelectPanelPatch
        {
            [HarmonyPrefix]
            private static void Prefix(MusicID musicID)
            {
                _inMusicSelectPanel = true;
                _panelAlbum = AlbumManager.GetAlbumByMusicID(musicID);
            }

            /// <summary>
            /// Finalizer라서 원본이 예외를 던져도 실행됩니다(반환형이 void면 원본 예외는 그대로 전파됩니다).
            /// 범위가 열린 채 남으면 곡 시작 화면에서도 패널 기준으로 판단하게 됩니다.
            /// </summary>
            [HarmonyFinalizer]
            private static void Finalizer()
            {
                _inMusicSelectPanel = false;
                _panelAlbum = null;
            }
        }

        [HarmonyPatch]
        private static class CreditGetterPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                return CreditByGetter.Keys
                    .Select(name => AccessTools.Method(typeof(MusicData), name))
                    .Where(method => method != null);
            }

            [HarmonyPrefix]
            private static bool Prefix(MethodBase __originalMethod, ref string __result)
            {
                try
                {
                    SongInfo songInfo = ResolveCustomSongInfo();
                    if (songInfo == null)
                        return true;

                    string value = CreditByGetter[__originalMethod.Name](songInfo);
                    __result = string.IsNullOrWhiteSpace(value)
                        ? soRythmGameMusicDataMap.INVALID_MUSIC_INFO_DISP
                        : value;
                    return false;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[CreditTextPatch] 크레딧 교체 오류 ({__originalMethod?.Name}): {ex.Message}");
                    return true;
                }
            }
        }
    }
}
