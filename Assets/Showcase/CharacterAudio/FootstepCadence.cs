namespace MeridianMotion.Showcase.CharacterAudio
{
    /// <summary>
    /// When a footstep falls: every stride of distance on the ground, what's over carried into the next. Reset as the
    /// character lands or starts, to half a stride, so the first step comes soon after it moves off.
    /// </summary>
    public struct FootstepCadence
    {
        private float distance;

        /// <summary>Goes so far: whether a footstep falls on it.</summary>
        public bool Advance(float travelled, float stride)
        {
            distance += travelled;
            if (stride <= 0f || distance < stride) { return false; }
            distance %= stride;
            return true;
        }

        /// <summary>Starts again, half a stride from the next step.</summary>
        public void Reset(float stride) => distance = stride * 0.5f;
    }
}
