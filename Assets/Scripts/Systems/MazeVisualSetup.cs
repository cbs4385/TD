using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Helper script to automatically set up the visual components for the maze.
    /// </summary>
    [RequireComponent(typeof(MazeGridBehaviour))]
    public class MazeVisualSetup : MonoBehaviour
    {
        [Header("Auto Setup")]
        [SerializeField]
        [Tooltip("Automatically add MazeRenderer if missing")]
        private bool autoAddRenderer = true;

        [SerializeField]
        [Tooltip("Automatically center camera on maze")]
        private bool autoCenterCamera = true;

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

            if (autoCenterCamera)
            {
                CenterCameraOnMaze();
            }
        }

        private void SetupRenderer()
        {
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
