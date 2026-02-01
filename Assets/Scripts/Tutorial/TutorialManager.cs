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

namespace FaeMaze.Tutorial
{
    /// <summary>
    /// Central controller for the tutorial system.
    /// Manages tutorial state, step progression, and coordination between UI and game systems.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        #region Singleton

        private static TutorialManager _instance;
        public static TutorialManager Instance => _instance;

        #endregion

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

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

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

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            // Ensure time is restored if destroyed during tutorial
            if (isPaused)
            {
                Time.timeScale = previousTimeScale;
            }
        }

        #endregion

        #region Tutorial Step Definitions

        /// <summary>
        /// Initializes all tutorial steps with their content and triggers.
        /// </summary>
        private void InitializeTutorialSteps()
        {
            steps = new List<TutorialStep>
            {
                // Phase 1: Introduction
                new TutorialStep(
                    id: "welcome",
                    title: "Welcome to FaeMaze",
                    description: "You are the spirit of a mystical forest maze. Your goal is to capture visitors and gather their essence before they escape through the exit portals.\n\nUse your powers wisely to survive!",
                    trigger: TutorialTriggerType.ButtonClick,
                    pause: true
                ),

                new TutorialStep(
                    id: "essence_bar",
                    title: "Essence - Your Life Force",
                    description: "The red bar at the top shows your Essence. This is your health and resource combined.\n\nEssence slowly drains over time. If it reaches zero, the game ends. Capture visitors to replenish it!\n\nFor this tutorial, you start with 200 Essence.",
                    trigger: TutorialTriggerType.ButtonClick,
                    highlight: TutorialHighlightType.UIElement,
                    highlightTarget: "EssenceBarPanel",
                    pause: true
                ),

                new TutorialStep(
                    id: "heart_intro",
                    title: "The Heart of the Maze",
                    description: "At the center of the maze is the Heart - your core. When visitors come close, a tongue emerges to capture them.\n\nYou can press [F5] anytime to return the camera focus here.",
                    trigger: TutorialTriggerType.ButtonClick,
                    highlight: TutorialHighlightType.WorldPosition,
                    pause: false
                ),

                new TutorialStep(
                    id: "camera_controls",
                    title: "Camera & Focal Point",
                    description: "The center of your screen is the Focal Point - your powers target wherever this point is located in the maze.\n\n" +
                                 "Control the camera to position it:\n\n" +
                                 "  WASD / Arrow Keys - Move camera\n" +
                                 "  Scroll Wheel - Zoom in/out\n" +
                                 "  Right-click drag - Orbit around focal point\n" +
                                 "  Middle-click drag - Pan camera\n\n" +
                                 "Try moving the camera now.",
                    trigger: TutorialTriggerType.CameraMove,
                    pause: false
                ),

                // Phase 2: Powers
                new TutorialStep(
                    id: "power_murmuring",
                    title: "Power 1: Murmuring Paths",
                    description: "First, move the camera to follow an edge away from the Heart to another node. This positions your focal point along a path.\n\nThen press [1] to activate Murmuring Paths.\n\nThis creates a fog along the path from your focal point back to the Heart. Visitors caught in the fog become confused and walk toward the Heart instead of the exit.\n\nCost: 100 Essence (50% of starting)",
                    trigger: TutorialTriggerType.PowerActivated,
                    triggerParam: "0", // MurmuringPaths index
                    highlight: TutorialHighlightType.UIElementCircular,
                    highlightTarget: "PowerButton_0",
                    pause: false,
                    spawn: false // Visitor spawns after power is activated
                ),

                new TutorialStep(
                    id: "power_murmuring_effect",
                    title: "Murmuring Paths Active",
                    description: "Watch the fog spread along the path from your focal point to the Heart.\n\nVisitors who walk through the fog become confused and start moving toward the Heart instead of the exit.\n\nThe fog fades after a short time.",
                    trigger: TutorialTriggerType.ButtonClick,
                    pause: false,
                    spawn: false // Visitor spawned by HandlePowerMurmuringEffectStep coroutine
                ),

                new TutorialStep(
                    id: "power_grasp",
                    title: "Power 2: Heartward Grasp",
                    description: "Press [2] to activate Heartward Grasp.\n\nA tongue emerges from the forest wall near your focal point. It grabs nearby visitors and pulls them deeper into the maze toward the Heart.\n\nCost: 10 Essence (5% of starting)",
                    trigger: TutorialTriggerType.PowerActivated,
                    triggerParam: "1", // HeartwardGrasp index
                    highlight: TutorialHighlightType.UIElementCircular,
                    highlightTarget: "PowerButton_1",
                    pause: false,
                    spawn: false // Visitor spawns after power is activated
                ),

                new TutorialStep(
                    id: "power_grasp_effect",
                    title: "Heartward Grasp Active",
                    description: "Watch the tongue emerge from the forest wall!\n\nIt will grab any visitor that comes too close and pull them deeper into the maze. This costs a small amount of essence from the grabbed visitor.\n\nThe tongue retracts after catching a visitor or after a short time.",
                    trigger: TutorialTriggerType.ButtonClick,
                    pause: false, // Let action play - camera will track visitor being grabbed
                    spawn: false // Visitor spawned by HandlePowerGraspEffectStep coroutine
                ),

                new TutorialStep(
                    id: "power_maw",
                    title: "Power 3: Devouring Maw",
                    description: "Press [3] to activate Devouring Maw.\n\nA great mouth emerges at your focal point. Any visitor who touches it is consumed instantly, granting you half their essence.\n\nCost: 50 Essence (25% of starting)",
                    trigger: TutorialTriggerType.PowerActivated,
                    triggerParam: "2", // DevouringMaw index
                    highlight: TutorialHighlightType.UIElementCircular,
                    highlightTarget: "PowerButton_2",
                    pause: false,
                    spawn: false // Visitor spawns after power is activated
                ),

                new TutorialStep(
                    id: "power_maw_effect",
                    title: "Devouring Maw Active",
                    description: "A monstrous maw has emerged from the ground!\n\nAny visitor who walks into the maw is consumed instantly, granting you half their essence value.\n\nWatch the visitor get devoured...",
                    trigger: TutorialTriggerType.Timer, // No Continue button - auto-advances when visitor is consumed
                    pause: false,
                    spawn: false // Visitor spawned by HandlePowerMawEffectStep coroutine
                ),

                new TutorialStep(
                    id: "power_sculpt",
                    title: "Power 4: Sculpting",
                    description: "Press [4] to open the Sculpting menu.\n\nThis lets you place or change props at maze nodes:\n" +
                                 "  Pond - Drowns visitors (with Puka hazard)\n" +
                                 "  Lantern - Fascinates visitors, drains essence\n" +
                                 "  Fairy Ring - Traps visitors, drains essence\n\n" +
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
                    description: "Select the Lantern from the radial menu to place it on this node.\n\nLanterns fascinate visitors, drawing them in and draining their essence over time.",
                    trigger: TutorialTriggerType.Timer, // Auto-advances when lantern is placed
                    pause: false,
                    rightSideDialog: true // Keep dialog on right during sculpt
                ),

                // Phase 3: Gameplay - Lantern Fascination Demo
                new TutorialStep(
                    id: "lantern_demo",
                    title: "Lantern Fascination",
                    description: "Watch as a visitor becomes fascinated by the lantern!\n\nFascinated visitors walk toward the lantern and stand mesmerized while their essence slowly drains.\n\nWait for the visitor to be fascinated...",
                    trigger: TutorialTriggerType.Timer, // Auto-advances when visitor is fascinated
                    pause: false
                ),

                new TutorialStep(
                    id: "essence_gain",
                    title: "Capturing Essence",
                    description: "When visitors are consumed by the Heart, the Maw, or drained by props, you gain their essence.\n\nKeep your essence high to survive! The longer you last, the more challenging visitors become.\n\nWait for a visitor to be captured...",
                    trigger: TutorialTriggerType.EssenceIncreased,
                    pause: false // No highlight - game must continue for essence to increase
                ),

                new TutorialStep(
                    id: "complete",
                    title: "Ready to Play",
                    description: "You now know the basics of FaeMaze!\n\n" +
                                 "Tips:\n" +
                                 "  - Press [F1] anytime for a quick reference\n" +
                                 "  - Watch your essence - it drains constantly\n" +
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
            if (autoStartOnFirstRun && !GameSettings.TutorialCompleted)
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
            Debug.Log($"[TutorialManager] AdvanceStep called. isActive={isActive}, isTransitioning={isTransitioning}, currentStepIndex={currentStepIndex}");

            if (!isActive) return;

            // Don't advance if we're in the middle of a transition
            if (isTransitioning)
            {
                Debug.Log("[TutorialManager] AdvanceStep blocked - transition in progress");
                return;
            }

            // Check if we just completed a step that requires camera transition to non-heart node
            var previousStep = CurrentStep;
            Debug.Log($"[TutorialManager] Previous step: {previousStep?.stepId ?? "null"}");

            // Steps that need camera transition away from heart before advancing
            bool needsCameraTransition = previousStep != null && (
                previousStep.stepId == "camera_controls" ||
                previousStep.stepId == "power_murmuring_effect" ||
                previousStep.stepId == "power_grasp_effect" ||
                previousStep.stepId == "power_maw_effect"
            );

            if (needsCameraTransition && IsFocalPointOnHeartNode())
            {
                Debug.Log($"[TutorialManager] Starting cinematic camera transition from {previousStep.stepId} step (focal point on heart)");
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
            Debug.Log($"[TutorialManager] AdvanceStepInternal: new currentStepIndex={currentStepIndex}, totalSteps={steps?.Count ?? 0}");

            if (currentStepIndex >= steps.Count)
            {
                Debug.Log("[TutorialManager] Tutorial complete - all steps finished");
                CompleteTutorial();
                return;
            }

            var step = CurrentStep;
            if (step == null)
            {
                Debug.LogError("[TutorialManager] CurrentStep is null after advancing!");
                return;
            }

            Debug.Log($"[TutorialManager] Now at step: id={step.stepId}, title={step.title}, trigger={step.triggerType}, highlight={step.highlightType}");

            // Special handling for power effect steps - spawn visitor and track with focal point
            if (step.stepId == "power_murmuring_effect")
            {
                Debug.Log($"[TutorialManager] Starting HandlePowerMurmuringEffectStep coroutine");
                StartCoroutine(HandlePowerMurmuringEffectStep(step));
                return;
            }

            if (step.stepId == "power_grasp_effect")
            {
                Debug.Log($"[TutorialManager] Starting HandlePowerGraspEffectStep coroutine");
                StartCoroutine(HandlePowerGraspEffectStep(step));
                return;
            }

            if (step.stepId == "power_maw_effect")
            {
                Debug.Log($"[TutorialManager] Starting HandlePowerMawEffectStep coroutine");
                StartCoroutine(HandlePowerMawEffectStep(step));
                return;
            }

            if (step.stepId == "power_sculpt")
            {
                Debug.Log($"[TutorialManager] Starting HandlePowerSculptStep coroutine (waits for devour despawn)");
                StartCoroutine(HandlePowerSculptStep(step));
                return;
            }

            if (step.stepId == "power_sculpt_effect")
            {
                Debug.Log($"[TutorialManager] Starting HandlePowerSculptEffectStep coroutine");
                StartCoroutine(HandlePowerSculptEffectStep(step));
                return;
            }

            if (step.stepId == "lantern_demo")
            {
                Debug.Log($"[TutorialManager] Starting HandleLanternDemoStep coroutine");
                StartCoroutine(HandleLanternDemoStep(step));
                return;
            }

            // Check if this step highlights a power button - if so, wait for peak brightness
            int powerButtonIndex = GetPowerButtonIndex(step);
            if (powerButtonIndex >= 0)
            {
                Debug.Log($"[TutorialManager] Step highlights PowerButton_{powerButtonIndex}, starting brightness sync");
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
                Debug.LogWarning("[TutorialManager] HeartPowerPanelController not found, showing step immediately");
                ShowStepImmediate(step);
                yield break;
            }

            // Wait for the button to reach peak brightness (>= 0.95 threshold)
            // The glow animation uses Time.unscaledTime, so it continues during pause
            const float BRIGHTNESS_THRESHOLD = 0.95f;
            const float MAX_WAIT_TIME = 2.0f; // Don't wait more than one full cycle

            float startTime = Time.unscaledTime;
            while (Time.unscaledTime - startTime < MAX_WAIT_TIME)
            {
                float brightness = panelController.GetButtonPulseBrightness(buttonIndex);
                if (brightness >= BRIGHTNESS_THRESHOLD)
                {
                    Debug.Log($"[TutorialManager] Button {buttonIndex} reached peak brightness ({brightness:F2}) after {Time.unscaledTime - startTime:F2}s");
                    break;
                }

                yield return null;
            }

            if (Time.unscaledTime - startTime >= MAX_WAIT_TIME)
            {
                Debug.Log($"[TutorialManager] Max wait time reached, showing step anyway");
            }

            // Lock the button at peak brightness so it stays bright while step is shown
            panelController.LockButtonAtPeakBrightness(buttonIndex);

            // Now show the step
            ShowStepImmediate(step);
        }

        /// <summary>
        /// Shows a tutorial step immediately without waiting for brightness sync.
        /// </summary>
        private void ShowStepImmediate(TutorialStep step)
        {
            // Check if power is already active for PowerActivated trigger steps
            // This prevents tutorial lock if player activated power before being prompted
            if (step.triggerType == TutorialTriggerType.PowerActivated && IsPowerAlreadyActiveForCurrentStep())
            {
                Debug.Log($"[TutorialManager] Power already active for step {step.stepId}, auto-advancing");
                AdvanceStep();
                return;
            }

            // Handle pause state - pause when dim overlay is visible (highlightType != None)
            // When highlightType is None, the player should see the game clearly without pause
            bool shouldPause = step.pauseGame || step.highlightType != TutorialHighlightType.None;
            Debug.Log($"[TutorialManager] Pause logic: pauseGame={step.pauseGame}, highlightType={step.highlightType}, shouldPause={shouldPause}, isPaused={isPaused}");

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
                Debug.Log($"[TutorialManager] Spawning visitor for step {step.stepId}");
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

            Debug.Log($"[TutorialManager] Invoking OnStepChanged event for step index {currentStepIndex}");
            OnStepChanged?.Invoke(currentStepIndex);
        }

        /// <summary>
        /// Performs a cinematic camera transition: dims scene, pauses, moves camera smoothly, then advances.
        /// </summary>
        private IEnumerator CinematicCameraTransition()
        {
            Debug.Log("[TutorialManager] CinematicCameraTransition started");
            isTransitioning = true;

            // Step 1: Dim the scene and pause (this will be done via uiController notifying of a special state)
            // Pause the game so the world freezes during the transition
            PauseGame(true);
            Debug.Log("[TutorialManager] Game paused for transition");

            // Notify UI to show dim overlay during transition
            uiController?.ShowTransitionOverlay(true);
            Debug.Log("[TutorialManager] Transition overlay shown");

            // Small delay to let the dim effect be visible
            yield return new WaitForSecondsRealtime(0.3f);
            Debug.Log("[TutorialManager] After 0.3s delay");

            // Step 2: Start smooth camera movement to nearest non-heart node
            Vector3 targetPos = GetNearestNonHeartNodePosition();
            Debug.Log($"[TutorialManager] Target position for camera: {targetPos}");

            var cameraController = FindFirstObjectByType<CameraController3D>();

            if (cameraController != null && targetPos != Vector3.zero)
            {
                Debug.Log("[TutorialManager] Starting smooth camera movement");
                // Use smooth movement (instant: false) with a comfortable speed
                cameraController.FocusOnPosition(targetPos, instant: false, lerpSpeed: 8f);

                // Step 3: Wait for camera to finish moving
                // Check IsFocalPointMoving property until it's false
                int waitFrames = 0;
                while (cameraController.IsFocalPointMoving)
                {
                    waitFrames++;
                    if (waitFrames % 60 == 0)
                    {
                        Debug.Log($"[TutorialManager] Waiting for camera... frames={waitFrames}, pos={cameraController.FocalPointPosition}");
                    }
                    yield return null; // Wait one frame (uses unscaled time since game is paused)
                }
                Debug.Log($"[TutorialManager] Camera movement complete after {waitFrames} frames");
            }
            else
            {
                Debug.LogWarning($"[TutorialManager] Camera movement skipped: cameraController={cameraController != null}, targetPos={targetPos}");
            }

            // Small delay after arriving to let the new view settle
            yield return new WaitForSecondsRealtime(0.2f);
            Debug.Log("[TutorialManager] After settle delay");

            // Step 4: Complete transition and advance to next step
            isTransitioning = false;
            Debug.Log("[TutorialManager] Hiding transition overlay");
            uiController?.ShowTransitionOverlay(false);

            // Now advance to the next step
            Debug.Log("[TutorialManager] Calling AdvanceStepInternal from transition");
            AdvanceStepInternal();
            Debug.Log("[TutorialManager] CinematicCameraTransition complete");
        }

        /// <summary>
        /// Handles the power_grasp_effect step with camera tracking.
        /// Positions the camera on the node side of the visitor-to-HGZ ray,
        /// spawns a visitor, and tracks them as they walk into the HGZ.
        /// </summary>
        private IEnumerator HandlePowerGraspEffectStep(TutorialStep step)
        {
            Debug.Log("[TutorialManager] HandlePowerGraspEffectStep started");

            // Get the active HeartwardGraspEffect
            var graspEffect = HeartwardGraspEffect.ActiveInstance;
            if (graspEffect == null)
            {
                Debug.LogWarning("[TutorialManager] No active HeartwardGraspEffect for power_grasp_effect step, falling back to normal display");
                ShowStepImmediate(step);
                yield break;
            }

            // Get the grabbing zone position
            Vector3 hgzPos = graspEffect.GrabbingZonePosition;
            Debug.Log($"[TutorialManager] HGZ position: {hgzPos}");

            // Get heart position for calculating spawn direction
            var mazeGrid = FindFirstObjectByType<MazeGridBehaviour>();
            Vector3 heartPos = mazeGrid != null ? mazeGrid.HeartWorldPosition : Vector3.zero;
            Debug.Log($"[TutorialManager] Heart position: {heartPos}");

            // Calculate spawn position: beyond the HGZ, away from heart, so visitor walks toward heart
            // The visitor will walk FROM spawnPos THROUGH the HGZ detection zone
            Vector3 dirToHeart = (heartPos - hgzPos).normalized;
            Vector3 spawnPos = hgzPos - dirToHeart * 5f; // 5 units beyond HGZ away from heart
            spawnPos.z = 0f; // Ensure on ground plane

            // Visitor destination is just past the HGZ toward the heart
            // This ensures the visitor's path goes directly through the HGZ detection zone
            Vector3 visitorDestination = hgzPos + dirToHeart * 3f; // 3 units past HGZ toward heart
            visitorDestination.z = 0f;

            // Camera focal point is at the midpoint of the visitor-to-HGZ ray
            // This keeps the camera looking at the path the visitor will walk
            Vector3 cameraFocalPos = (spawnPos + hgzPos) / 2f;
            cameraFocalPos.z = 0f;

            Debug.Log($"[TutorialManager] Spawn pos: {spawnPos}, HGZ pos: {hgzPos}, Visitor dest: {visitorDestination}");
            Debug.Log($"[TutorialManager] Camera focal pos (ray midpoint): {cameraFocalPos}");

            // Move focal point to the calculated position
            var cameraController = FindFirstObjectByType<CameraController3D>();
            if (cameraController != null)
            {
                Debug.Log("[TutorialManager] Moving focal point to node-side position");
                cameraController.FocusOnPosition(cameraFocalPos, instant: false, lerpSpeed: 8f);

                // Wait for camera to finish moving
                while (cameraController.IsFocalPointMoving)
                {
                    yield return null;
                }
                Debug.Log("[TutorialManager] Camera arrived at node-side position");
            }

            Debug.Log($"[TutorialManager] Spawning visitor at {spawnPos} heading toward {visitorDestination}");

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
            Debug.Log("[TutorialManager] Showing power_grasp_effect step UI");

            // Handle pause state based on step settings
            bool shouldPause = step.pauseGame || step.highlightType != TutorialHighlightType.None;
            if (shouldPause && !isPaused)
            {
                PauseGame(true);
            }
            else if (!shouldPause && isPaused)
            {
                PauseGame(false);
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

            // Fire the step changed event
            OnStepChanged?.Invoke(currentStepIndex);

            // Track visitor by directly updating focal point position until grabbed
            if (targetVisitor != null && cameraController != null)
            {
                Debug.Log($"[TutorialManager] Starting direct focal point tracking of visitor {targetVisitor.name}");

                // Phase 1: Track until visitor is grabbed
                while (targetVisitor != null && targetVisitor.State != VisitorControllerBase.VisitorState.Grabbed)
                {
                    Vector3 visitorPos = targetVisitor.transform.position;
                    visitorPos.z = 0f;
                    cameraController.FocusOnPosition(visitorPos, instant: true);
                    yield return null;
                }

                Debug.Log("[TutorialManager] Visitor grabbed, transitioning camera to pushing HGZ");

                // Phase 2: Move camera to pushing HGZ and rotate to look along heart->pushing axis
                if (targetVisitor != null && graspEffect != null)
                {
                    Vector3 pushingPos = graspEffect.PushingZonePosition;
                    pushingPos.z = 0f;

                    // Calculate direction from heart to pushing HGZ for camera rotation
                    Vector2 heartPos2D = new Vector2(heartPos.x, heartPos.y);
                    Vector2 pushingPos2D = new Vector2(pushingPos.x, pushingPos.y);
                    Vector2 dirHeartToPushing = (pushingPos2D - heartPos2D).normalized;

                    Debug.Log($"[TutorialManager] Moving focal point to pushing HGZ at {pushingPos}");
                    cameraController.FocusOnPosition(pushingPos, instant: false, lerpSpeed: 12f);
                    cameraController.SetFocalPointDirection(dirHeartToPushing);

                    // Wait for camera to arrive
                    while (cameraController.IsFocalPointMoving)
                    {
                        yield return null;
                    }

                    Debug.Log("[TutorialManager] Camera at pushing HGZ, waiting for visitor to emerge");

                    // Phase 3: Wait for visitor to become visible (back on ground)
                    // The visitor is hidden underground during transport, then becomes visible when tongue emerges
                    while (targetVisitor != null && !IsVisitorVisible(targetVisitor))
                    {
                        yield return null;
                    }

                    Debug.Log("[TutorialManager] Visitor visible, resuming tracking until consumed");

                    // Phase 4: Track visitor until consumed
                    while (targetVisitor != null && targetVisitor.State != VisitorControllerBase.VisitorState.Consumed)
                    {
                        Vector3 visitorPos = targetVisitor.transform.position;
                        visitorPos.z = 0f;
                        cameraController.FocusOnPosition(visitorPos, instant: true);
                        yield return null;
                    }

                    Debug.Log("[TutorialManager] Visitor consumed, tracking complete");
                }
            }
            else
            {
                Debug.LogWarning("[TutorialManager] Could not find spawned visitor to track");
            }

            Debug.Log("[TutorialManager] HandlePowerGraspEffectStep complete");
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
            Debug.Log("[TutorialManager] HandlePowerMurmuringEffectStep started");

            var cameraController = FindFirstObjectByType<CameraController3D>();

            // Spawn visitor using normal tutorial spawner
            if (visitorSpawner != null)
            {
                visitorSpawner.SpawnTutorialVisitor();
            }

            // Wait for spawn to complete
            yield return new WaitForSecondsRealtime(0.5f);

            // Find a spawned visitor
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

            // Track visitor until they are consumed or grabbed (terminal states)
            // We want to watch them walk through the fog and toward the heart
            if (targetVisitor != null && cameraController != null)
            {
                Debug.Log($"[TutorialManager] Starting focal point tracking of visitor {targetVisitor.name} for Murmuring Paths, state={targetVisitor.State}");

                while (targetVisitor != null &&
                       targetVisitor.State != VisitorControllerBase.VisitorState.Consumed &&
                       targetVisitor.State != VisitorControllerBase.VisitorState.Grabbed)
                {
                    Vector3 visitorPos = targetVisitor.transform.position;
                    visitorPos.z = 0f;
                    cameraController.FocusOnPosition(visitorPos, instant: true);
                    yield return null;
                }

                Debug.Log("[TutorialManager] Visitor tracking ended for Murmuring Paths");
            }
            else
            {
                Debug.LogWarning("[TutorialManager] Could not find spawned visitor to track for Murmuring Paths");
            }

            Debug.Log("[TutorialManager] HandlePowerMurmuringEffectStep complete");
        }

        /// <summary>
        /// Handles the power_maw_effect step with visitor tracking.
        /// Spawns a visitor and tracks them as they walk into the Devouring Maw.
        /// </summary>
        private IEnumerator HandlePowerMawEffectStep(TutorialStep step)
        {
            Debug.Log("[TutorialManager] HandlePowerMawEffectStep started");

            var cameraController = FindFirstObjectByType<CameraController3D>();

            // Spawn visitor using normal tutorial spawner
            if (visitorSpawner != null)
            {
                visitorSpawner.SpawnTutorialVisitor();
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

            // Track visitor until they are Consumed by the maw or destroyed
            if (targetVisitor != null && cameraController != null)
            {
                Debug.Log($"[TutorialManager] Starting focal point tracking of visitor {targetVisitor.name} for Devouring Maw, state={targetVisitor.State}");

                while (targetVisitor != null &&
                       targetVisitor.State != VisitorControllerBase.VisitorState.Consumed &&
                       targetVisitor.State != VisitorControllerBase.VisitorState.Grabbed)
                {
                    Vector3 visitorPos = targetVisitor.transform.position;
                    visitorPos.z = 0f;
                    cameraController.FocusOnPosition(visitorPos, instant: true);
                    yield return null;
                }

                Debug.Log("[TutorialManager] Visitor tracking ended for Devouring Maw");

                // Wait a moment for the maw animation to complete
                yield return new WaitForSecondsRealtime(1.5f);
            }
            else
            {
                Debug.LogWarning("[TutorialManager] Could not find spawned visitor to track for Devouring Maw");
                // Wait a bit even if no visitor, so player sees the maw
                yield return new WaitForSecondsRealtime(2.0f);
            }

            Debug.Log("[TutorialManager] HandlePowerMawEffectStep complete, auto-advancing");
            AdvanceStep();
        }

        /// <summary>
        /// Handles the power_sculpt step.
        /// Waits for devour effect to despawn before showing the step.
        /// </summary>
        private IEnumerator HandlePowerSculptStep(TutorialStep step)
        {
            Debug.Log("[TutorialManager] HandlePowerSculptStep started - waiting for devour to despawn");

            // Wait for DevouringMaw power to finish (no longer active)
            var heartPowerManager = FindFirstObjectByType<HeartPowerManager>();
            if (heartPowerManager != null)
            {
                while (heartPowerManager.IsPowerActive(HeartPowerType.DevouringMaw))
                {
                    yield return null;
                }
                Debug.Log("[TutorialManager] Devour effect has despawned");
            }

            // Small delay after devour despawns for visual clarity
            yield return new WaitForSecondsRealtime(0.3f);

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

            Debug.Log("[TutorialManager] HandlePowerSculptStep complete");
        }

        /// <summary>
        /// Coroutine version of WaitForPeakBrightnessThenShowStep for use in nested coroutines.
        /// </summary>
        private IEnumerator WaitForPeakBrightnessThenShowStepCoroutine(TutorialStep step, int buttonIndex)
        {
            var panelController = FindFirstObjectByType<HeartPowerPanelController>();
            if (panelController == null)
            {
                Debug.LogWarning("[TutorialManager] HeartPowerPanelController not found, showing step immediately");
                ShowStepImmediate(step);
                yield break;
            }

            const float BRIGHTNESS_THRESHOLD = 0.95f;
            const float MAX_WAIT_TIME = 2.0f;

            float startTime = Time.unscaledTime;
            while (Time.unscaledTime - startTime < MAX_WAIT_TIME)
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
        }

        /// <summary>
        /// Handles the power_sculpt_effect step.
        /// Waits for player to select a lantern from the sculpt menu, then auto-advances.
        /// </summary>
        private IEnumerator HandlePowerSculptEffectStep(TutorialStep step)
        {
            Debug.Log("[TutorialManager] HandlePowerSculptEffectStep started");

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
                Debug.Log($"[TutorialManager] Target node for lantern: {targetNodeIndex} at focal pos {focalPos}");
            }

            // Wait for the sculpt menu to open, then highlight only the lantern button
            while (SculptingEffect.ActiveInstance == null || !SculptingEffect.ActiveInstance.IsMenuActive)
            {
                yield return null;
            }

            // Highlight only the lantern button and disable others
            Debug.Log("[TutorialManager] Sculpt menu opened, highlighting lantern button only");
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
                Debug.Log($"[TutorialManager] After sculpt menu closed: lanternPlaced={lanternPlaced}, propType={propType}");
            }

            // If lantern wasn't placed, place one automatically
            if (!lanternPlaced && dynamicMaze != null && targetNodeIndex > 0)
            {
                Debug.Log("[TutorialManager] Lantern not placed by player, placing automatically");
                dynamicMaze.SetNodeProp(targetNodeIndex, DynamicMazeGrowth.NodePropType.FaeLantern);
            }

            Debug.Log("[TutorialManager] HandlePowerSculptEffectStep complete, auto-advancing");
            AdvanceStep();
        }

        /// <summary>
        /// Handles the lantern_demo step.
        /// Spawns a visitor near the lantern and waits for them to become fascinated.
        /// </summary>
        private IEnumerator HandleLanternDemoStep(TutorialStep step)
        {
            Debug.Log("[TutorialManager] HandleLanternDemoStep started");

            // Find the lantern we just placed
            var lanterns = FindObjectsByType<FaeLantern>(FindObjectsSortMode.None);
            FaeLantern targetLantern = null;

            if (lanterns.Length > 0)
            {
                targetLantern = lanterns[0]; // Use the first lantern found
                Debug.Log($"[TutorialManager] Found lantern at {targetLantern.transform.position}");
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
                // Calculate spawn position: on the edge of the node, pointing toward lantern
                Vector3 lanternPos = targetLantern.transform.position;
                Vector3 spawnOffset = new Vector3(3f, 0f, 0f); // NODE_RADIUS away
                Vector3 spawnPos = lanternPos + spawnOffset;
                spawnPos.z = 0f;

                // Destination is the lantern position (visitor will be drawn to it)
                Vector3 destPos = lanternPos;
                destPos.z = 0f;

                Debug.Log($"[TutorialManager] Spawning visitor at {spawnPos} to walk toward lantern");
                visitorSpawner.SpawnVisitorForHGZ(spawnPos, destPos); // Reuse HGZ spawner method
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
                Debug.Log($"[TutorialManager] Tracking visitor {targetVisitor.name} until fascinated");

                // Track visitor position with camera
                while (targetVisitor != null)
                {
                    // Check if visitor is fascinated by a lantern
                    if (targetVisitor.CurrentFaeLantern != null)
                    {
                        Debug.Log("[TutorialManager] Visitor became fascinated by lantern!");
                        break;
                    }

                    // Check for consumed state (game over for this visitor)
                    if (targetVisitor.State == VisitorControllerBase.VisitorState.Consumed ||
                        targetVisitor.State == VisitorControllerBase.VisitorState.Grabbed)
                    {
                        Debug.Log("[TutorialManager] Visitor was consumed/grabbed before fascination");
                        break;
                    }

                    // Track visitor position
                    Vector3 visitorPos = targetVisitor.transform.position;
                    visitorPos.z = 0f;
                    cameraController.FocusOnPosition(visitorPos, instant: true);

                    yield return null;
                }
            }
            else
            {
                Debug.LogWarning("[TutorialManager] Could not find visitor to track for lantern demo");
                yield return new WaitForSecondsRealtime(2f);
            }

            // Short pause to let player see the fascination effect
            yield return new WaitForSecondsRealtime(1.5f);

            Debug.Log("[TutorialManager] HandleLanternDemoStep complete, auto-advancing");
            AdvanceStep();
        }

        /// <summary>
        /// Gets the position of a node one edge away from the heart (shortest walkable distance).
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

            // Find a node that is one edge away from the heart (directly connected)
            // This ensures the shortest walkable distance
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
                    Debug.Log($"[TutorialManager] Found node {otherNodeId} one edge from heart at {neighborNode.Position}");
                    return new Vector3(neighborNode.Position.x, neighborNode.Position.y, 0f);
                }
            }

            // Fallback: find nearest node by straight-line distance
            Debug.LogWarning("[TutorialManager] No directly connected node found, falling back to nearest by distance");
            Vector2 heartPos = heartNode.Position;
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
            Debug.Log($"[TutorialManager] IsFocalPointOnHeartNode: dist={distToHeart:F2}, threshold={NODE_RADIUS}, onHeart={onHeart}");
            return onHeart;
        }

        /// <summary>
        /// Skips the tutorial entirely.
        /// </summary>
        public void SkipTutorial()
        {
            if (!isActive) return;

            CompleteTutorial();
        }

        /// <summary>
        /// Completes the tutorial and saves progress.
        /// </summary>
        private void CompleteTutorial()
        {
            isActive = false;
            currentStepIndex = -1;

            // Unlock any power button that was locked at peak brightness
            var panelController = FindFirstObjectByType<HeartPowerPanelController>();
            if (panelController != null)
            {
                panelController.UnlockButtonBrightness();
            }

            // Ensure game is unpaused
            if (isPaused)
            {
                PauseGame(false);
            }

            // Mark as completed
            GameSettings.TutorialCompleted = true;
            GameSettings.Save();

            OnTutorialCompleted?.Invoke();
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
