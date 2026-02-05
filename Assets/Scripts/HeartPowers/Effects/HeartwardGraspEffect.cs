using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FaeMaze.Props;
using FaeMaze.Visitors;
using FaeMaze.Systems;
using FaeMaze.Audio;
using FaeMaze.Roguelike;
using FaeMaze.Utilities;
using ForestMaze;

namespace FaeMaze.HeartPowers
{
    #region Heartward Grasp

    /// <summary>
    /// Creates two grasp zones (grabbing and pushing) at walls along the heart-to-focal-point ray.
    /// Grabbing HGZ captures visitors and pulls them into the wall.
    /// Pushing HGZ releases visitors near the heart with a daze effect.
    /// Toggle power: deactivates after firing its effect (tier-count captures).
    /// Animation is frame-based using the Animator component.
    /// </summary>
    public class HeartwardGraspEffect : ConsumptionBasedPowerEffect
    {
        #region Static Properties

        /// <summary>
        /// Static flag indicating whether the HeartwardGrasp tongue has active solid colliders.
        /// Visitors check this to avoid expensive Physics.OverlapSphere calls when no tongue exists.
        /// </summary>
        public static bool IsGraspTongueActiveWithColliders { get; private set; } = false;

        /// <summary>
        /// Static reference to the active HeartwardGraspEffect for collision callbacks.
        /// </summary>
        public static HeartwardGraspEffect ActiveInstance { get; private set; } = null;

        #endregion

        // Grabbing HGZ states - Same as HeartOfTheMaze: Idle -> Emerging -> Extending -> Retracting
        private enum GrabPhase
        {
            Idle,           // Waiting for visitors to enter the zone
            Emerging,       // Tongue rises from ground, tip not yet at ground level
            Extending,      // Tip above ground, extending horizontally toward visitor (collision triggers grab)
            Retracting      // Visitor grabbed, tongue retracts pulling visitor into ground
        }

        // Pushing HGZ states - Reverse of grab: tongue emerges with visitor, extends, releases
        private enum PushPhase
        {
            Idle,           // Tongue hidden underground
            Emerging,       // Tongue rises from ground with visitor attached to tip
            Extending,      // Tongue extends horizontally, pushing visitor onto walkable area
            Releasing,      // Visitor released, tongue retracts back underground
            Retracting      // Tongue retracts underground (visitor already released)
        }

        // Constants - Architectural (not configurable)
        private const int MIN_WALL_THICKNESS = 3;         // Minimum wall models required for valid wall intersection
        private const float HGZ_WALL_OFFSET = 0.8f;       // Offset from first wall hit to second rank (middle of 3 layers)
        private const float MIN_EDGE_DISTANCE = 3.0f;     // Minimum distance from path/node edge (~4 wall tiles * 0.8 spacing)
        private const float TONGUE_BEND_Z = -0.25f;       // Z where tongue bends from vertical to horizontal (middle of wall models)

        // Settings - Loaded from GameSettings
        private readonly float graspZoneRadius;           // Detection radius for HeartwardGrasp zones
        private readonly float tongueEmergeSpeed;         // Units per second for vertical movement
        private readonly float tongueRetractSpeed;        // Speed when retracting
        private readonly float grabEssenceCost;           // Essence deducted from visitor when grabbed
        // Tongue geometry constants from TongueUtility
        private const float TONGUE_START_Z = TongueUtility.TONGUE_START_Z;
        private const float TONGUE_GROUND_Z = TongueUtility.TONGUE_GROUND_Z;
        private const int BEND_BONE_COUNT = TongueUtility.BEND_BONE_COUNT;

        // Tongue bone tracking for grabbing HGZ (consolidated via TongueUtility)
        private TongueBoneData grabbingTongueData;
        private GameObject[] grabbingSolidColliders;          // Solid collider objects for collision detection

        // Grabbing HGZ
        private GameObject grabbingZoneObject;
        private SphereCollider grabbingCollider;
        private GameObject grabbingTongueInstance;         // Instantiated tongue prefab
        private Vector3 grabbingWallPos;                   // Wall tile position (spawn point, acts like heart center)
        private Vector3 grabbingPlacementPos;              // Path-edge position (where visitor should walk near)
        private Vector3 grabbingWallNormal;
        private float grabbingTongueZPosition = TONGUE_START_Z;  // Z position of tongue root (high = below ground, low = emerged)
        private float grabbingTargetAngle = 0f;            // Angle toward visitor (updated during Extending)
        private GrabPhase grabPhase = GrabPhase.Idle;

        // Pushing HGZ - reverse of grabbing (tongue emerges with visitor, extends, releases)
        private GameObject pushingZoneObject;
        private SphereCollider pushingCollider;
        private GameObject pushingTongueInstance;          // Instantiated tongue prefab
        // Tongue bone tracking for pushing HGZ (consolidated via TongueUtility)
        private TongueBoneData pushingTongueData;
        private float pushingTongueZPosition = TONGUE_START_Z;  // Z position of tongue root
        private float pushingTargetAngle = 0f;             // Angle toward heart (direction tongue extends)
        private Vector3 pushingWallPos;
        private Vector3 pushingWallNormal;
        private PushPhase pushPhase = PushPhase.Idle;
        private float pushPhaseStartTime = 0f;
        private const float VISITOR_CHECK_RADIUS = 0.3f;  // Radius to check for visitor collision with walls/paths

        // Visitor processing
        private Queue<VisitorControllerBase> pendingVisitors = new Queue<VisitorControllerBase>();
        private VisitorControllerBase currentVisitor;
        private Vector3 heartNodePosition;

        // Grab-time relative transform: visitor's rotation/position offset from tip bone at grab moment
        private Quaternion grabRotationOffset;
        private Vector3 grabPositionOffset;
        private bool pushVisitorRealigned;  // True once visitor rotation is reset to identity during push Extending
        private Vector3 pushWorldPositionOffset;  // World-space offset from tip bone to visitor after realignment

        // Particle effects
        private ParticleSystem grabbingParticles;
        private ParticleSystem pushingParticles;

        // Affected wall tiles in grabbing zone
        private List<GameObject> affectedGrabbingWalls = new List<GameObject>();
        private Dictionary<GameObject, Vector3> originalWallPositions = new Dictionary<GameObject, Vector3>();

        // Frightening event (registered when tongue is active)
        private FrighteningEventManager.FrighteningEvent currentFrighteningEvent;

        // Particle colors
        private static readonly Color LeafGreen = new Color(0.3f, 0.7f, 0.2f, 1f);
        private static readonly Color BarkBrown = new Color(0.6f, 0.4f, 0.15f, 1f);

        public HeartwardGraspEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition)
        {
            // Load settings from GameSettings
            graspZoneRadius = GameSettings.HeartwardGraspRadius;
            tongueEmergeSpeed = GameSettings.TongueEmergeSpeed;
            tongueRetractSpeed = GameSettings.TongueRetractSpeed;
            grabEssenceCost = GameSettings.HeartwardGraspEssenceCost;
        }

        /// <summary>
        /// Gets the position of the grabbing HGZ zone (for tutorial camera tracking).
        /// This is the wall position (in the forest).
        /// </summary>
        public Vector3 GrabbingZonePosition => grabbingWallPos;

        /// <summary>
        /// Gets the path-side placement position of the grabbing HGZ.
        /// This is the point on the walkable path edge where visitors should walk near to be grabbed.
        /// Use this for spawning visitors that need to walk through the grab zone.
        /// </summary>
        public Vector3 GrabbingPathPosition => grabbingPlacementPos;

        /// <summary>
        /// Gets the position of the pushing HGZ zone (for tutorial camera tracking).
        /// </summary>
        public Vector3 PushingZonePosition => pushingWallPos;

        public override void OnStart()
        {
            requiredConsumptions = manager.GetPowerTier(HeartPowerType.HeartwardGrasp);
            consumedCount = 0;
            hasExpired = false;

            // Set static instance for collision callbacks
            ActiveInstance = this;

            // Get heart node position
            if (manager.MazeGrid != null)
            {
                heartNodePosition = manager.MazeGrid.HeartWorldPosition;
            }

            // Find wall positions for both HGZs along the heart-to-focal ray
            FindWallPositions(targetPosition);

            // Create both HGZs
            CreateGrabbingHGZ();
            CreatePushingHGZ();

            // Create particle effects for both zones
            CreateParticleSystem(grabbingZoneObject, ref grabbingParticles);
            CreateParticleSystem(pushingZoneObject, ref pushingParticles);

            // Find and affect wall tiles in grabbing zone
            FindAffectedGrabbingWalls();

            // Register frightening event so visitors flee when they see the active HGZ
            if (FrighteningEventManager.Instance != null)
            {
                currentFrighteningEvent = FrighteningEventManager.Instance.RegisterEvent(
                    FrighteningEventManager.EventType.HeartwardGrasp,
                    grabbingWallPos,
                    this
                );
            }
        }

