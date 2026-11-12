using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// Builds the Meridian Complex without freezing the editor, and shows how far along it is. MeridianComplexBuilder's
    /// steps (one area at a time) run one per editor update, so the editor redraws between them, and the lighting is
    /// baked in the background (Lightmapping.BakeAsync), its progress shown as it goes. The build is also shown in the
    /// editor's Background Tasks. Each step still runs in one go, so the editor pauses briefly for each.
    ///
    /// Until the environment is replaced, cancelling leaves the level as it was (reopening the scene from disk).
    /// After that the scene is saved first, and cancelling the bake leaves it without its baked environment lighting.
    /// </summary>
    public sealed class MeridianBuildWindow : EditorWindow
    {
        // How much of the bar the steps take, the bake taking the rest: about their share of the time (under a second
        // for all the steps, about three and a half for the bake).
        private const float StepsShare = 0.2f;

        // The build in progress (not kept across a script reload, which stops it: see wasBuilding).
        [NonSerialized] private static MeridianBuildWindow running;
        [NonSerialized] private List<MeridianComplexBuilder.BuildStep> steps;
        [NonSerialized] private MeridianComplexBuilder.BuildState build;
        [NonSerialized] private int nextStep;
        [NonSerialized] private bool baking;
        [NonSerialized] private int progressId;
        [NonSerialized] private double startTime;

        // Kept across a script reload, so a build it stopped can be reported rather than seeming to vanish.
        [SerializeField] private bool wasBuilding;
        [SerializeField] private string status = "";
        [SerializeField] private float progress;
        [SerializeField] private string result;
        [SerializeField] private MessageType resultType;

        /// <summary>Whether the level is being built now.</summary>
        public static bool IsBuilding => running != null;

        /// <summary>Brings the window of the build in progress to the front, if there is one.</summary>
        public static void FocusBuild()
        {
            if (running != null) { running.Focus(); }
        }

        /// <summary>Starts building the level, replacing the open scene, and shows the window.</summary>
        public static void StartBuild()
        {
            if (running != null) { return; }
            var window = GetWindow<MeridianBuildWindow>(true, "Building the Meridian Complex", true);
            window.minSize = window.maxSize = new Vector2(420f, 150f);
            window.Begin();
        }

        private void Begin()
        {
            running = this;
            steps = MeridianComplexBuilder.Steps();
            build = new MeridianComplexBuilder.BuildState();
            nextStep = 0;
            baking = false;
            wasBuilding = true;
            result = null;
            startTime = EditorApplication.timeSinceStartup;
            status = steps[0].name;
            progress = 0f;
            progressId = Progress.Start("Building the Meridian Complex");
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
                if (baking)
                {
                    if (Lightmapping.isRunning)
                    {
                        Report(MeridianComplexBuilder.BakeStepName, StepsShare + (1f - StepsShare) * Mathf.Clamp01(Lightmapping.buildProgress));
                        return;
                    }
                    Complete();
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
                    Report(nextStep < steps.Count ? steps[nextStep].name : MeridianComplexBuilder.BakeStepName, StepsShare * nextStep / steps.Count);
                    return;
                }

                // Every step done: bake in the background.
                if (Lightmapping.BakeAsync())
                {
                    baking = true;
                    return;
                }
                MeridianComplexBuilder.WarnLightingNotBaked();
                Complete();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Stop($"The build failed at \"{status}\": {exception.Message} See the Console.", MessageType.Error);
            }
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
            MeridianComplexBuilder.Finish(build);
            double seconds = EditorApplication.timeSinceStartup - startTime;
            EndBuild(Progress.Status.Succeeded);
            Debug.Log($"[MeridianComplexBuilder] The build took {seconds:0} s.");
            Close();
        }

        /// <summary>Whether stopping now can leave the level as it was: only until a step that can't be undone has run.</summary>
        private bool CanUndo => nextStep == 0 || steps[nextStep - 1].cancellable;

        private void Cancel()
        {
            if (baking)
            {
                // The scene was saved before the bake; save it again with whatever lighting it has.
                Lightmapping.Cancel();
                MeridianComplexBuilder.Finish(build);
                MeridianComplexBuilder.WarnLightingNotBaked();
                Stop("Cancelled during the lighting bake. The level is built and saved, without its baked environment " +
                     "lighting: use Window > Rendering > Lighting > Generate Lighting.", MessageType.Warning);
                return;
            }
            // Before the environment: nothing on disk has changed but the folders, so put the old level back.
            bool hadLevel = File.Exists(MeridianComplexBuilder.ScenePath);
            if (hadLevel) { EditorSceneManager.OpenScene(MeridianComplexBuilder.ScenePath); }
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
            if (!baking && CanUndo)
            {
                Cancel();
                return;
            }
            MeridianComplexBuilder.BuildState carryOn = build;
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
                    MeridianComplexBuilder.Finish(carryOn);
                }
                Lightmapping.bakeCompleted += FinishAfterBake;
                Debug.Log("[MeridianComplexBuilder] The build window was closed: the lighting carries on baking, and the level is saved when it's done.");
            }
            else
            {
                MeridianComplexBuilder.WarnLightingNotBaked();
                MeridianComplexBuilder.Finish(carryOn);
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

            EditorGUILayout.LabelField("Building the Meridian Complex", EditorStyles.boldLabel);
            Rect bar = GUILayoutUtility.GetRect(18f, 20f, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(bar, progress, $"{progress * 100f:0}%");
            EditorGUILayout.LabelField(status + "…", EditorStyles.wordWrappedLabel);
            if (running == this)
            {
                double seconds = EditorApplication.timeSinceStartup - startTime;
                EditorGUILayout.LabelField($"{seconds:0} s", EditorStyles.miniLabel);
            }
            GUILayout.FlexibleSpace();

            bool cancellable = baking || steps != null && CanUndo;
            using (new EditorGUI.DisabledScope(running != this || !cancellable))
            {
                if (GUILayout.Button(new GUIContent("Cancel", cancellable ? "Stop the build." : "Not while the environment is being replaced and the scene saved.")))
                {
                    Cancel();
                }
            }
            // Keep the elapsed time ticking over while the bake runs in the background.
            if (running == this && baking) { Repaint(); }
        }
    }
}
