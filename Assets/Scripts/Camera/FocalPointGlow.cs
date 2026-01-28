using UnityEngine;
using FaeMaze.Systems;
using System.Collections.Generic;

namespace FaeMaze.Cameras
{
    /// <summary>
    /// Creates a surface of revolution indicator at the focal point position.
    /// The surface follows the curve z = -1/(10*r^1.5) rotated around the focal point.
    /// Uses additive blending with purple-to-blue gradient and animated ruby energy bolts.
    /// </summary>
    public class FocalPointGlow : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the MazeGridBehaviour")]
        private MazeGridBehaviour mazeGridBehaviour;

        [SerializeField]
        [Tooltip("Reference to the focal point transform")]
        private Transform focalPointTransform;

        [Header("Surface Settings")]
        [SerializeField]
        [Tooltip("Enable focal point indicator")]
        private bool enableIndicator = true;

        [SerializeField]
        [Tooltip("Maximum radius of the surface (outer edge)")]
        private float maxRadius = 2.0f;

        [SerializeField]
        [Tooltip("Minimum radius of the surface (inner edge, smaller = deeper cone)")]
        private float minRadius = 0.01f;


        [SerializeField]
        [Tooltip("Maximum Z clamp (furthest from ground, toward camera)")]
        private float maxZ = -10.0f;


        [Header("Energy Bolt Settings")]
        [SerializeField]
        [Tooltip("Number of energy bolts")]
        private int numBolts = 6;

        [SerializeField]
        [Tooltip("Speed of bolt movement")]
        private float boltSpeed = 2.0f;

        #endregion

        #region Private Fields

        private GameObject surfaceObject;

        // Energy bolt system
        private List<LineRenderer> energyBolts = new List<LineRenderer>();
        private List<float> boltProgress = new List<float>(); // 0-1 progress along path
        private List<float> boltAngle = new List<float>(); // Radial angle
        private List<float> boltDirection = new List<float>(); // +1 or -1 for direction

        // Dynamic z-clamp based on location (fog vs walkable)
        private bool isOverWalkableArea = true;
        private const float GROUND_Z_LEVEL = 0f;
        private const float FOG_Z_LEVEL = -1f;

        // Colors - alternating dark red and purple
        private static readonly Color deepPurple = new Color(0.4f, 0.1f, 0.6f, 1f);
        private static readonly Color darkRed = new Color(0.6f, 0.1f, 0.15f, 1f);

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Find references if not set
            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            if (focalPointTransform == null)
            {
                // Try to find it from CameraController3D
                var cameraController = FindFirstObjectByType<CameraController3D>();
                if (cameraController != null)
                {
                    // Use this GameObject's transform as the focal point
                    focalPointTransform = transform;
                }
            }

