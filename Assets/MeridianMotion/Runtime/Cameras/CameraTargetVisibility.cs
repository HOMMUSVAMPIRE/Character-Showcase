using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MeridianMotion.Cameras
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
        private Transform collectedTarget;
        // The size of the target's hierarchy when its renderers were collected, to notice objects added or removed later
        private int collectedHierarchyCount;

        /// <summary>
        /// Restores the previous target's renderers, then collects the new target's (including inactive ones) and
        /// remembers their shadow modes.
        /// </summary>
        public void Collect(Transform target)
        {
            Restore();
            renderers.Clear();
            originalShadowModes.Clear();
            collectedTarget = target;
            if (target == null) { return; }
            collectedHierarchyCount = target.hierarchyCount;

            target.GetComponentsInChildren(true, renderers);
            foreach (var r in renderers) { originalShadowModes.Add(r.shadowCastingMode); }
        }

        /// <summary>
        /// Collects the target's renderers again if objects have been added to or removed from its hierarchy since they
        /// were last collected (for example a character model loaded after the camera started following it).
        /// </summary>
        public void RefreshIfChanged()
        {
            if (collectedTarget == null || collectedTarget.hierarchyCount == collectedHierarchyCount) { return; }
            TargetHideMode mode = applied;
            Collect(collectedTarget);
            Apply(mode);
        }

        /// <summary>
        /// Hides, shadow-only renders or shows the collected renderers. Does nothing if that mode is already applied.
        /// </summary>
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

        /// <summary>Shows the collected renderers again, as they were.</summary>
        public void Restore() => Apply(TargetHideMode.None);
    }
}
