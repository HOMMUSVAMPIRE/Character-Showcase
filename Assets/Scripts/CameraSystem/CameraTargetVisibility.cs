using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CameraSystem
{
    /// <summary>
    /// Hides the followed target's renderers when the camera is inside or right next to it, and restores them
    /// exactly as they were afterwards. Uses forceRenderingOff / shadowCastingMode rather than toggling
    /// renderer.enabled, so it doesn't fight other systems that enable or disable renderers.
    /// </summary>
    public class CameraTargetVisibility
    {
        private readonly List<Renderer> renderers = new();
        private readonly List<ShadowCastingMode> originalShadowModes = new();
        private TargetHideMode applied = TargetHideMode.None;

        public void Collect(Transform target)
        {
            Restore();
            renderers.Clear();
            originalShadowModes.Clear();
            if (target == null) { return; }

            target.GetComponentsInChildren(true, renderers);
            foreach (var r in renderers) { originalShadowModes.Add(r.shadowCastingMode); }
        }

        public void Apply(TargetHideMode mode)
        {
            if (mode == applied) { return; }
            applied = mode;

            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer r = renderers[i];
                if (r == null) { continue; }
                switch (mode)
                {
                    case TargetHideMode.None:
                        r.forceRenderingOff = false;
                        r.shadowCastingMode = originalShadowModes[i];
                        break;
                    case TargetHideMode.Hide:
                        r.forceRenderingOff = true;
                        r.shadowCastingMode = originalShadowModes[i];
                        break;
                    case TargetHideMode.ShadowsOnly:
                        // Renderers that never cast shadows stay invisible rather than suddenly casting one.
                        bool castsShadows = originalShadowModes[i] != ShadowCastingMode.Off;
                        r.forceRenderingOff = !castsShadows;
                        if (castsShadows) { r.shadowCastingMode = ShadowCastingMode.ShadowsOnly; }
                        break;
                }
            }
        }

        public void Restore() => Apply(TargetHideMode.None);
    }
}