        /// <summary>
        /// Finds wall positions for both grabbing (near focal point) and pushing (near heart) HGZs.
        ///
        /// GRABBING HGZ: Ray from FOCAL toward HEART
        ///   - First wall hit = grabbing placement point
        ///   - If no hits, find wall closest to the focal-to-heart ray
        ///
        /// PUSHING HGZ: Ray from HEART toward FOCAL
        ///   - First wall hit = pushing placement point
        ///   - If no hits, find wall closest to the heart-to-focal ray
        ///
        /// Both placement points are then offset into the forest.
        /// </summary>
        private void FindWallPositions(Vector3 focalPos)
        {
            Vector3 heartPos3D = new Vector3(heartNodePosition.x, heartNodePosition.y, 0f);
            Vector3 focalPos3D = new Vector3(focalPos.x, focalPos.y, 0f);
            float totalDist = Vector3.Distance(heartPos3D, focalPos3D);

            Vector2 heartPos2D = new Vector2(heartNodePosition.x, heartNodePosition.y);
            Vector2 focalPos2D = new Vector2(focalPos.x, focalPos.y);

            // Direction vectors
            Vector3 focalToHeartDir = (heartPos3D - focalPos3D).normalized;
            Vector3 heartToFocalDir = -focalToHeartDir;
            Vector2 focalToHeartDir2D = new Vector2(focalToHeartDir.x, focalToHeartDir.y);
            Vector2 heartToFocalDir2D = new Vector2(heartToFocalDir.x, heartToFocalDir.y);

            // ===== GRABBING HGZ: Ray from FOCAL toward HEART =====
            // First wall hit along this ray is the grabbing placement point
            RaycastHit[] grabbingHits = Physics.RaycastAll(focalPos3D, focalToHeartDir, totalDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            // Filter to only wall tiles and sort by distance from focal
            var grabbingWallHits = new System.Collections.Generic.List<RaycastHit>();
            foreach (var hit in grabbingHits)
            {
                if (hit.collider != null && hit.collider.gameObject.name.StartsWith("WorldTile_#"))
                {
                    grabbingWallHits.Add(hit);
                }
            }
            grabbingWallHits.Sort((a, b) => a.distance.CompareTo(b.distance));

            Vector2 localGrabbingPlacementPos;
            if (grabbingWallHits.Count > 0)
            {
                // First wall hit = grabbing placement point
                var firstGrabbingWall = grabbingWallHits[0];
                localGrabbingPlacementPos = new Vector2(firstGrabbingWall.collider.transform.position.x, firstGrabbingWall.collider.transform.position.y);
            }
            else
            {
                // No wall hits - find wall closest to the focal-to-heart ray
                Transform closestWall = FindClosestWallToRay(focalPos2D, focalToHeartDir2D, totalDist);
                if (closestWall != null)
                {
                    localGrabbingPlacementPos = new Vector2(closestWall.position.x, closestWall.position.y);
                }
                else
                {
                    // Ultimate fallback - use a point along the ray at NODE_RADIUS from focal
                    const float NODE_RADIUS = 3.0f;
                    localGrabbingPlacementPos = focalPos2D + focalToHeartDir2D * NODE_RADIUS;
                }
            }

            // Offset grabbing placement into forest
            Vector2 grabbingForestDir = FindForestDirection(localGrabbingPlacementPos, heartToFocalDir2D, heartPos2D);
            Vector2 grabbingOffset = grabbingForestDir * HGZ_WALL_OFFSET;
            grabbingWallPos = new Vector3(localGrabbingPlacementPos.x + grabbingOffset.x, localGrabbingPlacementPos.y + grabbingOffset.y, -0.4f);
            grabbingWallNormal = new Vector3(grabbingForestDir.x, grabbingForestDir.y, 0f);

            // Store the placement position (path-side point) for tutorial spawning
            this.grabbingPlacementPos = new Vector3(localGrabbingPlacementPos.x, localGrabbingPlacementPos.y, 0f);

            // ===== PUSHING HGZ: Fixed distance from heart, in the heart node's wall ring =====
            // Place at NODE_RADIUS/2 + HGZ_WALL_OFFSET from heart center (in the second wall rank).
            // Start in the heart→focal direction; if that lands on a walkable path or too close
            // to an edge boundary, rotate around the heart until finding a valid position.
            float pushingDistance = 3.0f + HGZ_WALL_OFFSET * 1.5f;  // Layer 1 center: NODE_RADIUS + WALL_SPACING * 1.5 = 4.2
            var mazeData = manager.MazeGrid?.WorldSpaceMazeData;

            Vector2 pushingDir = heartToFocalDir2D;
            Vector2 pushingPlacementPos = heartPos2D + pushingDir * pushingDistance;

            // Position must be non-walkable AND at least MIN_EDGE_CLEARANCE from any walkable tile
            // (edge boundary). This prevents the pushing HGZ from being placed where an edge
            // connects to the heart node, which would be too close to the path.
            const float MIN_EDGE_CLEARANCE = 1.2f;  // 1.5 × WALL_SPACING (0.8)

            if (mazeData != null && !IsValidPushingHGZPosition(mazeData, pushingPlacementPos, MIN_EDGE_CLEARANCE))
            {
                const float ANGLE_STEP = 5f;
                float baseAngle = Mathf.Atan2(heartToFocalDir2D.y, heartToFocalDir2D.x) * Mathf.Rad2Deg;

                for (float offset = ANGLE_STEP; offset <= 180f; offset += ANGLE_STEP)
                {
                    // Try both clockwise and counter-clockwise
                    for (int sign = 1; sign >= -1; sign -= 2)
                    {
                        float testAngle = (baseAngle + offset * sign) * Mathf.Deg2Rad;
                        Vector2 testDir = new Vector2(Mathf.Cos(testAngle), Mathf.Sin(testAngle));
                        Vector2 testPos = heartPos2D + testDir * pushingDistance;

                        if (IsValidPushingHGZPosition(mazeData, testPos, MIN_EDGE_CLEARANCE))
                        {
                            pushingDir = testDir;
                            pushingPlacementPos = testPos;
                            goto foundPushingPos;
                        }
                    }
                }
                foundPushingPos:;
            }

            pushingWallPos = new Vector3(pushingPlacementPos.x, pushingPlacementPos.y, -0.4f);
            pushingWallNormal = new Vector3(pushingDir.x, pushingDir.y, 0f);

        }

        /// <summary>
        /// Finds the wall tile closest to a ray (line from origin in direction).
        /// Used when no walls are directly hit by the ray.
        /// </summary>
        private Transform FindClosestWallToRay(Vector2 rayOrigin, Vector2 rayDir, float maxDist)
        {
            // Search for walls in a wide area around the ray
            const float SEARCH_WIDTH = 10f;
            Vector2 rayEnd = rayOrigin + rayDir * maxDist;
            Vector2 rayCenter = (rayOrigin + rayEnd) / 2f;
            float searchRadius = maxDist / 2f + SEARCH_WIDTH;

            Collider[] colliders = Physics.OverlapSphere(
                new Vector3(rayCenter.x, rayCenter.y, 0f),
                searchRadius,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide
            );

            Transform closestWall = null;
            float closestDistToRay = float.MaxValue;

            foreach (var collider in colliders)
            {
                // Only consider wall tiles
                if (!collider.gameObject.name.StartsWith("WorldTile_#")) continue;

                Vector2 wallPos = new Vector2(collider.transform.position.x, collider.transform.position.y);

                // Calculate perpendicular distance from wall to ray
                Vector2 originToWall = wallPos - rayOrigin;
                float projectionLength = Vector2.Dot(originToWall, rayDir);

                // Only consider walls that are along the ray (positive projection, within max distance)
                if (projectionLength < 0 || projectionLength > maxDist) continue;

                Vector2 closestPointOnRay = rayOrigin + rayDir * projectionLength;
                float distToRay = Vector2.Distance(wallPos, closestPointOnRay);

                if (distToRay < closestDistToRay)
                {
                    closestDistToRay = distToRay;
                    closestWall = collider.transform;
                }
            }

            return closestWall;
        }

        private Transform FindClosestNodeBorderWallToRay(Vector2 heartPos, Vector3 rayDir)
        {
            // Find all wall models near the heart node border (NodeWalls_0 contains heart node walls)
            GameObject nodeWallsContainer = GameObject.Find("NodeWalls_0");
            if (nodeWallsContainer == null)
            {
                return null;
            }

            Transform closestWall = null;
            float closestDistToRay = float.MaxValue;

            Vector2 rayDir2D = new Vector2(rayDir.x, rayDir.y).normalized;

            // Iterate through all child wall models
            foreach (Transform child in nodeWallsContainer.transform)
            {
                // Only consider wall tiles (WorldTile_#)
                if (!child.name.StartsWith("WorldTile_#")) continue;

                Vector2 wallPos = new Vector2(child.position.x, child.position.y);

                // Calculate distance from wall to the ray line
                // Ray starts at heart and goes in rayDir direction
                Vector2 heartToWall = wallPos - heartPos;
                float projectionLength = Vector2.Dot(heartToWall, rayDir2D);

                // Only consider walls in the direction of the ray (positive projection)
                if (projectionLength < 0) continue;

                // Point on ray closest to the wall
                Vector2 closestPointOnRay = heartPos + rayDir2D * projectionLength;
                float distToRay = Vector2.Distance(wallPos, closestPointOnRay);

                if (distToRay < closestDistToRay)
                {
                    closestDistToRay = distToRay;
                    closestWall = child;
                }
            }

            return closestWall;
        }

        /// <summary>
        /// Finds the closest wall to a given point, preferring walls on the correct side of the ray.
        /// Used when the ray doesn't directly hit a wall near the focal point.
        /// </summary>
        private Transform FindClosestWallToPoint(Vector2 targetPoint, Vector3 rayDir)
        {
            // Search for walls near the target point using physics
            // Increased radius to find walls even when focal point is on a node
            const float SEARCH_RADIUS = 8f;
            Collider[] colliders = Physics.OverlapSphere(
                new Vector3(targetPoint.x, targetPoint.y, 0f),
                SEARCH_RADIUS,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide
            );

            Transform closestWall = null;
            float closestDist = float.MaxValue;

            foreach (var collider in colliders)
            {
                // Only consider wall tiles
                if (!collider.gameObject.name.StartsWith("WorldTile_#")) continue;

                Vector2 wallPos = new Vector2(collider.transform.position.x, collider.transform.position.y);
                float dist = Vector2.Distance(wallPos, targetPoint);

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestWall = collider.transform;
                }
            }

            return closestWall;
        }

