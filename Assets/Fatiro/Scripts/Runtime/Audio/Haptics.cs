using UnityEngine;

namespace Fatiro.Audio
{
    /// <summary>Short vibrations through the Android Vibrator service (Handheld.Vibrate is too long for a snap).</summary>
    public static class Haptics
    {
        public static void Pulse(long milliseconds)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (vibrator == null) return;
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                if (version.GetStatic<int>("SDK_INT") >= 26)
                {
                    using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    using var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, -1);
                    vibrator.Call("vibrate", effect);
                }
                else
                {
                    vibrator.Call("vibrate", milliseconds);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[Fatiro] Vibration unavailable: {exception.Message}");
            }
#endif
        }

        /// <summary>Never called. Referencing Handheld.Vibrate makes Unity add the VIBRATE permission.</summary>
        private static void RequestVibratePermission() => Handheld.Vibrate();
    }
}
