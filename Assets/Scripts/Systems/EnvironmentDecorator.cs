using UnityEngine;
using FaeMaze.Maze;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Spawns environment decoration (trees, walls) on non-walkable tiles.
    /// </summary>
    public class EnvironmentDecorator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("The maze grid behaviour to decorate")]
        private MazeGridBehaviour mazeGridBehaviour;

        [SerializeField]
        [Tooltip("Tree/wall prefab to spawn on non-walkable tiles")]
        private GameObject treePrefab;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Z position for spawned decorations")]
        private float zPosition = 0f;

        [SerializeField]
        [Tooltip("Random rotation on Y axis")]
        private bool randomYRotation = true;

        [SerializeField]
        [Tooltip("Parent transform for spawned decorations")]
        private Transform decorationParent;

        private void Start()
        {
            // Find MazeGridBehaviour if not assigned
            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            if (mazeGridBehaviour == null)
            {
                Debug.LogError("[EnvironmentDecorator] MazeGridBehaviour not found!");
                enabled = false;
                return;
            }

            if (treePrefab == null)
            {
                Debug.LogError("[EnvironmentDecorator] Tree prefab not assigned!");
                enabled = false;
                return;
            }

            // Create parent if not assigned
            if (decorationParent == null)
            {
                GameObject parentObj = new GameObject("Environment Decorations");
                decorationParent = parentObj.transform;
            }

            // Wait one frame for maze to be generated
            StartCoroutine(SpawnDecorationsNextFrame());
        }

        private System.Collections.IEnumerator SpawnDecorationsNextFrame()
        {
            yield return null; // Wait one frame

            SpawnDecorations();
        }

        private void SpawnDecorations()
        {
            if (mazeGridBehaviour.Grid == null)
            {
                Debug.LogError("[EnvironmentDecorator] Maze grid is null!");
                return;
            }

            int decorationCount = 0;
            int width = mazeGridBehaviour.Grid.Width;
            int height = mazeGridBehaviour.Grid.Height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var node = mazeGridBehaviour.Grid.GetNode(x, y);

                    // Only spawn on non-walkable tiles
                    if (node != null && !node.walkable)
                    {
                        Vector3 worldPos = mazeGridBehaviour.GridToWorld(x, y);
                        worldPos.z = zPosition;

                        Quaternion rotation = Quaternion.identity;
                        if (randomYRotation)
                        {
                            rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                        }

                        GameObject decoration = Instantiate(treePrefab, worldPos, rotation, decorationParent);
                        decoration.name = $"Tree_{x}_{y}";

                        decorationCount++;
                    }
                }
            }

            Debug.Log($"[EnvironmentDecorator] Spawned {decorationCount} decorations");
        }

        /// <summary>
        /// Clears all spawned decorations.
        /// </summary>
        public void ClearDecorations()
        {
            if (decorationParent != null)
            {
                foreach (Transform child in decorationParent)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        /// <summary>
        /// Regenerates all decorations.
        /// </summary>
        public void RegenerateDecorations()
        {
            ClearDecorations();
            SpawnDecorations();
        }
    }
}
