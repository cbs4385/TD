using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Component added to wall game objects to check for physics collisions with nodes and edges.
    /// Walls are children of GraphElementWallContainer and may ONLY be destroyed by:
    /// 1. Physics collision with MazePath/MazeNode tagged objects
    /// 2. Complete maze regeneration (IsMazeRegenerating = true)
    /// 3. Scene unloading (transitioning to another scene)
    /// Any other destruction is an ARCHITECTURE VIOLATION and throws an exception.
    /// </summary>
    public class WallCollisionChecker : MonoBehaviour
    {
        private const float WALL_CHECK_RADIUS = 0.3f;
        private const string PATH_TAG = "MazePath";
        private const string NODE_TAG = "MazeNode";

        private static List<WallCollisionChecker> activeCheckers = new List<WallCollisionChecker>();

        /// <summary>
        /// Tracks whether this wall is being legitimately destroyed due to path/node collision.
        /// </summary>
        private bool isBeingDestroyedLegitimately = false;

        /// <summary>
        /// Reference to parent container (if any).
        /// </summary>
        private GraphElementWallContainer parentContainer;

        /// <summary>
        /// Set to true during maze regeneration to allow bulk wall destruction.
        /// </summary>
        public static bool IsMazeRegenerating { get; set; } = false;

        /// <summary>
        /// True when application is quitting - allows wall destruction without errors.
        /// </summary>
        private static bool isApplicationQuitting = false;

        /// <summary>
        /// True when a scene is being unloaded - allows wall destruction without errors.
        /// </summary>
        private static bool isSceneUnloading = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            // Scene is about to change - set flag before any destruction happens
            isSceneUnloading = true;
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            // Reset flag after scene unload completes
            isSceneUnloading = false;
        }

        private void Awake()
        {
            // Find and register with parent container
            parentContainer = GetComponentInParent<GraphElementWallContainer>();
            if (parentContainer != null)
            {
                parentContainer.RegisterWall(this);
            }
        }

        private void OnEnable()
        {
            activeCheckers.Add(this);
        }

        private void OnApplicationQuit()
        {
            isApplicationQuitting = true;
        }

        private void OnDestroy()
        {
            activeCheckers.Remove(this);

            // Unregister from parent container
            if (parentContainer != null)
            {
                parentContainer.UnregisterWall(this);
            }

            // Validate that destruction is legitimate
            if (!isBeingDestroyedLegitimately && !IsMazeRegenerating && !isApplicationQuitting && !isSceneUnloading)
            {
                string wallInfo = $"Wall '{gameObject.name}' at {transform.position}";
                string containerInfo = parentContainer != null
                    ? $"Parent: {parentContainer.Type} {parentContainer.ElementId}"
                    : "Parent: NONE (orphan wall!)";
                string stackTrace = System.Environment.StackTrace;

                string errorMessage =
                    $"[WallCollisionChecker] ARCHITECTURE VIOLATION!\n" +
                    $"{wallInfo}\n" +
                    $"{containerInfo}\n" +
                    $"Wall was destroyed without colliding with a path or node!\n" +
                    $"Walls may ONLY be removed by:\n" +
                    $"  (1) Physics collision with MazePath/MazeNode tags\n" +
                    $"  (2) Complete maze regeneration (IsMazeRegenerating=true)\n" +
                    $"Stack trace:\n{stackTrace}";

                // Throw exception to halt execution and force investigation
                throw new System.InvalidOperationException(errorMessage);
            }
        }

        private void Start()
        {
            // Do NOT auto-check on Start - walls should only be checked when triggered
            // by their parent container or by explicit recheck calls after new geometry is added
        }

        /// <summary>
        /// Checks if this wall collides with any node or edge path using Unity physics.
        /// Destroys the wall if collision detected. Returns true if wall was destroyed.
        /// This should ONLY be called by GraphElementWallContainer.TriggerWallCollisionChecks()
        /// or by the static Recheck methods after new geometry is added.
        /// </summary>
        public bool CheckAndDestroyIfColliding()
        {
            if (this == null || gameObject == null)
                return false;

            // Sync transforms so newly created path/node colliders are detected
            Physics.SyncTransforms();

            Collider[] colliders = Physics.OverlapSphere(transform.position, WALL_CHECK_RADIUS);

            foreach (var collider in colliders)
            {
                if (collider == null) continue;

                if (collider.transform == transform || collider.transform.IsChildOf(transform))
                    continue;

                if (collider.CompareTag(PATH_TAG) || collider.CompareTag(NODE_TAG))
                {
                    // Mark as legitimate before destroying
                    isBeingDestroyedLegitimately = true;
                    Destroy(gameObject);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Re-check all existing walls in an area after new geometry is added.
        /// Uses Unity physics to detect collisions.
        /// </summary>
        public static void RecheckWallsInArea(Vector3 areaCenter, float areaRadius)
        {
            // Sync transforms so newly created colliders (like node cylinders) are detected
            Physics.SyncTransforms();

            Collider[] wallColliders = Physics.OverlapSphere(areaCenter, areaRadius);

            foreach (var collider in wallColliders)
            {
                if (collider == null) continue;

                var checker = collider.GetComponent<WallCollisionChecker>();
                if (checker != null)
                {
                    checker.CheckAndDestroyIfColliding();
                }
            }
        }

        /// <summary>
        /// Re-check all existing walls along a polyline path after a new edge is added.
        /// </summary>
        public static void RecheckWallsAlongPath(List<Vector2> polylinePoints, float pathWidth)
        {
            if (polylinePoints == null || polylinePoints.Count < 2)
                return;

            for (int i = 0; i < polylinePoints.Count - 1; i++)
            {
                Vector2 start = polylinePoints[i];
                Vector2 end = polylinePoints[i + 1];
                Vector2 center = (start + end) * 0.5f;
                float segmentLength = Vector2.Distance(start, end);

                float areaRadius = segmentLength * 0.5f + pathWidth + WALL_CHECK_RADIUS;
                Vector3 areaCenter = new Vector3(center.x, center.y, 0f);

                RecheckWallsInArea(areaCenter, areaRadius);
            }
        }

        /// <summary>
        /// Re-check all walls around a new node after it's added.
        /// </summary>
        public static void RecheckWallsAroundNode(Vector2 nodeCenter, float nodeRadius)
        {
            Vector3 areaCenter = new Vector3(nodeCenter.x, nodeCenter.y, 0f);
            float areaRadius = nodeRadius + WALL_CHECK_RADIUS * 3f;

            RecheckWallsInArea(areaCenter, areaRadius);
        }
    }
}
