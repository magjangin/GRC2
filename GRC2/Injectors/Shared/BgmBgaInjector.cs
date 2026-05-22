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
        private static string _hwaFolderPath;
        private static string _bgaFilePath;
        private static string _bgmFilePath;
        private static object _injectionCoroutine;
        private static bool _isPlayScene = false;

        public static void Initialize(string hwaFolderPath)
        {
            _hwaFolderPath = hwaFolderPath;
            
            // 게임 타입 찾기
            GameTypeSearcher.SearchGameTypes();
            
            // BGA 파일 검색 (mp4)
            var bgaFiles = Directory.GetFiles(_hwaFolderPath, "*.mp4", SearchOption.TopDirectoryOnly)
                .ToList();
            
            if (bgaFiles.Count > 0)
            {
                _bgaFilePath = bgaFiles[0];
                MelonLogger.Msg($"[BgmBgaInjector] BGA 파일 발견: {Path.GetFileName(_bgaFilePath)}");
            }
            else
            {
                MelonLogger.Msg("[BgmBgaInjector] BGA 파일을 찾을 수 없습니다.");
            }

            // BGM 파일 검색 (mp3, wav, ogg) - 성능 최적화: 한 번의 검색으로 처리
            var bgmFiles = Directory.EnumerateFiles(_hwaFolderPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f =>
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext == ".mp3" || ext == ".wav" || ext == ".ogg";
                })
                .ToList();

            if (bgmFiles.Count > 0)
            {
                // OGG 파일 우선 선택
                var oggFile = bgmFiles.FirstOrDefault(f => f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase));
                _bgmFilePath = oggFile ?? bgmFiles[0];
                MelonLogger.Msg($"[BgmBgaInjector] BGM 파일 발견: {Path.GetFileName(_bgmFilePath)}");
            }
            else
            {
                MelonLogger.Msg("[BgmBgaInjector] BGM 파일을 찾을 수 없습니다.");
            }
        }

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
                // 이미 플레이 씬이 아니면 false로 설정 (처음 호출 시에만)
                _isPlayScene = false;
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
                ResetInjectionState();
                
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

                // 현재 선택된 앨범의 파일 경로 다시 확인 (앨범 변경 대응)
                var currentBgaFile = Core.AlbumManager.GetCurrentBgaFile();
                var currentBgmFile = Core.AlbumManager.GetCurrentBgmFile();
                
                if (!string.IsNullOrEmpty(currentBgaFile) && currentBgaFile != _bgaFilePath)
                {
                    _bgaFilePath = currentBgaFile;
                    MelonLogger.Msg($"[BgmBgaInjector] BGA 파일 경로 업데이트: {Path.GetFileName(_bgaFilePath)}");
                }
                
                if (!string.IsNullOrEmpty(currentBgmFile) && currentBgmFile != _bgmFilePath)
                {
                    _bgmFilePath = currentBgmFile;
                    MelonLogger.Msg($"[BgmBgaInjector] BGM 파일 경로 업데이트: {Path.GetFileName(_bgmFilePath)}");
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
                    yield return BgmInjector.TryInjectBgmCoroutine(_bgmFilePath, GameTypeSearcher.BgmBeatManagerType, _isPlayScene);
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

                // 둘 다 주입 완료되면 종료
                if (BgaInjector.IsInjected && (BgmInjector.IsInjected || !_isPlayScene))
                {
                    break;
                }
            }
        }

        public static void Reset()
        {
            BgaInjector.Reset();
            BgmInjector.Reset();
            _isPlayScene = false;
        }
    }
}
