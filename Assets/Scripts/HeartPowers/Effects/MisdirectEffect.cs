using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FaeMaze.Props;
using FaeMaze.Visitors;
using FaeMaze.Systems;
using FaeMaze.Audio;
using FaeMaze.Roguelike;
using ForestMaze;

namespace FaeMaze.HeartPowers
{
    #region Misdirect (Heart Power 5)

    /// <summary>
    /// Misdirect effect: placed near a node/edge junction, reduces the perceived pathfinding
    /// cost of the nearest edge to 1/20th normal, making visitors strongly prefer it.
    /// When a visitor enters the misdirect zone, they are forced to walk the entire edge
    /// to the far sign, then resume pathing to their original destination without backtracking.
    /// Permanent until re-cast (which replaces the existing effect).
    /// </summary>
    public class MisdirectEffect : ActivePowerEffect
    {
        private int misdirectEdgeIndex = -1;
        private Dictionary<int, float> edgeCostMultipliers;

        // Edge appears 1/20th its real length to pathfinding
        private const float EDGE_COST_MULTIPLIER = 0.05f;

        // Visual feedback - fog quad (same approach as Power 1 MurmuringPaths)
        private GameObject fogContainer;
        private GameObject fogQuad;
        private Material fogMaterial;
        private Texture2D pathMaskTexture;
        private Bounds fogBounds;
        private List<GameObject> signInstances = new List<GameObject>();
        private float pulseTime = 0f;
        private const float FOG_Z_POSITION = -0.15f;
        private const float FOG_PADDING = 2.0f;
        private const float MASK_PIXELS_PER_UNIT = 4.0f;

        // Cyan/teal fog colors
        private static readonly Color FogColor = new Color(0.1f, 0.6f, 0.7f, 0.5f);
        private static readonly Color FogColorDark = new Color(0.05f, 0.4f, 0.5f, 0.5f);
        private static readonly Color GlowColor = new Color(0.3f, 0.9f, 1.0f, 1.0f);

        // Sign prefab
        private static GameObject signPrefab;
        private const string SIGN_PREFAB_PATH = "Prefabs/Props/sign";

        // Misdirect sign positions (for collider-based redirect)
        private Vector3 signPositionA;
        private Vector3 signPositionB;

        // Permanent until replaced
        public override bool IsExpired => false;

        public MisdirectEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition)
        {
            edgeCostMultipliers = new Dictionary<int, float>();
            LoadSignPrefab();
        }

