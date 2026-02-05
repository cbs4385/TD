using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using FaeMaze.Systems;
using FaeMaze.HeartPowers;
using FaeMaze.Cameras;
using FaeMaze.UI;
using FaeMaze.Visitors;
using FaeMaze.Props;
using FaeMaze.Maze;
using ForestMaze;
using Object = UnityEngine.Object;

namespace FaeMaze.Tutorial
{
    /// <summary>
    /// Central controller for the tutorial system.
    /// Manages tutorial state, step progression, and coordination between UI and game systems.
    /// </summary>
    public class TutorialManager : SingletonMonoBehaviour<TutorialManager>
    {

        #region Events

        /// <summary>Fired when tutorial starts</summary>
        public event Action OnTutorialStarted;

        /// <summary>Fired when tutorial step changes, passes new step index</summary>
        public event Action<int> OnStepChanged;

        /// <summary>Fired when tutorial completes or is skipped</summary>
        public event Action OnTutorialCompleted;

        #endregion

        #region Constants

        /// <summary>Starting essence for tutorial runs (higher to allow practicing all powers)</summary>
        public const int TUTORIAL_STARTING_ESSENCE = 200;

        /// <summary>Fixed seed for tutorial runs to ensure consistent maze layout</summary>
        public const int TUTORIAL_SEED = 14142135;

        #endregion

        #region Serialized Fields

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Auto-start tutorial on first run")]
        private bool autoStartOnFirstRun = true;

        [SerializeField]
        [Tooltip("Delay before starting tutorial after scene loads")]
        private float startDelay = 1.5f;

        #endregion

        #region Private Fields

        private List<TutorialStep> steps;
        private int currentStepIndex = -1;
        private bool isActive;
        private bool isPaused;
        private float previousTimeScale = 1f;

        // References
        private TutorialUIController uiController;
        private TutorialVisitorSpawner visitorSpawner;

        // Step tracking
        private int previousEssence;
        private int originalStartingEssence; // Store original to restore after tutorial
        private Vector3 previousCameraPosition;
        private float cameraMovementThreshold = 2f;

        // Camera transition state
        private bool isTransitioning;
        private Coroutine transitionCoroutine;

        #endregion

        #region Properties

        /// <summary>Gets whether the tutorial is currently active.</summary>
        public bool IsActive => isActive;

        /// <summary>Gets whether the game is paused for tutorial.</summary>
        public bool IsPaused => isPaused;

        /// <summary>Gets the current step index (-1 if not active).</summary>
        public int CurrentStepIndex => currentStepIndex;

        /// <summary>Gets the current tutorial step, or null if not active.</summary>
        public TutorialStep CurrentStep =>
            isActive && currentStepIndex >= 0 && currentStepIndex < steps.Count
                ? steps[currentStepIndex]
                : null;

        /// <summary>Gets the total number of steps.</summary>
        public int TotalSteps => steps?.Count ?? 0;

        #endregion

        #region Unity Lifecycle

        protected override void OnAwake()
        {
            InitializeTutorialSteps();
        }

        private void Start()
        {
            // Find or create UI controller
            uiController = GetComponent<TutorialUIController>();
            if (uiController == null)
            {
                uiController = gameObject.AddComponent<TutorialUIController>();
            }

            // Find or create visitor spawner
            visitorSpawner = GetComponent<TutorialVisitorSpawner>();
            if (visitorSpawner == null)
            {
                visitorSpawner = gameObject.AddComponent<TutorialVisitorSpawner>();
            }
        }

        protected override void OnSingletonDestroyed()
        {
            // Ensure time is restored if destroyed during tutorial
            if (isPaused)
            {
                Time.timeScale = previousTimeScale;
            }
        }

        #endregion

        #region Tutorial Step Definitions

        /// <summary>
        /// Gets the display name for a binding using InputBindingHelper.
        /// </summary>
        private string Bind(string binding)
        {
            return InputBindingHelper.GetDisplayName(binding);
        }

