using UnityEngine;

namespace MeridianMotion.Showcase.Audio
{
    public partial class AudioService
    {
        /// <summary>
        /// Each frame, on unscaled time: pauses the voices of buses paused with the game while it's paused (and moves their
        /// ends on by the pause when it ends, so they finish where they would have), advances fades, and frees voices whose
        /// clip has ended or whose fade is done.
        /// </summary>
        protected virtual void Update()
        {
            if (pool == null) { return; }
            float now = Time.unscaledTime;
            float delta = Time.unscaledDeltaTime;
            bool gamePaused = Time.timeScale == 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                Voice voice = pool[i];
                if (!voice.inUse) { continue; }
                bool pauses = voice.sound.Bus == null || voice.sound.Bus.PausesWithGame;
                if (pauses && gamePaused != voice.paused)
                {
                    if (gamePaused)
                    {
                        voice.paused = true;
                        voice.pausedAt = now;
                        voice.source.Pause();
                    }
                    else
                    {
                        voice.paused = false;
                        voice.endsAt += now - voice.pausedAt;
                        voice.source.UnPause();
                    }
                }
                if (voice.paused) { continue; }
                if (voice.fading)
                {
                    voice.fadeElapsed += delta;
                    if (voice.fadeElapsed >= voice.fadeLength) { Release(voice); continue; }
                    ApplyVolume(voice);
                }
                if (now >= voice.endsAt) { Release(voice); }
            }
        }
    }
}