        /// <summary>
        /// Calculates the "into forest" direction by averaging nearby wall normals.
        /// Wall's transform.right points TOWARD the path (front face of wall).
        /// So "into forest" = -transform.right (back of wall).
        /// Filters to only include walls on the correct side using the hint direction.
        /// </summary>
        /// <param name="position">Position to sample walls around</param>
        /// <param name="hintDirection">Expected "into forest" direction (used to filter walls)</param>
        private Vector2 GetIntoForestDirectionForEdge(Vector2 position, Vector2 hintDirection)
        {
            const float SAMPLE_RADIUS = 2.5f;  // Radius to sample nearby walls
            const float MIN_DOT_PRODUCT = 0.0f;  // Only include walls whose "into forest" aligns with hint

            Vector3 pos3D = new Vector3(position.x, position.y, 0f);
            Collider[] nearbyColliders = Physics.OverlapSphere(pos3D, SAMPLE_RADIUS, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            Vector2 sumNormals = Vector2.zero;
            int wallCount = 0;

            foreach (var collider in nearbyColliders)
            {
                // Only include wall tiles
                if (!collider.gameObject.name.StartsWith("WorldTile_#")) continue;

                // Wall's transform.right points TOWARD the path (wall's front face)
                // "Into forest" = OPPOSITE direction = -transform.right
                Vector2 intoForest = new Vector2(-collider.transform.right.x, -collider.transform.right.y);

                // Only include walls whose "into forest" direction aligns with the hint
                // This filters out walls on the opposite side of the path
                float dot = Vector2.Dot(intoForest.normalized, hintDirection.normalized);

                if (dot < MIN_DOT_PRODUCT)
                {
                    continue;
                }

                sumNormals += intoForest;
                wallCount++;
            }

            if (wallCount == 0)
            {
                // Fallback: use the hint direction
                return hintDirection.normalized;
            }

            // Average of "into forest" directions from filtered walls
            Vector2 avgNormal = (sumNormals / wallCount).normalized;
            return avgNormal;
        }

        /// <summary>
        /// Calculates the "into forest" direction for pushing HGZ (near heart/node border).
        /// For node borders, "into forest" = AWAY from heart center (radially outward).
        /// </summary>
        private Vector2 GetIntoForestDirectionForNode(Vector2 position)
        {
            Vector2 heartPos2D = new Vector2(heartNodePosition.x, heartNodePosition.y);
            Vector2 awayFromHeart = (position - heartPos2D).normalized;
            return awayFromHeart;
        }

        /// <summary>
        /// Checks if a candidate position is valid for the pushing HGZ:
        /// must be non-walkable AND at least minClearance distance from any walkable tile.
        /// Samples 12 directions around the position at the clearance radius.
        /// </summary>
        private bool IsValidPushingHGZPosition(WorldSpaceMazeData mazeData, Vector2 pos, float minClearance)
        {
            // Must be non-walkable (in the wall ring, not on a path)
            if (mazeData.IsWalkable(pos)) return false;

            // Check that no walkable tile is within minClearance distance.
            // Sample 12 evenly spaced directions around the candidate position.
            const int CLEARANCE_SAMPLES = 12;
            for (int s = 0; s < CLEARANCE_SAMPLES; s++)
            {
                float sampleAngle = s * (360f / CLEARANCE_SAMPLES) * Mathf.Deg2Rad;
                Vector2 samplePos = pos + new Vector2(
                    Mathf.Cos(sampleAngle), Mathf.Sin(sampleAngle)) * minClearance;
                if (mazeData.IsWalkable(samplePos))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Finds the direction from an HGZ position into the forest (non-walkable area).
        /// The "inside" of the forest is the direction where a point at fixed distance
        /// has the greatest minimum distance from all nearby graph elements (walkable tiles AND node centers).
        /// Node centers are unwalkable but are NOT forest - they are open areas we must avoid.
        /// </summary>
        /// <param name="hgzPosition">The HGZ position on the path</param>
        /// <param name="rayDir">Direction of the focal ray (heart to focal point)</param>
        /// <param name="heartPos">Position of the heart node</param>
        private Vector2 FindForestDirection(Vector2 hgzPosition, Vector2 rayDir, Vector2 heartPos)
        {
            const float ANGLE_STEP = 5f;           // Degrees between test directions
            const float SEARCH_RADIUS = 8f;        // Increased to catch nearby nodes
            const float CANDIDATE_DISTANCE = 3f;   // Distance to place candidate points
            const float TILE_SCAN_STEP = 0.5f;     // Step for finding walkable tiles
            const float NODE_RADIUS = 3.0f;        // Node clearing radius

            // Get the maze data for walkability checks
            var mazeData = manager.MazeGrid?.WorldSpaceMazeData;

            // Default perpendicular (fallback)
            Vector2 perpCCW = new Vector2(-rayDir.y, rayDir.x).normalized;

            if (mazeData == null)
            {
                return perpCCW;
            }

            // Collect all "graph element" positions - both walkable tiles AND node centers
            // We want to find the direction AWAY from all graph elements, not just walkable tiles
            List<Vector2> graphElements = new List<Vector2>();

            // Add walkable tiles
            for (float dx = -SEARCH_RADIUS; dx <= SEARCH_RADIUS; dx += TILE_SCAN_STEP)
            {
                for (float dy = -SEARCH_RADIUS; dy <= SEARCH_RADIUS; dy += TILE_SCAN_STEP)
                {
                    Vector2 testPoint = hgzPosition + new Vector2(dx, dy);
                    if (Vector2.Distance(testPoint, hgzPosition) <= SEARCH_RADIUS && mazeData.IsWalkable(testPoint))
                    {
                        graphElements.Add(testPoint);
                    }
                }
            }

            // Add node centers as graph elements (even though they're unwalkable, they're not forest!)
            var graphState = mazeData.GraphState;
            if (graphState != null && graphState.Nodes != null)
            {
                foreach (var node in graphState.Nodes)
                {
                    Vector2 nodeCenter = node.Position;
                    float distToNode = Vector2.Distance(hgzPosition, nodeCenter);
                    if (distToNode <= SEARCH_RADIUS + NODE_RADIUS)
                    {
                        // Add the node center itself
                        graphElements.Add(nodeCenter);
                        // Also add points around the node center to represent the full node area
                        for (float angle = 0; angle < 360; angle += 45)
                        {
                            float rad = angle * Mathf.Deg2Rad;
                            Vector2 nodeEdgePoint = nodeCenter + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (NODE_RADIUS * 0.5f);
                            graphElements.Add(nodeEdgePoint);
                        }
                    }
                }
            }

            // If no graph elements found, use fallback
            if (graphElements.Count == 0)
            {
                return perpCCW;
            }

            // Test all directions around the HGZ position
            // For each direction, place a candidate point at CANDIDATE_DISTANCE
            // Calculate the minimum distance from that point to any graph element
            // The direction with the greatest minimum distance is the "inside" of the forest
            Vector2 bestDir = perpCCW;
            float bestMinDist = -1f;

            for (float angle = 0f; angle < 360f; angle += ANGLE_STEP)
            {
                float rad = angle * Mathf.Deg2Rad;
                Vector2 testDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                Vector2 candidatePoint = hgzPosition + testDir * CANDIDATE_DISTANCE;

                // Skip if the candidate point itself is walkable (we want forest)
                if (mazeData.IsWalkable(candidatePoint))
                {
                    continue;
                }

                // Skip if candidate point is inside any node's area (node centers are not forest!)
                bool insideNode = false;
                if (graphState != null && graphState.Nodes != null)
                {
                    foreach (var node in graphState.Nodes)
                    {
                        if (Vector2.Distance(candidatePoint, node.Position) < NODE_RADIUS)
                        {
                            insideNode = true;
                            break;
                        }
                    }
                }
                if (insideNode)
                {
                    continue;
                }

                // Find minimum distance from candidate point to any graph element
                float minDistToGraph = float.MaxValue;
                foreach (var element in graphElements)
                {
                    float dist = Vector2.Distance(candidatePoint, element);
                    if (dist < minDistToGraph)
                    {
                        minDistToGraph = dist;
                    }
                }

                // Track the direction with the greatest minimum distance to graph elements
                if (minDistToGraph > bestMinDist)
                {
                    bestMinDist = minDistToGraph;
                    bestDir = testDir;
                }
            }

            return bestDir.normalized;
        }

        /// <summary>
        /// Validates that a position is at least MIN_EDGE_DISTANCE from any path/node edge.
        /// Returns a corrected position if the original is too close to an edge.
        /// For nodes, accounts for node radius (distance to edge = distance to center - radius).
        /// </summary>
        private Vector3 ValidateHGZPosition(Vector3 proposedPos, Vector2 offsetDir, float minEdgeDistance)
        {
            const float CHECK_RADIUS = 6.0f;  // Must be large enough to detect heart node center (radius 3.0 + buffer)
            const float NODE_RADIUS = 3.0f;   // Node radius from MazeRenderer


            // Check if there are any path or node tiles near the proposed position
            Collider[] nearbyColliders = Physics.OverlapSphere(proposedPos, CHECK_RADIUS, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);


            float closestEdgeDist = float.MaxValue;
            string closestName = "";
            int pathCount = 0;
            int nodeCount = 0;

            foreach (var collider in nearbyColliders)
            {
                // Check for path tiles (MazePath tag) or node tiles (MazeNode tag)
                bool isPath = collider.CompareTag("MazePath") || collider.gameObject.name.Contains("PathTile");
                bool isNode = collider.CompareTag("MazeNode") || collider.gameObject.name.Contains("NodeColumn") || collider.gameObject.name.Contains("NodeCylinder");

                if (isPath) pathCount++;
                if (isNode) nodeCount++;

                if (isPath || isNode)
                {
                    float distToCenter = Vector3.Distance(proposedPos, collider.transform.position);
                    float distToEdge;

                    if (isNode)
                    {
                        // For nodes, the edge is NODE_RADIUS away from center
                        distToEdge = distToCenter - NODE_RADIUS;
                    }
                    else
                    {
                        // For path tiles, the center IS approximately the edge (tiles are small)
                        distToEdge = distToCenter;
                    }

                    if (distToEdge < closestEdgeDist)
                    {
                        closestEdgeDist = distToEdge;
                        closestName = collider.gameObject.name;
                    }
                }
            }


            // If we're too close to a path/node edge, push further in the offset direction
            if (closestEdgeDist < minEdgeDistance)
            {
                float additionalOffset = minEdgeDistance - closestEdgeDist + 0.5f;  // Add extra margin
                Vector3 correctedPos = proposedPos + new Vector3(offsetDir.x, offsetDir.y, 0f) * additionalOffset;
                return correctedPos;
            }

            return proposedPos;
        }

        private void FindAffectedGrabbingWalls()
        {
            HeartPowerUtils.FindWallTilesInRadius(grabbingWallPos, graspZoneRadius, affectedGrabbingWalls, originalWallPositions);
        }

        private void UpdateWallShakeEffect()
        {
            if (affectedGrabbingWalls.Count == 0) return;

            // Only shake when in idle or early grab phases (waiting for or grabbing visitor)
            bool shouldShake = grabPhase == GrabPhase.Idle ||
                               grabPhase == GrabPhase.Emerging ||
                               grabPhase == GrabPhase.Extending;

            if (shouldShake)
            {
                HeartPowerUtils.ApplyShakeEffect(affectedGrabbingWalls, originalWallPositions, 0.03f);
            }
            else
            {
                HeartPowerUtils.ResetToOriginalPositions(affectedGrabbingWalls, originalWallPositions);
            }
        }

        private void ResetWallPositions()
        {
            HeartPowerUtils.ResetToOriginalPositions(affectedGrabbingWalls, originalWallPositions);
        }

        private void CreateGrabbingHGZ()
        {
            // Create grabbing zone at wall tile position
            // The wall tile position acts as the "heart" center - tongue emerges vertically from here
            grabbingZoneObject = new GameObject("GrabbingHGZ");
            grabbingZoneObject.transform.position = grabbingWallPos;

            // Add trigger collider for visitor detection
            grabbingCollider = grabbingZoneObject.AddComponent<SphereCollider>();
            grabbingCollider.radius = graspZoneRadius;
            grabbingCollider.isTrigger = true;

            // Add rigidbody for trigger detection
            grabbingZoneObject.AddKinematicRigidbody();

            // The tongue will be spawned as a child of this object when a visitor enters
            // It will emerge vertically from below ground (like HeartOfTheMaze)
            grabbingTongueInstance = null;
            grabbingTongueZPosition = TONGUE_START_Z;
        }

        private void CreatePushingHGZ()
        {
            // Create pushing zone at wall near heart
            pushingZoneObject = new GameObject("PushingHGZ");
            pushingZoneObject.transform.position = pushingWallPos;

            // Add trigger collider
            pushingCollider = pushingZoneObject.AddComponent<SphereCollider>();
            pushingCollider.radius = graspZoneRadius;
            pushingCollider.isTrigger = true;

            // Add rigidbody for trigger detection
            pushingZoneObject.AddKinematicRigidbody();

            // Calculate direction toward heart for push orientation
            Vector3 dirTowardHeart = (heartNodePosition - pushingWallPos).normalized;
            pushingTargetAngle = Mathf.Atan2(dirTowardHeart.y, dirTowardHeart.x) * Mathf.Rad2Deg;

            // Tongue will be spawned when visitor is transported here
            pushingTongueInstance = null;
            pushingTongueZPosition = TONGUE_START_Z;
        }

        /// <summary>
        /// Spawns a tongue instance for grabbing. Based on HeartOfTheMaze.PreCreateTongueInstance().
        /// The tongue emerges vertically from below ground, like HeartOfTheMaze.
        /// </summary>
        private void SpawnGrabbingTongue(Vector3 visitorPos)
        {
            GameObject tonguePrefab = manager.TonguePrefab;
            if (tonguePrefab == null)
            {
                Debug.LogError("[HeartwardGrasp] TonguePrefab is null!");
                return;
            }

            if (grabbingTongueInstance != null)
            {
                Object.Destroy(grabbingTongueInstance);
            }

            // Initial angle toward visitor (will be updated each frame during Extending)
            UpdateGrabbingTargetAngle();

            // Instantiate tongue as a root object (NOT parented to zone object).
            // The zone is at Z=-0.4 (wall depth) which offsets all bone world Z positions,
            // causing FindGroundBoneIndex to fail (bones can't reach TONGUE_BEND_Z within
            // 540 bones when shifted by -0.4). Using Z=0 matches HeartOfTheMaze behavior.
            grabbingTongueInstance = Object.Instantiate(tonguePrefab);
            grabbingTongueInstance.name = "GrabbingTongue";

            // Initialize Z position (below ground, like HeartOfTheMaze)
            grabbingTongueZPosition = TONGUE_START_Z;
            grabbingTongueInstance.transform.position = new Vector3(
                grabbingZoneObject.transform.position.x,
                grabbingZoneObject.transform.position.y,
                grabbingTongueZPosition);

            // Remove any Light components
            foreach (var light in grabbingTongueInstance.GetComponentsInChildren<Light>())
            {
                Object.Destroy(light);
            }

            // Find and store bone references via shared utility
            grabbingTongueData = TongueUtility.SetupTongueBones(grabbingTongueInstance);
            TongueUtility.CacheRestWorldZOffsets(grabbingTongueData, grabbingTongueInstance);

            // Find solid colliders that are baked into the prefab (for collision detection)
            FindGrabbingSolidColliders();
        }

        /// <summary>
        /// Spawns a tongue instance for pushing (releasing visitor near heart).
        /// The tongue emerges from underground with visitor attached to tip, extends, then releases.
        /// This is the reverse of the grabbing sequence.
        /// </summary>
        private void SpawnPushingTongue()
        {
            GameObject tonguePrefab = manager.TonguePrefab;
            if (tonguePrefab == null)
            {
                Debug.LogError("[HeartwardGrasp] TonguePrefab is null!");
                return;
            }

            if (pushingTongueInstance != null)
            {
                Object.Destroy(pushingTongueInstance);
            }

            // Instantiate tongue as a root object (NOT parented to zone object).
            // Same reason as grabbing tongue — zone Z=-0.4 offsets bones, breaking FindGroundBoneIndex.
            pushingTongueInstance = Object.Instantiate(tonguePrefab);
            pushingTongueInstance.name = "PushingTongue";

            // Initialize Z position (below ground, visitor attached - reverse of grab end state)
            pushingTongueZPosition = TONGUE_START_Z;
            pushingTongueInstance.transform.position = new Vector3(
                pushingZoneObject.transform.position.x,
                pushingZoneObject.transform.position.y,
                pushingTongueZPosition);

            // Remove lights
            foreach (var light in pushingTongueInstance.GetComponentsInChildren<Light>())
            {
                Object.Destroy(light);
            }

            // Find bones via shared utility
            pushingTongueData = TongueUtility.SetupTongueBones(pushingTongueInstance);
            TongueUtility.CacheRestWorldZOffsets(pushingTongueData, pushingTongueInstance);
        }


        /// <summary>
        /// Finds colliders baked into the prefab for contact detection and physics blocking.
        /// BoneCollider_N = triggers for contact detection
        /// SolidCollider_N = solid colliders for collision detection (same as HeartOfTheMaze).
        /// </summary>
        private void FindGrabbingSolidColliders()
        {
            // Colliders are now baked into the prefab - just find them
            if (grabbingTongueInstance == null) return;

            var solidColliders = new List<GameObject>();
            Transform[] allTransforms = grabbingTongueInstance.GetComponentsInChildren<Transform>(true);

            foreach (Transform t in allTransforms)
            {
                if (t.name.StartsWith("SolidCollider_"))
                {
                    solidColliders.Add(t.gameObject);
                }
            }

            grabbingSolidColliders = solidColliders.ToArray();

            // Disable colliders initially (enable during Extending phase)
            SetGrabbingSolidCollidersEnabled(false);
        }

        /// <summary>
        /// Enables or disables solid colliders for collision detection.
        /// Same pattern as HeartOfTheMaze.EnableBoneColliders/DisableBoneColliders.
        /// Also sets the static flag so visitors know to check for tongue collision.
        /// </summary>
        private void SetGrabbingSolidCollidersEnabled(bool enabled)
        {
            if (grabbingSolidColliders == null) return;

            foreach (var colliderObj in grabbingSolidColliders)
            {
                if (colliderObj != null)
                {
                    var collider = colliderObj.GetComponent<SphereCollider>();
                    if (collider != null) collider.enabled = enabled;
                }
            }

            // Set static flag so visitors know to check for tongue collision
            IsGraspTongueActiveWithColliders = enabled;
        }

        /// <summary>
        /// Called by visitor when they collide with a tongue bone collider.
        /// This is the signal to grab them - same pattern as HeartOfTheMaze.NotifyVisitorTouchedTongue().
        /// </summary>
        public void NotifyVisitorTouchedGraspTongue(VisitorControllerBase visitor)
        {
            // Only respond during Extending phase
            if (grabPhase != GrabPhase.Extending) return;

            // Only grab our target visitor
            if (visitor != currentVisitor) return;

            TransitionToRetracting();
        }

        /// <summary>
        /// Updates a tongue instance's world Z position (vertical emergence).
        /// </summary>
        private void UpdateTongueZPosition(GameObject tongueInstance, float zPosition)
        {
            if (tongueInstance == null) return;

            // Tongues are root objects (not parented to zone objects), so use world position
            Vector3 pos = tongueInstance.transform.position;
            pos.z = zPosition;
            tongueInstance.transform.position = pos;
        }

        private void CreateParticleSystem(GameObject parent, ref ParticleSystem particles)
        {
            if (parent == null) return;

            // Use shared utility for base setup
            particles = HeartPowerUtils.CreateBasicParticleSystem(
                parent,
                "DebrisParticles",
                parent.transform.position,
                emissionRate: 8f,
                startSize: new Vector2(0.15f, 0.35f),
                startSpeed: new Vector2(0.3f, 0.8f),
                lifetime: new Vector2(2f, 2f),
                color1: LeafGreen,
                color2: BarkBrown,
                shapeRadius: graspZoneRadius * 0.5f
            );

            // Customize beyond base: sphere shape, lower max particles, alpha fade
            var main = particles.main;
            main.maxParticles = 100;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.5f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            // Process grabbing HGZ state machine
            UpdateGrabbingHGZ(deltaTime);

            // Process pushing HGZ state machine
            UpdatePushingHGZ(deltaTime);

            // Update particle emission
            UpdateParticles();

            // Update wall shake effect in grabbing zone
            UpdateWallShakeEffect();
        }

        private void CheckForVisitorsInGrabbingZone()
        {
            if (hasExpired || grabPhase != GrabPhase.Idle) return;

            // Build exclude set from current and pending visitors
            var excludeSet = new HashSet<VisitorControllerBase>(pendingVisitors);
            if (currentVisitor != null) excludeSet.Add(currentVisitor);

            var found = HeartPowerUtils.FindAllVisitorsInRadius(grabbingWallPos, graspZoneRadius, excludeSet);
            foreach (var visitor in found)
            {
                pendingVisitors.Enqueue(visitor);
            }

            if (grabPhase == GrabPhase.Idle && pendingVisitors.Count > 0)
            {
                StartGrabbingVisitor(pendingVisitors.Dequeue());
            }
        }

        private void StartGrabbingVisitor(VisitorControllerBase visitor)
        {
            if (visitor == null || hasExpired) return;

            currentVisitor = visitor;
            // NOTE: Do NOT stop visitor here - they continue walking until tongue contacts them

            // Spawn the tongue
            SpawnGrabbingTongue(visitor.transform.position);

            // Start emerging phase - tongue rises from underground (like HeartOfTheMaze)
            grabPhase = GrabPhase.Emerging;

            if (grabbingParticles != null) grabbingParticles.Emit(25);
        }

        /// <summary>
        /// Resets the grabbing tongue to rest state.
        /// </summary>
        private void ResetGrabbingTongue()
        {
            TongueUtility.ResetBonesToRest(grabbingTongueData);
        }

        /// <summary>
        /// Destroys a tongue instance and clears its bone data.
        /// </summary>
        private static void DestroyTongueInstance(ref GameObject tongueInstance, ref TongueBoneData tongueData)
        {
            if (tongueInstance != null)
            {
                Object.Destroy(tongueInstance);
                tongueInstance = null;
            }
            tongueData = null;
        }

        /// <summary>
        /// Destroys the grabbing tongue instance and cleans up colliders.
        /// </summary>
        private void DestroyGrabbingTongue()
        {
            SetGrabbingSolidCollidersEnabled(false);
            grabbingSolidColliders = null;
            DestroyTongueInstance(ref grabbingTongueInstance, ref grabbingTongueData);
        }

        private void UpdateGrabbingHGZ(float deltaTime)
        {
            // Check for visitors when idle
            if (grabPhase == GrabPhase.Idle)
            {
                CheckForVisitorsInGrabbingZone();
                return;
            }

            if (currentVisitor == null)
            {
                DestroyGrabbingTongue();
                grabPhase = GrabPhase.Idle;
                return;
            }

            switch (grabPhase)
            {
                case GrabPhase.Emerging:
                    UpdateGrabEmergingPhase(deltaTime);
                    break;

                case GrabPhase.Extending:
                    UpdateGrabExtendingPhase(deltaTime);
                    break;

                case GrabPhase.Retracting:
                    UpdateGrabRetractingPhase(deltaTime);
                    break;
            }
        }

        /// <summary>
        /// Emerging phase: Tongue rises from underground.
        /// Transitions to Extending when tip reaches ground level.
        /// Same as HeartOfTheMaze.
        /// </summary>
        private void UpdateGrabEmergingPhase(float deltaTime)
        {
            if (grabbingTongueInstance == null || currentVisitor == null) return;

            // Move tongue up (-Z is up in this coordinate system)
            grabbingTongueZPosition -= tongueEmergeSpeed * deltaTime;
            UpdateTongueZPosition(grabbingTongueInstance, grabbingTongueZPosition);

            // Keep tongue straight (bones at rest pose)
            ApplyGrabbingTongueBoneState();

            // Use actual tip bone world Z (not mathematical estimate) to match FindGroundBoneIndex
            int tipIdx = grabbingTongueData.Bones.Length - 1;
            float tipWorldZ = grabbingTongueData.Bones[tipIdx].position.z;

            // Transition to Extending when tip reaches bend level (Z=-0.25)
            if (tipWorldZ <= TONGUE_BEND_Z)
            {
                grabPhase = GrabPhase.Extending;

                // Enable colliders for collision detection
                SetGrabbingSolidCollidersEnabled(true);
            }
        }

        /// <summary>
        /// Extending phase: Tongue continues rising, bones bend 90Â° at ground level.
        /// Tongue tracks visitor position continuously.
        /// Transition to Retracting happens when visitor collides with tongue (NotifyVisitorTouchedGraspTongue).
        /// Same as HeartOfTheMaze.
        /// </summary>
        private void UpdateGrabExtendingPhase(float deltaTime)
        {
            if (grabbingTongueInstance == null || currentVisitor == null) return;

            // Continue rising (this increases horizontal extension)
            grabbingTongueZPosition -= tongueEmergeSpeed * deltaTime;
            UpdateTongueZPosition(grabbingTongueInstance, grabbingTongueZPosition);

            // Update target angle to track visitor (like HeartOfTheMaze)
            UpdateGrabbingTargetAngle();

            // Apply bone rotations (bend at ground level, extend toward visitor)
            ApplyGrabbingTongueBoneState();
        }

        /// <summary>
        /// Retracting phase: Visitor is grabbed, tongue descends back underground.
        /// Visitor is attached to tongue tip and follows it down.
        /// Transitions to transport when fully retracted.
        /// Same as HeartOfTheMaze.
        /// </summary>
        private void UpdateGrabRetractingPhase(float deltaTime)
        {
            if (grabbingTongueInstance == null || currentVisitor == null) return;

            // Retract tongue (increase Z)
            grabbingTongueZPosition += tongueRetractSpeed * deltaTime;
            UpdateTongueZPosition(grabbingTongueInstance, grabbingTongueZPosition);

            // Apply bone rotations
            ApplyGrabbingTongueBoneState();

            // Move visitor with tongue tip
            MoveGrabbedVisitorToTip();

            // Check if visitor has reached the bend point (ground level where tongue enters ground)
            // Transport when visitor Z position drops below ground level (entering the ground)
            // This is when the tongue tip crosses below Z=0
            if (currentVisitor != null)
            {
                float visitorZ = currentVisitor.transform.position.z;
                // Transport when visitor goes below ground (Z > 0 in this coordinate system where +Z is down)
                // Actually, the tip bone world position determines this - when tip is below ground
                if (grabbingTongueData.Bones != null && grabbingTongueData.Bones.Length > 0)
                {
                    int tipIndex = grabbingTongueData.Bones.Length - 1;
                    Transform tipBone = grabbingTongueData.Bones[tipIndex];
                    if (tipBone != null)
                    {
                        // In this coordinate system, -Z is up, +Z is down
                        // Ground level is Z=0, below ground is Z>0
                        // Transport when tongue tip goes below ground (tip Z > 0.5 to give some margin)
                        float tipWorldZ = tipBone.position.z;
                        if (tipWorldZ > 0.5f)
                        {
                            TransportVisitorToPushingZone();
                            return;
                        }
                    }
                }
            }

            // Fallback: transport if fully retracted (safety check)
            if (grabbingTongueZPosition >= TONGUE_START_Z)
            {
                TransportVisitorToPushingZone();
            }
        }

        /// <summary>
        /// Transitions from Extending to Retracting phase.
        /// Called when visitor collides with tongue.
        /// Same pattern as HeartOfTheMaze.TransitionToGrabbing().
        /// </summary>
        private void TransitionToRetracting()
        {
            if (currentVisitor == null) return;

            // Capture the visitor's transform relative to the tongue chain direction at grab time.
            // Uses chain direction (lookback bone → tip bone) instead of tipBone.rotation because
            // ApplyBoneRotations keeps the tip horizontal until the very end of retraction.
            if (grabbingTongueData?.Bones != null && grabbingTongueData.Bones.Length > 0)
            {
                int tipIndex = grabbingTongueData.Bones.Length - 1;
                Transform tipBone = grabbingTongueData.Bones[tipIndex];
                if (tipBone != null)
                {
                    Quaternion chainRot = TongueUtility.GetChainRotationAtTip(grabbingTongueData);
                    Quaternion inverseChainRot = Quaternion.Inverse(chainRot);

                    // Force identity rotation so visitor stays upright during retraction
                    // (matches pushing tongue's behavior after pushVisitorRealigned)
                    grabRotationOffset = inverseChainRot * Quaternion.identity;

                    // Use visitor visual center instead of root (feet) position.
                    // The visitor's transform.position is at their feet (Z≈-0.01),
                    // but the tongue tip is at Z≈-0.25 (bend level). Using feet position
                    // causes the tongue to appear to grab the visitor at their feet.
                    const float VISITOR_HALF_HEIGHT = 0.3f;
                    Vector3 visitorCenter = currentVisitor.transform.position + new Vector3(0f, 0f, -VISITOR_HALF_HEIGHT);
                    grabPositionOffset = inverseChainRot * (visitorCenter - tipBone.position);
                }
            }

            // Stop visitor movement and set grabbed state (like HeartOfTheMaze)
            currentVisitor.SetGrabbedByHeart();

            // Deduct essence cost
            currentVisitor.DeductEssence(grabEssenceCost);

            // Notify nearby visitors
            HeartPowerEvents.NotifyVisitorGrabbedByGrasp(currentVisitor.transform.position);

            grabPhase = GrabPhase.Retracting;

            if (grabbingParticles != null) grabbingParticles.Emit(20);
        }

        /// <summary>
        /// Updates the target angle to track visitor position.
        /// Called each frame during Extending phase.
        /// Same as HeartOfTheMaze.UpdateTargetAngle().
        /// </summary>
        private void UpdateGrabbingTargetAngle()
        {
            if (currentVisitor == null) return;

            Vector2 wallPos2D = new Vector2(grabbingWallPos.x, grabbingWallPos.y);
            Vector2 visitorPos2D = new Vector2(currentVisitor.transform.position.x, currentVisitor.transform.position.y);
            Vector2 dirToVisitor = (visitorPos2D - wallPos2D).normalized;

            if (dirToVisitor.sqrMagnitude > 0.001f)
            {
                grabbingTargetAngle = Mathf.Atan2(dirToVisitor.y, dirToVisitor.x) * Mathf.Rad2Deg;
            }
        }

        /// <summary>
        /// Moves the grabbed visitor to the tongue tip bone.
        /// Called each frame during Retracting phase.
        /// Uses the relative offset captured at grab time so the visitor
        /// maintains its orientation at capture and rotates with the bone
        /// as the tongue retracts underground.
        /// Same pattern as HeartOfTheMaze.MoveVisitorToTip().
        /// </summary>
        private void MoveGrabbedVisitorToTip()
        {
            if (currentVisitor == null || grabbingTongueData == null || grabbingTongueData.Bones == null || grabbingTongueData.Bones.Length == 0) return;

            int tipIndex = grabbingTongueData.Bones.Length - 1;
            Transform tipBone = grabbingTongueData.Bones[tipIndex];

            if (tipBone != null)
            {
                Quaternion chainRot = TongueUtility.GetChainRotationAtTip(grabbingTongueData);
                currentVisitor.transform.position = tipBone.position + chainRot * grabPositionOffset;
                // Force identity rotation every frame so visitor stays upright
                // throughout retraction, matching pushing tongue behavior
                currentVisitor.transform.rotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Transports the visitor from grabbing zone to pushing zone.
        /// Called when tongue is fully retracted.
        /// Spawns the pushing tongue immediately and positions visitor at its tip.
        /// </summary>
        private void TransportVisitorToPushingZone()
        {
            // Hide visitor during transport (they stay hidden until tongue emerges above ground)
            SetVisitorVisible(currentVisitor, false);

            // Destroy grabbing tongue
            DestroyGrabbingTongue();

            // Spawn pushing tongue immediately (starts underground)
            SpawnPushingTongue();

            // Apply initial tongue state (straight, pointing up)
            ApplyPushingTongueBoneState();

            // Capture the visitor's transform relative to the pushing tongue chain direction.
            // The visitor starts at identity relative to the tip; as the tongue bends
            // at ground level the visitor rotates from vertical to horizontal naturally.
            if (pushingTongueData?.Bones != null && pushingTongueData.Bones.Length > 0 && currentVisitor != null)
            {
                int tipIndex = pushingTongueData.Bones.Length - 1;
                Transform tipBone = pushingTongueData.Bones[tipIndex];
                if (tipBone != null)
                {
                    // Place visitor at tip (offset by half-height so tongue binds to visual center)
                    const float VISITOR_HALF_HEIGHT = 0.3f;
                    currentVisitor.transform.position = tipBone.position + new Vector3(0f, 0f, -VISITOR_HALF_HEIGHT);
                    currentVisitor.SyncRigidbodyPosition();  // Prevent physics snap-back to grab location
                    Quaternion chainRot = TongueUtility.GetChainRotationAtTip(pushingTongueData);
                    Quaternion inverseChainRot = Quaternion.Inverse(chainRot);
                    grabRotationOffset = inverseChainRot * Quaternion.identity;
                    grabPositionOffset = inverseChainRot * (currentVisitor.transform.position - tipBone.position);
                }
            }

            // Start Emerging phase immediately (no pause needed)
            pushPhase = PushPhase.Emerging;
            pushPhaseStartTime = elapsedTime;
            pushVisitorRealigned = false;

            if (pushingParticles != null) pushingParticles.Emit(25);

            grabPhase = GrabPhase.Idle;
        }

        /// <summary>
        /// Applies bone rotations to the grabbing tongue based on current phase.
        /// Simplified to match HeartOfTheMaze - bones below ground straight, bend at ground, extend horizontally.
        /// </summary>
        private void ApplyGrabbingTongueBoneState()
        {
            if (grabbingTongueData == null || grabbingTongueData.Bones == null || grabbingTongueData.Bones.Length == 0 || grabbingTongueInstance == null) return;

            // Update tongue instance rotation to point toward visitor
            // Tongue is a root object (not parented), so use world rotation
            grabbingTongueInstance.transform.rotation =
                Quaternion.Euler(0f, 0f, grabbingTargetAngle) * Quaternion.Euler(0f, -90f, 0f);

            // During Emerging phase, keep all bones at rest pose (straight tongue)
            if (grabPhase == GrabPhase.Emerging)
            {
                TongueUtility.ResetBonesToRest(grabbingTongueData);
                return;
            }

            // Find which bone is at the bend level (Z=-0.25, middle of wall models)
            int groundBoneIndex = TongueUtility.FindGroundBoneIndex(grabbingTongueData, TONGUE_BEND_Z, grabbingTongueInstance);

            if (groundBoneIndex < 0)
            {
                TongueUtility.ResetBonesToRest(grabbingTongueData);
                return;
            }

            TongueUtility.ApplyBoneRotations(grabbingTongueData, groundBoneIndex, grabbingTargetAngle);
        }

        private void UpdatePushingHGZ(float deltaTime)
        {
            if (pushPhase == PushPhase.Idle) return;
            if (currentVisitor == null)
            {
                DestroyPushingTongue();
                pushPhase = PushPhase.Idle;
                return;
            }

            float phaseElapsed = elapsedTime - pushPhaseStartTime;

            switch (pushPhase)
            {
                case PushPhase.Emerging:
                    // Tongue rises from underground with visitor on tip
                    UpdatePushEmergingPhase(deltaTime);
                    break;

                case PushPhase.Extending:
                    // Tongue continues rising, extending toward walkable area
                    UpdatePushExtendingPhase(deltaTime);
                    break;

                case PushPhase.Releasing:
                    // Visitor released, tongue starts retracting
                    UpdatePushReleasingPhase(deltaTime);
                    break;

                case PushPhase.Retracting:
                    // Tongue retracts back underground
                    UpdatePushRetractingPhase(deltaTime);
                    break;
            }
        }

        /// <summary>
        /// Emerging phase (pushing): Tongue rises from underground with visitor attached to tip.
        /// Reverse of grab's Retracting phase.
        /// Transitions to Extending when tip reaches ground level.
        /// </summary>
        private void UpdatePushEmergingPhase(float deltaTime)
        {
            if (pushingTongueInstance == null || currentVisitor == null) return;

            // Move tongue up (-Z is up)
            pushingTongueZPosition -= tongueEmergeSpeed * deltaTime;
            UpdateTongueZPosition(pushingTongueInstance, pushingTongueZPosition);

            // Keep tongue straight during emergence
            ApplyPushingTongueBoneState();

            // Move visitor with tongue tip
            MovePushedVisitorToTip();

            // Use actual tip bone world Z (not mathematical estimate) to match FindGroundBoneIndex
            int tipIdx = pushingTongueData.Bones.Length - 1;
            float tipWorldZ = pushingTongueData.Bones[tipIdx].position.z;

            // Transition to Extending when tip reaches bend point (middle of wall model)
            if (tipWorldZ <= TONGUE_BEND_Z)
            {
                pushPhase = PushPhase.Extending;
                pushPhaseStartTime = elapsedTime;

                // Make visitor visible now that they're above ground
                SetVisitorVisible(currentVisitor, true);
            }
        }

        /// <summary>
        /// Extending phase (pushing): Tongue continues rising, bones bend at ground to extend horizontally.
        /// Reverse of grab's Extending phase.
        /// Transitions to Releasing when visitor is over walkable area.
        /// </summary>
        private void UpdatePushExtendingPhase(float deltaTime)
        {
            if (pushingTongueInstance == null || currentVisitor == null) return;

            // Continue rising (increases horizontal extension)
            pushingTongueZPosition -= tongueEmergeSpeed * deltaTime;
            UpdateTongueZPosition(pushingTongueInstance, pushingTongueZPosition);

            // Apply bone rotations (bend at ground level, extend toward heart)
            ApplyPushingTongueBoneState();

            // Once the tip bone is past the bend zone (fully horizontal), realign the
            // visitor to identity rotation.  Before this point the chain rotation carries
            // the visitor through the 90° bend naturally; after this point we want the
            // visitor upright (model up = -Z) while riding the horizontal tongue section.
            if (!pushVisitorRealigned && pushingTongueData?.Bones != null && pushingTongueData.Bones.Length > 0)
            {
                int lipBoneIndex = TongueUtility.FindGroundBoneIndex(pushingTongueData, TONGUE_BEND_Z, pushingTongueInstance);

                int tipIndex = pushingTongueData.Bones.Length - 1;
                // Tip is past bend zone when lipBoneIndex + BEND_BONE_COUNT < tipIndex
                if (lipBoneIndex >= 0 && lipBoneIndex + BEND_BONE_COUNT < tipIndex)
                {
                    Transform tipBone = pushingTongueData.Bones[tipIndex];
                    if (tipBone != null)
                    {
                        // Snap visitor to identity rotation and store world-space offset from tip.
                        // After this point, MovePushedVisitorToTip uses the world offset directly
                        // instead of chain-relative offset, so the visitor stays upright even as
                        // the chain rotation changes frame to frame.
                        currentVisitor.transform.rotation = Quaternion.identity;
                        pushWorldPositionOffset = currentVisitor.transform.position - tipBone.position;
                        // Zero the Z component: the tongue's parent is at Z=-0.4 (wall depth),
                        // which shifts bone world positions and accumulates in the offset via
                        // chain rotation during the bend. The visitor should ride at the tip
                        // bone's actual Z level, not offset by the parent's Z position.
                        pushWorldPositionOffset.z = 0f;
                        pushVisitorRealigned = true;
                    }
                }
            }

            // Move visitor with tongue tip
            MovePushedVisitorToTip();

            // Check if visitor is over walkable area
            Vector3 visitorPos = currentVisitor.transform.position;
            if (IsVisitorOnValidWalkableArea(visitorPos))
            {
                TransitionToReleasing();
            }
        }

        /// <summary>
        /// Transitions to Releasing phase - releases visitor and starts retraction.
        /// </summary>
        private void TransitionToReleasing()
        {
            // Resume visitor movement
            if (currentVisitor != null)
            {
                Vector3 posBeforeRelease = currentVisitor.transform.position;

                // CRITICAL: Set visitor to ground level Z before releasing
                // The tongue tip may be slightly above or below ground, but visitors must be at Zâ‰ˆ0
                const float GROUND_Z = -0.01f;  // Slightly above ground (Z=0 is ground, -Z is up)
                currentVisitor.transform.position = new Vector3(
                    posBeforeRelease.x,
                    posBeforeRelease.y,
                    GROUND_Z
                );

                // Reset rotation to identity - the chain rotation during tongue carry
                // leaves the visitor rotated to match tongue curvature; on release they
                // should be upright (model up = -Z world = Quaternion.identity)
                currentVisitor.transform.rotation = Quaternion.identity;

                // Ensure visitor is visible when released
                SetVisitorVisible(currentVisitor, true);

                // CRITICAL: Clear the Grabbed state before resuming
                // RefreshStateFromFlags() returns early for Grabbed state, so we must clear it first
                currentVisitor.ClearGrabbedState();

                // IMPORTANT: Update destination to the heart position
                // The visitor was transported closer to the heart by the pushing HGZ.
                // Their original destination may now be behind them (especially if it was
                // a short-distance destination like "3 units past grabbing HGZ").
                // Setting destination to heart ensures they continue inward after being pushed.
                currentVisitor.SetWorldDestination(heartNodePosition);

                // If path to heart failed, try recalculating - visitor might be at an edge position
                int pathIndex;
                var path = currentVisitor.GetCurrentPath(out pathIndex);
                if (path == null || path.Count == 0)
                {
                    currentVisitor.RecalculatePath();
                }

                currentVisitor.Resume();

                // Apply daze effect
                float dazeDuration = definition.param1 > 0 ? definition.param1 : 2f;
                currentVisitor.OnWitnessMazeGrowth(dazeDuration);

                // Notify of push
                HeartPowerEvents.NotifyVisitorPushedByGrasp(currentVisitor.transform.position);
            }

            pushPhase = PushPhase.Releasing;
            pushPhaseStartTime = elapsedTime;

            if (pushingParticles != null) pushingParticles.Emit(15);
        }

        /// <summary>
        /// Releasing phase: Brief pause after releasing visitor.
        /// Transitions to Retracting quickly.
        /// </summary>
        private void UpdatePushReleasingPhase(float deltaTime)
        {
            // Brief pause (0.2 seconds) then start retracting
            float phaseElapsed = elapsedTime - pushPhaseStartTime;
            if (phaseElapsed >= 0.2f)
            {
                pushPhase = PushPhase.Retracting;
                pushPhaseStartTime = elapsedTime;
            }
        }

        /// <summary>
        /// Retracting phase (pushing): Tongue descends back underground.
        /// Reverse of grab's Emerging phase.
        /// Finalizes capture when fully retracted.
        /// </summary>
        private void UpdatePushRetractingPhase(float deltaTime)
        {
            if (pushingTongueInstance == null) return;

            // Retract tongue (increase Z)
            pushingTongueZPosition += tongueRetractSpeed * deltaTime;
            UpdateTongueZPosition(pushingTongueInstance, pushingTongueZPosition);

            // Apply bone rotations
            ApplyPushingTongueBoneState();

            // Check if fully retracted
            if (pushingTongueZPosition >= TONGUE_START_Z)
            {
                FinalizeCapture();
            }
        }


        /// <summary>
        /// Moves the visitor being pushed to the tongue tip position.
        /// </summary>
        private void MovePushedVisitorToTip()
        {
            if (currentVisitor == null || pushingTongueData == null || pushingTongueData.Bones == null || pushingTongueData.Bones.Length == 0) return;

            int tipIndex = pushingTongueData.Bones.Length - 1;
            Transform tipBone = pushingTongueData.Bones[tipIndex];

            if (tipBone != null)
            {
                if (pushVisitorRealigned)
                {
                    // After realignment: visitor stays upright, position tracks tip with fixed world offset
                    currentVisitor.transform.position = tipBone.position + pushWorldPositionOffset;
                    currentVisitor.transform.rotation = Quaternion.identity;
                }
                else
                {
                    // Before realignment: chain rotation carries visitor through the bend
                    Quaternion chainRot = TongueUtility.GetChainRotationAtTip(pushingTongueData);
                    currentVisitor.transform.position = tipBone.position + chainRot * grabPositionOffset;
                    currentVisitor.transform.rotation = chainRot * grabRotationOffset;
                }

                // Keep Rigidbody in sync to prevent teleport on release
                currentVisitor.SyncRigidbodyPosition();
            }
        }

        /// <summary>
        /// Applies bone rotations to the pushing tongue.
        /// Same logic as grabbing tongue - bones below ground are straight,
        /// bones at ground level bend 90Â°, bones above ground extend horizontally toward heart.
        /// Uses the same ApplyTongueBoneRotations logic as the grabbing tongue.
        /// </summary>
        private void ApplyPushingTongueBoneState()
        {
            if (pushingTongueData == null || pushingTongueData.Bones == null || pushingTongueData.Bones.Length == 0 || pushingTongueInstance == null) return;

            // Update tongue instance rotation to point toward heart
            // Tongue is a root object (not parented), so use world rotation
            pushingTongueInstance.transform.rotation =
                Quaternion.Euler(0f, 0f, pushingTargetAngle) * Quaternion.Euler(0f, -90f, 0f);

            // Find which bone is at the bend level (Z=-0.25, middle of wall models)
            int groundBoneIndex = TongueUtility.FindGroundBoneIndex(pushingTongueData, TONGUE_BEND_Z, pushingTongueInstance);

            if (groundBoneIndex < 0)
            {
                TongueUtility.ResetBonesToRest(pushingTongueData);
                return;
            }

            TongueUtility.ApplyBoneRotations(pushingTongueData, groundBoneIndex, pushingTargetAngle);
        }

        /// <summary>
        /// Destroys the pushing tongue instance and cleans up.
        /// </summary>
        private void DestroyPushingTongue()
        {
            DestroyTongueInstance(ref pushingTongueInstance, ref pushingTongueData);
        }

        private void FinalizeCapture()
        {
            if (currentVisitor != null)
            {
                currentVisitor.Resume();
                currentVisitor.RecalculatePath();
            }

            OnVisitorConsumed();

            currentVisitor = null;
            pushPhase = PushPhase.Idle;

            // Destroy the pushing tongue
            DestroyPushingTongue();
        }

        /// <summary>
        /// Checks if the visitor position is on a valid walkable area using maze data as the authoritative source.
        /// </summary>
        private bool IsVisitorOnValidWalkableArea(Vector3 visitorPos)
        {
            // Use maze data as the authoritative source for walkability
            var mazeData = manager?.MazeGrid?.WorldSpaceMazeData;
            if (mazeData == null)
            {
                return false;
            }

            Vector2 pos2D = new Vector2(visitorPos.x, visitorPos.y);
            return mazeData.IsWalkable(pos2D);
        }

        private void SetVisitorVisible(VisitorControllerBase visitor, bool visible)
        {
            HeartPowerUtils.SetVisitorVisible(visitor, visible);
        }

        private void UpdateParticles()
        {
            bool grabActive = grabPhase != GrabPhase.Idle;
            bool pushActive = pushPhase != PushPhase.Idle;

            if (grabbingParticles != null)
            {
                var emission = grabbingParticles.emission;
                emission.rateOverTime = grabActive ? 25f : 8f;
            }

            if (pushingParticles != null)
            {
                var emission = pushingParticles.emission;
                emission.rateOverTime = pushActive ? 25f : 8f;
            }
        }

        public override void OnEnd()
        {
            // Unregister frightening event
            if (currentFrighteningEvent != null && FrighteningEventManager.Instance != null)
            {
                FrighteningEventManager.Instance.UnregisterEvent(currentFrighteningEvent);
                currentFrighteningEvent = null;
            }

            // Clear static instance reference
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }

            // Clear tongue references and destroy tongue instances
            DestroyGrabbingTongue();
            DestroyPushingTongue();

            if (grabbingZoneObject != null)
            {
                Object.Destroy(grabbingZoneObject);
                grabbingZoneObject = null;
            }

            if (pushingZoneObject != null)
            {
                Object.Destroy(pushingZoneObject);
                pushingZoneObject = null;
            }

            if (currentVisitor != null)
            {
                SetVisitorVisible(currentVisitor, true);
                currentVisitor.Resume();
                currentVisitor = null;
            }

            pendingVisitors.Clear();

            // Reset wall positions before cleanup
            ResetWallPositions();
            affectedGrabbingWalls.Clear();
            originalWallPositions.Clear();

            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.HeartwardGrasp);
            }
        }

        public override void ApplyWorldOffset(Vector3 worldOffset)
        {
            targetPosition += worldOffset;
            heartNodePosition += worldOffset;
            grabbingWallPos += worldOffset;
            pushingWallPos += worldOffset;

            if (grabbingZoneObject != null)
                grabbingZoneObject.transform.position += worldOffset;
            if (pushingZoneObject != null)
                pushingZoneObject.transform.position += worldOffset;

            // Tongues are root objects (not parented to zone objects), so shift independently
            if (grabbingTongueInstance != null)
                grabbingTongueInstance.transform.position += worldOffset;
            if (pushingTongueInstance != null)
                pushingTongueInstance.transform.position += worldOffset;
        }

        /// <summary>
        /// Returns the world positions of both grabbing and pushing zones.
        /// Used by WaryWayfarer for hazard avoidance pathfinding.
        /// </summary>
        public List<Vector3> GetZonePositions()
        {
            var positions = new List<Vector3>();
            if (grabbingZoneObject != null)
                positions.Add(grabbingZoneObject.transform.position);
            if (pushingZoneObject != null)
                positions.Add(pushingZoneObject.transform.position);
            return positions;
        }
    }

    #endregion
}
