using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FaeMaze.Props;
using FaeMaze.Visitors;
using FaeMaze.Systems;
using FaeMaze.Audio;

namespace FaeMaze.HeartPowers
{
    #region Heartbeat of Longing

    /// <summary>
    /// Amplifies FaeLanterns to pull visitors more strongly and tilt routes through their influence.
    /// Coverage scales with tier: I = 50% map, II = 75% map, III = 100% map (entire map).
    /// Attraction strength diminishes with distance from lantern.
    /// Now uses world-space coordinates instead of grid positions.
    /// </summary>
    public class HeartbeatOfLongingEffect : ActivePowerEffect
    {
        private List<FaeLantern> affectedLanterns = new List<FaeLantern>();
        private HashSet<Vector3> lanternInfluencePositions = new HashSet<Vector3>();
        private const string ModifierSourceId = "HeartbeatOfLonging";

        public HeartbeatOfLongingEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            // Mark all FaeLanterns as Heart-linked for duration
            affectedLanterns.AddRange(FaeLantern.All);

            // Get tier-based radius for world-space area effect
            float tierRadius = GetTierBasedRadius();

            foreach (var lantern in affectedLanterns)
            {
                Vector3 lanternWorldPos = lantern.transform.position;
                lanternInfluencePositions.Add(lanternWorldPos);

                // Add world-space tile visual at lantern position
                if (manager.TileVisualizer != null)
                {
                    float intensity = 1.0f;
                    manager.TileVisualizer.AddTileEffectAtWorldPos(lanternWorldPos, HeartPowerType.HeartbeatOfLonging, intensity, definition.duration);
                }
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            // Tier I: Echoing Thrum - Check visitors approaching lantern areas
            if (definition.tier >= 1 && definition.flag1)
            {
                ApplyEchoingThrum();
            }

            // Tier III: Devouring Chorus - Check for consumed visitors in lantern influence
            if (definition.tier >= 3 && definition.flag2)
            {
                CheckForDevouringChorus();
            }
        }

        public override void OnEnd()
        {
            // Remove tile visuals
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.HeartbeatOfLonging);
            }

