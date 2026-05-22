using System;
using UnityEngine;

namespace GRC2.Core
{
    internal static class ArtworkImageFinder
    {
        public const string DefaultArtworkObjectName = "ArtWork";

        public static bool IsCachedImageValid(UnityEngine.UI.Image image, int sceneHash)
        {
            return image != null &&
                image &&
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetHashCode() == sceneHash;
        }

        public static UnityEngine.UI.Image FindArtworkImage(string objectName = DefaultArtworkObjectName)
        {
            var image = FindImageByGameObjectName(objectName);
            if (image != null)
                return image;

            image = FindImageUnderSceneRoots(objectName);
            if (image != null)
                return image;

            return FindImageByScanningImages(objectName);
        }

        private static UnityEngine.UI.Image FindImageByGameObjectName(string objectName)
        {
            GameObject artWorkObj = GameObject.Find(objectName);
            return artWorkObj != null ? artWorkObj.GetComponent<UnityEngine.UI.Image>() : null;
        }

        private static UnityEngine.UI.Image FindImageUnderSceneRoots(string objectName)
        {
            var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                Transform found = root?.transform.Find(objectName);
                if (found == null)
                    continue;

                var image = found.GetComponent<UnityEngine.UI.Image>();
                if (image != null)
                    return image;
            }

            return null;
        }

        private static UnityEngine.UI.Image FindImageByScanningImages(string objectName)
        {
            UnityEngine.UI.Image[] images = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Image>();
            if (images == null)
                return null;

            foreach (var image in images)
            {
                if (image != null &&
                    image.gameObject != null &&
                    image.gameObject.name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                {
                    return image;
                }
            }

            return null;
        }
    }
}