        /// <summary>
        /// Initializes all tutorial steps with their content and triggers.
        /// Uses current binding settings so control callouts are always accurate.
        /// </summary>
        private void InitializeTutorialSteps()
        {
            // Resolve current bindings for display
            string p1Key = Bind(GameSettings.HeartPower1Binding);
            string p2Key = Bind(GameSettings.HeartPower2Binding);
            string p3Key = Bind(GameSettings.HeartPower3Binding);
            string p4Key = Bind(GameSettings.HeartPower4Binding);
            string p5Key = Bind(GameSettings.HeartPower5Binding);
            string focusHeartKey = Bind(GameSettings.CameraFocusHeartBinding);
            string moveKeys = $"{Bind(GameSettings.CameraMoveForwardBinding)}/{Bind(GameSettings.CameraMoveBackwardBinding)}/{Bind(GameSettings.CameraTurnLeftBinding)}/{Bind(GameSettings.CameraTurnRightBinding)}";
            string orbitKey = Bind(GameSettings.CameraOrbitBinding);
            string panKey = Bind(GameSettings.CameraPanBinding);

            steps = new List<TutorialStep>
            {
                // Phase 1: Introduction
                new TutorialStep(
                    id: "welcome",
                    title: "Welcome to FaeMaze",
                    description: "You are the spirit of a mystical forest maze. Your goal is to capture visitors and gather their threads of fate before they escape through the exit portals.\n\nUse your powers wisely to survive!",
                    trigger: TutorialTriggerType.ButtonClick,
                    pause: true
                ),

                new TutorialStep(
                    id: "essence_bar",
                    title: "Threads - Your Life Force",
                    description: "The red bar at the top shows your Threads. This is your health and resource combined.\n\nThreads slowly drain over time. If they reach zero, the game ends. Capture visitors to replenish them!\n\nFor this tutorial, you start with 200 Threads.",
                    trigger: TutorialTriggerType.ButtonClick,
                    highlight: TutorialHighlightType.UIElement,
                    highlightTarget: "EssenceBarPanel",
                    pause: true
                ),

                new TutorialStep(
                    id: "heart_intro",
                    title: "The Heart of the Maze",
                    description: $"At the center of the maze is the Heart - your core. When visitors come close, a tongue emerges to capture them.\n\nYou can press [{focusHeartKey}] anytime to return the camera focus here.",
                    trigger: TutorialTriggerType.ButtonClick,
                    highlight: TutorialHighlightType.WorldPosition,
                    pause: false
                ),

                new TutorialStep(
                    id: "camera_controls",
                    title: "Camera & Focal Point",
                    description: "The center of your screen is the Focal Point - your powers target wherever this point is located in the maze.\n\n" +
                                 "Control the camera to position it:\n\n" +
                                 $"  {moveKeys} - Move camera\n" +
                                 "  Scroll Wheel - Zoom in/out\n" +
                                 $"  {orbitKey} drag - Orbit around focal point\n" +
                                 $"  {panKey} drag - Pan camera\n\n" +
                                 "Try moving the camera now.",
                    trigger: TutorialTriggerType.CameraMove,
                    pause: false
                ),

                // Phase 2: Powers
                new TutorialStep(
                    id: "power_murmuring",
                    title: "Power 1: Spooky Fog",
                    description: $"Press [{p1Key}] to activate Spooky Fog.\n\nThis creates a fog along the path from your focal point back to the Heart. Visitors caught in the fog become confused and walk toward the Heart instead of the exit.\n\nThe fog persists until a visitor is consumed or grabbed by the Heart.\n\nCost: 100 Threads (50% of starting)",
                    trigger: TutorialTriggerType.PowerActivated,
                    triggerParam: "0", // MurmuringPaths index
                    highlight: TutorialHighlightType.UIElementCircular,
                    highlightTarget: "PowerButton_0",
                    pause: false,
                    spawn: false // Visitor spawns after power is activated
                ),

                new TutorialStep(
                    id: "power_murmuring_effect",
                    title: "Spooky Fog Active",
                    description: "Watch the fog spread along the path from your focal point to the Heart.\n\nVisitors who walk through the fog become confused and start moving toward the Heart instead of the exit.\n\nThe fog persists until a visitor is consumed or grabbed by the Heart.",
                    trigger: TutorialTriggerType.Timer,
                    pause: false,
                    spawn: false // Visitor spawned by HandlePowerMurmuringEffectStep coroutine
                ),

                new TutorialStep(
                    id: "power_grasp",
                    title: "Power 2: Yoink!",
                    description: $"Press [{p2Key}] to activate Yoink!\n\nA tongue emerges from the forest wall near your focal point. It grabs nearby visitors and pulls them deeper into the maze toward the Heart.\n\nCost: 10 Threads (5% of starting)",
                    trigger: TutorialTriggerType.PowerActivated,
                    triggerParam: "1", // HeartwardGrasp index
                    highlight: TutorialHighlightType.UIElementCircular,
                    highlightTarget: "PowerButton_1",
                    pause: false,
                    spawn: false // Visitor spawns after power is activated
                ),

                new TutorialStep(
                    id: "power_grasp_effect",
                    title: "Yoink! Active",
                    description: "Watch the tongue emerge from the forest wall!\n\nIt will grab any visitor that comes too close and pull them deeper into the maze. This costs a small amount of threads from the grabbed visitor.\n\nThe tongue retracts after catching a visitor.",
                    trigger: TutorialTriggerType.Timer,
                    pause: false, // Let action play - camera will track visitor being grabbed
                    spawn: false // Visitor spawned by HandlePowerGraspEffectStep coroutine
                ),

                new TutorialStep(
                    id: "power_maw",
                    title: "Power 3: Nom Nom",
                    description: $"Press [{p3Key}] to activate Nom Nom.\n\nA great mouth emerges at your focal point. Any visitor who touches it is consumed instantly, granting you half their threads.\n\nCost: 50 Threads (25% of starting)",
                    trigger: TutorialTriggerType.PowerActivated,
                    triggerParam: "2", // DevouringMaw index
                    highlight: TutorialHighlightType.UIElementCircular,
                    highlightTarget: "PowerButton_2",
                    pause: false,
                    spawn: false // Visitor spawns after power is activated
                ),

                new TutorialStep(
                    id: "power_maw_effect",
                    title: "Nom Nom Active",
                    description: "A monstrous maw has emerged from the ground!\n\nAny visitor who walks into the maw is consumed instantly, granting you half their thread value.\n\nThe maw stays active until it consumes a visitor.",
                    trigger: TutorialTriggerType.Timer, // No Continue button - auto-advances when visitor is consumed
                    pause: false,
                    spawn: false // Visitor spawned by HandlePowerMawEffectStep coroutine
                ),

                new TutorialStep(
                    id: "power_sculpt",
                    title: "Power 4: Redecorating",
                    description: $"Press [{p4Key}] to open the Redecorating menu.\n\nThis lets you place or change hazards at maze nodes:\n" +
                                 "  Pond - Drowns visitors (with Puka hazard)\n" +
                                 "  Lantern - Fascinates visitors, drains threads\n" +
                                 "  Fairy Ring - Traps visitors, drains threads\n\n" +
                                 "Cost: Free",
                    trigger: TutorialTriggerType.PowerActivated,
                    triggerParam: "3", // Sculpting index
                    highlight: TutorialHighlightType.UIElementCircular,
                    highlightTarget: "PowerButton_3",
                    pause: false,
                    rightSideDialog: true // Move dialog to right so it doesn't obstruct sculpt menu
                ),

                new TutorialStep(
                    id: "power_sculpt_effect",
                    title: "Place a Lantern",
                    description: "Select the Lantern from the radial menu to place it on this node.\n\nLanterns fascinate visitors, drawing them in and draining their threads over time.",
                    trigger: TutorialTriggerType.Timer, // Auto-advances when lantern is placed
                    pause: false,
                    rightSideDialog: true // Keep dialog on right during sculpt
                ),

                // Phase 3: Gameplay - Lantern Fascination Demo
                new TutorialStep(
                    id: "lantern_demo",
                    title: "Lantern Fascination",
                    description: "Watch as a visitor becomes fascinated by the lantern!\n\nFascinated visitors walk toward the lantern and stand mesmerized while their threads slowly drain.\n\nWait for the visitor to be fascinated...",
                    trigger: TutorialTriggerType.Timer, // Auto-advances when visitor is fascinated
                    pause: false
                ),

                new TutorialStep(
                    id: "essence_gain",
                    title: "Capturing Threads",
                    description: "When visitors are consumed by the Heart, Nom Nom'd, or drained by hazards, you gain their threads.\n\nKeep your threads high to survive! The longer you last, the more challenging visitors become.\n\nWait for a visitor to be captured...",
                    trigger: TutorialTriggerType.EssenceIncreased,
                    pause: false // No highlight - game must continue for essence to increase
                ),

                // Phase 4: Misdirect (after lantern fascination demo)
                new TutorialStep(
                    id: "power_misdirect",
                    title: "Power 5: Misdirect",
                    description: $"Press [{p5Key}] to activate Misdirect.\n\nPlace it near a node/edge junction to make the nearest edge irresistible to visitors. They will strongly prefer that path when passing through the connected node.\n\nThe effect is permanent until you re-cast it on a different edge.\n\nCost: 50 Threads (25% of starting)",
                    trigger: TutorialTriggerType.PowerActivated,
                    triggerParam: "4", // Misdirect index
                    highlight: TutorialHighlightType.UIElementCircular,
                    highlightTarget: "PowerButton_4",
                    pause: false,
                    rightSideDialog: true // Move dialog to right so visitor is visible
                ),

                new TutorialStep(
                    id: "power_misdirect_effect",
                    title: "Misdirect Active",
                    description: "The edge now glows with an enticing fog, and signs mark both ends.\n\nWatch as a visitor is lured down the misdirected path!\n\nRe-cast Misdirect to move the effect to a different edge.",
                    trigger: TutorialTriggerType.Timer, // Auto-advances when visitor walks along edge
                    pause: false,
                    spawn: false, // Visitor spawned by HandlePowerMisdirectEffectStep coroutine
                    rightSideDialog: true // Keep dialog on right so visitor is visible
                ),

                new TutorialStep(
                    id: "complete",
                    title: "Ready to Play",
                    description: "You now know the basics of FaeMaze!\n\n" +
                                 "Tips:\n" +
                                 "  - Press [F1] anytime for a quick reference\n" +
                                 "  - Watch your threads - they drain constantly\n" +
                                 "  - Position your focal point wisely\n" +
                                 "  - Combine powers for maximum effect\n\n" +
                                 "Good luck, forest spirit!",
                    trigger: TutorialTriggerType.ButtonClick,
                    pause: true
                )
            };
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Checks if tutorial should auto-start and starts it if needed.
        /// Called from GameController.Start().
        /// </summary>
        public void CheckAutoStartTutorial()
        {
            // Check both the component's setting AND the GameSettings
            // GameSettings.ShowTutorialOnFirstRun is controlled by the Options/MainMenu toggle
            if (autoStartOnFirstRun && !GameSettings.TutorialCompleted && GameSettings.ShowTutorialOnFirstRun)
            {
                // Delay start to allow scene to fully initialize
                Invoke(nameof(StartTutorial), startDelay);
            }
        }

        /// <summary>
        /// Starts the tutorial from the beginning.
        /// </summary>
        public void StartTutorial()
        {
            if (isActive) return;

            isActive = true;
            currentStepIndex = -1;

            // Reset frightened speed to default so tutorial tongue can catch visitors
            GameSettings.ResetFrightenedSpeedMultiplier();

            // Set tutorial starting essence (higher than normal to allow practicing all powers)
            if (GameController.Instance != null)
            {
                // Store original and set tutorial essence
                originalStartingEssence = GameSettings.StartingEssence;
                GameController.Instance.SetEssence(TUTORIAL_STARTING_ESSENCE);
                previousEssence = TUTORIAL_STARTING_ESSENCE;
            }

            // Lock all heart power buttons until tutorial explicitly enables them
            var panelController = FindFirstObjectByType<HeartPowerPanelController>();
            if (panelController != null)
            {
                panelController.SetTutorialPowerLock(true);
            }

            var cam = Camera.main;
            if (cam != null)
            {
                previousCameraPosition = cam.transform.position;
            }

            OnTutorialStarted?.Invoke();
            AdvanceStep();
        }

        /// <summary>
        /// Advances to the next tutorial step.
        /// </summary>
        public void AdvanceStep()
        {
            if (!isActive) return;

            // Don't advance if we're in the middle of a transition
            if (isTransitioning)
            {
                return;
            }

            // Check if we just completed a step that requires camera transition to non-heart node
            var previousStep = CurrentStep;

            // Steps that need camera transition away from heart before advancing
            bool needsCameraTransition = previousStep != null && (
                previousStep.stepId == "camera_controls" ||
                previousStep.stepId == "power_murmuring_effect" ||
                previousStep.stepId == "power_grasp_effect" ||
                previousStep.stepId == "power_maw_effect"
            );

            if (needsCameraTransition && IsFocalPointOnHeartNode())
            {
                // Check if the NEXT step is a power activation step that will do its own cinematic transition.
                // If so, skip this intermediate transition to avoid a double camera move.
                int nextIndex = currentStepIndex + 1;
                if (nextIndex < steps.Count)
                {
                    string nextStepId = steps[nextIndex].stepId;
                    bool nextStepHasOwnTransition = nextStepId == "power_murmuring" ||
                                                     nextStepId == "power_grasp" ||
                                                     nextStepId == "power_maw" ||
                                                     nextStepId == "power_misdirect";
                    if (nextStepHasOwnTransition)
                    {
                        // Skip intermediate transition — the next step will handle camera movement
                        AdvanceStepInternal();
                        return;
                    }
                }

                // Start the cinematic camera transition instead of immediately advancing
                if (transitionCoroutine != null)
                {
                    StopCoroutine(transitionCoroutine);
                }
                transitionCoroutine = StartCoroutine(CinematicCameraTransition());
                return;
            }

            // Normal step advancement
            AdvanceStepInternal();
        }

        /// <summary>
        /// Internal method that performs the actual step advancement.
        /// Called directly or after a camera transition completes.
        /// </summary>
        private void AdvanceStepInternal()
        {
            // Unlock any power button that was locked at peak brightness from previous step
            var panelController = FindFirstObjectByType<HeartPowerPanelController>();
            if (panelController != null)
            {
                panelController.UnlockButtonBrightness();
            }

            currentStepIndex++;

            if (currentStepIndex >= steps.Count)
            {
                CompleteTutorial();
                return;
            }

            var step = CurrentStep;
            if (step == null)
            {
                Debug.LogError("[TutorialManager] CurrentStep is null after advancing!");
                return;
            }

            // For all power activation steps, always do cinematic camera move to ensure focal point is in correct position
            // NOTE: power_sculpt is NOT included here - it has a dedicated handler that waits for DevouringMaw to finish
            bool isPowerActivationStep = step.stepId == "power_murmuring" ||
                                         step.stepId == "power_grasp" ||
                                         step.stepId == "power_maw" ||
                                         step.stepId == "power_misdirect";
            if (isPowerActivationStep)
            {
                StartCoroutine(CinematicCameraTransitionThenShowStep(step));
                return;
            }

            // Special handling for power effect steps - spawn visitor and track with focal point
            if (step.stepId == "power_murmuring_effect")
            {
                StartCoroutine(HandlePowerMurmuringEffectStep(step));
                return;
            }

            if (step.stepId == "power_grasp_effect")
            {
                StartCoroutine(HandlePowerGraspEffectStep(step));
                return;
            }

            if (step.stepId == "power_maw_effect")
            {
                StartCoroutine(HandlePowerMawEffectStep(step));
                return;
            }

            if (step.stepId == "power_sculpt")
            {
                StartCoroutine(HandlePowerSculptStep(step));
                return;
            }

            if (step.stepId == "power_sculpt_effect")
            {
                StartCoroutine(HandlePowerSculptEffectStep(step));
                return;
            }

            if (step.stepId == "power_misdirect_effect")
            {
                StartCoroutine(HandlePowerMisdirectEffectStep(step));
                return;
            }

            if (step.stepId == "lantern_demo")
            {
                StartCoroutine(HandleLanternDemoStep(step));
                return;
            }

            if (step.stepId == "essence_gain")
            {
                StartCoroutine(HandleEssenceGainStep(step));
                return;
            }

            // Check if this step highlights a power button - if so, wait for peak brightness
            int powerButtonIndex = GetPowerButtonIndex(step);
            if (powerButtonIndex >= 0)
            {
                StartCoroutine(WaitForPeakBrightnessThenShowStep(step, powerButtonIndex));
                return;
            }

            // Normal step display (no power button sync needed)
            ShowStepImmediate(step);
        }

        /// <summary>
        /// Gets the power button index from a step's highlight target, or -1 if not a power button.
        /// </summary>
        private int GetPowerButtonIndex(TutorialStep step)
        {
            if (step.highlightType != TutorialHighlightType.UIElementCircular)
                return -1;

            if (string.IsNullOrEmpty(step.highlightTargetName))
                return -1;

            if (!step.highlightTargetName.StartsWith("PowerButton_"))
                return -1;

            string indexStr = step.highlightTargetName.Substring("PowerButton_".Length);
            if (int.TryParse(indexStr, out int index))
                return index;

            return -1;
        }

        /// <summary>
        /// Waits for the specified power button to reach peak brightness, then shows the step.
        /// This synchronizes the tutorial pause with the button's glow cycle for better visual impact.
        /// The glow animation uses Time.unscaledTime, so it continues even while the game is paused.
        /// </summary>
        private IEnumerator WaitForPeakBrightnessThenShowStep(TutorialStep step, int buttonIndex)
        {
            var panelController = FindFirstObjectByType<HeartPowerPanelController>();
            if (panelController == null)
            {
                ShowStepImmediate(step);
                yield break;
            }

            // Wait for the button to reach peak brightness (>= 0.95 threshold)
            // The glow animation uses Time.unscaledTime, so it continues during pause
            const float BRIGHTNESS_THRESHOLD = 0.95f;

            while (true)
            {
                float brightness = panelController.GetButtonPulseBrightness(buttonIndex);
                if (brightness >= BRIGHTNESS_THRESHOLD)
                {
                    break;
                }

                yield return null;
            }

            // Lock the button at peak brightness so it stays bright while step is shown
            panelController.LockButtonAtPeakBrightness(buttonIndex);

            // Now show the step (modal appears telling player to press the button)
            ShowStepImmediate(step);

            // AFTER the modal is shown, unlock this power so the player can activate it
            panelController.EnablePowerForTutorial(buttonIndex);
        }

        /// <summary>
        /// Shows a tutorial step immediately without waiting for brightness sync.
        /// </summary>
        private void ShowStepImmediate(TutorialStep step)
        {
            // Check if power is already active for PowerActivated trigger steps
            // This prevents tutorial lock if player activated power before being prompted
            // NOTE: We ALWAYS check this, even after cinematic transitions - if the player
            // activated the power during the transition, we should still advance
            if (step.triggerType == TutorialTriggerType.PowerActivated && IsPowerAlreadyActiveForCurrentStep())
            {
                // Lock all powers — the activated power has served its purpose
                var pc = FindFirstObjectByType<HeartPowerPanelController>();
                if (pc != null) pc.DisableAllPowersForTutorial();

                AdvanceStep();
                return;
            }

            // Handle pause state - pause when dim overlay is visible (highlightType != None)
            // When highlightType is None, the player should see the game clearly without pause
            bool shouldPause = step.pauseGame || step.highlightType != TutorialHighlightType.None;

            // For PowerActivated trigger steps, we must unpause so the player can activate the power
            // and the power effect can initialize (some use coroutines with WaitForSeconds)
            if (step.triggerType == TutorialTriggerType.PowerActivated && isPaused)
            {
                PauseGame(false);
                shouldPause = false;
            }

            if (shouldPause && !isPaused)
            {
                PauseGame(true);
            }
            else if (!shouldPause && isPaused)
            {
                PauseGame(false);
            }

            // Spawn visitor if needed
            if (step.spawnVisitor && visitorSpawner != null)
            {
                visitorSpawner.SpawnTutorialVisitor();
            }

            // Update tracking for triggers
            if (GameController.Instance != null)
            {
                previousEssence = GameController.Instance.CurrentEssence;
            }

            var cam = Camera.main;
            if (cam != null)
            {
                previousCameraPosition = cam.transform.position;
            }

            OnStepChanged?.Invoke(currentStepIndex);
        }

        /// <summary>
        /// Performs a cinematic camera transition: dims scene, pauses, moves camera smoothly.
        /// </summary>
        /// <param name="advanceAfter">If true, advances to next step after transition. If false, just does the camera move.</param>
        private IEnumerator CinematicCameraTransition(bool advanceAfter = true, bool useFurthestNode = false)
        {
            isTransitioning = true;

            // Step 1: Dim the scene and pause
            PauseGame(true);

            // Notify UI to show dim overlay during transition
            uiController?.ShowTransitionOverlay(true);

            // Small delay to let the dim effect be visible
            yield return new WaitForSecondsRealtime(0.3f);

            // Step 2: Start smooth camera movement to target node
            Vector3 targetPos = useFurthestNode
                ? GetFurthestNonHeartNodePosition()
                : GetNearestNonHeartNodePosition();

            var cameraController = FindFirstObjectByType<CameraController3D>();

            if (cameraController != null && targetPos != Vector3.zero)
            {
                cameraController.FocusOnPosition(targetPos, instant: false, lerpSpeed: 8f);

                // Step 3: Wait for camera to finish moving
                while (cameraController.IsFocalPointMoving)
                {
                    yield return null;
                }
            }

            // Small delay after arriving to let the new view settle
            yield return new WaitForSecondsRealtime(0.2f);

            // Step 4: Complete transition
            isTransitioning = false;
            uiController?.ShowTransitionOverlay(false);

            // Optionally advance to the next step
            if (advanceAfter)
            {
                AdvanceStepInternal();
            }
        }

        /// <summary>
        /// Handles the power_grasp_effect step with camera tracking.
        /// Positions the camera on the node side of the visitor-to-HGZ ray,
        /// spawns a visitor, and tracks them as they walk into the HGZ.
        /// </summary>
        private IEnumerator HandlePowerGraspEffectStep(TutorialStep step)
        {
            // CRITICAL: Always unpause first if needed - the game must be running
            bool shouldPause = step.pauseGame || step.highlightType != TutorialHighlightType.None;
            if (!shouldPause && isPaused)
            {
                PauseGame(false);
            }

            // Check if the power is still active - if not, skip this step
            var heartPowerManager = HeartPowerManager.Instance;
            if (heartPowerManager == null || !heartPowerManager.IsPowerActive(HeartPowerType.HeartwardGrasp))
            {
                AdvanceStep();
                yield break;
            }

            // Get the active HeartwardGraspEffect
            var graspEffect = HeartwardGraspEffect.ActiveInstance;
            if (graspEffect == null)
            {
                AdvanceStep();
                yield break;
            }

            // Get the path-side grabbing position (where visitor should walk near)
            // This is the point on the walkable path, not the forest wall position
            Vector3 hgzPos = graspEffect.GrabbingPathPosition;

            // Get heart position for calculating spawn direction
            var mazeGrid = FindFirstObjectByType<MazeGridBehaviour>();
            Vector3 heartPos = mazeGrid != null ? mazeGrid.HeartWorldPosition : Vector3.zero;

            // Calculate spawn position: beyond the HGZ, away from heart, so visitor walks toward heart
            // The visitor will walk FROM spawnPos THROUGH the HGZ detection zone
            Vector3 dirToHeart = (heartPos - hgzPos).normalized;
            Vector3 spawnPos = hgzPos - dirToHeart * 5f; // 5 units beyond HGZ away from heart
            spawnPos.z = 0f; // Ensure on ground plane

            // Destination is the heart - visitor walks through the HGZ detection zone on its way
            // Using a short destination caused the visitor to reach it and go Idle before being grabbed
            Vector3 visitorDestination = heartPos;
            visitorDestination.z = 0f;

            // Camera focal point is at the midpoint of the visitor-to-HGZ ray
            // This keeps the camera looking at the path the visitor will walk
            Vector3 cameraFocalPos = (spawnPos + hgzPos) / 2f;
            cameraFocalPos.z = 0f;

            // Move focal point to the calculated position
            var cameraController = FindFirstObjectByType<CameraController3D>();
            if (cameraController != null)
            {
                cameraController.FocusOnPosition(cameraFocalPos, instant: false, lerpSpeed: 8f);

                // Wait for camera to finish moving
                while (cameraController.IsFocalPointMoving)
                {
                    yield return null;
                }
            }

            // Spawn visitor at calculated position - destination goes through HGZ
            if (visitorSpawner != null)
            {
                visitorSpawner.SpawnVisitorForHGZ(spawnPos, visitorDestination);
            }

            // Wait for spawn to complete
            yield return new WaitForSecondsRealtime(0.5f);

            // Find the spawned visitor
            var visitors = FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);
            VisitorControllerBase targetVisitor = null;
            float closestDist = float.MaxValue;
            foreach (var v in visitors)
            {
                if (v == null) continue;
                float dist = Vector3.Distance(v.transform.position, spawnPos);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    targetVisitor = v;
                }
            }

