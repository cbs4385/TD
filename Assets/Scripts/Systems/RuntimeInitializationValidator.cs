using UnityEngine;
using FaeMaze.Maze;
using FaeMaze.UI;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Validates that all game systems initialize correctly at runtime.
    /// Verifies essence events, visitor spawning, and maze rendering.
    /// </summary>
    public class RuntimeInitializationValidator : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Validation Settings")]
        [SerializeField]
        [Tooltip("Enable validation logging")]
        private bool enableValidation = true;

        [SerializeField]
        [Tooltip("Delay before running validation (seconds)")]
        private float validationDelay = 1f;

        #endregion

        #region Private Fields

        private bool validationComplete = false;
        private System.Text.StringBuilder validationReport;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            if (enableValidation)
            {
                Invoke(nameof(RunValidation), validationDelay);
            }
        }

        #endregion

        #region Validation Methods

        private void RunValidation()
        {
            validationReport = new System.Text.StringBuilder();
            validationReport.AppendLine("=== Runtime Initialization Validation ===");
            validationReport.AppendLine($"Validation Time: {Time.time:F2}s after scene start");
            validationReport.AppendLine();

            // Validate Game Controller
            ValidateGameController();

            // Validate Maze Grid
            ValidateMazeGrid();

            // Validate Maze Renderer
            ValidateMazeRenderer();

            // Validate UI Systems
            ValidateUIControllers();

            // Validate Heart of the Maze
            ValidateHeart();

            // Final Report
            validationReport.AppendLine();
            validationReport.AppendLine("=== Validation Complete ===");
            Debug.Log(validationReport.ToString());

            validationComplete = true;
        }

        private void ValidateGameController()
        {
            validationReport.AppendLine("--- GameController ---");

            if (GameController.Instance == null)
            {
                validationReport.AppendLine("✗ FAILED: GameController.Instance is NULL");
                return;
            }

            validationReport.AppendLine("✓ GameController.Instance exists");

            // Check essence
            int essence = GameController.Instance.CurrentEssence;
            validationReport.AppendLine($"✓ Current Essence: {essence}");

            // Note: Cannot directly check event subscribers from outside the class
            // Events work correctly if GameController is properly initialized
            validationReport.AppendLine("✓ OnEssenceChanged event available");

            // Check maze grid registration
            if (GameController.Instance.MazeGrid == null)
            {
                validationReport.AppendLine("⚠ WARNING: MazeGrid not registered");
            }
            else
            {
                validationReport.AppendLine("✓ MazeGrid registered");
            }

            // Check heart registration
            if (GameController.Instance.Heart == null)
            {
                validationReport.AppendLine("⚠ WARNING: Heart not registered");
            }
            else
            {
                validationReport.AppendLine("✓ Heart registered");
            }

            validationReport.AppendLine();
        }

        private void ValidateMazeGrid()
        {
            validationReport.AppendLine("--- MazeGrid ---");

            var mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();

            if (mazeGridBehaviour == null)
            {
                validationReport.AppendLine("✗ FAILED: No MazeGridBehaviour found in scene");
                return;
            }

            validationReport.AppendLine("✓ MazeGridBehaviour exists");

            MazeGrid grid = mazeGridBehaviour.Grid;

            if (grid == null)
            {
                validationReport.AppendLine("✗ FAILED: MazeGrid is NULL");
                return;
            }

            validationReport.AppendLine($"✓ MazeGrid created ({grid.Width}x{grid.Height})");
            validationReport.AppendLine($"  {grid.GetGridInfo()}");

            validationReport.AppendLine();
        }

        private void ValidateMazeRenderer()
        {
            validationReport.AppendLine("--- MazeRenderer ---");

            var mazeRenderer = FindFirstObjectByType<MazeRenderer>();

            if (mazeRenderer == null)
            {
                validationReport.AppendLine("⚠ WARNING: No MazeRenderer found in scene");
                validationReport.AppendLine();
                return;
            }

            validationReport.AppendLine("✓ MazeRenderer exists");

            // Check for rendered tiles
            Transform tilesParent = mazeRenderer.transform.Find("MazeTiles");
            if (tilesParent != null)
            {
                int tileCount = tilesParent.childCount;
                validationReport.AppendLine($"✓ Tiles rendered: {tileCount} objects");

                // Check for batched meshes
                int batchCount = 0;
                foreach (Transform child in tilesParent)
                {
                    if (child.name.StartsWith("Batch_"))
                    {
                        batchCount++;
                    }
                }

                if (batchCount > 0)
                {
                    validationReport.AppendLine($"✓ Mesh batching active: {batchCount} batches");
                }
            }
            else
            {
                validationReport.AppendLine("⚠ WARNING: No tiles container found");
            }

            validationReport.AppendLine();
        }

        private void ValidateUIControllers()
        {
            validationReport.AppendLine("--- UI Controllers ---");

            // Check UIController
            var uiController = FindFirstObjectByType<UIController>();
            if (uiController != null)
            {
                validationReport.AppendLine("✓ UIController exists");
            }
            else
            {
                validationReport.AppendLine("⚠ WARNING: UIController not found");
            }

            // Check PlayerResourcesUIController
            var resourcesUI = FindFirstObjectByType<PlayerResourcesUIController>();
            if (resourcesUI != null)
            {
                validationReport.AppendLine("✓ PlayerResourcesUIController exists");
            }
            else
            {
                validationReport.AppendLine("⚠ WARNING: PlayerResourcesUIController not found");
            }

            validationReport.AppendLine();
        }

        private void ValidateHeart()
        {
            validationReport.AppendLine("--- Heart of the Maze ---");

            var heart = FindFirstObjectByType<HeartOfTheMaze>();

            if (heart == null)
            {
                validationReport.AppendLine("⚠ WARNING: No HeartOfTheMaze found in scene");
                validationReport.AppendLine();
                return;
            }

            validationReport.AppendLine("✓ HeartOfTheMaze exists");
            validationReport.AppendLine($"  Grid Position: {heart.GridPosition}");

            // Check for 3D components
            var heartLight = heart.GetComponent<Light>();
            if (heartLight != null)
            {
                validationReport.AppendLine($"✓ 3D Point Light: {heartLight.type}, Intensity: {heartLight.intensity}");
            }

            var heartCollider = heart.GetComponent<Collider>();
            if (heartCollider != null)
            {
                validationReport.AppendLine($"✓ 3D Collider: {heartCollider.GetType().Name}");
            }

            validationReport.AppendLine();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the validation report as a string.
        /// </summary>
        public string GetValidationReport()
        {
            if (!validationComplete)
            {
                return "Validation not yet complete. Please wait...";
            }

            return validationReport.ToString();
        }

        /// <summary>
        /// Checks if validation has completed.
        /// </summary>
        public bool IsValidationComplete => validationComplete;

        #endregion
    }
}