            affectedLanterns.Clear();
            lanternInfluencePositions.Clear();
        }

        /// <summary>
        /// Calculates tier-based radius for lantern influence in world units.
        /// </summary>
        private float GetTierBasedRadius()
        {
            // Use a reasonable world-space default radius
            float baseRadius = 20f;

            float coveragePercent = definition.tier switch
            {
                1 => 0.5f,  // 50% coverage
                2 => 0.75f, // 75% coverage
                3 => 1.0f,  // 100% coverage (entire map)
                _ => 0.5f   // Default to tier 1
            };

            return baseRadius * coveragePercent;
        }

        private void ApplyEchoingThrum()
        {
            // Use visitor registry instead of expensive FindObjectsByType
            var visitors = VisitorRegistry.All;
            float tierRadius = GetTierBasedRadius();

            foreach (var visitor in visitors)
            {
                if (visitor == null || visitor.State == VisitorControllerBase.VisitorState.Fascinated)
                {
                    continue;
                }

                // Check if visitor is near any lantern influence position (world-space distance)
                Vector3 visitorPos = visitor.transform.position;
                bool nearInfluence = IsNearLanternInfluence(visitorPos, tierRadius * 0.2f);

                if (nearInfluence && Random.value < 0.3f)
                {
                    // Apply fascination if the visitor has a public method for it
                }
            }
        }

        private void CheckForDevouringChorus()
        {
            // Placeholder for consumed visitor events
        }

        public void OnVisitorConsumed(VisitorControllerBase visitor)
        {
            if (definition.tier < 3)
            {
                return;
            }

            var visitors = VisitorRegistry.All;
            float tierRadius = GetTierBasedRadius();

            foreach (var v in visitors)
            {
                if (v == null || v.State == VisitorControllerBase.VisitorState.Consumed ||
                    v.State == VisitorControllerBase.VisitorState.Escaping)
                {
                    continue;
                }

                Vector3 vPos = v.transform.position;
                if (IsNearLanternInfluence(vPos, tierRadius))
                {
                    // Apply temporary strong Heart bias
                }
            }
        }

        private bool IsNearLanternInfluence(Vector3 worldPos, float range)
        {
            foreach (var lanternPos in lanternInfluencePositions)
            {
                float dist = Vector3.Distance(worldPos, lanternPos);
                if (dist <= range)
                {
                    return true;
                }
            }
            return false;
        }
    }

    #endregion

    #region Murmuring Paths

    /// <summary>
    /// Creates corridors of desire or sealing from selected position to the Heart.
    /// Simplified for world-space: creates visual path effect without grid-based pathfinding.
    /// </summary>
    public class MurmuringPathsEffect : ActivePowerEffect
    {
        private List<Vector3> pathPositions = new List<Vector3>();
        private const string ModifierSourceId = "MurmuringPaths";
        private static int segmentCounter = 0;
        private string instanceSourceId;
        private GameObject pathVisualObject;
        private LineRenderer pathLineRenderer;
        private float animationTime = 0f;

        public MurmuringPathsEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition)
        {
            instanceSourceId = $"{ModifierSourceId}_{segmentCounter++}";
        }

        public override void OnStart()
        {
            // Create a simple visual path from target to heart using world positions
            pathPositions = GeneratePathPositions(targetPosition);

            if (pathPositions.Count > 0)
            {
                // Determine mode: Lure (default) or Seal (Tier III)
                bool sealMode = definition.tier >= 3 && definition.flag2;

                // Create continuous glowing path visualization
                CreatePathVisualization(pathPositions, sealMode);
            }
        }

        public override void OnEnd()
        {
            // Remove path visualization
            if (pathVisualObject != null)
            {
                Object.Destroy(pathVisualObject);
                pathVisualObject = null;
                pathLineRenderer = null;
            }

            // Clear Lured state from all visitors
            var activeVisitors = VisitorRegistry.All;
            if (activeVisitors != null)
            {
                foreach (var visitor in activeVisitors)
                {
                    if (visitor != null && visitor.State == VisitorControllerBase.VisitorState.Lured)
                    {
                        visitor.SetLured(false);
                    }
                }
            }

            pathPositions.Clear();
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            // Animate the path visual with jagged moving edges
            animationTime += deltaTime;
            if (pathLineRenderer != null)
            {
                UpdatePathAnimation();
            }

            // Find all active visitors and check if they're near the path
            var activeVisitors = VisitorRegistry.All;
            if (activeVisitors == null || pathPositions == null || pathPositions.Count == 0)
                return;

            float pathProximity = 1.5f; // World units

            foreach (var visitor in activeVisitors)
            {
                if (visitor == null || visitor.State == VisitorControllerBase.VisitorState.Consumed)
                    continue;

                Vector3 visitorPos = visitor.transform.position;
                bool onPath = IsNearPath(visitorPos, pathProximity);

                if (onPath && visitor.State != VisitorControllerBase.VisitorState.Lured)
                {
                    visitor.SetLured(true);
                }
                else if (!onPath && visitor.State == VisitorControllerBase.VisitorState.Lured)
                {
                    visitor.SetLured(false);
                }
            }
        }

        private bool IsNearPath(Vector3 pos, float proximity)
        {
            foreach (var pathPos in pathPositions)
            {
                if (Vector3.Distance(pos, pathPos) <= proximity)
                {
                    return true;
                }
            }
            return false;
        }

        private List<Vector3> GeneratePathPositions(Vector3 startPos)
        {
            List<Vector3> positions = new List<Vector3>();

            // Get heart world position
            Vector3 heartPos = manager.MazeGrid.HeartWorldPosition;

            // Create a simple straight-line path from start to heart
            // (No grid-based A* pathfinding - just visual effect)
            int numPoints = 20;
            for (int i = 0; i <= numPoints; i++)
            {
                float t = i / (float)numPoints;
                Vector3 point = Vector3.Lerp(startPos, heartPos, t);
                positions.Add(point);
            }

            return positions;
        }

        private void CreatePathVisualization(List<Vector3> path, bool sealMode)
        {
            if (path == null || path.Count == 0)
                return;

            pathVisualObject = new GameObject($"MurmuringPath_{instanceSourceId}");
            pathLineRenderer = pathVisualObject.AddComponent<LineRenderer>();

            pathLineRenderer.startWidth = 0.8f;
            pathLineRenderer.endWidth = 0.8f;
            pathLineRenderer.positionCount = path.Count;

            Color pathColor = sealMode
                ? new Color(0.8f, 0.1f, 0.1f, 0.7f)
                : new Color(1.0f, 0.5f, 0.0f, 0.7f);

            pathLineRenderer.startColor = pathColor;
            pathLineRenderer.endColor = pathColor;

            pathLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            pathLineRenderer.material.color = pathColor;

            pathLineRenderer.sortingLayerName = "Default";
            pathLineRenderer.sortingOrder = 5;

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 worldPos = path[i];
                worldPos.z = -0.1f;
                pathLineRenderer.SetPosition(i, worldPos);
            }
        }

        private void UpdatePathAnimation()
        {
            if (pathLineRenderer == null || pathPositions == null || pathPositions.Count == 0)
                return;

            float baseWidth = 0.8f;
            float jaggedAmount = 0.3f;
            float animSpeed = 2.0f;

            AnimationCurve widthCurve = new AnimationCurve();

            for (int i = 0; i < pathPositions.Count; i++)
            {
                float t = (float)i / pathPositions.Count;
                float jaggedOffset = Mathf.Sin((t * 10.0f) + (animationTime * animSpeed)) * jaggedAmount;
                jaggedOffset += Mathf.Sin((t * 5.0f) - (animationTime * animSpeed * 1.5f)) * jaggedAmount * 0.5f;

                float width = baseWidth + jaggedOffset;
                widthCurve.AddKey(t, width);
            }

            pathLineRenderer.widthCurve = widthCurve;

            float pulseAlpha = 0.5f + Mathf.Sin(animationTime * 3.0f) * 0.2f;
            Color currentColor = pathLineRenderer.startColor;
            currentColor.a = pulseAlpha;
            pathLineRenderer.startColor = currentColor;
            pathLineRenderer.endColor = currentColor;
        }
    }

    #endregion

    #region Dream Snare

    /// <summary>
    /// AoE that Mesmerizes visitors, then pushes them into Lost state with Heart bias.
    /// Uses world-space radius checks instead of grid iteration.
    /// </summary>
    public class DreamSnareEffect : ActivePowerEffect
    {
        private HashSet<VisitorControllerBase> affectedVisitors = new HashSet<VisitorControllerBase>();
        private HashSet<Vector3> thornPositions = new HashSet<Vector3>();
        private Vector3 centerWorldPos;
        private const string ModifierSourceId = "DreamSnare";

        public DreamSnareEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            centerWorldPos = targetPosition;
            float radius = definition.radius > 0 ? definition.radius : 3f;

            // Add world-space tile visual at center
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.AddTileEffectAtWorldPos(centerWorldPos, HeartPowerType.DreamSnare, 1.0f, definition.duration);
            }

            // Find all visitors in world-space AoE and apply Mesmerized
            var visitors = Object.FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);

            foreach (var visitor in visitors)
            {
                Vector3 visitorPos = visitor.transform.position;
                float distance = Vector3.Distance(targetPosition, visitorPos);

                if (distance <= radius)
                {
                    float mesmerizeDuration = definition.param1 > 0 ? definition.param1 : 4f;
                    visitor.SetMesmerized(mesmerizeDuration);
                    affectedVisitors.Add(visitor);

                    if (definition.tier >= 3)
                    {
                        // Mark for harvest
                    }
                }
            }

            // Tier I: Create lingering thorn positions at edge
            if (definition.tier >= 1 && definition.flag1)
            {
                CreateLingeringThorns(radius);
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
        }

        public override void OnEnd()
        {
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.DreamSnare);
            }

            thornPositions.Clear();
            affectedVisitors.Clear();
        }

        private void CreateLingeringThorns(float radius)
        {
            // Create thorn positions at edge of AoE (world-space)
            int numThorns = 8;
            for (int i = 0; i < numThorns; i++)
            {
                float angle = (i / (float)numThorns) * Mathf.PI * 2f;
                Vector3 thornPos = centerWorldPos + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
                thornPositions.Add(thornPos);
            }
        }

        public bool IsNearThorn(Vector3 worldPos)
        {
            float thornRadius = 0.5f;
            foreach (var thornPos in thornPositions)
            {
                if (Vector3.Distance(worldPos, thornPos) <= thornRadius)
                {
                    return true;
                }
            }
            return false;
        }

        public void OnVisitorStepOnThornTile(VisitorControllerBase visitor)
        {
            if (!IsNearThorn(visitor.transform.position))
            {
                return;
            }

            float frightenedDuration = definition.param2 > 0 ? definition.param2 : 2f;
            visitor.SetFrightened(frightenedDuration);
        }
    }

    #endregion

    #region Feastward Panic

    /// <summary>
    /// Releases a wave of fear that makes everywhere but the Heart feel deadly.
    /// Uses world-space distance checks instead of grid iteration.
    /// </summary>
    public class FeastwardPanicEffect : ActivePowerEffect
    {
        private HashSet<VisitorControllerBase> frightenedVisitors = new HashSet<VisitorControllerBase>();
        private const string ModifierSourceId = "FeastwardPanic";
        private Vector3 heartWorldPos;

        public FeastwardPanicEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            heartWorldPos = manager.MazeGrid.HeartWorldPosition;

            bool selectiveMode = definition.tier >= 1 && definition.flag1;

            var visitors = Object.FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);

            foreach (var visitor in visitors)
            {
                if (visitor.State == VisitorControllerBase.VisitorState.Fascinated ||
                    visitor.State == VisitorControllerBase.VisitorState.Consumed ||
                    visitor.State == VisitorControllerBase.VisitorState.Escaping)
                {
                    continue;
                }

                bool shouldAffect = true;

                if (selectiveMode)
                {
                    shouldAffect = IsInCone(visitor.transform.position);
                }

                if (shouldAffect)
                {
                    float frightenedDuration = definition.duration > 0 ? definition.duration : 5f;
                    visitor.SetFrightened(frightenedDuration);
                    frightenedVisitors.Add(visitor);
                }
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (definition.tier >= 2 && definition.flag2)
            {
                CheckLastRefuge();
            }
        }

        public override void OnEnd()
        {
            frightenedVisitors.Clear();

            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.FeastwardPanic);
            }
        }

        private bool IsInCone(Vector3 visitorWorldPos)
        {
            Vector3 directionToTarget = (targetPosition - heartWorldPos).normalized;
            Vector3 directionToVisitor = (visitorWorldPos - heartWorldPos).normalized;

            float angle = Vector3.Angle(directionToTarget, directionToVisitor);
            float coneAngle = definition.param1 > 0 ? definition.param1 : 60f;

            return angle <= coneAngle / 2f;
        }

        private void CheckLastRefuge()
        {
            // Placeholder for visitor path analysis
        }

        public void OnVisitorConsumed(VisitorControllerBase visitor)
        {
            if (definition.tier < 3 || !frightenedVisitors.Contains(visitor))
            {
                return;
            }

            float extensionTime = definition.param3 > 0 ? definition.param3 : 1f;
            elapsedTime -= extensionTime;
        }
    }

    #endregion

    #region Covenant with the Wisps

    /// <summary>
    /// Wisps temporarily obey you, prioritizing marked victims and Heart-preferred routes.
    /// Uses world-space positioning.
    /// </summary>
    public class CovenantWithWispsEffect : ActivePowerEffect
    {
        private List<WillowTheWisp> affectedWisps = new List<WillowTheWisp>();

        public CovenantWithWispsEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            affectedWisps.AddRange(Object.FindObjectsByType<WillowTheWisp>(FindObjectsSortMode.None));

            // Add world-space tile visual at beacon position
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.AddTileEffectAtWorldPos(targetPosition, HeartPowerType.CovenantWithWisps, 1.0f, definition.duration);
            }

            foreach (var wisp in affectedWisps)
            {
                if (definition.tier >= 1)
                {
                    // Enable twin flames
                }

                if (definition.tier >= 2 && definition.flag1)
                {
                    PlaceBeacon(wisp);
                }
            }
        }

        public override void OnEnd()
        {
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.CovenantWithWisps);
            }

            affectedWisps.Clear();
        }

        private void PlaceBeacon(WillowTheWisp wisp)
        {
            // Set wisp to patrol around target position
        }

        public void OnWispDeliverVisitor(WillowTheWisp wisp, VisitorControllerBase visitor)
        {
            if (definition.tier < 3 || !affectedWisps.Contains(wisp))
            {
                return;
            }

            int bonusEssence = definition.intParam1 > 0 ? definition.intParam1 : 2;
            manager.AddEssence(bonusEssence);
        }
    }

    #endregion

    #region Puka's Bargain

    /// <summary>
    /// Bribes a Puka: less random drowning, more helpful teleportation near Heart.
    /// Uses world-space positioning.
    /// </summary>
    public class PukasBargainEffect : ActivePowerEffect
    {
        private PukaHazard targetPuka;
        private HashSet<Vector3> pactPoolPositions = new HashSet<Vector3>();
        private bool undertowUsed = false;

        public PukasBargainEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            var pukas = Object.FindObjectsByType<PukaHazard>(FindObjectsSortMode.None);
            float minDist = float.MaxValue;

            foreach (var puka in pukas)
            {
                float dist = Vector3.Distance(puka.transform.position, targetPosition);
                if (dist < minDist)
                {
                    minDist = dist;
                    targetPuka = puka;
                }
            }

            if (targetPuka == null)
            {
                return;
            }

            if (definition.tier >= 1)
            {
                IdentifyPactPools();
            }
        }

        public override void OnEnd()
        {
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.PukasBargain);
            }

            pactPoolPositions.Clear();
        }

        private void IdentifyPactPools()
        {
            // Simplified: just mark positions near the heart
            Vector3 heartPos = manager.MazeGrid.HeartWorldPosition;
            float searchRadius = definition.intParam1 > 0 ? definition.intParam1 : 15f;

            // Add tile visual at heart area
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.AddTileEffectAtWorldPos(heartPos, HeartPowerType.PukasBargain, 1.0f, definition.duration);
            }
        }

        public Vector3? GetPreferredTeleportTarget()
        {
            if (definition.tier >= 3 && !undertowUsed && Random.value < 0.2f)
            {
                undertowUsed = true;
                return manager.MazeGrid.HeartWorldPosition;
            }

            if (pactPoolPositions.Count > 0)
            {
                int randomIndex = Random.Range(0, pactPoolPositions.Count);
                return pactPoolPositions.ElementAt(randomIndex);
            }

            return null;
        }

        public float GetKillChanceModifier()
        {
            return definition.param1 > 0 ? definition.param1 : 0.5f;
        }

        public void OnPukaKillVisitor(Vector3 killWorldPos)
        {
            if (definition.tier < 2)
            {
                return;
            }

            float aoeRadius = definition.param2 > 0 ? definition.param2 : 3f;

            var visitors = Object.FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);
            foreach (var visitor in visitors)
            {
                float dist = Vector3.Distance(visitor.transform.position, killWorldPos);
                if (dist <= aoeRadius)
                {
                    visitor.SetFrightened(3f);
                }
            }
        }
    }

    #endregion

    #region Ring of Invitations

    /// <summary>
    /// FairyRings become irresistible invitations that redirect pilgrims toward Heart.
    /// Uses world-space positioning.
    /// </summary>
    public class RingOfInvitationsEffect : ActivePowerEffect
    {
        private List<FairyRing> affectedRings = new List<FairyRing>();
        private HashSet<VisitorControllerBase> entrancedVisitors = new HashSet<VisitorControllerBase>();

        public RingOfInvitationsEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            affectedRings.AddRange(Object.FindObjectsByType<FairyRing>(FindObjectsSortMode.None));

            foreach (var ring in affectedRings)
            {
                // Add world-space tile visual at ring position
                if (manager.TileVisualizer != null)
                {
                    manager.TileVisualizer.AddTileEffectAtWorldPos(ring.transform.position, HeartPowerType.RingOfInvitations, 1.0f, definition.duration);
                }

                if (definition.tier >= 1)
                {
                    SpawnIllusoryRings(ring);
                }
            }
        }

        public override void OnEnd()
        {
            if (definition.tier >= 3)
            {
                ApplyClosingDance();
            }

            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.RingOfInvitations);
            }

            affectedRings.Clear();
            entrancedVisitors.Clear();
        }

        private void SpawnIllusoryRings(FairyRing sourceRing)
        {
            // Spawn temporary ring colliders
        }

        private void ApplyClosingDance()
        {
            var visitors = Object.FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);

            foreach (var ring in affectedRings)
            {
                foreach (var visitor in visitors)
                {
                    float dist = Vector3.Distance(visitor.transform.position, ring.transform.position);
                    float ringRadius = 1f;

                    if (dist <= ringRadius)
                    {
                        float mesmerizeDuration = definition.param1 > 0 ? definition.param1 : 3f;
                        visitor.SetMesmerized(mesmerizeDuration);
                    }
                }
            }
        }

        public void OnVisitorEntranced(VisitorControllerBase visitor)
        {
            entrancedVisitors.Add(visitor);

            if (definition.tier >= 2)
            {
                // Circle Remembered
            }
        }
    }

    #endregion

    #region Heartward Grasp

    /// <summary>
    /// Pulls a visitor through a wall toward the Heart.
    /// Uses world-space positioning and simplified animation.
    /// </summary>
    public class HeartwardGraspEffect : ActivePowerEffect
    {
        private enum AnimationPhase
        {
            InitialPause,
            PullToActivation,
            Repositioning,
            PushToDestination,
            FinalPause,
            Complete
        }

        private GameObject graspVisual;
        private VisitorControllerBase targetVisitor;
        private Vector3 activationWorldPos;
        private Vector3 visitorStartWorldPos;
        private Vector3 pullDestinationWorldPos;
        private const string ModifierSourceId = "HeartwardGrasp";

        private AnimationPhase currentPhase = AnimationPhase.InitialPause;
        private float phaseStartTime = 0f;
        private Vector3 lerpStartPosition;
        private Vector3 lerpEndPosition;
        private bool visitorMovementStopped = false;

        private Vector3 graspBasePosition;
        private Vector3 graspAnimationDirection;

        public HeartwardGraspEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            activationWorldPos = targetPosition;
            Vector3 heartWorldPos = manager.MazeGrid.HeartWorldPosition;

            // Find nearest visitor within range of activation position
            float pullRange = definition.param1 > 0 ? definition.param1 : 3f;
            targetVisitor = FindNearestVisitor(activationWorldPos, pullRange);

            if (targetVisitor == null)
            {
                return;
            }

            visitorStartWorldPos = targetVisitor.transform.position;

            // Find destination: position toward heart from activation point
            pullDestinationWorldPos = FindDestinationTowardHeart(activationWorldPos, heartWorldPos);

            // Spawn grasp prefab
            SpawnGraspPrefab(activationWorldPos, visitorStartWorldPos);

            // Stop visitor movement
            StopVisitor();

            currentPhase = AnimationPhase.InitialPause;
            phaseStartTime = elapsedTime;
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (targetVisitor == null || currentPhase == AnimationPhase.Complete)
            {
                return;
            }

            float phaseElapsed = elapsedTime - phaseStartTime;

            switch (currentPhase)
            {
                case AnimationPhase.InitialPause:
                    if (phaseElapsed >= 0.75f)
                    {
                        currentPhase = AnimationPhase.PullToActivation;
                        phaseStartTime = elapsedTime;
                        lerpStartPosition = targetVisitor.transform.position;
                        lerpEndPosition = activationWorldPos;
                    }
                    break;

                case AnimationPhase.PullToActivation:
                    float pullT = Mathf.Clamp01(phaseElapsed / 0.25f);
                    targetVisitor.transform.position = Vector3.Lerp(lerpStartPosition, lerpEndPosition, pullT);

                    if (phaseElapsed >= 0.25f)
                    {
                        currentPhase = AnimationPhase.Repositioning;
                        targetVisitor.transform.position = activationWorldPos;

                        UpdateGraspDirection(activationWorldPos, pullDestinationWorldPos);

                        currentPhase = AnimationPhase.PushToDestination;
                        phaseStartTime = elapsedTime;
                        lerpStartPosition = activationWorldPos;
                        lerpEndPosition = pullDestinationWorldPos;
                    }
                    break;

                case AnimationPhase.PushToDestination:
                    float pushT = Mathf.Clamp01(phaseElapsed / 0.25f);
                    targetVisitor.transform.position = Vector3.Lerp(lerpStartPosition, lerpEndPosition, pushT);

                    if (phaseElapsed >= 0.25f)
                    {
                        currentPhase = AnimationPhase.FinalPause;
                        phaseStartTime = elapsedTime;
                        targetVisitor.RecalculatePath();
                        ApplyTierEffects();
                    }
                    break;

                case AnimationPhase.FinalPause:
                    if (phaseElapsed >= 0.75f)
                    {
                        currentPhase = AnimationPhase.Complete;
                        ResumeVisitor();

                        if (graspVisual != null)
                        {
                            Object.Destroy(graspVisual);
                            graspVisual = null;
                        }
                    }
                    break;
            }

            if (graspVisual != null)
            {
                UpdateGraspVisualAnimation();
            }
        }

        private void UpdateGraspVisualAnimation()
        {
            float animTime = elapsedTime;
            Vector3 offset = Vector3.zero;

            if (animTime < 1.0f)
            {
                if (animTime < 0.25f)
                {
                    float t = animTime / 0.25f;
                    offset = graspAnimationDirection * t;
                }
                else if (animTime < 0.75f)
                {
                    offset = graspAnimationDirection;
                }
                else
                {
                    float t = (1.0f - animTime) / 0.25f;
                    offset = graspAnimationDirection * t;
                }
            }
            else if (animTime < 2.0f)
            {
                float secondHalfTime = animTime - 1.0f;

                if (secondHalfTime < 0.25f)
                {
                    float t = secondHalfTime / 0.25f;
                    offset = graspAnimationDirection * t;
                }
                else if (secondHalfTime < 0.75f)
                {
                    offset = graspAnimationDirection;
                }
                else
                {
                    float t = (1.0f - secondHalfTime) / 0.25f;
                    offset = graspAnimationDirection * t;
                }
            }

            graspVisual.transform.position = graspBasePosition + offset;
        }

        public override void OnEnd()
        {
            if (targetVisitor != null && visitorMovementStopped)
            {
                ResumeVisitor();
            }

            if (graspVisual != null)
            {
                Object.Destroy(graspVisual);
                graspVisual = null;
            }

            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.HeartwardGrasp);
            }
        }

        public override void ApplyWorldOffset(Vector3 worldOffset)
        {
            targetPosition += worldOffset;
            activationWorldPos += worldOffset;
            visitorStartWorldPos += worldOffset;
            pullDestinationWorldPos += worldOffset;
            lerpStartPosition += worldOffset;
            lerpEndPosition += worldOffset;
            graspBasePosition += worldOffset;

            if (graspVisual != null)
            {
                graspVisual.transform.position += worldOffset;
            }
        }

        private void StopVisitor()
        {
            if (targetVisitor == null || visitorMovementStopped)
            {
                return;
            }

            targetVisitor.Stop();
            visitorMovementStopped = true;
        }

        private void ResumeVisitor()
        {
            if (targetVisitor == null || !visitorMovementStopped)
            {
                return;
            }

            targetVisitor.Resume();
            visitorMovementStopped = false;
        }

        private void SpawnGraspPrefab(Vector3 position, Vector3 pointToward)
        {
            GameObject graspPrefab = Resources.Load<GameObject>("Prefabs/Props/Grasp/grasp");

            if (graspPrefab == null)
            {
                return;
            }

            Vector3 graspPosition = position;
            graspPosition.z = 0f;

            Vector3 direction = pointToward - graspPosition;
            direction.z = 0f;

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.up;
            }
            else
            {
                direction.Normalize();
            }

            Quaternion rotation = Quaternion.LookRotation(Vector3.forward, direction);

            graspVisual = Object.Instantiate(graspPrefab, graspPosition, rotation);
            graspVisual.name = "GraspEffect";

            graspBasePosition = graspPosition;
            graspAnimationDirection = direction;
        }

        private void UpdateGraspDirection(Vector3 from, Vector3 to)
        {
            if (graspVisual == null)
            {
                return;
            }

            Vector3 direction = to - from;
            direction.z = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
                Quaternion rotation = Quaternion.LookRotation(Vector3.forward, direction);
                graspVisual.transform.rotation = rotation;
                graspAnimationDirection = direction;
            }
        }

        private Vector3 FindDestinationTowardHeart(Vector3 from, Vector3 heartPos)
        {
            Vector3 direction = (heartPos - from).normalized;
            float pullDistance = 3f;
            return from + direction * pullDistance;
        }

        private void ApplyTierEffects()
        {
            if (targetVisitor == null)
            {
                return;
            }

            if (definition.tier >= 3 && definition.flag1)
            {
                float mesmerizeDuration = definition.param3 > 0 ? definition.param3 : 3f;
                targetVisitor.SetMesmerized(mesmerizeDuration);
            }
        }

        private VisitorControllerBase FindNearestVisitor(Vector3 worldPos, float range)
        {
            var visitors = VisitorRegistry.All;
            VisitorControllerBase nearest = null;
            float minDistance = float.MaxValue;

            foreach (var visitor in visitors)
            {
                if (visitor == null ||
                    visitor.State == VisitorControllerBase.VisitorState.Consumed ||
                    visitor.State == VisitorControllerBase.VisitorState.Escaping)
                {
                    continue;
                }

                float distance = Vector3.Distance(visitor.transform.position, worldPos);

                if (distance <= range && distance < minDistance)
                {
                    nearest = visitor;
                    minDistance = distance;
                }
            }

            return nearest;
        }
    }

    #endregion

    #region Devouring Maw

    /// <summary>
    /// Instantly consumes a visitor on the targeted position, granting essence.
    /// Uses world-space positioning.
    /// </summary>
    public class DevouringMawEffect : ActivePowerEffect
    {
        private enum AnimationPhase
        {
            Pause,
            SinkAndDevour,
            Complete
        }

        private GameObject devourVisual;
        private VisitorControllerBase consumedVisitor;
        private Vector3 targetWorldPos;
        private AnimationPhase currentPhase;
        private float phaseStartTime;
        private Vector3 visitorStartPosition;
        private bool hasConsumedVisitor;
        private Vector3 devourBasePosition;

        public DevouringMawEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            targetWorldPos = targetPosition;

            // Always spawn devour prefab at target position
            InstantiateDevourVisual(targetWorldPos);

            // Add tile visualizer effect
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.AddTileEffectAtWorldPos(targetWorldPos, HeartPowerType.DevouringMaw, 1.0f, 2.0f);
            }

            // Find visitor near the targeted position
            float targetRadius = 1.0f;
            VisitorControllerBase targetVisitorFound = FindVisitorNearPosition(targetWorldPos, targetRadius);

            if (targetVisitorFound == null)
            {
                return;
            }

            consumedVisitor = targetVisitorFound;

            consumedVisitor.Stop();
            visitorStartPosition = consumedVisitor.transform.position;

            currentPhase = AnimationPhase.Pause;
            phaseStartTime = 0f;
            hasConsumedVisitor = false;

            // Tier I: Apply fear to nearby visitors
            if (definition.tier >= 1 && definition.flag1)
            {
                ApplyEchoingTerror(targetWorldPos);
            }

            // Tier II: Slow nearby visitors
            if (definition.tier >= 2 && definition.flag2)
            {
                ApplyDrainingEmbrace(targetWorldPos);
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (devourVisual != null)
            {
                UpdateDevourVisualAnimation();
            }

            if (devourVisual != null && elapsedTime >= 1.0f)
            {
                Object.Destroy(devourVisual);
                devourVisual = null;
            }

            if (consumedVisitor == null)
            {
                return;
            }

            float phaseElapsed = elapsedTime - phaseStartTime;

            switch (currentPhase)
            {
                case AnimationPhase.Pause:
                    if (phaseElapsed >= 0.75f)
                    {
                        currentPhase = AnimationPhase.SinkAndDevour;
                        phaseStartTime = elapsedTime;
                    }
                    break;

                case AnimationPhase.SinkAndDevour:
                    float sinkDuration = 0.25f;
                    float sinkT = Mathf.Clamp01(phaseElapsed / sinkDuration);

                    Vector3 sinkPosition = visitorStartPosition;
                    sinkPosition.z = Mathf.Lerp(visitorStartPosition.z, visitorStartPosition.z + 1f, sinkT);
                    consumedVisitor.transform.position = sinkPosition;

                    if (sinkT >= 1f && !hasConsumedVisitor)
                    {
                        hasConsumedVisitor = true;
                        ConsumeVisitor(consumedVisitor);

                        if (definition.tier >= 3)
                        {
                            ApplySoulHarvest();
                        }

                        currentPhase = AnimationPhase.Complete;
                    }
                    break;

                case AnimationPhase.Complete:
                    break;
            }
        }

        public override void OnEnd()
        {
            if (devourVisual != null)
            {
                Object.Destroy(devourVisual);
                devourVisual = null;
            }

            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.DevouringMaw);
            }
        }

        public override void ApplyWorldOffset(Vector3 worldOffset)
        {
            targetPosition += worldOffset;
            targetWorldPos += worldOffset;
            visitorStartPosition += worldOffset;
            devourBasePosition += worldOffset;

            if (devourVisual != null)
            {
                devourVisual.transform.position += worldOffset;
            }
        }

        private VisitorControllerBase FindVisitorNearPosition(Vector3 worldPos, float radius)
        {
            var visitors = VisitorRegistry.All;

            foreach (var visitor in visitors)
            {
                if (visitor == null)
                {
                    continue;
                }

                if (visitor.State == VisitorControllerBase.VisitorState.Consumed ||
                    visitor.State == VisitorControllerBase.VisitorState.Escaping)
                {
                    continue;
                }

                float distance = Vector3.Distance(visitor.transform.position, worldPos);
                if (distance <= radius)
                {
                    return visitor;
                }
            }

            return null;
        }

        private void InstantiateDevourVisual(Vector3 position)
        {
            GameObject devourPrefab = Resources.Load<GameObject>("Prefabs/Props/devour");

            if (devourPrefab == null)
            {
                return;
            }

            Vector3 worldPos = position;
            worldPos.z = 0.75f;

            devourVisual = Object.Instantiate(devourPrefab, worldPos, Quaternion.identity);
            devourBasePosition = worldPos;
        }

        private void UpdateDevourVisualAnimation()
        {
            if (devourVisual == null)
            {
                return;
            }

            float animTime = elapsedTime;
            Vector3 offset = Vector3.zero;

            if (animTime < 0.25f)
            {
                float t = animTime / 0.25f;
                offset.z = Mathf.Lerp(0f, -1f, t);
            }
            else if (animTime < 0.75f)
            {
                offset.z = -1f;
            }
            else if (animTime < 1.0f)
            {
                float t = (animTime - 0.75f) / 0.25f;
                offset.z = Mathf.Lerp(-1f, 0f, t);
            }
            else
            {
                offset.z = 0f;
            }

            devourVisual.transform.position = devourBasePosition + offset;
        }

        private void ConsumeVisitor(VisitorControllerBase visitor)
        {
            if (visitor == null)
            {
                return;
            }

            int essence = visitor.GetEssenceReward();

            if (manager.GameController != null)
            {
                manager.GameController.AddEssence(essence);
            }

            if (Systems.GameStatsTracker.Instance != null)
            {
                Systems.GameStatsTracker.Instance.RecordVisitorConsumed();
            }

            SoundManager.Instance?.PlayVisitorConsumed();

            Object.Destroy(visitor.gameObject);
        }

        private void ApplyEchoingTerror(Vector3 centerWorldPos)
        {
            float fearRadius = definition.param1 > 0 ? definition.param1 : 3f;
            float fearDuration = definition.param2 > 0 ? definition.param2 : 3f;

            var visitors = VisitorRegistry.All;

            foreach (var visitor in visitors)
            {
                if (visitor == null ||
                    visitor.State == VisitorControllerBase.VisitorState.Consumed ||
                    visitor.State == VisitorControllerBase.VisitorState.Escaping ||
                    visitor == consumedVisitor)
                {
                    continue;
                }

                float distance = Vector3.Distance(visitor.transform.position, centerWorldPos);

                if (distance <= fearRadius)
                {
                    visitor.SetFrightened(fearDuration);
                }
            }
        }

        private void ApplyDrainingEmbrace(Vector3 centerWorldPos)
        {
            float slowRadius = definition.intParam1 > 0 ? definition.intParam1 : 3f;
            float slowDuration = definition.param3 > 0 ? definition.param3 : 4f;

            var visitors = VisitorRegistry.All;

            foreach (var visitor in visitors)
            {
                if (visitor == null ||
                    visitor.State == VisitorControllerBase.VisitorState.Consumed ||
                    visitor.State == VisitorControllerBase.VisitorState.Escaping ||
                    visitor == consumedVisitor)
                {
                    continue;
                }

                float distance = Vector3.Distance(visitor.transform.position, centerWorldPos);

                if (distance <= slowRadius)
                {
                    visitor.SetMesmerized(slowDuration);
                }
            }
        }

        private void ApplySoulHarvest()
        {
            int bonusEssence = definition.intParam2 > 0 ? definition.intParam2 : 3;

            if (manager.GameController != null)
            {
                manager.GameController.AddEssence(bonusEssence);
            }
        }
    }

    #endregion
}
