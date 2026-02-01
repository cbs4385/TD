using UnityEngine;

namespace FaeMaze.Tutorial
{
    /// <summary>
    /// Defines how a tutorial step is completed/advanced.
    /// </summary>
    public enum TutorialTriggerType
    {
        /// <summary>User clicks Continue button</summary>
        ButtonClick,
        /// <summary>User presses a specific key</summary>
        KeyPress,
        /// <summary>A Heart Power is activated</summary>
        PowerActivated,
        /// <summary>A visitor spawns</summary>
        VisitorSpawned,
        /// <summary>Essence value increases</summary>
        EssenceIncreased,
        /// <summary>Player moves the camera</summary>
        CameraMove,
        /// <summary>Auto-advance after delay</summary>
        Timer
    }

    /// <summary>
    /// Defines what element to highlight during a tutorial step.
    /// </summary>
    public enum TutorialHighlightType
    {
        /// <summary>No highlight</summary>
        None,
        /// <summary>Highlight a UI element by name (rectangular)</summary>
        UIElement,
        /// <summary>Highlight a circular UI element by name (like power buttons)</summary>
        UIElementCircular,
        /// <summary>Highlight a world-space position with arrow</summary>
        WorldPosition,
        /// <summary>Highlight the focal point indicator</summary>
        FocalPoint
    }

    /// <summary>
    /// Data class defining a single tutorial step.
    /// </summary>
    [System.Serializable]
    public class TutorialStep
    {
        [Header("Identification")]
        [Tooltip("Unique identifier for this step")]
        public string stepId;

        [Header("Content")]
        [Tooltip("Short title displayed at top of dialog")]
        public string title;

        [Tooltip("Main instruction text")]
        [TextArea(3, 6)]
        public string description;

        [Header("Visual Highlight")]
        [Tooltip("What type of element to highlight")]
        public TutorialHighlightType highlightType = TutorialHighlightType.None;

        [Tooltip("Name of UI element to highlight (for UIElement type)")]
        public string highlightTargetName;

        [Tooltip("World position to highlight (for WorldPosition type)")]
        public Vector3 worldHighlightPosition;

        [Header("Completion")]
        [Tooltip("How this step is completed")]
        public TutorialTriggerType triggerType = TutorialTriggerType.ButtonClick;

        [Tooltip("Parameter for trigger (key name, power index, etc.)")]
        public string triggerParameter;

        [Tooltip("Auto-advance delay in seconds (for Timer trigger)")]
        public float autoAdvanceDelay = 0f;

        [Header("Behavior")]
        [Tooltip("Pause the game during this step")]
        public bool pauseGame = true;

        [Tooltip("Show skip button during this step")]
        public bool allowSkip = true;

        [Tooltip("Spawn a tutorial visitor at this step")]
        public bool spawnVisitor = false;

        [Tooltip("Position dialog on right side of screen (default is left)")]
        public bool dialogOnRight = false;

        /// <summary>
        /// Creates a tutorial step with the given parameters.
        /// </summary>
        public TutorialStep(
            string id,
            string title,
            string description,
            TutorialTriggerType trigger = TutorialTriggerType.ButtonClick,
            string triggerParam = null,
            TutorialHighlightType highlight = TutorialHighlightType.None,
            string highlightTarget = null,
            bool pause = true,
            bool canSkip = true,
            bool spawn = false,
            bool rightSideDialog = false)
        {
            this.stepId = id;
            this.title = title;
            this.description = description;
            this.triggerType = trigger;
            this.triggerParameter = triggerParam;
            this.highlightType = highlight;
            this.highlightTargetName = highlightTarget;
            this.pauseGame = pause;
            this.allowSkip = canSkip;
            this.spawnVisitor = spawn;
            this.dialogOnRight = rightSideDialog;
        }

        /// <summary>
        /// Default constructor for serialization.
        /// </summary>
        public TutorialStep() { }
    }
}
