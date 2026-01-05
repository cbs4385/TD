using UnityEngine;
using FaeMaze.Cameras;

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

        private bool focalPointConfigured = false;

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
            if (mazeGrid == null || mazeGrid.Grid == null)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            // Center camera on the heart of the maze
            Vector3 heartWorldPos = mazeGrid.GridToWorld(mazeGrid.HeartGridPos.x, mazeGrid.HeartGridPos.y);

            // Check if camera has CameraController3D with focal point mode
            CameraController3D cameraController3D = mainCamera.GetComponent<CameraController3D>();
            if (cameraController3D != null)
            {
                // For perspective cameras with focal point mode, set up the focal point
                ConfigureFocalPointCamera(cameraController3D, heartWorldPos);
            }
            else
            {
                // For orthographic cameras, just move the camera position
                Vector3 cameraPos = mainCamera.transform.position;
                cameraPos.x = heartWorldPos.x;
                cameraPos.y = heartWorldPos.y;
                mainCamera.transform.position = cameraPos;

                // Set orthographic size to show entire maze
                if (mainCamera.orthographic)
                {
                    float maxDimension = Mathf.Max(mazeGrid.Grid.Width, mazeGrid.Grid.Height);
                    mainCamera.orthographicSize = maxDimension * 0.6f; // 0.6 gives some padding
                }
            }
        }

        private void ConfigureFocalPointCamera(CameraController3D cameraController, Vector3 heartWorldPos)
        {
            // Find or create the Focal Point GameObject
            Transform focalPointTransform = cameraController.FocalPointTransform;

            if (focalPointTransform == null)
            {
                // Look for existing Focal Point in scene
                GameObject focalPointObj = GameObject.Find("Focal Point");
                if (focalPointObj == null)
                {
                    // Create new Focal Point GameObject
                    focalPointObj = new GameObject("Focal Point");
                }
                focalPointTransform = focalPointObj.transform;
            }

            // Position focal point at heart of maze
            heartWorldPos.z = 0f; // Ensure on XY plane
            focalPointTransform.position = heartWorldPos;

            // Set the focal point to face towards the camera's current forward direction
            Camera cam = cameraController.GetComponent<Camera>();
            if (cam != null)
            {
                Vector3 forward = cam.transform.forward;
                forward.z = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                {
                    forward.Normalize();
                    focalPointTransform.rotation = Quaternion.LookRotation(forward, Vector3.forward);
                }
            }

            focalPointConfigured = true;
        }

        private void Start()
        {
            // Try camera centering again in Start if it failed in Awake
            if (autoCenterCamera && !focalPointConfigured)
            {
                CenterCameraOnMaze();
            }
        }

        private void LateUpdate()
        {
            // Ensure focal point is configured once the maze is fully initialized
            if (autoCenterCamera && !focalPointConfigured)
            {
                MazeGridBehaviour mazeGrid = GetComponent<MazeGridBehaviour>();
                if (mazeGrid != null && mazeGrid.Grid != null)
                {
                    Camera mainCamera = Camera.main;
                    if (mainCamera != null)
                    {
                        CameraController3D cameraController3D = mainCamera.GetComponent<CameraController3D>();
                        if (cameraController3D != null && cameraController3D.FocalPointTransform != null)
                        {
                            // Now that focal point exists, reposition it at the heart
                            Vector3 heartWorldPos = mazeGrid.GridToWorld(mazeGrid.HeartGridPos.x, mazeGrid.HeartGridPos.y);
                            heartWorldPos.z = 0f;
                            cameraController3D.FocalPointTransform.position = heartWorldPos;

                            // Set the focal point rotation to face a reasonable direction
                            Vector3 forward = mainCamera.transform.forward;
                            forward.z = 0f;
                            if (forward.sqrMagnitude > 0.0001f)
                            {
                                forward.Normalize();
                                cameraController3D.FocalPointTransform.rotation = Quaternion.LookRotation(forward, Vector3.forward);
                            }

                            focalPointConfigured = true;
                        }
                    }
                }
            }
        }
    }
}
