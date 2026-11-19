using UnityEngine;

namespace MeridianMotion.Tests
{
    /// <summary>
    /// Runs a test faster than real time, but no more than a given slice of game time per frame, whatever the frame rate:
    /// each frame it sets the time scale from how long the last frame really took. A fixed time scale can't do that (and
    /// Time.maximumDeltaTime limits the real frame time, before the time scale multiplies it): sped up 50 times, a frame
    /// of the editor's (drawn 60 times a second, say) covers most of a second of game time, where one in batch mode, far
    /// shorter, covers a twentieth. Tests that watch something change frame by frame (the camera easing) need the small
    /// slices. Restores the time scale when it's destroyed.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class FrameTimeLimit : MonoBehaviour
    {
        private float maxTimeScale, maxFrameTime, previousTimeScale;

        /// <summary>Adds a limit to a new object: up to maxTimeScale times real speed, and at most maxFrameTime (s) of game time a frame.</summary>
        public static FrameTimeLimit Start(float maxTimeScale, float maxFrameTime)
        {
            var limit = new GameObject("Frame Time Limit").AddComponent<FrameTimeLimit>();
            limit.maxTimeScale = maxTimeScale;
            limit.maxFrameTime = maxFrameTime;
            limit.previousTimeScale = Time.timeScale;
            limit.Apply();
            return limit;
        }

        private void Update() => Apply();

        private void Apply()
        {
            // The next frame will probably take about as long as the last one did.
            float realFrame = Time.unscaledDeltaTime;
            Time.timeScale = realFrame > 1e-5f ? Mathf.Min(maxTimeScale, maxFrameTime / realFrame) : maxTimeScale;
        }

        private void OnDestroy() => Time.timeScale = previousTimeScale;
    }
}
