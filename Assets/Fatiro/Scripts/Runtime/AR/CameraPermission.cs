using System;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Fatiro.AR
{
    /// <summary>Requests camera access on first launch and reports a denial so the UI can explain why it is needed (FR-01).</summary>
    public static class CameraPermission
    {
        public static bool IsGranted
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return Permission.HasUserAuthorizedPermission(Permission.Camera);
#else
                return true;
#endif
            }
        }

        public static void Request(Action<bool> onResult)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (IsGranted)
            {
                onResult?.Invoke(true);
                return;
            }

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => onResult?.Invoke(true);
            callbacks.PermissionDenied += _ => onResult?.Invoke(false);
            Permission.RequestUserPermission(Permission.Camera, callbacks);
#else
            onResult?.Invoke(true);
#endif
        }

        public static void OpenAppSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var uriClass = new AndroidJavaClass("android.net.Uri");
                using var uri = uriClass.CallStatic<AndroidJavaObject>("fromParts", "package", Application.identifier, null);
                using var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS", uri);
                activity.Call("startActivity", intent);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Fatiro] Could not open app settings: {exception.Message}");
            }
#endif
        }
    }
}
