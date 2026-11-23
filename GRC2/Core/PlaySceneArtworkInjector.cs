using System;
using System.Collections;
using MelonLoader;
using GRC2.Helpers;
using UnityEngine;

namespace GRC2.Core
{
    /// <summary>
    /// 플레이 씬에서 커스텀 아트워크 주입을 담당하는 클래스
    /// </summary>
    public static class PlaySceneArtworkInjector
    {
        // ArtWork Image 캐시 (성능 최적화)
        private static UnityEngine.UI.Image _cachedArtWorkImage = null;
        private static int _lastSceneHash = 0;
        private static object _artworkInjectionCoroutine = null; // 아트워크 주입 코루틴 참조

        /// <summary>
        /// 아트워크 주입 코루틴 시작 (중복 방지)
        /// </summary>
        public static void StartArtworkInjection()
        {
            // 재시작 시 캐시 무효화 (씬이 다시 로드되면 GameObject가 새로 생성되므로)
            ResetCache();

            // 먼저 한 번 즉시 적용해 봅니다(대기 없이). 게임이 나중에 스프라이트를 덮어쓸 수 있으므로
            // 즉시 적용의 성공 여부와 상관없이 아래 코루틴이 항상 한 번 더 확인합니다.
            ApplyArtworkImmediately();

            // 이미 실행 중인 코루틴이 있으면 중지
            if (_artworkInjectionCoroutine != null)
            {
                MelonLoader.MelonCoroutines.Stop(_artworkInjectionCoroutine);
                _artworkInjectionCoroutine = null;
            }

            _artworkInjectionCoroutine = MelonLoader.MelonCoroutines.Start(InjectArtworkInPlaySceneCoroutine());
        }

        /// <summary>
        /// 아트워크 캐시 리셋 (재시작 시 호출)
        /// </summary>
        public static void ResetCache()
        {
            _cachedArtWorkImage = null;
            _lastSceneHash = 0;
            ModLog.Verbose("[PlaySceneArtworkInjector] 아트워크 캐시 리셋 완료");
        }

        /// <summary>
        /// 아트워크를 대기 없이 한 번 적용해 봅니다. 대상 Image를 아직 못 찾았거나 비활성이면 아무것도 하지 않습니다.
        /// </summary>
        private static void ApplyArtworkImmediately()
        {
            try
            {
                if (CustomAssetManager.IsSceneWhereInjectionDisallowed())
                    return;

                Sprite customSprite = CustomAssetManager.GetCustomArtwork();
                if (customSprite == null)
                {
                    return;
                }

                // 캐시된 Image가 유효한지 확인
                int currentSceneHash = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetHashCode();
                if (!ArtworkImageFinder.IsCachedImageValid(_cachedArtWorkImage, _lastSceneHash))
                {
                    _cachedArtWorkImage = ArtworkImageFinder.FindArtworkImage();
                    _lastSceneHash = _cachedArtWorkImage != null ? currentSceneHash : 0;

                    if (_cachedArtWorkImage == null ||
                        !_cachedArtWorkImage.gameObject.activeInHierarchy ||
                        !_cachedArtWorkImage.enabled)
                    {
                        return;
                    }
                }

                // 캐시된 Image에 스프라이트 설정
                if (_cachedArtWorkImage != null && _cachedArtWorkImage)
                {
                    _cachedArtWorkImage.sprite = customSprite;
                }
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogWarning(ex, "[PlaySceneArtworkInjector]", "즉시 적용 중 오류");
            }
        }

        /// <summary>
        /// 플레이 씬에서 커스텀 아트워크 주입 코루틴 (최적화 버전)
        /// </summary>
        private static IEnumerator InjectArtworkInPlaySceneCoroutine()
        {
            yield return new WaitForSeconds(0.05f);

            if (CustomAssetManager.IsSceneWhereInjectionDisallowed())
                yield break;

            try
            {
                Sprite customSprite = CustomAssetManager.GetCustomArtwork();
                if (customSprite == null)
                {
                    yield break;
                }

                // 캐시된 Image가 유효한지 확인
                int currentSceneHash = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetHashCode();
                if (!ArtworkImageFinder.IsCachedImageValid(_cachedArtWorkImage, _lastSceneHash))
                {
                    _cachedArtWorkImage = ArtworkImageFinder.FindArtworkImage();
                    _lastSceneHash = _cachedArtWorkImage != null ? currentSceneHash : 0;
                }

                // 캐시된 Image에 스프라이트 설정
                if (_cachedArtWorkImage != null && _cachedArtWorkImage)
                {
                    _cachedArtWorkImage.sprite = customSprite;
                    MelonLogger.Msg($"[PlaySceneArtworkInjector] ✅ 플레이 씬에 커스텀 아트워크 주입 완료 (크기: {customSprite.texture.width}x{customSprite.texture.height})");
                }
                else
                {
                    // 이 오브젝트는 게임 타입이 아니라 이름("ArtWork")으로만 찾으므로 호환성 점검에 걸리지 않습니다.
                    // 예전에는 못 찾으면 아무 로그도 없어 게임 UI 이름이 바뀌어도 알 수 없었습니다. 씬마다 한 번만 남깁니다(R3).
                    string scene = ActiveSceneName.Current;
                    ModLog.InfoOnChange("artwork-image-missing:" + scene,
                        $"[PlaySceneArtworkInjector] {scene}에서 '{ArtworkImageFinder.DefaultArtworkObjectName}' 이미지를 찾지 못해 아트워크를 바꾸지 않았습니다(게임 UI 이름 변경 의심).");
                }
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[PlaySceneArtworkInjector]", "플레이 씬 아트워크 주입 중 오류");
            }
            finally
            {
                // 코루틴 참조 해제
                _artworkInjectionCoroutine = null;
            }
        }
    }
}
