using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Ensures a MazeParticleSystem exists in the scene. Add this component to any GameObject in the scene.
    /// </summary>
    [DefaultExecutionOrder(-50)] // Run early, before most other scripts
    public class MazeParticleSystemSpawner : MonoBehaviour
    {
        [Header("Particle System Settings")]
        [SerializeField]
        [Tooltip("Create particle system on Awake if not found in scene")]
        private bool autoCreateOnAwake = true;

        private void Awake()
        {
            if (!autoCreateOnAwake)
                return;

            // Check if a MazeParticleSystem already exists in the scene
            MazeParticleSystem existingParticleSystem = FindFirstObjectByType<MazeParticleSystem>();
            if (existingParticleSystem != null)
            {
                Debug.Log("[MazeParticleSystemSpawner] MazeParticleSystem already exists in scene");
                return; // Already exists, don't create another
            }

            // Create a new GameObject for the particle system
            GameObject particleSystemObj = new GameObject("MazeParticleSystem");
            MazeParticleSystem particleSystem = particleSystemObj.AddComponent<MazeParticleSystem>();

            // Position at world origin
            particleSystemObj.transform.position = Vector3.zero;

            Debug.Log("[MazeParticleSystemSpawner] Created MazeParticleSystem");
        }
    }
}
