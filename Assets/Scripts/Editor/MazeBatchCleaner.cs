using UnityEngine;
using UnityEditor;
using FaeMaze.Systems;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to clear batched maze geometry and force regeneration.
    /// Use this when batch chunks have incorrect position data baked in.
    /// </summary>
    public class MazeBatchCleaner : EditorWindow
    {
        [MenuItem("Tools/Maze/Clear and Regenerate Batches")]
        public static void ShowWindow()
        {
            GetWindow<MazeBatchCleaner>("Maze Batch Cleaner");
        }

        private void OnGUI()
        {
            GUILayout.Label("Maze Batch Cleaner", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "This tool clears old batched maze geometry that may have incorrect position data baked in.\n\n" +
                "Use this if you see the maze offset from its expected position.",
                MessageType.Info
            );

            GUILayout.Space(10);

            if (GUILayout.Button("Clear Batch Chunks", GUILayout.Height(30)))
            {
                ClearBatchChunks();
            }

            GUILayout.Space(5);

            if (GUILayout.Button("Clear and Regenerate Maze", GUILayout.Height(30)))
            {
                ClearAndRegenerateMaze();
            }

            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "After clearing, enter Play mode to regenerate the maze with correct positioning.",
                MessageType.Warning
            );
        }

        private static void ClearBatchChunks()
        {
            // Find MazeTiles container
            GameObject mazeTiles = GameObject.Find("MazeTiles");

            if (mazeTiles == null)
            {
                EditorUtility.DisplayDialog("Maze Batch Cleaner",
                    "Could not find MazeTiles GameObject in the scene.",
                    "OK");
                return;
            }

            int deletedCount = 0;

            // Delete all Batch_Chunk_* children
            Transform[] children = mazeTiles.GetComponentsInChildren<Transform>(true);

            foreach (Transform child in children)
            {
                if (child != mazeTiles.transform && child.name.StartsWith("Batch_Chunk_"))
                {
                    DestroyImmediate(child.gameObject);
                    deletedCount++;
                }
            }

            Debug.Log($"[MazeBatchCleaner] Deleted {deletedCount} batch chunks");

            EditorUtility.DisplayDialog("Maze Batch Cleaner",
                $"Deleted {deletedCount} batch chunks.\n\nEnter Play mode to regenerate the maze.",
                "OK");
        }

        private static void ClearAndRegenerateMaze()
        {
            // Find MazeTiles container
            GameObject mazeTiles = GameObject.Find("MazeTiles");

            if (mazeTiles == null)
            {
                EditorUtility.DisplayDialog("Maze Batch Cleaner",
                    "Could not find MazeTiles GameObject in the scene.",
                    "OK");
                return;
            }

            // Delete the entire MazeTiles container
            DestroyImmediate(mazeTiles);

            Debug.Log("[MazeBatchCleaner] Deleted MazeTiles container");

            // Find MazeRenderer to trigger regeneration
            MazeRenderer renderer = Object.FindFirstObjectByType<MazeRenderer>();

            if (renderer != null)
            {
                Debug.Log("[MazeBatchCleaner] MazeRenderer found - maze will regenerate on Play");

                EditorUtility.DisplayDialog("Maze Batch Cleaner",
                    "Deleted MazeTiles container.\n\nEnter Play mode to regenerate the maze with correct positioning.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Maze Batch Cleaner",
                    "Deleted MazeTiles container, but could not find MazeRenderer.\n\nMake sure MazeRenderer component exists in the scene.",
                    "OK");
            }
        }

        [MenuItem("Tools/Maze/Quick Clear Batches")]
        public static void QuickClearBatches()
        {
            if (EditorUtility.DisplayDialog("Clear Maze Batches",
                "This will delete all batch chunks. Enter Play mode after to regenerate.\n\nContinue?",
                "Yes", "Cancel"))
            {
                ClearBatchChunks();
            }
        }

        [MenuItem("Tools/Maze/Quick Regenerate Maze")]
        public static void QuickRegenerateMaze()
        {
            if (EditorUtility.DisplayDialog("Regenerate Maze",
                "This will delete the entire MazeTiles container and regenerate on Play.\n\nContinue?",
                "Yes", "Cancel"))
            {
                ClearAndRegenerateMaze();
            }
        }
    }
}
