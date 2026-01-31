using UnityEngine;
using System;
using System.Collections.Generic;
using FaeMaze.Systems;
using FaeMaze.HeartPowers;

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
        private Vector3 previousCameraPosition;
        private float cameraMovementThreshold = 2f;

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
                    description: "The red bar at the top shows your Essence. This is your health and resource combined.\n\nEssence slowly drains over time. If it reaches zero, the game ends. Capture visitors to replenish it!",
                    trigger: TutorialTriggerType.ButtonClick,
                    highlight: TutorialHighlightType.UIElement,
                    highlightTarget: "EssenceBarPanel",
                    pause: true
                ),

                new TutorialStep(
                    id: "heart_intro",
                    title: "The Heart of the Maze",
                    description: "At the center of the maze is the Heart - your core. When visitors come close, a tongue emerges to capture them.\n\nPress [F5] to focus the camera on the Heart.",
                    trigger: TutorialTriggerType.KeyPress,
                    triggerParam: "F5",
                    highlight: TutorialHighlightType.WorldPosition,
                    pause: false
                ),

                new TutorialStep(
                    id: "focal_point",
                    title: "The Focal Point",
                    description: "The glowing spiral at the center of your screen is the Focal Point. Your powers target wherever this point is located in the maze.\n\nMove the camera to position it over different areas.",
                    trigger: TutorialTriggerType.ButtonClick,
                    highlight: TutorialHighlightType.FocalPoint,
                    pause: true
                ),

                new TutorialStep(
                    id: "camera_controls",
                    title: "Camera Movement",
                    description: "Control the camera to survey your maze:\n\n" +
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
                    description: "Press [1] to activate Murmuring Paths.\n\nThis creates a fog along the path from your focal point to the Heart. Visitors caught in the fog become confused and walk toward the Heart instead of the exit.\n\nCost: 100 Essence",
                    trigger: TutorialTriggerType.PowerActivated,
                    triggerParam: "0", // MurmuringPaths index
                    highlight: TutorialHighlightType.UIElement,
                    highlightTarget: "PowerButton_0",
                    pause: false,
                    spawn: true
                ),

                new TutorialStep(
                    id: "power_grasp",
                    title: "Power 2: Heartward Grasp",
                    description: "Press [2] to activate Heartward Grasp.\n\nA tongue emerges from the forest wall near your focal point. It grabs nearby visitors and pulls them deeper into the maze toward the Heart.\n\nCost: 10 Essence",
                    trigger: TutorialTriggerType.PowerActivated,
                    triggerParam: "1", // HeartwardGrasp index
                    highlight: TutorialHighlightType.UIElement,
                    highlightTarget: "PowerButton_1",
                    pause: false,
                    spawn: true
                ),

                new TutorialStep(
                    id: "power_maw",
                    title: "Power 3: Devouring Maw",
                    description: "Press [3] to activate Devouring Maw.\n\nA great mouth emerges at your focal point. Any visitor who touches it is consumed instantly, granting you their full essence.\n\nCost: 50 Essence",
                    trigger: TutorialTriggerType.PowerActivated,
                    triggerParam: "2", // DevouringMaw index
                    highlight: TutorialHighlightType.UIElement,
                    highlightTarget: "PowerButton_2",
                    pause: false,
                    spawn: true
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
                    highlight: TutorialHighlightType.UIElement,
                    highlightTarget: "PowerButton_3",
                    pause: false
                ),

                // Phase 3: Gameplay
                new TutorialStep(
                    id: "visitors",
                    title: "The Visitors",
                    description: "Visitors enter through portals at the maze edges and try to find their way to the exit.\n\nDifferent visitor types behave differently:\n" +
                                 "  Some wander easily and get confused\n" +
                                 "  Some are cautious and avoid traps\n" +
                                 "  Some are drawn to lanterns\n\n" +
                                 "Watch how they move through the maze.",
                    trigger: TutorialTriggerType.VisitorSpawned,
                    pause: false,
                    spawn: true
                ),

                new TutorialStep(
                    id: "essence_gain",
                    title: "Capturing Essence",
                    description: "When visitors are consumed by the Heart, the Maw, or drained by props, you gain their essence.\n\nKeep your essence high to survive! The longer you last, the more challenging visitors become.\n\nWait for a visitor to be captured...",
                    trigger: TutorialTriggerType.EssenceIncreased,
                    highlight: TutorialHighlightType.UIElement,
                    highlightTarget: "EssenceBarPanel",
                    pause: false
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

            // Store initial state
            if (GameController.Instance != null)
            {
                previousEssence = GameController.Instance.CurrentEssence;
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

            currentStepIndex++;

            if (currentStepIndex >= steps.Count)
            {
                CompleteTutorial();
                return;
            }

            var step = CurrentStep;
            if (step == null) return;

            // Handle pause state
            if (step.pauseGame && !isPaused)
            {
                PauseGame(true);
            }
            else if (!step.pauseGame && isPaused)
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
