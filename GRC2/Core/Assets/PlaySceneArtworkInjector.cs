using System;
using System.Collections;
using MelonLoader;
using UnityEngine;
using GRC2;

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
            
            // 성능 최적화: 먼저 즉시 적용 시도 (대기 없이)
            if (TryInjectArtworkImmediately())
            {
                return; // 즉시 적용 성공하면 코루틴 불필요
            }

            // 이미 실행 중인 코루틴이 있으면 중지
            if (_artworkInjectionCoroutine != null)
            {
                MelonLoader.MelonCoroutines.Stop(_artworkInjectionCoroutine);
                _artworkInjectionCoroutine = null;
            }

            // 새 코루틴 시작 (즉시 적용 실패 시에만)
            _artworkInjectionCoroutine = MelonLoader.MelonCoroutines.Start(InjectArtworkInPlaySceneCoroutine());
        }

        /// <summary>
        /// 아트워크 캐시 리셋 (재시작 시 호출)
        /// </summary>
        public static void ResetCache()
        {
            _cachedArtWorkImage = null;
            _lastSceneHash = 0;
            MelonLogger.Msg("[PlaySceneArtworkInjector] 아트워크 캐시 리셋 완료");
        }

        /// <summary>
        /// 아트워크 즉시 적용 시도 (성능 최적화 - 대기 없이)
        /// </summary>
        private static bool TryInjectArtworkImmediately()
        {
            try
            {
                if (CustomAssetManager.IsSceneWhereInjectionDisallowed())
                    return false;

                Sprite customSprite = CustomAssetManager.GetCustomArtwork();
                if (customSprite == null)
                {
                    return false;
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
                        return false;
                    }
                }

                // 캐시된 Image에 스프라이트 설정
                if (_cachedArtWorkImage != null && _cachedArtWorkImage)
                {
                    _cachedArtWorkImage.sprite = customSprite;
                    // 게임이 나중에 스프라이트를 덮어쓸 수 있으므로, 코루틴도 실행하여 재확인
                    return false; // 즉시 적용했지만 코루틴도 실행하여 재확인
                }
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogWarning(ex, "[PlaySceneArtworkInjector]", "즉시 적용 중 오류");
            }
            return false;
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