        public override void OnStart()
        {
            FindNearestEdge();

            if (misdirectEdgeIndex >= 0)
            {
                edgeCostMultipliers[misdirectEdgeIndex] = EDGE_COST_MULTIPLIER;
                CreateEdgeVisual();
                PlaceSignsAtEdgeEnds();

                // Recalculate paths for all walking visitors so they respond to the new edge cost
                foreach (var visitor in FaeMaze.Visitors.VisitorRegistry.All)
                {
                    if (visitor != null && visitor.State == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Walking)
                    {
                        visitor.RecalculatePath();
                    }
                }
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            pulseTime += deltaTime;

            // Animate fog wave - slow oscillating pulse
            if (fogMaterial != null)
            {
                float wave = (Mathf.Sin(pulseTime * 0.5f * Mathf.PI) + 1f) * 0.5f;
                fogMaterial.SetFloat("_WaveProgress", wave);
            }

        }

        public override void OnEnd()
        {
            if (fogMaterial != null)
            {
                Object.Destroy(fogMaterial);
                fogMaterial = null;
            }
            if (pathMaskTexture != null)
            {
                Object.Destroy(pathMaskTexture);
                pathMaskTexture = null;
            }
            if (fogContainer != null)
            {
                Object.Destroy(fogContainer);
                fogContainer = null;
            }
            fogQuad = null;

            foreach (var obj in signInstances)
            {
                if (obj != null) Object.Destroy(obj);
            }
            signInstances.Clear();
            MisdirectSignTrigger.ClearRedirectTracking();

            edgeCostMultipliers.Clear();
            misdirectEdgeIndex = -1;
        }

        public override void ApplyWorldOffset(Vector3 worldOffset)
        {
            if (fogContainer != null)
            {
                fogContainer.transform.position += worldOffset;
            }
            foreach (var obj in signInstances)
            {
                if (obj != null)
                {
                    obj.transform.position += worldOffset;
                }
            }
        }

        /// <summary>
        /// Returns the edge cost multipliers for pathfinding, or null if no edge is affected.
        /// </summary>
        public Dictionary<int, float> GetEdgeCostMultipliers()
        {
            return edgeCostMultipliers.Count > 0 ? edgeCostMultipliers : null;
        }

        /// <summary>
        /// Returns the index of the misdirected edge.
        /// </summary>
        public int GetMisdirectEdgeIndex() => misdirectEdgeIndex;

        /// <summary>
        /// Returns the position of the OTHER misdirect sign (the one the visitor should walk toward).
        /// Returns Vector3.zero if no valid position exists.
        /// </summary>
        public Vector3 GetOtherSignPosition(Vector3 thisSignPosition)
        {
            float distA = Vector3.Distance(thisSignPosition, signPositionA);
            float distB = Vector3.Distance(thisSignPosition, signPositionB);
            return distA < distB ? signPositionB : signPositionA;
        }

        /// <summary>
        /// Finds the nearest edge to the target position.
        /// Prioritizes edge tiles over node tiles.
        /// </summary>
        private void FindNearestEdge()
        {
            if (manager.MazeGrid == null || manager.MazeGrid.WorldSpaceMazeData == null)
                return;

            var mazeData = manager.MazeGrid.WorldSpaceMazeData;
            Vector2 targetPos2D = new Vector2(targetPosition.x, targetPosition.y);

            float searchRadius = 5f;
            var nearbyTiles = mazeData.GetTilesNear(targetPos2D, searchRadius);

            float minDist = float.MaxValue;

            foreach (var tile in nearbyTiles)
            {
                if (!tile.Walkable) continue;
                if (tile.EdgeIndex < 0) continue; // Only edge tiles

                float dist = Vector2.Distance(targetPos2D, tile.Position);
                if (dist < minDist)
                {
                    minDist = dist;
                    misdirectEdgeIndex = tile.EdgeIndex;
                }
            }
        }

        /// <summary>
        /// Creates a fog effect covering the misdirected edge, using the same PowerFog shader as Power 1.
        /// </summary>
        private void CreateEdgeVisual()
        {
            if (manager.MazeGrid == null || manager.MazeGrid.WorldSpaceMazeData == null)
                return;

            var mazeData = manager.MazeGrid.WorldSpaceMazeData;

            // Collect all tile positions on the misdirected edge
            var edgeTilePositions = new List<Vector3>();
            foreach (var tile in mazeData.Tiles)
            {
                if (tile.EdgeIndex != misdirectEdgeIndex) continue;
                if (!tile.Walkable) continue;
                edgeTilePositions.Add(new Vector3(tile.Position.x, tile.Position.y, 0f));
            }

            if (edgeTilePositions.Count == 0) return;

            fogContainer = new GameObject("MisdirectFog");

            // Calculate bounds
            fogBounds = new Bounds(edgeTilePositions[0], Vector3.zero);
            foreach (var pos in edgeTilePositions)
            {
                fogBounds.Encapsulate(pos);
            }
            fogBounds.Expand(FOG_PADDING * 2f);

            // Generate path mask texture
            GenerateMisdirectMask(edgeTilePositions);

            // Create fog material using PowerFog shader
            CreateMisdirectFogMaterial(edgeTilePositions);

            // Create fog quad
            CreateMisdirectFogQuad();
        }

        /// <summary>
        /// Generates a mask texture showing only the misdirected edge area.
        /// </summary>
        private void GenerateMisdirectMask(List<Vector3> tilePositions)
        {
            int texWidth = Mathf.CeilToInt(fogBounds.size.x * MASK_PIXELS_PER_UNIT);
            int texHeight = Mathf.CeilToInt(fogBounds.size.y * MASK_PIXELS_PER_UNIT);
            texWidth = Mathf.Clamp(texWidth, 32, 512);
            texHeight = Mathf.Clamp(texHeight, 32, 512);

            pathMaskTexture = new Texture2D(texWidth, texHeight, TextureFormat.R8, false);
            pathMaskTexture.filterMode = FilterMode.Bilinear;
            pathMaskTexture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[texWidth * texHeight];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.black;

            float worldToTexX = texWidth / fogBounds.size.x;
            float worldToTexY = texHeight / fogBounds.size.y;
            float tileRadius = 1.5f;
            int radiusPixels = Mathf.CeilToInt(tileRadius * Mathf.Max(worldToTexX, worldToTexY));

            foreach (var pos in tilePositions)
            {
                float worldX = pos.x - fogBounds.min.x;
                float worldY = pos.y - fogBounds.min.y;
                int centerTexX = Mathf.RoundToInt(worldX * worldToTexX);
                int centerTexY = Mathf.RoundToInt(worldY * worldToTexY);

                for (int dy = -radiusPixels; dy <= radiusPixels; dy++)
                {
                    for (int dx = -radiusPixels; dx <= radiusPixels; dx++)
                    {
                        int px = centerTexX + dx;
                        int py = centerTexY + dy;
                        if (px < 0 || px >= texWidth || py < 0 || py >= texHeight)
                            continue;

                        float distWorld = Mathf.Sqrt(dx * dx / (worldToTexX * worldToTexX) + dy * dy / (worldToTexY * worldToTexY));
                        float fogAmount;
                        if (distWorld <= tileRadius * 0.5f)
                            fogAmount = 1f;
                        else if (distWorld <= tileRadius)
                        {
                            float t = (distWorld - tileRadius * 0.5f) / (tileRadius * 0.5f);
                            fogAmount = Mathf.SmoothStep(1f, 0f, t);
                        }
                        else
                            fogAmount = 0f;

                        int pixelIndex = py * texWidth + px;
                        pixels[pixelIndex].r = Mathf.Max(pixels[pixelIndex].r, fogAmount);
                    }
                }
            }

            pathMaskTexture.SetPixels(pixels);
            pathMaskTexture.Apply();
        }

        /// <summary>
        /// Creates the fog material with the PowerFog shader using cyan/teal colors.
        /// </summary>
        private void CreateMisdirectFogMaterial(List<Vector3> tilePositions)
        {
            var shader = HeartPowerUtils.LoadShader("Custom/PowerFog", "Sprites/Default");

            fogMaterial = new Material(shader);
            fogMaterial.SetColor("_FogColor", FogColor);
            fogMaterial.SetColor("_FogColorDark", FogColorDark);
            fogMaterial.SetColor("_GlowColor", GlowColor);

            if (pathMaskTexture != null)
                fogMaterial.SetTexture("_PathMask", pathMaskTexture);

            // Use edge endpoints as heart/furthest for wave animation direction
            Vector3 startPos = tilePositions[0];
            Vector3 endPos = tilePositions[tilePositions.Count - 1];
            fogMaterial.SetVector("_HeartPosition", new Vector4(startPos.x, startPos.y, 0, 0));
            fogMaterial.SetVector("_FurthestPosition", new Vector4(endPos.x, endPos.y, 0, 0));
            fogMaterial.SetFloat("_WaveProgress", 0.5f);
            fogMaterial.SetFloat("_WaveIntensity", 1.0f);
            fogMaterial.SetFloat("_WindSpeed", 0.03f);
        }

        /// <summary>
        /// Creates the fog quad covering the misdirected edge area.
        /// </summary>
        private void CreateMisdirectFogQuad()
        {
            fogQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fogQuad.name = "MisdirectFogQuad";
            fogQuad.transform.SetParent(fogContainer.transform);

            fogQuad.transform.position = new Vector3(fogBounds.center.x, fogBounds.center.y, FOG_Z_POSITION);
            fogQuad.transform.localScale = new Vector3(fogBounds.size.x, fogBounds.size.y, 1f);
            fogQuad.transform.rotation = Quaternion.identity;

            var collider = fogQuad.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            var renderer = fogQuad.GetComponent<Renderer>();
            renderer.material = fogMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// Loads the sign prefab from Resources.
        /// </summary>
        private void LoadSignPrefab()
        {
            if (signPrefab != null)
            {
                return;
            }

            signPrefab = Resources.Load<GameObject>(SIGN_PREFAB_PATH);
        }

        /// <summary>
        /// Places sign prefab instances at each end of the misdirected edge.
        /// Signs face away from the connected node (along the edge direction).
        /// The sign model's forward is +X, up is -Z (world up).
        /// </summary>
        private void PlaceSignsAtEdgeEnds()
        {
            if (signPrefab == null)
            {
                return;
            }
            if (manager.MazeGrid == null || manager.MazeGrid.WorldSpaceMazeData == null)
            {
                return;
            }

            var graphState = manager.MazeGrid.WorldSpaceMazeData.GraphState;
            if (graphState == null)
            {
                return;
            }
            if (misdirectEdgeIndex < 0 || misdirectEdgeIndex >= graphState.Edges.Count)
            {
                return;
            }

            var edge = graphState.Edges[misdirectEdgeIndex];
            var polyline = edge.PolylinePoints;
            if (polyline == null || polyline.Count < 2)
            {
                return;
            }

            // NodeA end: PolylinePoints[0] is near NodeA
            // Direction away from NodeA = from node center toward the edge
            {
                Vector2 edgePoint = polyline[0];
                Vector2 nextPoint = polyline[1];
                Vector2 dirAwayFromNode = (nextPoint - edgePoint).normalized;

                signPositionA = new Vector3(edgePoint.x, edgePoint.y, 0f);
                PlaceSign(edgePoint, dirAwayFromNode);
            }

            // NodeB end: PolylinePoints[last] is near NodeB (if it exists)
            if (edge.NodeB.HasValue)
            {
                int last = polyline.Count - 1;
                Vector2 edgePoint = polyline[last];
                Vector2 prevPoint = polyline[last - 1];
                Vector2 dirAwayFromNode = (prevPoint - edgePoint).normalized;

                signPositionB = new Vector3(edgePoint.x, edgePoint.y, 0f);
                PlaceSign(edgePoint, dirAwayFromNode);
            }

            // Now wire up the trigger components with references to the other sign's position
            foreach (var sign in signInstances)
            {
                var trigger = sign.GetComponent<MisdirectSignTrigger>();
                if (trigger != null)
                {
                    trigger.SetEffect(this);
                }
            }
        }

        /// <summary>
        /// Instantiates a sign at the given position, rotated so it faces the given direction.
        /// The sign prefab has a baked-in Y=180 rotation that must be preserved.
        /// We compose the Z rotation (for XY plane orientation) with the prefab's default rotation.
        /// Scale set to 0.5 to match the maze proportions.
        /// </summary>
        private void PlaceSign(Vector2 position, Vector2 faceDirection)
        {
            if (signPrefab == null) return;

            float angle = Mathf.Atan2(faceDirection.y, faceDirection.x) * Mathf.Rad2Deg;
            GameObject sign = Object.Instantiate(signPrefab);
            sign.name = "MisdirectSign";
            sign.transform.position = new Vector3(position.x, position.y, 0f);

            // Compose Z rotation (XY plane orientation) with the prefab's baked-in rotation
            // Prefab has Y=180 rotation baked in; we apply Z rotation on top of that
            Quaternion zRotation = Quaternion.Euler(0f, 0f, angle);
            sign.transform.rotation = zRotation * sign.transform.rotation;

            // Scale down to fit maze proportions
            sign.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            // Add trigger collider for visitor redirection (radius 1 in world space)
            var sphere = sign.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 2.0f; // Local radius; sign scale is 0.5, so world radius = 1.0
            sphere.center = Vector3.zero;

            // Add redirect trigger component
            sign.AddComponent<MisdirectSignTrigger>();

            signInstances.Add(sign);
        }
    }

    /// <summary>
    /// Trigger component placed on misdirect signs. When a walking visitor enters the trigger
    /// from sign A (entry sign), the visitor is forced to walk to sign B (far sign) by overriding
    /// their destination. When they reach sign B, their original destination is restored and the
    /// misdirect edge is penalized to prevent backtracking.
    /// </summary>
    public class MisdirectSignTrigger : MonoBehaviour
    {
        private MisdirectEffect effect;

        // Tracks which visitors have been redirected by which sign to prevent ping-pong.
        // Key: visitor instance ID, Value: (redirectingSignId, expiryTime)
        // expiryTime = float.MaxValue means actively in transit toward the far sign
        private static Dictionary<int, (int signId, float expiry)> redirectCooldowns =
            new Dictionary<int, (int, float)>();
        private const float REDIRECT_COOLDOWN = 10f;

        // Visitors that have reached this (far) sign and are waiting to exit the collider
        // before CompleteMisdirect is called. This prevents pathfinding from starting
        // while the visitor is still inside the trigger volume.
        private HashSet<int> pendingCompletion = new HashSet<int>();

        public void SetEffect(MisdirectEffect misdirectEffect)
        {
            effect = misdirectEffect;
        }

        /// <summary>
        /// Clears redirect tracking. Called when the misdirect effect is re-cast.
        /// </summary>
        public static void ClearRedirectTracking()
        {
            redirectCooldowns.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (effect == null) return;

            var visitor = other.GetComponentInParent<FaeMaze.Visitors.VisitorControllerBase>();
            if (visitor == null) return;

            // Accept any state where the visitor is actively moving: Walking, Confused, Frightened, Lost, etc.
            // Only reject terminal/inactive states where redirection makes no sense.
            var vState = visitor.State;
            if (vState == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Consumed ||
                vState == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Escaping ||
                vState == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Grabbed ||
                vState == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Dazed)
            {
                Debug.Log($"[Misdirect] OnTriggerEnter: {visitor.name} REJECTED (state={vState}) at sign pos=({transform.position.x:F1},{transform.position.y:F1})");
                return;
            }

            int visitorId = visitor.GetInstanceID();
            int thisSignId = gameObject.GetInstanceID();

            Debug.Log($"[Misdirect] OnTriggerEnter: {visitor.name} entered sign at ({transform.position.x:F1},{transform.position.y:F1}) state={vState} isMisdirected={visitor.IsMisdirected} signId={thisSignId}");

            // Check if this visitor was redirected by the OTHER sign (meaning this is the destination sign)
            if (redirectCooldowns.TryGetValue(visitorId, out var cooldown))
            {
                if (cooldown.signId != thisSignId)
                {
                    if (cooldown.expiry == float.MaxValue || Time.time < cooldown.expiry)
                    {
                        // This is the far sign -- mark visitor for completion when they EXIT
                        // the collider. This ensures pathfinding starts from outside the trigger.
                        pendingCompletion.Add(visitorId);
                        redirectCooldowns[visitorId] = (cooldown.signId, Time.time + REDIRECT_COOLDOWN);
                        Debug.Log($"[Misdirect] OnTriggerEnter: {visitor.name} reached FAR SIGN — pending completion on exit. originSignId={cooldown.signId}");
                        return;
                    }
                    else
                    {
                        Debug.Log($"[Misdirect] OnTriggerEnter: {visitor.name} cooldown EXPIRED (was from signId={cooldown.signId}, expired at {cooldown.expiry:F1}, now={Time.time:F1}) — treating as new entry");
                    }
                }
                else
                {
                    Debug.Log($"[Misdirect] OnTriggerEnter: {visitor.name} re-entered SAME sign (signId={thisSignId}) — treating as new entry");
                }
            }

            // This is the entry sign -- force the visitor to walk to the far sign
            Vector3 farSignPos = effect.GetOtherSignPosition(transform.position);
            if (farSignPos == Vector3.zero)
            {
                Debug.Log($"[Misdirect] OnTriggerEnter: {visitor.name} ABORTED — farSignPos is zero");
                return;
            }

            int edgeIndex = effect.GetMisdirectEdgeIndex();

            // Record that THIS sign redirected the visitor (in-transit)
            redirectCooldowns[visitorId] = (thisSignId, float.MaxValue);

            Debug.Log($"[Misdirect] OnTriggerEnter: {visitor.name} REDIRECTED by entry sign → farSign at ({farSignPos.x:F1},{farSignPos.y:F1}) edgeIndex={edgeIndex}");

            // Force the visitor to walk to the far sign position
            visitor.SetMisdirectDestination(farSignPos, edgeIndex);
        }

        private void OnTriggerExit(Collider other)
        {
            var visitor = other.GetComponentInParent<FaeMaze.Visitors.VisitorControllerBase>();
            if (visitor == null) return;

            int visitorId = visitor.GetInstanceID();
            Debug.Log($"[Misdirect] OnTriggerExit: {visitor.name} exited sign at ({transform.position.x:F1},{transform.position.y:F1}) pendingCompletion={pendingCompletion.Contains(visitorId)} isMisdirected={visitor.IsMisdirected}");

            if (pendingCompletion.Remove(visitorId))
            {
                // Visitor has exited the far sign collider — now safe to complete misdirect
                Debug.Log($"[Misdirect] OnTriggerExit: {visitor.name} COMPLETING misdirect — will recalculate path freely");
                visitor.CompleteMisdirect();
            }
        }
    }

    #endregion
}
