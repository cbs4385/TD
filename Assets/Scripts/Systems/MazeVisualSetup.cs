using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Helper script to automatically set up the visual components for the maze.
    /// Attach this to any GameObject with MazeGridBehaviour and it will add the necessary components.
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
            }
        }

        private void CenterCameraOnMaze()
        {
            MazeGridBehaviour mazeGrid = GetComponent<MazeGridBehaviour>();
            if (mazeGrid == null || mazeGrid.Grid == null)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            // Center camera on maze
            float centerX = mazeGrid.Grid.Width / 2f;
            float centerY = mazeGrid.Grid.Height / 2f;

            Vector3 cameraPos = mainCamera.transform.position;
            cameraPos.x = centerX;
            cameraPos.y = centerY;
            mainCamera.transform.position = cameraPos;

            // Set orthographic size to show entire maze
            float maxDimension = Mathf.Max(mazeGrid.Grid.Width, mazeGrid.Grid.Height);
            mainCamera.orthographicSize = maxDimension * 0.6f; // 0.6 gives some padding

        }

        private void Start()
        {
            // Try camera centering again in Start if it failed in Awake
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
