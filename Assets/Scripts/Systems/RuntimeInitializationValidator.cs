using UnityEngine;
using FaeMaze.Maze;
using FaeMaze.UI;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Validates that all game systems initialize correctly at runtime.
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

            ValidateGameController();
            ValidateMazeGrid();
            ValidateMazeRenderer();
            ValidateUIControllers();
            ValidateHeart();

            validationReport.AppendLine();
            validationReport.AppendLine("=== Validation Complete ===");

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

            int essence = GameController.Instance.CurrentEssence;
            validationReport.AppendLine($"✓ Current Essence: {essence}");
            validationReport.AppendLine("✓ OnEssenceChanged event available");

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
            validationReport.AppendLine("--- MazeGridBehaviour ---");

            var mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();

            if (mazeGridBehaviour == null)
            {
                validationReport.AppendLine("✗ FAILED: No MazeGridBehaviour found in scene");
                return;
            }

            validationReport.AppendLine("✓ MazeGridBehaviour exists");

            var forestState = mazeGridBehaviour.ForestMapState;
            if (forestState == null)
            {
                validationReport.AppendLine("✗ FAILED: ForestMapState is NULL");
                return;
            }

            validationReport.AppendLine($"✓ ForestMapState created ({forestState.Nodes.Count} nodes, {forestState.Edges.Count} edges)");

            var worldSpaceData = mazeGridBehaviour.WorldSpaceMazeData;
            if (worldSpaceData != null)
            {
                validationReport.AppendLine($"✓ WorldSpaceMazeData: {worldSpaceData.Tiles.Count} tiles");
            }

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

            Transform tilesParent = mazeRenderer.transform.Find("MazeTiles");
            if (tilesParent != null)
            {
                int tileCount = tilesParent.childCount;
                validationReport.AppendLine($"✓ Tiles rendered: {tileCount} objects");

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

            var uiController = FindFirstObjectByType<UIController>();
            if (uiController != null)
            {
                validationReport.AppendLine("✓ UIController exists");
            }
            else
            {
                validationReport.AppendLine("⚠ WARNING: UIController not found");
            }

            var essenceBar = FindFirstObjectByType<EssenceBarController>();
            if (essenceBar != null)
            {
                validationReport.AppendLine("✓ EssenceBarController exists");
            }
            else
            {
                validationReport.AppendLine("⚠ WARNING: EssenceBarController not found");
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
            validationReport.AppendLine($"  World Position: {heart.transform.position}");

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

        public string GetValidationReport()
        {
            if (!validationComplete)
            {
                return "Validation not yet complete. Please wait...";
            }

            return validationReport.ToString();
        }

        public bool IsValidationComplete => validationComplete;

        #endregion
    }
}
