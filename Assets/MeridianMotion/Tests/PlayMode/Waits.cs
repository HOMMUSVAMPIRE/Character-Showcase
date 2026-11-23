using System;
using System.Collections;
using UnityEngine;

namespace MeridianMotion.Tests
{
    /// <summary>
    /// Waiting frame by frame for something to happen, or to settle, rather than for a time or a count of frames chosen
    /// for one tuning: as long as it takes however it's tuned (the camera's smoothing, a model loading). Neither fails by
    /// itself on its timeout: assert what should then be true. For the character's own steps, use the rig's RunUntil,
    /// RunUntilStill and Settle.
    /// </summary>
    public static class Waits
    {
        /// <summary>How many frames running something must change by less than its tolerance to count as settled.</summary>
        public const int SteadyFrames = 5;

        /// <summary>Waits frame by frame until done says so, or timeoutSeconds pass.</summary>
        public static IEnumerator WaitUntil(Func<bool> done, float timeoutSeconds = 5f)
        {
            float until = Time.time + timeoutSeconds;
            while (!done() && Time.time < until) { yield return null; }
        }

        /// <summary>
        /// Waits frame by frame until value stops changing (by less than tolerance a frame, for SteadyFrames frames
        /// running), or timeoutSeconds pass.
        /// </summary>
        public static IEnumerator WaitUntilSettled(Func<float> value, float tolerance, float timeoutSeconds = 5f)
        {
            float until = Time.time + timeoutSeconds, last = value();
            int steady = 0;
            while (steady < SteadyFrames && Time.time < until)
            {
                yield return null;
                float now = value();
                steady = Mathf.Abs(now - last) < tolerance ? steady + 1 : 0;
                last = now;
            }
        }

        /// <summary>As WaitUntilSettled, for a position: until it moves less than tolerance (m) a frame, SteadyFrames running.</summary>
        public static IEnumerator WaitUntilSettled(Func<Vector3> value, float tolerance, float timeoutSeconds = 5f)
        {
            float until = Time.time + timeoutSeconds;
            Vector3 last = value();
            int steady = 0;
            while (steady < SteadyFrames && Time.time < until)
            {
                yield return null;
                Vector3 now = value();
                steady = Vector3.Distance(now, last) < tolerance ? steady + 1 : 0;
                last = now;
            }
        }
    }
}