            // Show the step UI (game not paused - action continues)
            // Pause state already handled at start of method - no need to check again

            // Update tracking for triggers
            if (GameController.Instance != null)
            {
                previousEssence = GameController.Instance.CurrentEssence;
            }

            var cam = Camera.main;
            if (cam != null)
            {
                previousCameraPosition = cam.transform.position;
            }

            // Fire the step changed event
            OnStepChanged?.Invoke(currentStepIndex);

            // Track visitor by directly updating focal point position until grabbed
            if (targetVisitor != null && cameraController != null)
            {
                // Phase 1: Track until visitor is grabbed
                while (targetVisitor != null && targetVisitor.State != VisitorControllerBase.VisitorState.Grabbed)
                {
                    Vector3 visitorPos = targetVisitor.transform.position;
                    visitorPos.z = 0f;
                    cameraController.FocusOnPosition(visitorPos, instant: true);
                    yield return null;
                }

                // Phase 2: Move camera to pushing HGZ and rotate to look along heart->pushing axis
                if (targetVisitor != null && graspEffect != null && targetVisitor.State == VisitorControllerBase.VisitorState.Grabbed)
                {
                    Vector3 pushingPos = graspEffect.PushingZonePosition;
                    pushingPos.z = 0f;

                    // Calculate direction from heart to pushing HGZ for camera rotation
                    Vector2 heartPos2D = new Vector2(heartPos.x, heartPos.y);
                    Vector2 pushingPos2D = new Vector2(pushingPos.x, pushingPos.y);
                    Vector2 dirHeartToPushing = (pushingPos2D - heartPos2D).normalized;

                    cameraController.FocusOnPosition(pushingPos, instant: false, lerpSpeed: 12f);
                    cameraController.SetFocalPointDirection(dirHeartToPushing);

                    // Wait for camera to arrive
                    while (cameraController.IsFocalPointMoving)
                    {
                        yield return null;
                    }

                    // Phase 3: Wait for visitor to become visible (back on ground)
                    while (targetVisitor != null && !IsVisitorVisible(targetVisitor))
                    {
                        yield return null;
                    }

                    // Phase 4: Track visitor until consumed
                    while (targetVisitor != null && targetVisitor.State != VisitorControllerBase.VisitorState.Consumed)
                    {
                        Vector3 visitorPos = targetVisitor.transform.position;
                        visitorPos.z = 0f;
                        cameraController.FocusOnPosition(visitorPos, instant: true);
                        yield return null;
                    }

                }
            }

