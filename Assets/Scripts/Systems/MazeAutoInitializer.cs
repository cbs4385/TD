using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Automatically initializes maze visual components when the scene loads.
    /// This ensures the maze is always visible even if components weren't added in the editor.
    /// </summary>
    public static class MazeAutoInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {

            // Find MazeGridBehaviour in scene
            MazeGridBehaviour mazeGrid = Object.FindFirstObjectByType<MazeGridBehaviour>();
            if (mazeGrid == null)
            {
                return;
            }

            // Check if MazeRenderer exists
            MazeRenderer renderer = mazeGrid.GetComponent<MazeRenderer>();
            if (renderer == null)
            {
                renderer = mazeGrid.gameObject.AddComponent<MazeRenderer>();
            }

            // Setup particle system
            SetupParticleSystem(mazeGrid);

            // Setup camera
            SetupCamera(mazeGrid);
        }

        private static void SetupParticleSystem(MazeGridBehaviour mazeGrid)
        {
            // Check if a MazeParticleSystem already exists in the scene
            MazeParticleSystem existingParticleSystem = Object.FindFirstObjectByType<MazeParticleSystem>();
            if (existingParticleSystem != null)
            {
                return; // Already exists, don't create another
            }

            // Create a new GameObject for the particle system
            GameObject particleSystemObj = new GameObject("MazeParticleSystem");
            MazeParticleSystem particleSystem = particleSystemObj.AddComponent<MazeParticleSystem>();

            // Position at world origin
            particleSystemObj.transform.position = Vector3.zero;

        }

        private static void SetupCamera(MazeGridBehaviour mazeGrid)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            // Wait a frame for the grid to initialize, then center camera
            CoroutineRunner runner = new GameObject("CoroutineRunner").AddComponent<CoroutineRunner>();
            runner.StartCoroutine(CenterCameraDelayed(mainCamera, mazeGrid));
        }

        private static System.Collections.IEnumerator CenterCameraDelayed(Camera camera, MazeGridBehaviour mazeGrid)
        {
            // Wait for grid to be initialized
            yield return new WaitForEndOfFrame();

            if (mazeGrid.Grid == null)
            {
                yield break;
            }

            // Center camera on maze
            float centerX = mazeGrid.Grid.Width / 2f;
            float centerY = mazeGrid.Grid.Height / 2f;

            Vector3 cameraPos = camera.transform.position;
            cameraPos.x = centerX;
            cameraPos.y = centerY;
            camera.transform.position = cameraPos;

            // Set orthographic size to show entire maze
            float maxDimension = Mathf.Max(mazeGrid.Grid.Width, mazeGrid.Grid.Height);
            camera.orthographicSize = maxDimension * 0.6f; // 0.6 gives some padding

        }

        // Helper class to run coroutines
        private class CoroutineRunner : MonoBehaviour
        {
        }
    }
}
