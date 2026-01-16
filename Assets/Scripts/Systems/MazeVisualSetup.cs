using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Helper script to automatically set up the visual components for the maze.
    /// Runs early to ensure the correct renderer is used before rendering begins.
    /// </summary>
    [DefaultExecutionOrder(-90)] // Run after MazeGridBehaviour (-100) but before renderers
    [RequireComponent(typeof(MazeGridBehaviour))]
    public class MazeVisualSetup : MonoBehaviour
    {
        [Header("Auto Setup")]
        [SerializeField]
        [Tooltip("Automatically add MazeRenderer if missing")]
        private bool autoAddRenderer = true;

        [SerializeField]
        [Tooltip("Use WorldSpaceMazeRenderer (shape-based) instead of MazeRenderer (tile-based)")]
        private bool useWorldSpaceRenderer = false;

        [SerializeField]
        [Tooltip("Automatically center camera on maze")]
        private bool autoCenterCamera = true;

        [SerializeField]
        [Tooltip("Automatically add VoidFogGenerator to fill non-walkable areas with fog")]
        private bool autoAddVoidFog = true;

        [Header("Defaults")]
        [SerializeField]
        [Tooltip("Wall prefab/model to inject when MazeRenderer is missing a reference")]
        private GameObject defaultWallPrefab;

        [SerializeField]
        [Tooltip("Undergrowth prefab/model to inject when MazeRenderer is missing a reference")]
        private GameObject defaultUndergrowthPrefab;

        [SerializeField]
        [Tooltip("Water prefab/model to inject when MazeRenderer is missing a reference")]
        private GameObject defaultWaterPrefab;

        private void Awake()
        {
            if (autoAddRenderer)
            {
                SetupRenderer();
            }

            if (autoAddVoidFog)
            {
                SetupVoidFog();
            }

            if (autoCenterCamera)
            {
                CenterCameraOnMaze();
            }
        }

        private void SetupVoidFog()
        {
            // Check if VoidFogGenerator already exists in the scene
            VoidFogGenerator existingFog = FindFirstObjectByType<VoidFogGenerator>();
            if (existingFog != null)
            {
                return;
            }

            // Create a new GameObject for the void fog
            GameObject fogObject = new GameObject("VoidFogGenerator");
            fogObject.transform.SetParent(transform);
            fogObject.AddComponent<VoidFogGenerator>();
        }

        private void SetupRenderer()
        {
            if (useWorldSpaceRenderer)
            {
                // Disable any existing MazeRenderer to prevent both from running
                MazeRenderer existingRenderer = GetComponent<MazeRenderer>();
                if (existingRenderer != null)
                {
                    existingRenderer.enabled = false;
                    DestroyImmediate(existingRenderer);
                }

                // Use shape-based WorldSpaceMazeRenderer (LineRenderers for edges, circles for nodes)
                WorldSpaceMazeRenderer wsRenderer = GetComponent<WorldSpaceMazeRenderer>();
                if (wsRenderer == null)
                {
                    gameObject.AddComponent<WorldSpaceMazeRenderer>();
                    wsRenderer = GetComponent<WorldSpaceMazeRenderer>();
                }

                // Set wall prefab if available
                if (wsRenderer != null && defaultWallPrefab != null)
                {
                    wsRenderer.SetWallPrefab(defaultWallPrefab);
                }
            }
            else
            {
                // Disable any existing WorldSpaceMazeRenderer to prevent both from running
                WorldSpaceMazeRenderer existingWsRenderer = GetComponent<WorldSpaceMazeRenderer>();
                if (existingWsRenderer != null)
                {
                    existingWsRenderer.enabled = false;
                    DestroyImmediate(existingWsRenderer);
                }

                // Use tile-based MazeRenderer
                MazeRenderer renderer = GetComponent<MazeRenderer>();
                if (renderer == null)
                {
                    gameObject.AddComponent<MazeRenderer>();
                    renderer = GetComponent<MazeRenderer>();
                }

                if (renderer != null && !renderer.HasWallPrefab && defaultWallPrefab != null)
                {
                    renderer.SetWallPrefab(defaultWallPrefab);
                }

                if (renderer != null && !renderer.HasUndergrowthPrefab && defaultUndergrowthPrefab != null)
                {
                    renderer.SetUndergrowthPrefab(defaultUndergrowthPrefab);
                }

                if (renderer != null && !renderer.HasWaterPrefab && defaultWaterPrefab != null)
                {
                    renderer.SetWaterPrefab(defaultWaterPrefab);
                }
            }
        }

        private void CenterCameraOnMaze()
        {
            MazeGridBehaviour mazeGrid = GetComponent<MazeGridBehaviour>();
            if (mazeGrid == null || mazeGrid.ForestMapState == null)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            // Set camera background to near-black (#0a0a0a) to match maze background
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.039f, 0.039f, 0.039f, 1f);

            // Center camera on the heart of the maze (for orthographic cameras)
            if (mainCamera.orthographic)
            {
                Vector3 heartWorldPos = mazeGrid.HeartWorldPosition;
                Vector3 cameraPos = mainCamera.transform.position;
                cameraPos.x = heartWorldPos.x;
                cameraPos.y = heartWorldPos.y;
                mainCamera.transform.position = cameraPos;

                // Set orthographic size based on world-space bounds
                if (mazeGrid.WorldSpaceMazeData != null)
                {
                    var bounds = mazeGrid.WorldSpaceMazeData.Bounds;
                    float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y);
                    mainCamera.orthographicSize = maxDimension * 0.6f;
                }
            }
        }

        private void Start()
        {
            if (autoCenterCamera)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null && mainCamera.transform.position == new Vector3(0, 0, mainCamera.transform.position.z))
                {
                    CenterCameraOnMaze();
                }
            }
        }
    }
}
