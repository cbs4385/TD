using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Automatically initializes maze visual components when the scene loads.
    /// </summary>
    public static class MazeAutoInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            MazeGridBehaviour mazeGrid = Object.FindFirstObjectByType<MazeGridBehaviour>();
            if (mazeGrid == null)
            {
                return;
            }

            MazeRenderer renderer = mazeGrid.GetComponent<MazeRenderer>();
            if (renderer == null)
            {
                renderer = mazeGrid.gameObject.AddComponent<MazeRenderer>();
            }

            SetupCamera(mazeGrid);
        }

        private static void SetupCamera(MazeGridBehaviour mazeGrid)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            CoroutineRunner runner = new GameObject("CoroutineRunner").AddComponent<CoroutineRunner>();
            runner.StartCoroutine(CenterCameraDelayed(mainCamera, mazeGrid));
        }

        private static System.Collections.IEnumerator CenterCameraDelayed(Camera camera, MazeGridBehaviour mazeGrid)
        {
            yield return new WaitForEndOfFrame();

            if (mazeGrid.ForestMapState == null)
            {
                yield break;
            }

            // Center camera on the heart of the maze (world-space position)
            Vector3 heartWorldPos = mazeGrid.HeartWorldPosition;
            Vector3 cameraPos = camera.transform.position;
            cameraPos.x = heartWorldPos.x;
            cameraPos.y = heartWorldPos.y;
            camera.transform.position = cameraPos;

            // Set orthographic size based on world-space bounds
            if (mazeGrid.WorldSpaceMazeData != null)
            {
                var bounds = mazeGrid.WorldSpaceMazeData.Bounds;
                float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y);
                camera.orthographicSize = maxDimension * 0.6f;
            }
        }

        private class CoroutineRunner : MonoBehaviour
        {
        }
    }
}