            AdvanceStep();
        }

        /// <summary>
        /// Checks if a visitor is visible (renderer enabled and above ground).
        /// </summary>
        private bool IsVisitorVisible(VisitorControllerBase visitor)
        {
            if (visitor == null) return false;

            // Check if any renderer is enabled
            var renderers = visitor.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r.enabled) return true;
            }
            return false;
        }

        /// <summary>
        /// Handles the power_murmuring_effect step with visitor tracking.
        /// Spawns a visitor and tracks them as they walk through the fog.
        /// </summary>
        private IEnumerator HandlePowerMurmuringEffectStep(TutorialStep step)
        {
            // CRITICAL: Always unpause first if needed - the game must be running
            // This step has pause: false, so ensure the game is unpaused
            bool shouldPause = step.pauseGame || step.highlightType != TutorialHighlightType.None;
            if (!shouldPause && isPaused)
            {
                PauseGame(false);
            }

            // Check if the power is still active - if not, skip this step
            var heartPowerManager = HeartPowerManager.Instance;
            if (heartPowerManager == null || !heartPowerManager.IsPowerActive(HeartPowerType.MurmuringPaths))
            {
                AdvanceStep();
                yield break;
            }

            var cameraController = FindFirstObjectByType<CameraController3D>();

            // Record existing visitors so we can identify the newly spawned one
            var existingVisitors = new HashSet<VisitorControllerBase>(
                FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None));

            // Spawn visitor at the furthest point on the fog path so they walk THROUGH the fog
            if (visitorSpawner != null)
            {
                var heartPowerMgr = HeartPowerManager.Instance;
                // Use the furthest tile position on the fog (farthest from heart)
                Vector3? furthestPos = heartPowerMgr?.GetActiveMurmuringPathsFurthestPosition();
                Vector3? fogTarget = heartPowerMgr?.GetActiveMurmuringPathsTargetPosition();

                if (furthestPos.HasValue)
                {
                    var mazeGrid = FindFirstObjectByType<MazeGridBehaviour>();
                    Vector3 heartPos = mazeGrid?.HeartWorldPosition ?? Vector3.zero;

                    // Offset the spawn position 90 degrees around the node so the visitor
                    // approaches along the walkable ring instead of straight through the
                    // unwalkable node center (where the pond prop sits).
                    Vector3 dirToHeart = (heartPos - furthestPos.Value).normalized;
                    Vector3 perpDir = new Vector3(-dirToHeart.y, dirToHeart.x, 0f);
                    Vector3 spawnPos = furthestPos.Value + perpDir * 3f;
                    spawnPos.z = 0f;

                    // Find nearest walkable tile to the offset position
                    var mazeData = mazeGrid?.WorldSpaceMazeData;
                    if (mazeData != null)
                    {
                        var walkableTile = ForestMaze.MazePathfinding.FindNearestWalkableTile(
                            mazeData, new Vector2(spawnPos.x, spawnPos.y));
                        if (walkableTile != null)
                        {
                            spawnPos = new Vector3(walkableTile.Position.x, walkableTile.Position.y, 0f);
                        }
                    }

                    visitorSpawner.SpawnVisitorForHGZ(spawnPos, heartPos);
                }
                else if (fogTarget.HasValue)
                {
                    var mazeGrid = FindFirstObjectByType<MazeGridBehaviour>();
                    Vector3 heartPos = mazeGrid?.HeartWorldPosition ?? Vector3.zero;

                    Vector3 dirToHeart = (heartPos - fogTarget.Value).normalized;
                    Vector3 perpDir = new Vector3(-dirToHeart.y, dirToHeart.x, 0f);
                    Vector3 spawnPos = fogTarget.Value + perpDir * 3f;
                    spawnPos.z = 0f;

                    var mazeData = mazeGrid?.WorldSpaceMazeData;
                    if (mazeData != null)
                    {
                        var walkableTile = ForestMaze.MazePathfinding.FindNearestWalkableTile(
                            mazeData, new Vector2(spawnPos.x, spawnPos.y));
                        if (walkableTile != null)
                        {
                            spawnPos = new Vector3(walkableTile.Position.x, walkableTile.Position.y, 0f);
                        }
                    }

                    visitorSpawner.SpawnVisitorForHGZ(spawnPos, heartPos);
                }
                else
                {
                    visitorSpawner.SpawnTutorialVisitorTowardHeart();
                }
            }

            // Wait for spawn to complete
            yield return new WaitForSecondsRealtime(0.5f);

            // Find the NEWLY spawned visitor (not one from a previous step)
            var allVisitors = FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);
            VisitorControllerBase targetVisitor = null;
            foreach (var v in allVisitors)
            {
                if (v == null) continue;
                if (!existingVisitors.Contains(v))
                {
                    targetVisitor = v;
                    break;
                }
            }

            // Fallback: take any visitor
            if (targetVisitor == null && allVisitors.Length > 0)
            {
                targetVisitor = allVisitors[0];
            }

            // Force the visitor to be lured so they path toward the heart through the fog
            if (targetVisitor != null)
            {
                targetVisitor.SetLured(true);
            }

            // Show the step UI
            ShowStepImmediate(step);

            // Track visitor until they are consumed (destroyed by the heart)
            // We want to watch them walk through the fog, get grabbed, retract, and be consumed
            if (targetVisitor != null && cameraController != null)
            {
                while (targetVisitor != null &&
                       targetVisitor.State != VisitorControllerBase.VisitorState.Consumed)
                {
                    Vector3 visitorPos = targetVisitor.transform.position;
                    visitorPos.z = 0f;
                    cameraController.FocusOnPosition(visitorPos, instant: true);
                    yield return null;
                }
            }
            // Wait for heart consumption animation to complete
            yield return new WaitForSecondsRealtime(1.5f);

            AdvanceStep();
        }

        /// <summary>
        /// Handles the power_maw_effect step with visitor tracking.
        /// Spawns a visitor and tracks them as they walk into the Devouring Maw.
        /// </summary>
        private IEnumerator HandlePowerMawEffectStep(TutorialStep step)
        {
            // CRITICAL: Always unpause first if needed - the game must be running
            // This step has pause: false, so ensure the game is unpaused
            bool shouldPause = step.pauseGame || step.highlightType != TutorialHighlightType.None;
            if (!shouldPause && isPaused)
            {
                PauseGame(false);
            }

            // Check if the power is still active - if not, skip this step
            var heartPowerManager = HeartPowerManager.Instance;
            if (heartPowerManager == null || !heartPowerManager.IsPowerActive(HeartPowerType.DevouringMaw))
            {
                AdvanceStep();
                yield break;
            }

            var cameraController = FindFirstObjectByType<CameraController3D>();

            // Spawn visitor that walks THROUGH the active Devouring Maw
            if (visitorSpawner != null)
            {
                visitorSpawner.SpawnVisitorThroughMaw();
            }

            // Wait for spawn to complete
            yield return new WaitForSecondsRealtime(0.5f);

            // Find the most recently spawned visitor
            var visitors = FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);
            VisitorControllerBase targetVisitor = null;
            foreach (var v in visitors)
            {
                if (v == null) continue;
                if (v.transform.position.magnitude > 0)
                {
                    targetVisitor = v;
                    break;
                }
            }

            // Fallback: take any visitor
            if (targetVisitor == null && visitors.Length > 0)
            {
                targetVisitor = visitors[0];
            }

            // Show the step UI
            ShowStepImmediate(step);

            // Track visitor until they are Consumed by the maw (destroyed)
            if (targetVisitor != null && cameraController != null)
            {
                while (targetVisitor != null &&
                       targetVisitor.State != VisitorControllerBase.VisitorState.Consumed)
                {
                    Vector3 visitorPos = targetVisitor.transform.position;
                    visitorPos.z = 0f;
                    cameraController.FocusOnPosition(visitorPos, instant: true);
                    yield return null;
                }

                // Wait a moment for the maw animation to complete
                yield return new WaitForSecondsRealtime(1.5f);
            }

            AdvanceStep();
        }

        /// <summary>
        /// Handles the power_sculpt step.
        /// Waits for devour effect to despawn before showing the step.
        /// </summary>
        private IEnumerator HandlePowerSculptStep(TutorialStep step)
        {
            // Wait for DevouringMaw power to finish (no longer active)
            var heartPowerManager = FindFirstObjectByType<HeartPowerManager>();
            if (heartPowerManager != null)
            {
                while (heartPowerManager.IsPowerActive(HeartPowerType.DevouringMaw))
                {
                    yield return null;
                }
            }

            // Small delay after devour despawns for visual clarity
            yield return new WaitForSecondsRealtime(0.3f);

            // Perform cinematic camera transition to position focal point on a node
            yield return CinematicCameraTransition(advanceAfter: false);

            // Check if this step highlights a power button
            int powerButtonIndex = GetPowerButtonIndex(step);
            if (powerButtonIndex >= 0)
            {
                // Use coroutine to wait for peak brightness
                yield return StartCoroutine(WaitForPeakBrightnessThenShowStepCoroutine(step, powerButtonIndex));
            }
            else
            {
                ShowStepImmediate(step);
            }

        }

        /// <summary>
        /// Coroutine version of WaitForPeakBrightnessThenShowStep for use in nested coroutines.
        /// </summary>
        private IEnumerator WaitForPeakBrightnessThenShowStepCoroutine(TutorialStep step, int buttonIndex)
        {
            var panelController = FindFirstObjectByType<HeartPowerPanelController>();
            if (panelController == null)
            {
                ShowStepImmediate(step);
                yield break;
            }

            const float BRIGHTNESS_THRESHOLD = 0.95f;

            while (true)
            {
                float brightness = panelController.GetButtonPulseBrightness(buttonIndex);
                if (brightness >= BRIGHTNESS_THRESHOLD)
                {
                    break;
                }
                yield return null;
            }

            panelController.LockButtonAtPeakBrightness(buttonIndex);
            ShowStepImmediate(step);

            // AFTER the modal is shown, unlock this power so the player can activate it
            panelController.EnablePowerForTutorial(buttonIndex);
        }

        /// <summary>
        /// Handles the power_sculpt_effect step.
        /// Waits for player to select a lantern from the sculpt menu, then auto-advances.
        /// </summary>
        private IEnumerator HandlePowerSculptEffectStep(TutorialStep step)
        {
            // CRITICAL: Always unpause first if needed - the game must be running
            // This step has pause: false, so ensure the game is unpaused
            bool shouldPause = step.pauseGame || step.highlightType != TutorialHighlightType.None;
            if (!shouldPause && isPaused)
            {
                PauseGame(false);
            }

            // Check if the sculpt power is still active - if not, skip this step
            var heartPowerManager = HeartPowerManager.Instance;
            if (heartPowerManager == null || !heartPowerManager.IsPowerActive(HeartPowerType.Sculpting))
            {
                AdvanceStep();
                yield break;
            }

            // Show the step UI immediately
            ShowStepImmediate(step);

            // Get the node index at the focal point for placing the lantern
            var cameraController = FindFirstObjectByType<CameraController3D>();
            var dynamicMaze = FindFirstObjectByType<DynamicMazeGrowth>();
            int targetNodeIndex = -1;

            if (cameraController != null && dynamicMaze != null)
            {
                Vector3 focalPos = cameraController.FocalPointPosition;
                targetNodeIndex = dynamicMaze.FindNodeIndexAtPosition(focalPos);
            }

            // Wait for the sculpt menu to open, then highlight only the lantern button
            while (SculptingEffect.ActiveInstance == null || !SculptingEffect.ActiveInstance.IsMenuActive)
            {
                yield return null;
            }

            // Highlight only the lantern button and disable others
            SculptingEffect.ActiveInstance.HighlightLanternButtonOnly();

            // Wait for the sculpt menu to close OR for a lantern to be placed
            while (SculptingEffect.ActiveInstance != null && SculptingEffect.ActiveInstance.IsMenuActive)
            {
                yield return null;
            }

            // Check if a lantern was placed at the target node
            bool lanternPlaced = false;
            if (dynamicMaze != null && targetNodeIndex > 0)
            {
                var propType = dynamicMaze.GetNodePropType(targetNodeIndex);
                lanternPlaced = propType == DynamicMazeGrowth.NodePropType.FaeLantern;
            }

            // If lantern wasn't placed, place one automatically
            if (!lanternPlaced && dynamicMaze != null && targetNodeIndex > 0)
            {
                dynamicMaze.SetNodeProp(targetNodeIndex, DynamicMazeGrowth.NodePropType.FaeLantern);
            }

            AdvanceStep();
        }

        /// <summary>
        /// Handles the power_misdirect_effect step.
        /// Spawns a visitor at one end of the misdirected edge and tracks them walking along it.
        /// Auto-advances when the visitor reaches the destination node or enters a terminal state.
        /// </summary>
        private IEnumerator HandlePowerMisdirectEffectStep(TutorialStep step)
        {
            // Ensure unpaused
            bool shouldPause = step.pauseGame || step.highlightType != TutorialHighlightType.None;
            if (!shouldPause && isPaused)
            {
                PauseGame(false);
            }

            // Check if the misdirect power is still active - if not, skip this step
            var heartPowerManager = HeartPowerManager.Instance;
            if (heartPowerManager == null || !heartPowerManager.IsPowerActive(HeartPowerType.Misdirect))
            {
                AdvanceStep();
                yield break;
            }

            // Get the misdirected edge endpoints to determine spawn/destination
            int edgeIndex = heartPowerManager.GetMisdirectEdgeIndex();
            var mazeData = FindFirstObjectByType<MazeGridBehaviour>()?.WorldSpaceMazeData;

            if (edgeIndex < 0 || mazeData == null || mazeData.GraphState == null)
            {
                ShowStepImmediate(step);
                yield return new WaitForSecondsRealtime(3f);
                AdvanceStep();
                yield break;
            }

            var edge = mazeData.GraphState.Edges[edgeIndex];
            var polyline = edge.PolylinePoints;
            if (polyline == null || polyline.Count < 2)
            {
                ShowStepImmediate(step);
                yield return new WaitForSecondsRealtime(3f);
                AdvanceStep();
                yield break;
            }

            // Determine which end is farther from heart (spawn there)
            Vector3 heartPos = mazeData.GraphState.Nodes[0].Position; // Root node is index 0
            var mazeGrid = FindFirstObjectByType<MazeGridBehaviour>();
            Vector3 heartWorldPos = mazeGrid != null ? mazeGrid.HeartWorldPosition : new Vector3(heartPos.x, heartPos.y, 0f);
            heartWorldPos.z = 0f;

            Vector2 endA = polyline[0];
            Vector2 endB = polyline[polyline.Count - 1];
            float distA = Vector2.Distance(endA, new Vector2(heartPos.x, heartPos.y));
            float distB = Vector2.Distance(endB, new Vector2(heartPos.x, heartPos.y));

            Vector2 spawnEnd = distA > distB ? endA : endB;
            Vector2 edgeDestEnd = distA > distB ? endB : endA; // End closer to heart (node B)

            // Find nearest walkable tile to spawn position
            var spawnTile = ForestMaze.MazePathfinding.FindNearestWalkableTile(
                mazeData, spawnEnd);

            Vector3 spawnPos = spawnTile != null
                ? new Vector3(spawnTile.Position.x, spawnTile.Position.y, 0f)
                : new Vector3(spawnEnd.x, spawnEnd.y, 0f);

            // Find an exit portal as destination - this prevents backtracking when tutorial ends
            // Choose the portal that is on the opposite side of the heart from the spawn position
            Vector3 destPos = heartWorldPos; // Default fallback
            var dynamicMaze = FindFirstObjectByType<DynamicMazeGrowth>();
            if (dynamicMaze != null)
            {
                var portalPositions = dynamicMaze.GetPortalPositions();
                if (portalPositions != null && portalPositions.Count > 0)
                {
                    // Find portal that makes the visitor walk through the misdirect edge toward heart
                    // This is the portal closest to the "dest end" of the misdirect edge (closer to heart)
                    Vector3 bestPortal = portalPositions[0];
                    float bestDist = float.MaxValue;
                    foreach (var portal in portalPositions)
                    {
                        // Find portal closest to the destination end of the misdirect edge
                        float distToDestEnd = Vector2.Distance(new Vector2(portal.x, portal.y), edgeDestEnd);
                        if (distToDestEnd < bestDist)
                        {
                            bestDist = distToDestEnd;
                            bestPortal = portal;
                        }
                    }
                    destPos = new Vector3(bestPortal.x, bestPortal.y, 0f);
                }
            }

            // Spawn visitor
            if (visitorSpawner != null)
            {
                visitorSpawner.SpawnVisitorForHGZ(spawnPos, destPos);
            }

            // Wait for spawn to complete
            yield return new WaitForSecondsRealtime(0.5f);

            // Find the spawned visitor
            var visitors = FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);
            VisitorControllerBase targetVisitor = null;
            float minDist = float.MaxValue;
            foreach (var v in visitors)
            {
                if (v == null) continue;
                float d = Vector3.Distance(v.transform.position, spawnPos);
                if (d < minDist)
                {
                    minDist = d;
                    targetVisitor = v;
                }
            }

            // Set the visitor as lured so it paths toward the heart through the misdirected edge
            if (targetVisitor != null)
            {
                targetVisitor.SetLured(true);
            }

            // Show the step UI
            ShowStepImmediate(step);

            // Track visitor with camera as they walk along the misdirected edge toward heart
            var cameraController = FindFirstObjectByType<CameraController3D>();

            if (targetVisitor != null && cameraController != null)
            {
                while (targetVisitor != null &&
                       targetVisitor.State != VisitorControllerBase.VisitorState.Consumed &&
                       targetVisitor.State != VisitorControllerBase.VisitorState.Grabbed)
                {
                    // Check if visitor reached the destination end of the misdirected edge (node B)
                    Vector2 visitorPos2D = new Vector2(targetVisitor.transform.position.x, targetVisitor.transform.position.y);
                    float distToEdgeEnd = Vector2.Distance(visitorPos2D, edgeDestEnd);
                    if (distToEdgeEnd < 3.0f) // NODE_RADIUS - visitor has entered the destination node
                    {
                        break;
                    }

                    Vector3 visitorPos = targetVisitor.transform.position;
                    visitorPos.z = 0f;
                    cameraController.FocusOnPosition(visitorPos, instant: true);
                    yield return null;
                }
            }

            AdvanceStep();
        }

        /// <summary>
        /// Handles the lantern_demo step.
        /// Spawns a visitor near the lantern and waits for them to become fascinated.
        /// </summary>
        private IEnumerator HandleLanternDemoStep(TutorialStep step)
        {
            // CRITICAL: Always unpause first if needed - the game must be running
            // This step has pause: false, so ensure the game is unpaused
            bool shouldPause = step.pauseGame || step.highlightType != TutorialHighlightType.None;
            if (!shouldPause && isPaused)
            {
                PauseGame(false);
            }

            // Find the lantern we just placed
            var lanterns = FindObjectsByType<FaeLantern>(FindObjectsSortMode.None);

            FaeLantern targetLantern = null;

            if (lanterns.Length > 0)
            {
                targetLantern = lanterns[0]; // Use the first lantern found
            }

            // Show the step UI
            ShowStepImmediate(step);

            // Position camera on the lantern
            var cameraController = FindFirstObjectByType<CameraController3D>();
            if (cameraController != null && targetLantern != null)
            {
                Vector3 lanternPos = targetLantern.transform.position;
                lanternPos.z = 0f;
                cameraController.FocusOnPosition(lanternPos, instant: true);
            }

            // Spawn visitor near the lantern (on the same node)
            if (visitorSpawner != null && targetLantern != null)
            {
                // Calculate spawn position: on the edge of the node, opposite side from heart
                // Visitor will walk THROUGH the lantern toward the heart, getting fascinated along the way
                Vector3 lanternPos = targetLantern.transform.position;
                var mazeGrid = FindFirstObjectByType<MazeGridBehaviour>();

                // Get heart position with multiple fallbacks
                Vector3 heartPos = Vector3.zero;

                // First try: GameController.Instance.Heart (most reliable)
                if (GameController.Instance != null && GameController.Instance.Heart != null)
                {
                    heartPos = GameController.Instance.Heart.transform.position;
                    heartPos.z = 0f;
                }
                // Second try: MazeGridBehaviour.HeartWorldPosition
                else if (mazeGrid != null && mazeGrid.HeartWorldPosition.sqrMagnitude > 0.01f)
                {
                    heartPos = mazeGrid.HeartWorldPosition;
                }
                // Third try: ForestMapState node 0
                else if (mazeGrid != null && mazeGrid.ForestMapState != null && mazeGrid.ForestMapState.Nodes.Count > 0)
                {
                    var seedNode = mazeGrid.ForestMapState.Nodes[0];
                    heartPos = new Vector3(seedNode.Position.x, seedNode.Position.y, 0f);
                }

                // Direction from lantern to heart
                Vector3 dirToHeart = (heartPos - lanternPos).normalized;
                if (dirToHeart.sqrMagnitude < 0.01f)
                {
                    dirToHeart = new Vector3(1f, 0f, 0f); // Fallback direction
                }

                // Calculate ideal spawn position: perpendicular to heart direction
                // Rotate 90 degrees in XY plane so the visitor approaches along the walkable ring
                // instead of bouncing through the unwalkable node center
                Vector3 perpDir = new Vector3(-dirToHeart.y, dirToHeart.x, 0f);
                Vector3 idealSpawnPos = lanternPos + perpDir * 5f;
                idealSpawnPos.z = 0f;

                // CRITICAL: Find the nearest walkable tile to the ideal spawn position
                // The calculated position might be off the path (in the forest)
                var mazeData = mazeGrid?.WorldSpaceMazeData;
                Vector3 spawnPos;
                if (mazeData != null)
                {
                    var nearestWalkableTile = MazePathfinding.FindNearestWalkableTile(
                        mazeData, new Vector2(idealSpawnPos.x, idealSpawnPos.y));

                    if (nearestWalkableTile != null)
                    {
                        spawnPos = new Vector3(nearestWalkableTile.Position.x, nearestWalkableTile.Position.y, 0f);
                    }
                    else
                    {
                        spawnPos = lanternPos - dirToHeart * 2f;
                        spawnPos.z = 0f;
                    }
                }
                else
                {
                    spawnPos = idealSpawnPos;
                }

                // Destination is the heart (visitor walks toward heart, through lantern area)
                Vector3 destPos = heartPos;
                destPos.z = 0f;

                // Lantern demo visitor must NOT be fascination-immune
                visitorSpawner.SpawnVisitorForHGZ(spawnPos, destPos, fascinationImmune: false);
            }

            // Wait for spawn to complete
            yield return new WaitForSecondsRealtime(0.5f);

            // Find the spawned visitor
            var visitors = FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);
            VisitorControllerBase targetVisitor = null;
            foreach (var v in visitors)
            {
                if (v == null) continue;
                // Find visitor closest to where we spawned
                if (targetLantern != null)
                {
                    float dist = Vector3.Distance(v.transform.position, targetLantern.transform.position);
                    if (dist < 10f)
                    {
                        targetVisitor = v;
                        break;
                    }
                }
            }

            // Track visitor until they become fascinated by the lantern
            if (targetVisitor != null && cameraController != null)
            {
                while (targetVisitor != null)
                {
                    float distToLantern = targetLantern != null ? Vector3.Distance(targetVisitor.transform.position, targetLantern.transform.position) : -1f;

                    // Check if visitor is fascinated by a lantern
                    if (targetVisitor.CurrentFaeLantern != null)
                    {
                        break;
                    }

                    // Check for consumed state (game over for this visitor)
                    if (targetVisitor.State == VisitorControllerBase.VisitorState.Consumed ||
                        targetVisitor.State == VisitorControllerBase.VisitorState.Grabbed)
                    {
                        break;
                    }

                    // Force fascination as soon as visitor enters lantern influence radius
                    if (targetLantern != null && distToLantern < targetLantern.InfluenceRadius)
                    {
                        targetVisitor.ForceFascinateByLantern(targetLantern);
                        break;
                    }

                    // Track visitor position
                    Vector3 visitorPos = targetVisitor.transform.position;
                    visitorPos.z = 0f;
                    cameraController.FocusOnPosition(visitorPos, instant: true);

                    yield return null;
                }
            }

            // Short pause to let player see the fascination effect
            yield return new WaitForSecondsRealtime(1.5f);

            AdvanceStep();
        }

        /// <summary>
        /// Handles the essence_gain step by keeping the camera focused on the lantern area
        /// while waiting for essence to increase (via the fascinated visitor draining).
        /// Without this, the camera would snap back to the focal point after the lantern step.
        /// </summary>
        private IEnumerator HandleEssenceGainStep(TutorialStep step)
        {
            // Ensure game is unpaused
            if (isPaused)
            {
                PauseGame(false);
            }

            // Show the step UI
            ShowStepImmediate(step);

            var cameraController = FindFirstObjectByType<CameraController3D>();

            // Find the lantern to keep camera focused on
            var lanterns = FindObjectsByType<FaeLantern>(FindObjectsSortMode.None);
            Vector3 focusPos = Vector3.zero;
            if (lanterns.Length > 0)
            {
                focusPos = lanterns[0].transform.position;
                focusPos.z = 0f;
            }

            // Track essence changes - the step trigger (EssenceIncreased) will call AdvanceStep()
            // via NotifyEssenceChanged(), but we need to keep the camera focused until then
            while (isActive && CurrentStep != null && CurrentStep.stepId == "essence_gain")
            {
                // Keep camera on the lantern area
                if (cameraController != null && focusPos != Vector3.zero)
                {
                    cameraController.FocusOnPosition(focusPos, instant: true);
                }

                yield return null;
            }

        }

        /// <summary>
        /// Gets the position of a node one edge away from the heart with the shortest walking distance.
        /// Walking distance is calculated from the edge's polyline points.
        /// Returns Vector3.zero if no valid node found.
        /// </summary>
        private Vector3 GetNearestNonHeartNodePosition()
        {
            var mazeGrid = FindFirstObjectByType<MazeGridBehaviour>();
            if (mazeGrid == null || mazeGrid.ForestMapState == null) return Vector3.zero;

            var forestState = mazeGrid.ForestMapState;
            if (forestState.Nodes == null || forestState.Nodes.Count < 2) return Vector3.zero;
            if (forestState.Edges == null || forestState.Edges.Count == 0) return Vector3.zero;

            // Get heart node (node 0 is always the heart/root)
            var heartNode = forestState.Nodes[0];
            Vector2 heartPos = heartNode.Position;

            // Find the node directly connected to the heart with the shortest walking distance
            float shortestWalkingDistance = float.MaxValue;
            int bestNodeId = -1;
            Vector2 bestNodePos = Vector2.zero;

            foreach (int edgeId in heartNode.IncidentEdges)
            {
                if (edgeId < 0 || edgeId >= forestState.Edges.Count) continue;

                var edge = forestState.Edges[edgeId];

                // Find the other node connected by this edge
                int otherNodeId = -1;
                if (edge.NodeA == 0 && edge.NodeB.HasValue)
                {
                    otherNodeId = edge.NodeB.Value;
                }
                else if (edge.NodeB.HasValue && edge.NodeB.Value == 0)
                {
                    otherNodeId = edge.NodeA;
                }

                if (otherNodeId > 0 && otherNodeId < forestState.Nodes.Count)
                {
                    var neighborNode = forestState.Nodes[otherNodeId];

                    // Calculate walking distance from polyline points
                    float walkingDistance = CalculatePolylineLength(edge.PolylinePoints);

                    // If no polyline, fallback to straight-line distance
                    if (walkingDistance <= 0)
                    {
                        walkingDistance = Vector2.Distance(heartPos, neighborNode.Position);
                    }

                    if (walkingDistance < shortestWalkingDistance)
                    {
                        shortestWalkingDistance = walkingDistance;
                        bestNodeId = otherNodeId;
                        bestNodePos = neighborNode.Position;
                    }
                }
            }

            if (bestNodeId >= 0)
            {
                return new Vector3(bestNodePos.x, bestNodePos.y, 0f);
            }

            // Fallback: find nearest node by straight-line distance
            float nearestDist = float.MaxValue;
            Vector2 nearestNodePos = heartPos;

            for (int i = 1; i < forestState.Nodes.Count; i++)
            {
                var node = forestState.Nodes[i];
                float dist = Vector2.Distance(heartPos, node.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestNodePos = node.Position;
                }
            }

            return new Vector3(nearestNodePos.x, nearestNodePos.y, 0f);
        }

        /// <summary>
        /// Gets the position of the non-heart node furthest from the heart (by walking distance).
        /// Used for Yoink! demo to show the power reaching across the maze.
        /// </summary>
        private Vector3 GetFurthestNonHeartNodePosition()
        {
            var mazeGrid = FindFirstObjectByType<MazeGridBehaviour>();
            if (mazeGrid == null || mazeGrid.ForestMapState == null) return Vector3.zero;

            var forestState = mazeGrid.ForestMapState;
            if (forestState.Nodes == null || forestState.Nodes.Count < 2) return Vector3.zero;

            // Find the node with the greatest straight-line distance from heart
            var heartNode = forestState.Nodes[0];
            Vector2 heartPos = heartNode.Position;

            float furthestDist = 0f;
            Vector2 furthestNodePos = heartPos;

            for (int i = 1; i < forestState.Nodes.Count; i++)
            {
                var node = forestState.Nodes[i];
                float dist = Vector2.Distance(heartPos, node.Position);
                if (dist > furthestDist)
                {
                    furthestDist = dist;
                    furthestNodePos = node.Position;
                }
            }

            return new Vector3(furthestNodePos.x, furthestNodePos.y, 0f);
        }

        /// <summary>
        /// Calculates the total length of a polyline path.
        /// </summary>
        private float CalculatePolylineLength(List<Vector2> points)
        {
            if (points == null || points.Count < 2) return 0f;

            float totalLength = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                totalLength += Vector2.Distance(points[i - 1], points[i]);
            }
            return totalLength;
        }

        /// <summary>
        /// Checks if the camera's focal point is currently on or near the heart (root) node.
        /// Uses NODE_RADIUS to determine if the focal point is within the heart node's area.
        /// </summary>
        private bool IsFocalPointOnHeartNode()
        {
            const float NODE_RADIUS = 3.0f;

            var cameraController = FindFirstObjectByType<CameraController3D>();
            if (cameraController == null) return false;

            var mazeGrid = FindFirstObjectByType<MazeGridBehaviour>();
            if (mazeGrid == null || mazeGrid.ForestMapState == null) return false;

            var forestState = mazeGrid.ForestMapState;
            if (forestState.Nodes == null || forestState.Nodes.Count == 0) return false;

            // Node 0 is always the heart/root
            Vector2 heartPos = forestState.Nodes[0].Position;
            Vector3 focalPos = cameraController.FocalPointPosition;
            Vector2 focalPos2D = new Vector2(focalPos.x, focalPos.y);

            float distToHeart = Vector2.Distance(focalPos2D, heartPos);

            // Consider on heart if within NODE_RADIUS
            bool onHeart = distToHeart <= NODE_RADIUS;
            return onHeart;
        }

        /// <summary>
        /// Performs cinematic camera transition then shows the step.
        /// Used to ensure camera is in correct position before showing power steps.
        /// </summary>
        private IEnumerator CinematicCameraTransitionThenShowStep(TutorialStep step)
        {
            // For Yoink! (power_grasp), move camera to the furthest node to better demonstrate the power
            bool useFurthest = step.stepId == "power_grasp";

            // Perform the cinematic camera transition WITHOUT auto-advancing
            yield return CinematicCameraTransition(advanceAfter: false, useFurthestNode: useFurthest);

            // Now show the step normally (will go through power button brightness sync)
            int powerButtonIndex = GetPowerButtonIndex(step);
            if (powerButtonIndex >= 0)
            {
                StartCoroutine(WaitForPeakBrightnessThenShowStep(step, powerButtonIndex));
            }
            else
            {
                ShowStepImmediate(step);
            }
        }

        /// <summary>
        /// Skips the tutorial entirely.
        /// Also disables tutorial auto-start for future runs.
        /// </summary>
        public void SkipTutorial()
        {
            if (!isActive) return;

            // Disable tutorial auto-start for future runs
            // Player explicitly chose to skip, so don't show it again automatically
            GameSettings.ShowTutorialOnFirstRun = false;

            CompleteTutorial();
        }

        /// <summary>
        /// Completes the tutorial and saves progress.
        /// </summary>
        private void CompleteTutorial()
        {
            isActive = false;
            currentStepIndex = -1;

            // Unlock all power buttons and release tutorial lock
            var panelController = FindFirstObjectByType<HeartPowerPanelController>();
            if (panelController != null)
            {
                panelController.UnlockButtonBrightness();
                panelController.SetTutorialPowerLock(false);
            }

            // Ensure game is unpaused
            if (isPaused)
            {
                PauseGame(false);
            }

            // Assign valid exit destinations to any remaining visitors
            AssignExitDestinationsToRemainingVisitors();

            // Mark as completed
            GameSettings.TutorialCompleted = true;
            GameSettings.Save();

            OnTutorialCompleted?.Invoke();
        }

        /// <summary>
        /// Assigns valid exit portal destinations to all remaining visitors and recalculates their paths.
        /// Called at the end of the tutorial to ensure visitors have proper navigation targets.
        /// </summary>
        private void AssignExitDestinationsToRemainingVisitors()
        {
            var dynamicMaze = FindFirstObjectByType<DynamicMazeGrowth>();
            if (dynamicMaze == null)
            {
                return;
            }

            var portalPositions = dynamicMaze.GetPortalPositions();
            if (portalPositions == null || portalPositions.Count == 0)
            {
                return;
            }

            var visitors = FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);
            int assignedCount = 0;

            foreach (var visitor in visitors)
            {
                if (visitor == null) continue;

                // Skip visitors in terminal states
                var state = visitor.State;
                if (state == VisitorControllerBase.VisitorState.Consumed ||
                    state == VisitorControllerBase.VisitorState.Grabbed ||
                    state == VisitorControllerBase.VisitorState.Escaping)
                {
                    continue;
                }

                // Skip misdirected visitors — let them finish walking the misdirect edge naturally.
                // CompleteMisdirect() will restore their original destination when they reach the end.
                if (visitor.IsMisdirected)
                {
                    continue;
                }

                // Find the closest exit portal to this visitor
                Vector3 visitorPos = visitor.transform.position;
                Vector3 bestExit = portalPositions[0];
                float bestDist = float.MaxValue;

                foreach (var portal in portalPositions)
                {
                    float dist = Vector3.Distance(visitorPos, portal);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestExit = portal;
                    }
                }

                // End any fascination state first (lantern or ring)
                if (state == VisitorControllerBase.VisitorState.Fascinated)
                {
                    visitor.EndLanternFascination();
                    visitor.EndRingFascination();
                }

                // Set the exit as the visitor's destination and recalculate path
                visitor.SetWorldDestination(bestExit);
                visitor.Resume(); // Ensure visitor is not stuck in Idle state
                visitor.RecalculatePath();
                assignedCount++;
            }
        }

        /// <summary>
        /// Pauses or unpauses the game for tutorial.
        /// </summary>
        public void PauseGame(bool pause)
        {
            if (pause && !isPaused)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                isPaused = true;
            }
            else if (!pause && isPaused)
            {
                Time.timeScale = previousTimeScale;
                isPaused = false;
            }
        }

        /// <summary>
        /// Called when a key is pressed. Checks if it matches current step trigger.
        /// </summary>
        public void NotifyKeyPressed(string keyName)
        {
            if (!isActive || CurrentStep == null) return;

            var step = CurrentStep;
            if (step.triggerType == TutorialTriggerType.KeyPress &&
                string.Equals(step.triggerParameter, keyName, StringComparison.OrdinalIgnoreCase))
            {
                AdvanceStep();
            }
        }

        /// <summary>
        /// Called when a Heart Power is activated.
        /// Also checks if a power is already active when entering a PowerActivated trigger step.
        /// </summary>
        public void NotifyPowerActivated(int powerIndex)
        {
            if (!isActive || CurrentStep == null) return;

            var step = CurrentStep;

            if (step.triggerType == TutorialTriggerType.PowerActivated &&
                step.triggerParameter == powerIndex.ToString())
            {
                // Lock all powers immediately — the activated power has served its purpose
                var panelController = FindFirstObjectByType<HeartPowerPanelController>();
                if (panelController != null)
                {
                    panelController.DisableAllPowersForTutorial();
                }

                AdvanceStep();
            }
        }

        /// <summary>
        /// Checks if the expected power for the current step is already active.
        /// Called when showing a PowerActivated trigger step to auto-advance if power was activated early.
        /// </summary>
        private bool IsPowerAlreadyActiveForCurrentStep()
        {
            var step = CurrentStep;
            if (step == null || step.triggerType != TutorialTriggerType.PowerActivated)
                return false;

            if (!int.TryParse(step.triggerParameter, out int powerIndex))
                return false;

            var heartPowerManager = FindFirstObjectByType<HeartPowerManager>();
            if (heartPowerManager == null)
                return false;

            // Check if the power is currently active
            HeartPowerType powerType = (HeartPowerType)powerIndex;
            return heartPowerManager.IsPowerActive(powerType);
        }

        /// <summary>
        /// Called when a visitor spawns.
        /// </summary>
        public void NotifyVisitorSpawned()
        {
            if (!isActive || CurrentStep == null) return;

            var step = CurrentStep;
            if (step.triggerType == TutorialTriggerType.VisitorSpawned)
            {
                AdvanceStep();
            }
        }

        /// <summary>
        /// Called when essence changes. Checks for increase.
        /// </summary>
        public void NotifyEssenceChanged(int newEssence)
        {
            if (!isActive || CurrentStep == null) return;

            var step = CurrentStep;
            if (step.triggerType == TutorialTriggerType.EssenceIncreased &&
                newEssence > previousEssence)
            {
                AdvanceStep();
            }

            previousEssence = newEssence;
        }

        /// <summary>
        /// Called when camera moves significantly.
        /// </summary>
        public void NotifyCameraMoved(Vector3 newPosition)
        {
            if (!isActive || CurrentStep == null) return;

            var step = CurrentStep;
            if (step.triggerType == TutorialTriggerType.CameraMove)
            {
                float distance = Vector3.Distance(previousCameraPosition, newPosition);
                if (distance > cameraMovementThreshold)
                {
                    AdvanceStep();
                }
            }
        }

        /// <summary>
        /// Resets the tutorial so it can be played again.
        /// </summary>
        public static void ResetTutorial()
        {
            GameSettings.TutorialCompleted = false;
            GameSettings.Save();
        }

        #endregion
    }
}
