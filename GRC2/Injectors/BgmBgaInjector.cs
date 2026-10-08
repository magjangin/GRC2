using System;
using System.Collections;
using System.IO;
using System.Linq;
using MelonLoader;
using UnityEngine;
using GRC2.Core;

namespace GRC2.Injectors
{
    public static class BgmBgaInjector
    {
        private static string _bgaFilePath;
        private static string _bgmFilePath;
        private static object _injectionCoroutine;
        private static bool _isPlayScene = false;

        public static void StartInjection(bool isPlayScene = false)
        {
            // 플레이 씬이면 무조건 true로 설정 (다른 씬에서 false로 덮어씌워지는 것 방지)
            if (isPlayScene)
            {
                _isPlayScene = true;
                MelonLogger.Msg($"[BgmBgaInjector] StartInjection 호출: 플레이 씬 감지, _isPlayScene=true로 설정");
            }
            else if (!_isPlayScene)
            {
                // 이미 플레이 씬 상태가 아니므로 _isPlayScene은 그대로 false입니다.
                MelonLogger.Msg($"[BgmBgaInjector] StartInjection 호출: 일반 씬, _isPlayScene=false");
            }
            else
            {
                // 이미 플레이 씬이면 false로 덮어쓰지 않음
                MelonLogger.Msg($"[BgmBgaInjector] StartInjection 호출: 플레이 씬 상태 유지 (_isPlayScene=true)");
            }
            
            // 재시작 시 주입 상태 리셋
            if (_injectionCoroutine != null)
            {
                MelonLogger.Msg($"[BgmBgaInjector] 기존 코루틴 중지 및 상태 리셋 (재시작)");
                StopInjection();
                
                // 재시작 시 아트워크 캐시도 리셋 (플레이 씬 재로드 시 새로운 GameObject를 찾기 위해)
                Core.PlaySceneArtworkInjector.ResetCache();
            }

            // MelonCoroutines를 사용하여 코루틴 시작
            _injectionCoroutine = MelonCoroutines.Start(InjectBgmBgaCoroutine());
        }

        private static void ResetInjectionState()
        {
            BgaInjector.Reset();
            BgmInjector.Reset();
            MelonLogger.Msg("[BgmBgaInjector] 주입 상태 리셋 완료");
        }

        public static void StopInjection()
        {
            if (_injectionCoroutine != null)
            {
                MelonCoroutines.Stop(_injectionCoroutine);
                _injectionCoroutine = null;
            }

            ResetInjectionState();
        }

        public static void ResetPlaySceneState()
        {
            _isPlayScene = false;
            MelonLogger.Msg("[BgmBgaInjector] 플레이 씬 상태 리셋: _isPlayScene=false");
        }

        public static bool IsPlayScene()
        {
            return _isPlayScene;
        }

        private static IEnumerator InjectBgmBgaCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(2f); // 2초마다 체크

                // 커스텀 차트가 선택되지 않았거나, 주입 금지 씬(SoundPlayerScene/MoviePlayer_MovieSelect)이면 주입 건너뛰기
                if (!CustomAssetManager.ShouldInjectCustomContent())
                {
                    continue;
                }

                // 현재 선택된 앨범의 파일 경로를 매번 그대로 따라갑니다(앨범 변경 대응).
                // 현재 앨범에 해당 파일이 없으면 null이 되어 그 주입은 건너뜁니다. 예전에는 비어 있으면
                // 갱신하지 않아 직전 앨범(또는 hwa 루트)의 BGA/BGM이 다른 곡에 들어갔습니다.
                var currentBgaFile = Core.AlbumManager.GetCurrentBgaFile();
                var currentBgmFile = Core.AlbumManager.GetCurrentBgmFile();

                if (currentBgaFile != _bgaFilePath)
                {
                    _bgaFilePath = currentBgaFile;
                    MelonLogger.Msg(_bgaFilePath != null
                        ? $"[BgmBgaInjector] BGA 파일 경로 업데이트: {Path.GetFileName(_bgaFilePath)}"
                        : "[BgmBgaInjector] 현재 앨범에 BGA 파일이 없어 원본 BGA를 그대로 씁니다.");
                }

                if (currentBgmFile != _bgmFilePath)
                {
                    _bgmFilePath = currentBgmFile;
                    MelonLogger.Msg(_bgmFilePath != null
                        ? $"[BgmBgaInjector] BGM 파일 경로 업데이트: {Path.GetFileName(_bgmFilePath)}"
                        : "[BgmBgaInjector] 현재 앨범에 BGM 파일이 없어 원본 BGM을 그대로 씁니다.");
                }

                // BGA는 플레이 씬에서만 시도
                if (!BgaInjector.IsInjected && !string.IsNullOrEmpty(_bgaFilePath) && _isPlayScene)
                {
                    yield return BgaInjector.TryInjectBgaCoroutine(_bgaFilePath);
                    if (BgaInjector.IsInjected)
                    {
                        MelonLogger.Msg("[BgmBgaInjector] BGA 주입 완료");
                    }
                }

                // BGM은 플레이 씬에서만 시도
                if (!BgmInjector.IsInjected && !string.IsNullOrEmpty(_bgmFilePath) && _isPlayScene)
                {
                    MelonLogger.Msg("[BgmBgaInjector] BGM 주입 시도 시작");
                    yield return BgmInjector.TryInjectBgmCoroutine(_bgmFilePath);
                    if (BgmInjector.IsInjected)
                    {
                        MelonLogger.Msg("[BgmBgaInjector] BGM 주입 완료");
                    }
                }
                else if (!BgmInjector.IsInjected && !string.IsNullOrEmpty(_bgmFilePath))
                {
                    // 플레이 씬이 아닐 때 로그 (디버깅용)
                    if (BgmInjector.AttemptCount == 0)
                    {
                        MelonLogger.Msg($"[BgmBgaInjector] BGM 주입 대기 중 (플레이 씬 아님: _isPlayScene={_isPlayScene})");
                    }
                }

                // 할 일이 남지 않으면 종료. 파일이 없는 쪽은 할 일이 없으므로 끝난 것으로 봅니다.
                // (예전에는 BGA 파일이 없는 곡에서 이 조건이 영원히 거짓이라 코루틴이 끝나지 않았습니다.)
                bool bgaDone = BgaInjector.IsInjected || string.IsNullOrEmpty(_bgaFilePath);
                bool bgmDone = BgmInjector.IsInjected || string.IsNullOrEmpty(_bgmFilePath) || !_isPlayScene;
                if (bgaDone && bgmDone)
                {
                    break;
                }
            }
        }
    }
}
