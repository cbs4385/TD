using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using FaeMaze.Systems;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to set up EnvironmentDecorator component in scenes.
    /// </summary>
    public static class EnvironmentDecoratorSetup
    {
        [MenuItem("FaeMaze/Setup Environment Decorator")]
        public static void SetupEnvironmentDecorator()
        {
            // Find or create GameObject for EnvironmentDecorator
            GameObject decoratorObj = GameObject.Find("GameRoot");
            if (decoratorObj == null)
            {
                return;
            }

            // Check if component already exists
            EnvironmentDecorator decorator = decoratorObj.GetComponent<EnvironmentDecorator>();
            if (decorator == null)
            {
                decorator = decoratorObj.AddComponent<EnvironmentDecorator>();
            }

            // Find MazeGridBehaviour
            MazeGridBehaviour mazeGrid = Object.FindFirstObjectByType<MazeGridBehaviour>();
            if (mazeGrid == null)
            {
                return;
            }

            // Load tree prefab
            string treePrefabPath = "Assets/Prefabs/Tile/tree.prefab";
            GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(treePrefabPath);
            if (treePrefab == null)
            {
                return;
            }

            // Configure EnvironmentDecorator
            SerializedObject decoratorSO = new SerializedObject(decorator);
            decoratorSO.FindProperty("mazeGridBehaviour").objectReferenceValue = mazeGrid;
            decoratorSO.FindProperty("treePrefab").objectReferenceValue = treePrefab;
            decoratorSO.FindProperty("zPosition").floatValue = 0f;
            decoratorSO.FindProperty("zRotationVariance").floatValue = 5f;
            decoratorSO.FindProperty("backgroundPadding").intValue = 20;
            decoratorSO.FindProperty("createBlackBackdrop").boolValue = true;
            decoratorSO.FindProperty("backdropZPosition").floatValue = 1000f;
            decoratorSO.FindProperty("transparencyRadius").floatValue = 3f;
            decoratorSO.FindProperty("transparentAlpha").floatValue = 0.25f;
            decoratorSO.FindProperty("opaqueAlpha").floatValue = 1f;
            decoratorSO.FindProperty("transitionSmoothness").floatValue = 0.5f;
            decoratorSO.ApplyModifiedProperties();

            // Mark scene as dirty
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());


            Selection.activeGameObject = decoratorObj;
        }
    }
}
