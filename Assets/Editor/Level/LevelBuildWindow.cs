using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// Builds a graybox scene (the Meridian Complex, or the Level Playground) without freezing the editor, and shows
    /// how far along it is. The scene's build steps (see GrayboxScene: one area at a time) run one per editor update,
    /// so the editor redraws between them, then the lighting (Lightmapping.BakeAsync) and the occlusion culling
    /// (StaticOcclusionCulling.GenerateInBackground, if the generation settings bake it) are baked in the background,
    /// the progress shown as they go. The build is also shown in the editor's Background Tasks. Each step still runs in
    /// one go, so the editor pauses briefly for each. One scene is built at a time.
    ///
    /// Until the environment is replaced, cancelling leaves the level as it was (reopening the scene from disk).
    /// After that the scene is saved first, and cancelling a bake leaves it without that bake's data (the lighting, or
    /// the occlusion culling).
    /// </summary>
    public sealed class LevelBuildWindow : EditorWindow
    {
        // How much of the bar the steps take, the bake taking the rest: about their share of the time (under a second
        // for all the steps, about three and a half for the bake).
        private const float StepsShare = 0.2f;

        // The build in progress (not kept across a script reload, which stops it: see wasBuilding).
        [NonSerialized] private static LevelBuildWindow running;
        [NonSerialized] private GrayboxScene level;
        [NonSerialized] private List<GrayboxScene.BuildStep> steps;
        [NonSerialized] private GrayboxScene.BuildState build;
        [NonSerialized] private int nextStep;
        [NonSerialized] private bool baking, occluding;
        [NonSerialized] private int progressId;
        [NonSerialized] private double startTime;

        // Kept across a script reload, so a build it stopped can be reported rather than seeming to vanish.
        [SerializeField] private bool wasBuilding;
        [SerializeField] private string levelName = "";
        [SerializeField] private string status = "";
        [SerializeField] private float progress;
        [SerializeField] private string result;
        [SerializeField] private MessageType resultType;

        /// <summary>Whether a level is being built now.</summary>
        public static bool IsBuilding => running != null;

        /// <summary>
        /// For a level's menu item: starts building it in the background, after offering to save the open scene (which
        /// the build replaces); or, if a level's already being built, brings that build's window to the front.
        /// </summary>
        internal static void BuildFromMenu(GrayboxScene level)
        {
            if (running != null)
            {
                running.Focus();
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            var window = GetWindow<LevelBuildWindow>(true, $"Building the {level.Name}", true);
            window.minSize = window.maxSize = new Vector2(420f, 150f);
            window.Begin(level);
        }

        private void Begin(GrayboxScene toBuild)
        {
            running = this;
            level = toBuild;
            levelName = level.Name;
            steps = level.Steps();
            build = new GrayboxScene.BuildState();
            nextStep = 0;
            baking = false;
            occluding = false;
            wasBuilding = true;
            result = null;
            startTime = EditorApplication.timeSinceStartup;
            status = steps[0].name;
            progress = 0f;
            progressId = Progress.Start($"Building the {level.Name}");
            EditorApplication.update += Tick;
            Repaint();
        }

        /// <summary>
        /// Runs the next step, or follows the bake, once per editor update. A step runs on the update after the window
        /// has shown that it's starting, so the label is always the step being worked on.
        /// </summary>
        private void Tick()
        {
            try
            {
                if (occluding)
                {
                    if (!StaticOcclusionCulling.isRunning) { Complete(); }
                    return;
                }
                if (baking)
                {
                    if (Lightmapping.isRunning)
                    {
                        Report(GrayboxScene.BakeStepName, StepsShare + (1f - StepsShare) * 0.9f * Mathf.Clamp01(Lightmapping.buildProgress));
                        return;
                    }
                    baking = false;
                    AfterLighting();
                    return;
                }

                if (nextStep > 0 && !(build.scene.IsValid() && build.scene.isLoaded))
                {
                    Stop("The build stopped: its scene was closed while it was being built. The level on disk wasn't replaced.", MessageType.Error);
                    return;
                }

                if (nextStep < steps.Count)
                {
                    steps[nextStep].run(build);
                    nextStep++;
                    Report(nextStep < steps.Count ? steps[nextStep].name : GrayboxScene.BakeStepName, StepsShare * nextStep / steps.Count);
                    return;
                }

                // Every step done: bake in the background.
                if (Lightmapping.BakeAsync())
                {
                    baking = true;
                    return;
                }
                level.WarnLightingNotBaked();
                AfterLighting();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Stop($"The build failed at \"{status}\": {exception.Message} See the Console.", MessageType.Error);
            }
        }

        /// <summary>After the lighting: bakes the occlusion culling in the background (the settings willing), or finishes.</summary>
        private void AfterLighting()
        {
            if (level.StartOcclusionBake())
            {
                occluding = true;
                Report(GrayboxScene.OcclusionStepName, 0.95f);
                return;
            }
            Complete();
        }

        private void Report(string nextStatus, float fraction)
        {
            status = nextStatus;
            progress = fraction;
            Progress.Report(progressId, fraction, nextStatus);
            Repaint();
        }

        /// <summary>Saves the built level, opens it and closes the window.</summary>
        private void Complete()
        {
            level.Finish(build);
            double seconds = EditorApplication.timeSinceStartup - startTime;
            EndBuild(Progress.Status.Succeeded);
            level.Log($"The build took {seconds:0} s.");
            Close();
        }

        /// <summary>Whether stopping now can leave the level as it was: only until a step that can't be undone has run.</summary>
        private bool CanUndo => nextStep == 0 || steps[nextStep - 1].cancellable;

        private void Cancel()
        {
            if (occluding)
            {
                StaticOcclusionCulling.Cancel();
                level.Finish(build);
                level.WarnOcclusionNotBaked();
                Stop("Cancelled during the occlusion bake. The level is built and saved, with its lighting, without occlusion " +
                     "culling data: use Window > Rendering > Occlusion Culling > Bake.", MessageType.Warning);
                return;
            }
            if (baking)
            {
                // The scene was saved before the bake; save it again with whatever lighting it has.
                Lightmapping.Cancel();
                level.Finish(build);
                level.WarnLightingNotBaked();
                Stop("Cancelled during the lighting bake. The level is built and saved, without its baked environment " +
                     "lighting: use Window > Rendering > Lighting > Generate Lighting.", MessageType.Warning);
                return;
            }
            // Before the environment: nothing on disk has changed but the folders, so put the old level back.
            bool hadLevel = File.Exists(level.ScenePath);
            if (hadLevel) { EditorSceneManager.OpenScene(level.ScenePath); }
            Stop(hadLevel ? "Cancelled. The level is as it was before the build." : "Cancelled. The level hasn't been built.", MessageType.Info);
        }

        private void Stop(string message, MessageType type)
        {
            result = message;
            resultType = type;
            EndBuild(type == MessageType.Error ? Progress.Status.Failed : Progress.Status.Canceled);
            Repaint();
        }

        private void EndBuild(Progress.Status outcome)
        {
            EditorApplication.update -= Tick;
            if (Progress.Exists(progressId)) { Progress.Finish(progressId, outcome); }
            running = null;
            wasBuilding = false;
        }

        private void OnEnable()
        {
            // A script reload stops the build (nothing that runs it survives one).
            if (wasBuilding && running != this)
            {
                wasBuilding = false;
                result = $"The build stopped at \"{status}\": the scripts were reloaded while it ran. Build the level again.";
                resultType = MessageType.Error;
            }
        }

        private void OnDisable()
        {
            // A script reload (OnEnable then reports it) or the window closing (OnDestroy follows): stop stepping.
            EditorApplication.update -= Tick;
        }

        /// <summary>
        /// The window closed mid-build. Before anything that can't be undone, that cancels it. After, the build is
        /// finished without the window: the rest of the steps at once (they're short), and the bake in the background,
        /// saved when it completes.
        /// </summary>
        private void OnDestroy()
        {
            if (running != this) { return; }
            if (!baking && !occluding && CanUndo)
            {
                Cancel();
                return;
            }
            GrayboxScene carryOnLevel = level;
            GrayboxScene.BuildState carryOn = build;
            if (occluding)
            {
                // The occlusion bake carries on in the background: save the level when it's done.
                void SaveWhenOccluded()
                {
                    if (StaticOcclusionCulling.isRunning) { return; }
                    EditorApplication.update -= SaveWhenOccluded;
                    carryOnLevel.Finish(carryOn);
                }
                EditorApplication.update += SaveWhenOccluded;
                level.Log("The build window was closed: the occlusion culling carries on baking, and the level is saved when it's done.");
                EndBuild(Progress.Status.Succeeded);
                return;
            }
            if (!baking)
            {
                for (; nextStep < steps.Count; nextStep++) { steps[nextStep].run(carryOn); }
                baking = Lightmapping.BakeAsync();
            }
            if (baking)
            {
                void FinishAfterBake()
                {
                    Lightmapping.bakeCompleted -= FinishAfterBake;
                    carryOnLevel.BakeOcclusion();
                    carryOnLevel.Finish(carryOn);
                }
                Lightmapping.bakeCompleted += FinishAfterBake;
                level.Log("The build window was closed: the lighting carries on baking, then the occlusion culling, and the level is saved when they're done.");
            }
            else
            {
                level.WarnLightingNotBaked();
                level.BakeOcclusion();
                level.Finish(carryOn);
            }
            EndBuild(Progress.Status.Succeeded);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            if (result != null)
            {
                EditorGUILayout.HelpBox(result, resultType);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close")) { Close(); }
                return;
            }

            EditorGUILayout.LabelField($"Building the {levelName}", EditorStyles.boldLabel);
            Rect bar = GUILayoutUtility.GetRect(18f, 20f, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(bar, progress, $"{progress * 100f:0}%");
            EditorGUILayout.LabelField(status + "…", EditorStyles.wordWrappedLabel);
            if (running == this)
            {
                double seconds = EditorApplication.timeSinceStartup - startTime;
                EditorGUILayout.LabelField($"{seconds:0} s", EditorStyles.miniLabel);
            }
            GUILayout.FlexibleSpace();

            bool cancellable = baking || occluding || steps != null && CanUndo;
            using (new EditorGUI.DisabledScope(running != this || !cancellable))
            {
                if (GUILayout.Button(new GUIContent("Cancel", cancellable ? "Stop the build." : "Not while the environment is being replaced and the scene saved.")))
                {
                    Cancel();
                }
            }
            // Keep the elapsed time ticking over while the bake runs in the background.
            if (running == this && (baking || occluding)) { Repaint(); }
        }
    }
}