            CreateSurface();
            CreateEnergyBolts();
        }

        private void Update()
        {
            if (enableIndicator && surfaceObject != null && focalPointTransform != null)
            {
                UpdateZClampBasedOnLocation();
                UpdateSurfacePosition();
                UpdateEnergyBolts();
            }
        }

        /// <summary>
        /// Checks if the focal point is over a walkable tile.
        /// Over walkable area: extend to ground (z=0)
        /// Over fog area: only show cone below fog level (z less than -1)
        /// </summary>
        private void UpdateZClampBasedOnLocation()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
            {
                isOverWalkableArea = true;
                return;
            }

            Vector2 focalPos2D = new Vector2(focalPointTransform.position.x, focalPointTransform.position.y);

            // Check if focal point is over a walkable tile
            isOverWalkableArea = false;
            var mazeData = mazeGridBehaviour.WorldSpaceMazeData;

            foreach (var tile in mazeData.Tiles)
            {
                if (!tile.Walkable) continue;

                float halfSize = tile.Size * 0.5f;
                float distSq = (tile.Position - focalPos2D).sqrMagnitude;

                // Check if focal point is within tile bounds (using half-size as radius)
                if (distSq <= halfSize * halfSize)
                {
                    isOverWalkableArea = true;
                    break;
                }
            }
        }

        private void OnDestroy()
        {
            // Clean up bolt objects
            foreach (var bolt in energyBolts)
            {
                if (bolt != null)
                {
                    if (bolt.material != null)
                        Destroy(bolt.material);
                    Destroy(bolt.gameObject);
                }
            }
            energyBolts.Clear();

            // Clean up branch objects
            foreach (var branches in boltBranches)
            {
                foreach (var branch in branches)
                {
                    if (branch != null)
                    {
                        if (branch.material != null)
                            Destroy(branch.material);
                        Destroy(branch.gameObject);
                    }
                }
            }
            boltBranches.Clear();
        }

        #endregion

        #region Surface Creation

        private void CreateSurface()
        {
            if (!enableIndicator)
                return;

            // Create empty game object as container for energy bolts (no mesh/rings)
            surfaceObject = new GameObject("FocalPointSurface");
            surfaceObject.transform.SetParent(transform, false);
        }

        private const float EXPONENT = 1.5f;
        private const float SCALE_FACTOR = 10f; // z = -1 / (SCALE_FACTOR * r^exp)

        private float CalculateZForRadius(float radius)
        {
            if (radius <= 0.001f)
                return maxZ;

            // z = -1 / (SCALE_FACTOR * r^1.5)
            float rPow = Mathf.Pow(radius, EXPONENT);
            return -1f / (SCALE_FACTOR * rPow);
        }

        private float CalculateRadiusForZ(float z)
        {
            // z = -1 / (SCALE_FACTOR * r^1.5)
            // SCALE_FACTOR * r^1.5 = -1/z
            // r^1.5 = -1 / (SCALE_FACTOR * z)
            // r = (-1 / (SCALE_FACTOR * z))^(1/1.5)
            if (Mathf.Abs(z) < 0.001f)
                return maxRadius;

            float rPow = -1f / (SCALE_FACTOR * z);
            if (rPow <= 0)
                return maxRadius;

            return Mathf.Pow(rPow, 1f / EXPONENT);
        }

        #endregion

        #region Surface Updates

        private void UpdateSurfacePosition()
        {
            // Position the surface at the focal point's X/Y with Z=0
            Vector3 focalPos = focalPointTransform.position;
            surfaceObject.transform.position = new Vector3(focalPos.x, focalPos.y, 0f);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the focal point transform to track.
        /// </summary>
        public void SetFocalPointTransform(Transform focal)
        {
            focalPointTransform = focal;
        }

        /// <summary>
        /// Sets the maze grid behaviour reference.
        /// </summary>
        public void SetMazeGridBehaviour(MazeGridBehaviour grid)
        {
            mazeGridBehaviour = grid;
        }

        #endregion

        #region Energy Bolts

        // Lightning branch system
        private List<List<LineRenderer>> boltBranches = new List<List<LineRenderer>>();
        private const int BRANCHES_PER_BOLT = 4;
        private const int MAIN_BOLT_POINTS = 12;
        private const int BRANCH_POINTS = 5;

        // Lightning state - regenerated periodically for jagged appearance
        private List<float[]> boltJitterOffsets = new List<float[]>();
        private List<float> boltRegenerateTime = new List<float>();
        private const float BOLT_REGENERATE_INTERVAL = 0.08f; // Regenerate shape frequently

        private void CreateEnergyBolts()
        {
            if (!enableIndicator)
                return;

            for (int i = 0; i < numBolts; i++)
            {
                // Create main bolt game object with LineRenderer
                GameObject boltObj = new GameObject($"EnergyBolt_{i}");
                boltObj.transform.SetParent(surfaceObject.transform, false);

                LineRenderer lr = boltObj.AddComponent<LineRenderer>();
                SetupBoltMaterial(lr, 0.06f, 0.02f, MAIN_BOLT_POINTS);

                energyBolts.Add(lr);

                // Create branches for this bolt
                List<LineRenderer> branches = new List<LineRenderer>();
                for (int b = 0; b < BRANCHES_PER_BOLT; b++)
                {
                    GameObject branchObj = new GameObject($"EnergyBolt_{i}_Branch_{b}");
                    branchObj.transform.SetParent(surfaceObject.transform, false);

                    LineRenderer branchLr = branchObj.AddComponent<LineRenderer>();
                    SetupBoltMaterial(branchLr, 0.04f, 0.01f, BRANCH_POINTS);

                    branches.Add(branchLr);
                }
                boltBranches.Add(branches);

                // Initialize bolt state
                boltAngle.Add((float)i / numBolts * Mathf.PI * 2f);
                boltProgress.Add(Random.value);
                boltDirection.Add(Random.value > 0.5f ? 1f : -1f);

                // Initialize jitter offsets for jagged lightning
                float[] jitters = new float[MAIN_BOLT_POINTS * 2]; // x and y jitter per point
                RegenerateBoltJitter(jitters);
                boltJitterOffsets.Add(jitters);
                boltRegenerateTime.Add(0f);
            }
        }

        private void RegenerateBoltJitter(float[] jitters)
        {
            for (int j = 0; j < jitters.Length; j++)
            {
                // Sharp random offsets for jagged lightning appearance
                jitters[j] = Random.Range(-0.25f, 0.25f);
            }
        }

        private void SetupBoltMaterial(LineRenderer lr, float startWidth, float endWidth, int points)
        {
            // Use sprites default with additive blending for glow effect
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lr.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            lr.material.SetInt("_ZWrite", 0);
            // Render after fog (fog is at Transparent-100 = 2900)
            lr.material.renderQueue = 2950;

            lr.startWidth = startWidth;
            lr.endWidth = endWidth;
            lr.positionCount = points;
            lr.useWorldSpace = false;

            // Dark red to purple gradient - start (outer) is dark red, end (inner) is purple
            lr.startColor = new Color(darkRed.r, darkRed.g, darkRed.b, 0.9f);
            lr.endColor = new Color(deepPurple.r, deepPurple.g, deepPurple.b, 0.5f);
        }

        private void UpdateEnergyBolts()
        {
            if (!enableIndicator || energyBolts.Count == 0)
                return;

            float extendedMaxRadius = maxRadius * 1.3f;

            for (int i = 0; i < energyBolts.Count; i++)
            {
                LineRenderer lr = energyBolts[i];
                if (lr == null)
                    continue;

                // Update progress - bolt spirals along the surface
                boltProgress[i] += boltDirection[i] * boltSpeed * Time.deltaTime * 0.12f;

                // Wrap around at ends
                if (boltProgress[i] > 1f)
                {
                    boltProgress[i] = 0f;
                    boltAngle[i] += Random.Range(-0.3f, 0.3f);
                }
                else if (boltProgress[i] < 0f)
                {
                    boltProgress[i] = 1f;
                    boltAngle[i] += Random.Range(-0.3f, 0.3f);
                }

                // Regenerate jitter periodically for flickering jagged effect
                boltRegenerateTime[i] -= Time.deltaTime;
                if (boltRegenerateTime[i] <= 0f)
                {
                    RegenerateBoltJitter(boltJitterOffsets[i]);
                    boltRegenerateTime[i] = BOLT_REGENERATE_INTERVAL + Random.Range(0f, 0.04f);
                }

                // Spiral parameters
                float spiralTurns = 1.5f;
                float baseAngle = boltAngle[i];
                float[] jitters = boltJitterOffsets[i];

                // Store main bolt positions for branch attachment
                Vector3[] mainPositions = new Vector3[lr.positionCount];

                // Calculate main bolt positions along spiral with jagged offsets
                // When over fog: don't render portions above z=-1 (bolt disappears into fog)
                // When over walkable: render all the way to ground (z=0)
                float topZCutoff = isOverWalkableArea ? GROUND_Z_LEVEL : FOG_Z_LEVEL;

                for (int p = 0; p < lr.positionCount; p++)
                {
                    float pointProgress = boltProgress[i] + (float)p / lr.positionCount * 0.2f;
                    pointProgress = Mathf.Clamp01(pointProgress);

                    float radius = Mathf.Lerp(extendedMaxRadius, minRadius * 2f, pointProgress);
                    float z = CalculateZForRadius(radius);
                    z = Mathf.Max(z, maxZ);

                    // Spiral angle
                    float spiralAngle = baseAngle + pointProgress * spiralTurns * Mathf.PI * 2f;

                    // Apply jagged jitter (sharp random offsets)
                    float jitterX = jitters[p * 2] * (1f - pointProgress * 0.5f); // Less jitter near center
                    float jitterY = jitters[p * 2 + 1] * (1f - pointProgress * 0.5f);

                    float x = Mathf.Cos(spiralAngle) * radius + jitterX;
                    float y = Mathf.Sin(spiralAngle) * radius + jitterY;

                    // If this point is above the cutoff, collapse it to the previous valid point
                    // This makes the bolt disappear into the fog rather than pooling at the edge
                    if (z > topZCutoff && p > 0)
                    {
                        // Use the previous point's position (which is below the cutoff)
                        mainPositions[p] = mainPositions[p - 1];
                        lr.SetPosition(p, mainPositions[p - 1]);
                    }
                    else
                    {
                        Vector3 pos = new Vector3(x, y, z);
                        mainPositions[p] = pos;
                        lr.SetPosition(p, pos);
                    }
                }

                // Update branches
                if (i < boltBranches.Count)
                {
                    UpdateBoltBranches(i, mainPositions);
                }

                // Intense flickering color - alternating between dark red and purple
                float flicker = Random.Range(0.5f, 1.0f);
                // Alternate colors based on bolt index
                Color boltColor = (i % 2 == 0) ? darkRed : deepPurple;
                Color startColor = boltColor * flicker;
                Color endColor = boltColor * flicker * 0.7f;
                lr.startColor = new Color(
                    Mathf.Min(1f, startColor.r * 1.3f),
                    Mathf.Min(1f, startColor.g * 1.3f),
                    Mathf.Min(1f, startColor.b * 1.3f),
                    0.9f * flicker
                );
                lr.endColor = new Color(endColor.r, endColor.g, endColor.b, 0.4f * flicker);
            }
        }

        private void UpdateBoltBranches(int boltIndex, Vector3[] mainPositions)
        {
            List<LineRenderer> branches = boltBranches[boltIndex];

            for (int b = 0; b < branches.Count; b++)
            {
                LineRenderer branch = branches[b];
                if (branch == null)
                    continue;

                // Each branch forks from a different point along the main bolt
                int forkPoint = 2 + b * 2;
                forkPoint = Mathf.Clamp(forkPoint, 0, mainPositions.Length - 1);

                Vector3 forkPos = mainPositions[forkPoint];

                // Random direction for branch
                float branchDir = boltAngle[boltIndex] + b * 1.57f + Random.Range(-0.3f, 0.3f);

                // Branches extend from fork point - cut off at fog level when over fog
                float branchTopZ = isOverWalkableArea ? GROUND_Z_LEVEL : FOG_Z_LEVEL;

                // If fork point is above the cutoff, skip this branch entirely
                if (forkPos.z > branchTopZ)
                {
                    // Hide branch by collapsing all points to fork position
                    for (int p = 0; p < branch.positionCount; p++)
                    {
                        branch.SetPosition(p, forkPos);
                    }
                    continue;
                }

                for (int p = 0; p < branch.positionCount; p++)
                {
                    float t = (float)p / (branch.positionCount - 1);

                    // Start from fork point, extend outward with jagged offsets
                    float branchLen = 0.4f;
                    float jitterMag = 0.1f;

                    float baseX = forkPos.x + Mathf.Cos(branchDir) * t * branchLen;
                    float baseY = forkPos.y + Mathf.Sin(branchDir) * t * branchLen;

                    // Sharp random jitter per segment
                    float jx = Random.Range(-jitterMag, jitterMag) * (1f - t * 0.5f);
                    float jy = Random.Range(-jitterMag, jitterMag) * (1f - t * 0.5f);

                    // Branch stays at roughly same z as fork point (slightly toward surface)
                    float branchZ = forkPos.z + t * 0.1f;
                    branchZ = Mathf.Min(branchZ, branchTopZ);

                    branch.SetPosition(p, new Vector3(baseX + jx, baseY + jy, branchZ));
                }

                // Heavy flicker on branches - match parent bolt color
                float flicker = Random.Range(0.3f, 1.0f);
                // Branches use same color scheme as parent bolt (alternating dark red/purple)
                Color boltColor = (boltIndex % 2 == 0) ? darkRed : deepPurple;
                Color branchStart = boltColor * flicker;
                Color branchEnd = boltColor * flicker * 0.6f;
                branch.startColor = new Color(branchStart.r, branchStart.g, branchStart.b, 0.7f * flicker);
                branch.endColor = new Color(branchEnd.r, branchEnd.g, branchEnd.b, 0.1f);
            }
        }

        #endregion
    }
}
