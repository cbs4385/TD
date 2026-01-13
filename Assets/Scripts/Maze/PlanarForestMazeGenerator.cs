using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ForestMaze
{
    /// <summary>
    /// Generates a planar, organic forest maze where nodes represent clearings (circles)
    /// and edges represent paths (corridors). Maintains absolute planarity - paths never cross.
    ///
    /// Based on the Python forest_map_generator implementation.
    /// </summary>
    public static class PlanarForestMazeGenerator
    {
        // Constants
        private const float NODE_RADIUS = 3.0f;
        private const float NODE_PADDING = 0.7f;
        private const float R_KEEP = NODE_RADIUS + NODE_PADDING; // 3.7
        private const float PATH_WIDTH = 1.0f;
        private const float PATH_RADIUS = PATH_WIDTH / 2.0f; // 0.5
        private const float EXCLUSION_ZONE = NODE_RADIUS + 2.0f; // 5.0 - minimum distance for frontier endpoints from nodes

        // Tunable parameters
        private const float ANGLE_MIN_SEPARATION = 60.0f; // degrees - ensures ~3.14 units arc between edges at node boundary for 3 wall tiles (1x1 each)
        private const float ANGLE_EXCLUSION_TOWARD_NODES = 15.0f; // degrees - exclude orientations within this angle toward existing nodes
        private const float ROTATE_STEP = 6.0f; // degrees
        private const float SHORTEN_STEP = 0.3f;
        private const float MIN_CORRIDOR_LENGTH = 0.3f;
        private const float WALL_BUFFER = 1.0f; // Minimum wall buffer (in world units) between elements
        private const float MAX_CROSSING_GAP = 0.25f; // Maximum triangular gap area at edge crossings (sq units)
        private const int MIN_NEW_EDGES = 2; // Minimum new frontier edges per growth step
        private const int MAX_NEW_EDGES = 3; // Maximum new frontier edges per growth step

        public class Node
        {
            public int Id;
            public Vector2 Position;
            public string Kind; // "root" or "normal"
            public int MaxDegree;
            public List<int> IncidentEdges = new List<int>();
            public List<float> UsedAngles = new List<float>(); // in radians

            public bool HasCapacity() => IncidentEdges.Count < MaxDegree;

            public void AddEdge(int edgeId, float angle)
            {
                IncidentEdges.Add(edgeId);
                UsedAngles.Add(angle);
            }
        }

        public class Edge
        {
            public int Id;
            public int NodeA;
            public int? NodeB; // null if partial (open endpoint)
            public List<Vector2> PolylinePoints = new List<Vector2>();
            public bool Partial = true;
            public Vector2? GhostCenter; // Reserved position for future node

            /// <summary>
            /// The Bezier curve representing this edge's path.
            /// PolylinePoints is generated from this curve for backward compatibility.
            /// </summary>
            public BezierCurve Curve = null;

            /// <summary>
            /// Flag indicating this edge's polyline was modified and needs wall regeneration.
            /// Set by merge operations, cleared after wall regeneration.
            /// </summary>
            public bool NeedsWallRegeneration = false;

            /// <summary>
            /// Stores the old polyline path before modification for wall removal.
            /// Populated when NeedsWallRegeneration is set, cleared after wall regeneration.
            /// </summary>
            public List<Vector2> OldPolylinePoints = null;

            public bool IsComplete() => !Partial && NodeB.HasValue;

            /// <summary>
            /// Regenerates the polyline from the Bezier curve.
            /// Call this after modifying the curve.
            /// </summary>
            public void RegeneratePolylineFromCurve(int segments = 8)
            {
                if (Curve != null)
                {
                    PolylinePoints = Curve.ToPolyline(segments);
                }
            }
        }

        public class ForestMapState
        {
            public List<Node> Nodes = new List<Node>();
            public List<Edge> Edges = new List<Edge>();
            public List<int> Frontier = new List<int>(); // Partial edge IDs
            public List<Vector2> GhostCenters = new List<Vector2>();
            public int NextNodeId = 0;
            public int NextEdgeId = 0;
            public int TurnCount = 0;
            public System.Random Random;
            public bool ValidationPassed = true; // Always true in world-space mode
            public string ValidationError = null;
            public bool HasCrossConnection = false; // Track if any non-parent connections exist

            /// <summary>
            /// Fill segments to cover gaps created by edge spacing adjustments.
            /// Each segment is a pair of points (start, end) that should be rendered as path tiles.
            /// </summary>
            public List<(Vector2 start, Vector2 end)> AdjustmentFills = new List<(Vector2, Vector2)>();

            // DEPRECATED - Legacy grid rasterization fields, kept for compatibility but unused
            // In world-space mode, graph positions ARE world positions (no transform needed)
            [System.Obsolete("No longer used - graph positions are world positions")]
            public float Scale = 1.0f;
            [System.Obsolete("No longer used - graph positions are world positions")]
            public Vector2 Offset = Vector2.zero;
        }

        private class ValidationResult
        {
            public bool IsValid;
            public string ErrorMessage;
            public Dictionary<int, Vector2> ActualNodePositions = new Dictionary<int, Vector2>();
            public Dictionary<int, List<Vector2>> ActualEdgePaths = new Dictionary<int, List<Vector2>>();
            public HashSet<(int, int)> DetectedConnections = new HashSet<(int, int)>();
        }

        /// <summary>
        /// Generate a planar organic forest maze graph and return the state for dynamic growth.
        /// Works in pure world-space - graph positions ARE world positions (no transform needed).
        /// </summary>
        /// <param name="gridWidth">Ignored - legacy parameter kept for API compatibility</param>
        /// <param name="gridHeight">Ignored - legacy parameter kept for API compatibility</param>
        /// <param name="turns">Number of growth turns (more turns = more nodes)</param>
        /// <param name="seed">Random seed</param>
        /// <param name="seedOnly">If true, only create seed node with one frontier edge (for step-by-step debugging)</param>
        /// <returns>Tuple of (empty string for legacy compatibility, generation state with world-space positions)</returns>
        public static (string maze, ForestMapState state) GenerateMazeWithState(int gridWidth, int gridHeight, int turns = 20, int? seed = null, bool seedOnly = false)
        {
            int baseSeed = seed.HasValue ? seed.Value : System.Environment.TickCount;

            var state = new ForestMapState
            {
                Random = new System.Random(baseSeed),
                ValidationPassed = true // Always valid in world-space mode
            };

            // Initialize with root and first edge (seed only mode)
            InitializeSeed(state);

            // If seed only, return immediately without any growth
            if (seedOnly || turns <= 0)
            {
                return ("", state);
            }

            // Grow for the specified number of turns
            const int minNodeCount = 6; // Root + at least 5 normal nodes
            int totalSteps = 0;
            while (totalSteps < turns && state.Nodes.Count < minNodeCount)
            {
                if (state.Frontier.Count == 0 || !Step(state))
                    break;
                totalSteps++;
            }

            // Ensure at least one cross-connection exists after initial growth
            EnsureCrossConnection(state);

            // Return empty string for legacy compatibility - no rasterization in world-space mode
            // Graph positions ARE world positions
            return ("", state);
        }

        /// <summary>
        /// Generate a planar organic forest maze (backwards compatibility wrapper).
        /// </summary>
        public static string GenerateMaze(int gridWidth, int gridHeight, int turns = 20, int? seed = null)
        {
            return GenerateMazeWithState(gridWidth, gridHeight, turns, seed).maze;
        }

        /// <summary>
        /// Initializes the seed: root node at origin with one frontier edge radiating outward.
        /// This is the minimal starting point for step-by-step graph growth.
        /// </summary>
        private static void InitializeSeed(ForestMapState state)
        {
            // Create root node at origin
            var root = new Node
            {
                Id = state.NextNodeId++,
                Position = Vector2.zero,
                Kind = "root",
                MaxDegree = 1
            };
            state.Nodes.Add(root);

            // Create one frontier edge at random orientation
            float angle = (float)(state.Random.NextDouble() * 2.0 * Math.PI);
            float length = (float)(state.Random.NextDouble() * 15.0 + 12.0);

            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 ghostCenter = root.Position + direction * (2 * NODE_RADIUS + length);

            // Build edge curve: node to ghost (start is node, end is ghost)
            var boundaries = GetEdgeBoundaries(root.Position, ghostCenter, startIsNode: true, endIsNode: true);
            if (!boundaries.HasValue)
                return; // Too short, shouldn't happen for seed

            var curve = CreateSimpleCurve(boundaries.Value.start, boundaries.Value.end, state.Random);
            var polyline = GenerateSCurvePolyline(boundaries.Value.start, boundaries.Value.end, state.Random);

            var edge = new Edge
            {
                Id = state.NextEdgeId++,
                NodeA = root.Id,
                NodeB = null,
                Curve = curve,
                PolylinePoints = polyline,
                Partial = true,
                GhostCenter = ghostCenter
            };

            state.Edges.Add(edge);
            state.Frontier.Add(edge.Id);
            state.GhostCenters.Add(ghostCenter);
            root.AddEdge(edge.Id, angle);
        }

        private static void Initialize(ForestMapState state)
        {
            // Create root node at origin
            var root = new Node
            {
                Id = state.NextNodeId++,
                Position = Vector2.zero,
                Kind = "root",
                MaxDegree = 1
            };
            state.Nodes.Add(root);

            // Create first normal node at random orientation
            float angle = (float)(state.Random.NextDouble() * 2.0 * Math.PI);
            float length = (float)(state.Random.NextDouble() * 7.0 + 3.0);
            float distance = 2 * NODE_RADIUS + length;

            Vector2 node1Pos = new Vector2(
                Mathf.Cos(angle) * distance,
                Mathf.Sin(angle) * distance
            );

            // Max degree = 1 (parent) + 2-3 (new edges) = 3-4
            int maxDegree = MIN_NEW_EDGES + 1;
            var node1 = new Node
            {
                Id = state.NextNodeId++,
                Position = node1Pos,
                Kind = "normal",
                MaxDegree = maxDegree
            };
            state.Nodes.Add(node1);

            // Create edge between root and node1 (node-to-node)
            var initialBoundaries = GetEdgeBoundaries(root.Position, node1Pos, startIsNode: true, endIsNode: true);
            BezierCurve initialCurve = null;
            List<Vector2> initialPolyline;

            if (initialBoundaries.HasValue)
            {
                initialCurve = CreateSimpleCurve(initialBoundaries.Value.start, initialBoundaries.Value.end, state.Random);
                initialPolyline = GenerateSCurvePolyline(initialBoundaries.Value.start, initialBoundaries.Value.end, state.Random);
            }
            else
            {
                // Fallback for too-short edge
                Vector2 direction = (node1Pos - root.Position).normalized;
                Vector2 startBoundary = root.Position + direction * (NODE_RADIUS - 0.5f);
                Vector2 endBoundary = node1Pos - direction * (NODE_RADIUS - 0.5f);
                initialPolyline = GenerateSCurvePolyline(startBoundary, endBoundary, state.Random);
            }

            var edge = new Edge
            {
                Id = state.NextEdgeId++,
                NodeA = root.Id,
                NodeB = node1.Id,
                Curve = initialCurve,
                PolylinePoints = initialPolyline,
                Partial = false,
                GhostCenter = null
            };

            state.Edges.Add(edge);
            root.AddEdge(edge.Id, angle);
            float reverseAngle = (angle + Mathf.PI) % (2 * Mathf.PI);
            node1.AddEdge(edge.Id, reverseAngle);

            // Add 2-3 frontier edges from node1 (as per spec: always 2-3 new edges)
            int numNewEdges = state.Random.Next(MIN_NEW_EDGES, MAX_NEW_EDGES + 1);
            int frontierEdgesAdded = 0;

            for (int i = 0; i < numNewEdges && node1.HasCapacity(); i++)
            {
                if (AddPartialEdgeWithNodeExclusion(state, node1))
                {
                    frontierEdgesAdded++;
                }
            }

            // Ensure at least MIN_NEW_EDGES frontier edges
            while (frontierEdgesAdded < MIN_NEW_EDGES && node1.IncidentEdges.Count < 6)
            {
                node1.MaxDegree++;
                if (AddPartialEdge(state, node1))
                {
                    frontierEdgesAdded++;
                }
                else
                {
                    break;
                }
            }

        }

        /// <summary>
        /// Executes one growth step: selects a frontier edge and creates a new node.
        /// This is used by both initial generation and dynamic growth.
        ///
        /// Growth rules:
        /// - Remove closest-to-seed frontier portal, replace with new node
        /// - On even steps with >2 existing nodes: create connection to existing node
        ///   (select from 3 closest, prefer fewest connections)
        /// - Always spawn 2-3 new frontier edges at random orientations
        ///   (excluding ±15° toward existing nodes)
        /// </summary>
        public static bool Step(ForestMapState state)
        {
            // Select a frontier edge (closest to seed/root)
            int? edgeId = SelectFrontierEdgeBiased(state);
            if (!edgeId.HasValue)
                return false;

            var edge = state.Edges[edgeId.Value];
            if (!edge.GhostCenter.HasValue)
                return false;

            // Create new node at ghost position (portal location)
            // Max degree is 1 (parent) + connection (if even step) + 2-3 new edges = 4-5
            int maxDegree = MIN_NEW_EDGES + 2; // Parent + potential connection + min new edges
            var newNode = new Node
            {
                Id = state.NextNodeId++,
                Position = edge.GhostCenter.Value,
                Kind = "normal",
                MaxDegree = maxDegree
            };
            state.Nodes.Add(newNode);

            // Convert partial edge to complete
            edge.NodeB = newNode.Id;
            edge.Partial = false;

            // Calculate reverse angle for new node
            var nodeA = state.Nodes[edge.NodeA];
            Vector2 direction = (newNode.Position - nodeA.Position).normalized;
            float reverseAngle = Mathf.Atan2(-direction.y, -direction.x);
            reverseAngle = (reverseAngle + 2 * Mathf.PI) % (2 * Mathf.PI);

            newNode.AddEdge(edgeId.Value, reverseAngle);

            // Remove from frontier and ghost list
            state.Frontier.Remove(edgeId.Value);
            state.GhostCenters.RemoveAll(g => Vector2.Distance(g, edge.GhostCenter.Value) < 1e-6f);
            edge.GhostCenter = null;

            // Cross-connection: always try to connect to closest unconnected node
            bool hasExistingConnection = false;
            int otherNodesCount = state.Nodes.Count - 1; // Exclude the new node we just added

            if (otherNodesCount >= 1)
            {
                // Try to connect to existing node using new selection criteria:
                // Pick from 3 closest, prefer the one with fewest connections
                if (TryConnectToExistingWithCriteria(state, newNode, edge.NodeA))
                {
                    hasExistingConnection = true;
                    state.HasCrossConnection = true;
                    Debug.Log($"[Step] Cross-connection created from node {newNode.Id}");
                }
            }

            // Always add 2-3 new frontier edges at random orientations
            // Exclude orientations within ±15° toward existing nodes
            int numNewEdges = state.Random.Next(MIN_NEW_EDGES, MAX_NEW_EDGES + 1);
            int frontierEdgesAdded = 0;

            Debug.Log($"[Step] Creating {numNewEdges} frontier edges from node {newNode.Id} at {newNode.Position}, HasCapacity={newNode.HasCapacity()}, MaxDegree={newNode.MaxDegree}, IncidentEdges={newNode.IncidentEdges.Count}");

            for (int i = 0; i < numNewEdges && newNode.HasCapacity(); i++)
            {
                bool success = AddPartialEdgeWithNodeExclusion(state, newNode);
                Debug.Log($"[Step] AddPartialEdgeWithNodeExclusion attempt {i}: {(success ? "SUCCESS" : "FAILED")}");
                if (success)
                {
                    frontierEdgesAdded++;
                }
            }

            // If we couldn't add enough frontier edges due to space constraints,
            // expand capacity and try with standard method
            while (frontierEdgesAdded < MIN_NEW_EDGES && newNode.IncidentEdges.Count < 6)
            {
                newNode.MaxDegree++;
                bool success = AddPartialEdge(state, newNode);
                Debug.Log($"[Step] AddPartialEdge fallback: {(success ? "SUCCESS" : "FAILED")}");
                if (success)
                {
                    frontierEdgesAdded++;
                }
                else
                {
                    break; // No more valid positions
                }
            }

            Debug.Log($"[Step] Total frontier edges added: {frontierEdgesAdded}, Frontier count: {state.Frontier.Count}");
            state.TurnCount++;
            return true;
        }

        /// <summary>
        /// Tries to connect to an existing node using new criteria:
        /// - Select from 3 closest potential nodes
        /// - Prefer the node with the fewest existing connections
        /// - Ensure the connecting edge doesn't cross through any nodes
        /// </summary>
        private static bool TryConnectToExistingWithCriteria(ForestMapState state, Node newNode, int parentNodeId)
        {
            var connectedNodeIds = GetConnectedNodeIds(state, newNode.Id);

            // Find candidate nodes (excluding self, parent, and already-connected)
            var candidates = state.Nodes
                .Where(n => n.Id != newNode.Id)
                .Where(n => n.Id != parentNodeId)
                .Where(n => !connectedNodeIds.Contains(n.Id))
                .OrderBy(n => Vector2.Distance(n.Position, newNode.Position))
                .Take(3) // Select 3 closest
                .ToList();

            Debug.Log($"[CrossConnect] Node {newNode.Id} trying cross-connect, candidates={candidates.Count}, parentId={parentNodeId}");

            if (candidates.Count == 0)
                return false;

            // Sort by fewest connections (prefer less-connected nodes)
            candidates = candidates
                .OrderBy(n => n.IncidentEdges.Count)
                .ThenBy(n => Vector2.Distance(n.Position, newNode.Position))
                .ToList();

            foreach (var candidate in candidates)
            {
                // Check if connection would cross through any other node
                if (WouldEdgeCrossNode(state, newNode.Position, candidate.Position, newNode.Id, candidate.Id))
                {
                    Debug.Log($"[CrossConnect] Rejected candidate {candidate.Id}: would cross node");
                    continue;
                }

                // Find valid angles for both nodes
                Vector2 directDirection = (candidate.Position - newNode.Position).normalized;
                float directAngle = Mathf.Atan2(directDirection.y, directDirection.x);
                directAngle = (directAngle + 2 * Mathf.PI) % (2 * Mathf.PI);

                float reverseAngle = (directAngle + Mathf.PI) % (2 * Mathf.PI);

                // For cross-connections, use relaxed angle constraints (30° instead of 60°)
                // since the S-curve will diverge from the existing edge
                const float crossConnectMinAngle = 30.0f;
                if (!IsAngleValid(newNode, directAngle, crossConnectMinAngle))
                {
                    Debug.Log($"[CrossConnect] Rejected candidate {candidate.Id}: angle invalid on new node (min {crossConnectMinAngle}°)");
                    continue;
                }
                if (!IsAngleValid(candidate, reverseAngle, crossConnectMinAngle))
                {
                    Debug.Log($"[CrossConnect] Rejected candidate {candidate.Id}: angle invalid on candidate (min {crossConnectMinAngle}°)");
                    continue;
                }

                // Build edge curve (node-to-node)
                var boundaries = GetEdgeBoundaries(newNode.Position, candidate.Position, startIsNode: true, endIsNode: true);
                if (!boundaries.HasValue)
                {
                    Debug.Log($"[CrossConnect] Rejected candidate {candidate.Id}: boundaries null");
                    continue;
                }

                var curve = CreateSimpleCurve(boundaries.Value.start, boundaries.Value.end, state.Random);
                var polyline = GenerateSCurvePolyline(boundaries.Value.start, boundaries.Value.end, state.Random);
                if (!IsPolylineValid(state, polyline, new List<int> { newNode.Id, candidate.Id }, null, true))
                {
                    Debug.Log($"[CrossConnect] Rejected candidate {candidate.Id}: polyline invalid");
                    continue;
                }

                // Expand candidate's capacity if needed
                if (!candidate.HasCapacity())
                {
                    candidate.MaxDegree++;
                }

                var newEdge = new Edge
                {
                    Id = state.NextEdgeId++,
                    NodeA = newNode.Id,
                    NodeB = candidate.Id,
                    Curve = curve,
                    PolylinePoints = polyline,
                    Partial = false,
                    GhostCenter = null
                };

                state.Edges.Add(newEdge);
                newNode.AddEdge(newEdge.Id, directAngle);
                candidate.AddEdge(newEdge.Id, reverseAngle);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a straight-line edge between two positions would cross through any node.
        /// </summary>
        private static bool WouldEdgeCrossNode(ForestMapState state, Vector2 start, Vector2 end, int excludeNode1, int excludeNode2)
        {
            foreach (var node in state.Nodes)
            {
                if (node.Id == excludeNode1 || node.Id == excludeNode2)
                    continue;

                // Check distance from node center to the line segment
                float dist = PointToSegmentDistance(node.Position, start, end);
                if (dist < NODE_RADIUS + PATH_RADIUS)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Creates a gentle S-curve polyline between two boundary points.
        /// Uses sine wave to create 2-3 visible inflection points.
        /// </summary>
        private static List<Vector2> GenerateSCurvePolyline(Vector2 start, Vector2 end, System.Random random)
        {
            var polyline = new List<Vector2>();

            Vector2 direction = (end - start).normalized;
            float length = Vector2.Distance(start, end);
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);

            // Number of sample points along the curve (more = smoother)
            int numPoints = Mathf.Max(8, Mathf.CeilToInt(length / 0.25f)); // Max 0.25 unit spacing

            // Wave parameters for visible S-curve with 2-3 inflection points
            // Amplitude: 5-10% of length, minimum 0.3 units for visibility
            float amplitude = Mathf.Max(0.3f, length * 0.08f);
            // Frequency: 1.5 to 2.5 periods along the length = 2-3 inflection points
            float frequency = 1.5f + (float)random.NextDouble() * 1.0f;
            // Random phase offset for variety
            float phase = (float)random.NextDouble() * Mathf.PI * 2f;
            // Random direction
            int side = random.Next(2) == 0 ? 1 : -1;

            for (int i = 0; i <= numPoints; i++)
            {
                float t = (float)i / numPoints;

                // Base position along the straight line
                Vector2 basePos = Vector2.Lerp(start, end, t);

                // Sine wave offset perpendicular to direction
                // Use sin that starts and ends at 0 for smooth connection to nodes
                float waveT = t * Mathf.PI * frequency;
                float envelope = Mathf.Sin(t * Mathf.PI); // Fade in/out at endpoints
                float waveOffset = Mathf.Sin(waveT + phase) * amplitude * envelope * side;

                Vector2 point = basePos + perpendicular * waveOffset;
                polyline.Add(point);
            }

            return polyline;
        }

        /// <summary>
        /// Creates a BezierCurve object for API compatibility.
        /// Note: The actual path shape comes from GenerateSCurvePolyline, not this curve.
        /// </summary>
        private static BezierCurve CreateSimpleCurve(Vector2 start, Vector2 end, System.Random random)
        {
            // Create a simple curve for API compatibility
            // The actual polyline is generated separately by GenerateSCurvePolyline
            Vector2 midpoint = (start + end) * 0.5f;
            return new BezierCurve(start, midpoint, end);
        }

        /// <summary>
        /// Calculates edge boundary points based on edge type.
        /// Returns null if edge is too short.
        /// </summary>
        private static (Vector2 start, Vector2 end)? GetEdgeBoundaries(
            Vector2 startCenter, Vector2 endCenter, bool startIsNode, bool endIsNode)
        {
            Vector2 direction = (endCenter - startCenter).normalized;
            float distance = Vector2.Distance(startCenter, endCenter);

            // Calculate boundaries based on what's at each end
            float startOffset = startIsNode ? (NODE_RADIUS - 0.5f) : 0f;
            float endOffset = endIsNode ? (NODE_RADIUS - 0.5f) : 0f;

            float corridorLength = distance - startOffset - endOffset;
            if (corridorLength < MIN_CORRIDOR_LENGTH)
                return null;

            Vector2 start = startCenter + direction * startOffset;
            Vector2 end = endCenter - direction * endOffset;

            return (start, end);
        }

        /// <summary>
        /// Adds a partial (frontier) edge with orientation exclusion toward existing nodes.
        /// Excludes angles within ±15° of directions toward existing nodes.
        /// </summary>
        private static bool AddPartialEdgeWithNodeExclusion(ForestMapState state, Node node)
        {
            if (!node.HasCapacity())
            {
                Debug.Log($"[AddPartialEdgeWithNodeExclusion] Node {node.Id} has no capacity");
                return false;
            }

            // Calculate forbidden angles (within ±15° toward existing nodes)
            var forbiddenAngles = new List<(float center, float halfWidth)>();
            float exclusionHalfWidth = ANGLE_EXCLUSION_TOWARD_NODES * Mathf.Deg2Rad;

            foreach (var otherNode in state.Nodes)
            {
                if (otherNode.Id == node.Id)
                    continue;

                Vector2 toNode = (otherNode.Position - node.Position).normalized;
                float angle = Mathf.Atan2(toNode.y, toNode.x);
                angle = (angle + 2 * Mathf.PI) % (2 * Mathf.PI);
                forbiddenAngles.Add((angle, exclusionHalfWidth));
            }

            // Try random angles, excluding forbidden zones
            float theta0 = (float)(state.Random.NextDouble() * 2.0 * Math.PI);
            float length0 = (float)(state.Random.NextDouble() * 15.0 + 12.0);

            int maxRotations = (int)(180 / ROTATE_STEP);
            int forbiddenCount = 0, angleInvalidCount = 0, ghostInvalidCount = 0, polylineInvalidCount = 0;

            for (int rotStep = 0; rotStep < maxRotations; rotStep++)
            {
                float theta;
                if (rotStep == 0)
                    theta = theta0;
                else if (rotStep % 2 == 1)
                    theta = theta0 + (rotStep / 2 + 1) * ROTATE_STEP * Mathf.Deg2Rad;
                else
                    theta = theta0 - (rotStep / 2) * ROTATE_STEP * Mathf.Deg2Rad;

                theta = ((theta % (2 * Mathf.PI)) + 2 * Mathf.PI) % (2 * Mathf.PI);

                // Check if angle is in forbidden zone
                if (IsAngleInForbiddenZone(theta, forbiddenAngles))
                {
                    forbiddenCount++;
                    continue;
                }

                if (!IsAngleValid(node, theta))
                {
                    angleInvalidCount++;
                    continue;
                }

                float length = length0;
                while (length >= MIN_SEGMENT_LENGTH * 2)
                {
                    Vector2 direction = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
                    Vector2 ghostCenter = node.Position + direction * (2 * NODE_RADIUS + length);

                    // Use EXCLUSION_ZONE for ghost position validation
                    if (!IsGhostPositionValidWithExclusionZone(state, ghostCenter, node.Id))
                    {
                        ghostInvalidCount++;
                        length -= SHORTEN_STEP * 2;
                        continue;
                    }

                    // Build edge curve (node to ghost)
                    var boundaries = GetEdgeBoundaries(node.Position, ghostCenter, startIsNode: true, endIsNode: true);
                    if (boundaries.HasValue)
                    {
                        var curve = CreateSimpleCurve(boundaries.Value.start, boundaries.Value.end, state.Random);
                        var polyline = GenerateSCurvePolyline(boundaries.Value.start, boundaries.Value.end, state.Random);
                        if (IsPolylineValid(state, polyline, new List<int> { node.Id }, ghostCenter))
                        {
                            // Success! Create the partial edge
                            var newEdge = new Edge
                            {
                                Id = state.NextEdgeId++,
                                NodeA = node.Id,
                                NodeB = null,
                                Curve = curve,
                                PolylinePoints = polyline,
                                Partial = true,
                                GhostCenter = ghostCenter
                            };

                            state.Edges.Add(newEdge);
                            state.Frontier.Add(newEdge.Id);
                            state.GhostCenters.Add(ghostCenter);
                            node.AddEdge(newEdge.Id, theta);

                            return true;
                        }
                        else
                        {
                            polylineInvalidCount++;
                        }
                    }

                    length -= SHORTEN_STEP * 2;
                }
            }

            Debug.Log($"[AddPartialEdgeWithNodeExclusion] FAILED for node {node.Id}: forbidden={forbiddenCount}, angleInvalid={angleInvalidCount}, ghostInvalid={ghostInvalidCount}, polylineInvalid={polylineInvalidCount}");
            return false;
        }

        /// <summary>
        /// Checks if an angle falls within any forbidden zone.
        /// </summary>
        private static bool IsAngleInForbiddenZone(float angle, List<(float center, float halfWidth)> forbiddenAngles)
        {
            foreach (var (center, halfWidth) in forbiddenAngles)
            {
                float diff = Mathf.Abs(angle - center);
                diff = Mathf.Min(diff, 2 * Mathf.PI - diff);
                if (diff < halfWidth)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Validates ghost position with the new exclusion zone (radius + 2 = 5 units).
        /// </summary>
        private static bool IsGhostPositionValidWithExclusionZone(ForestMapState state, Vector2 ghostPos, int sourceNodeId)
        {
            // Check against all existing nodes with exclusion zone
            foreach (var node in state.Nodes)
            {
                if (node.Id == sourceNodeId)
                    continue;

                if (Vector2.Distance(node.Position, ghostPos) < 2 * EXCLUSION_ZONE)
                    return false;
            }

            // Check against other ghost centers
            foreach (var ghost in state.GhostCenters)
            {
                if (Vector2.Distance(ghost, ghostPos) < 2 * EXCLUSION_ZONE)
                    return false;
            }

            // Check against existing edges
            foreach (var edge in state.Edges)
            {
                if (edge.PolylinePoints.Count < 2)
                    continue;

                if (edge.NodeA == sourceNodeId || (edge.NodeB.HasValue && edge.NodeB.Value == sourceNodeId))
                    continue;

                float dist = PolylineToNodeDistance(edge.PolylinePoints, ghostPos, false);
                float minRequired = NODE_RADIUS + PATH_RADIUS + WALL_BUFFER;

                if (dist < minRequired - 1e-6f)
                    return false;
            }

            return true;
        }

        private static bool AddPartialEdge(ForestMapState state, Node node)
        {
            if (!node.HasCapacity())
                return false;

            float theta0 = (float)(state.Random.NextDouble() * 2.0 * Math.PI);
            // Longer initial length to accommodate curved paths
            float length0 = (float)(state.Random.NextDouble() * 15.0 + 12.0);

            int maxRotations = (int)(180 / ROTATE_STEP);

            for (int rotStep = 0; rotStep < maxRotations; rotStep++)
            {
                float theta;
                if (rotStep == 0)
                    theta = theta0;
                else if (rotStep % 2 == 1)
                    theta = theta0 + (rotStep / 2 + 1) * ROTATE_STEP * Mathf.Deg2Rad;
                else
                    theta = theta0 - (rotStep / 2) * ROTATE_STEP * Mathf.Deg2Rad;

                theta = ((theta % (2 * Mathf.PI)) + 2 * Mathf.PI) % (2 * Mathf.PI);

                if (!IsAngleValid(node, theta))
                    continue;

                float length = length0;
                while (length >= MIN_SEGMENT_LENGTH * 2)
                {
                    Vector2 direction = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
                    Vector2 ghostCenter = node.Position + direction * (2 * NODE_RADIUS + length);

                    if (!IsGhostPositionValidWithExclusionZone(state, ghostCenter, node.Id))
                    {
                        length -= SHORTEN_STEP * 2;
                        continue;
                    }

                    // Build edge curve (node to ghost)
                    var boundaries = GetEdgeBoundaries(node.Position, ghostCenter, startIsNode: true, endIsNode: true);
                    if (boundaries.HasValue)
                    {
                        var curve = CreateSimpleCurve(boundaries.Value.start, boundaries.Value.end, state.Random);
                        var polyline = GenerateSCurvePolyline(boundaries.Value.start, boundaries.Value.end, state.Random);
                        if (IsPolylineValid(state, polyline, new List<int> { node.Id }, ghostCenter))
                        {
                            // Success! Create the partial edge
                            var edge = new Edge
                            {
                                Id = state.NextEdgeId++,
                                NodeA = node.Id,
                                NodeB = null,
                                Curve = curve,
                                PolylinePoints = polyline,
                                Partial = true,
                                GhostCenter = ghostCenter
                            };

                            state.Edges.Add(edge);
                            state.Frontier.Add(edge.Id);
                            state.GhostCenters.Add(ghostCenter);
                            node.AddEdge(edge.Id, theta);

                            return true;
                        }
                    }

                    length -= SHORTEN_STEP * 2;
                }
            }

            return false;
        }

        private static List<Vector2> BuildCurvedPolylineToGhost(ForestMapState state, Vector2 nodeCenter, Vector2 ghostCenter,
            List<int> incidentNodes)
        {
            Vector2 overallDirection = (ghostCenter - nodeCenter).normalized;
            float totalDistance = Vector2.Distance(nodeCenter, ghostCenter);
            float corridorDistance = totalDistance - 2 * NODE_RADIUS;

            if (corridorDistance < MIN_SEGMENT_LENGTH)
            {
                // Too short for curved path, use straight line
                // Start/end slightly inside node boundaries to ensure tile overlap
                Vector2 start = nodeCenter + overallDirection * (NODE_RADIUS - 0.5f);
                Vector2 end = ghostCenter - overallDirection * (NODE_RADIUS - 0.5f);
                return new List<Vector2> { start, end };
            }

            // Start/end slightly inside node boundaries to ensure tile overlap
            Vector2 startBoundary = nodeCenter + overallDirection * (NODE_RADIUS - 0.5f);
            Vector2 endBoundary = ghostCenter - overallDirection * (NODE_RADIUS - 0.5f);

            // Determine number of segments
            int numSegments = Mathf.Clamp(
                Mathf.RoundToInt(corridorDistance / ((MIN_SEGMENT_LENGTH + MAX_SEGMENT_LENGTH) / 2f)),
                MIN_SEGMENTS,
                MAX_SEGMENTS
            );

            // Try curved path
            var polyline = TryBuildCurvedPath(state, startBoundary, endBoundary, overallDirection,
                corridorDistance, numSegments, incidentNodes);

            if (polyline != null)
                return polyline;

            // Fallback to straight line
            return new List<Vector2> { startBoundary, endBoundary };
        }

        private static bool TryConnectToExisting(ForestMapState state, Node newNode, int? prohibitedNodeId = null, int? parentNodeId = null, bool allowForceCapacity = false)
        {
            var connectedNodeIds = GetConnectedNodeIds(state, newNode.Id);

            // Log new node state for debugging
            string newNodeAngles = string.Join(", ", newNode.UsedAngles.Select(a => $"{a * Mathf.Rad2Deg:F0}°"));
            // Debug.Log($"[PlanarForest] TryConnect for node {newNode.Id} at {newNode.Position}, edges={newNode.IncidentEdges.Count}/{newNode.MaxDegree}, usedAngles=[{newNodeAngles}]");

            // First try nodes with capacity
            var candidates = state.Nodes
                .Where(n => n.Id != newNode.Id && n.HasCapacity())
                .Where(n => !prohibitedNodeId.HasValue || n.Id != prohibitedNodeId.Value)
                .Where(n => !connectedNodeIds.Contains(n.Id))
                .ToList();

            int candidatesWithCapacity = candidates.Count;

            // If no candidates with capacity and we're allowed to force, include nodes at capacity
            if (candidates.Count == 0 && allowForceCapacity)
            {
                candidates = state.Nodes
                    .Where(n => n.Id != newNode.Id)
                    .Where(n => !prohibitedNodeId.HasValue || n.Id != prohibitedNodeId.Value)
                    .Where(n => !connectedNodeIds.Contains(n.Id))
                    .ToList();
            }

            if (candidates.Count == 0)
            {
                int totalNodes = state.Nodes.Count;
                int excludedSelf = 1;
                int excludedProhibited = prohibitedNodeId.HasValue ? 1 : 0;
                int excludedConnected = connectedNodeIds.Count;
                // Debug.Log($"[PlanarForest] TryConnect FAILED: 0 candidates. Total nodes={totalNodes}, excluded: self=1, prohibited={excludedProhibited}, alreadyConnected={excludedConnected}");
                return false;
            }

            // Sort candidates by distance to newNode (closest first)
            candidates = candidates
                .OrderBy(c => Vector2.Distance(c.Position, newNode.Position))
                .ToList();

            // Debug.Log($"[PlanarForest] TryConnect: {candidates.Count} candidates ({candidatesWithCapacity} with capacity, allowForce={allowForceCapacity})");

            foreach (var candidate in candidates)
            {
                float dist = Vector2.Distance(newNode.Position, candidate.Position);
                string candAngles = string.Join(", ", candidate.UsedAngles.Select(a => $"{a * Mathf.Rad2Deg:F0}°"));
                // Debug.Log($"[PlanarForest] Trying candidate {candidate.Id} at dist={dist:F1}, edges={candidate.IncidentEdges.Count}/{candidate.MaxDegree}, usedAngles=[{candAngles}]");

                if (connectedNodeIds.Contains(candidate.Id))
                {
                    // Debug.Log($"[PlanarForest]   -> SKIP: Already connected to node {candidate.Id}");
                    continue;
                }

                // Find any valid angle pair by searching all possible angles on source node
                bool foundValidAngles = false;
                float validSourceAngle = 0f;
                float validTargetAngle = 0f;

                // Calculate direction to candidate for preference ordering
                Vector2 directDirection = (candidate.Position - newNode.Position).normalized;
                float directAngle = Mathf.Atan2(directDirection.y, directDirection.x);
                directAngle = (directAngle + 2 * Mathf.PI) % (2 * Mathf.PI);

                // Try angles in 10-degree increments around the full circle
                float angleStep = 10f * Mathf.Deg2Rad;
                int numSteps = Mathf.CeilToInt(2 * Mathf.PI / angleStep);
                int sourceAngleBlocked = 0;
                int targetAngleBlocked = 0;

                for (int i = 0; i < numSteps && !foundValidAngles; i++)
                {
                    float offset = (i / 2 + 1) * angleStep * (i % 2 == 0 ? 1 : -1);
                    if (i == 0) offset = 0;

                    float testSourceAngle = (directAngle + offset + 2 * Mathf.PI) % (2 * Mathf.PI);
                    float testTargetAngle = (testSourceAngle + Mathf.PI) % (2 * Mathf.PI);

                    bool sourceValid = IsAngleValid(newNode, testSourceAngle);
                    bool targetValid = IsAngleValid(candidate, testTargetAngle);

                    if (!sourceValid) sourceAngleBlocked++;
                    if (!targetValid) targetAngleBlocked++;

                    if (sourceValid && targetValid)
                    {
                        foundValidAngles = true;
                        validSourceAngle = testSourceAngle;
                        validTargetAngle = testTargetAngle;
                    }
                }

                if (!foundValidAngles)
                {
                    // Debug.Log($"[PlanarForest]   -> SKIP: No valid angles. Tested {numSteps} angles: {sourceAngleBlocked} blocked on source, {targetAngleBlocked} blocked on target");
                    continue;
                }

                // Debug.Log($"[PlanarForest]   -> Found valid angles: source={validSourceAngle * Mathf.Rad2Deg:F0}°, target={validTargetAngle * Mathf.Rad2Deg:F0}°");

                // Build edge curve (node-to-node)
                var boundaries = GetEdgeBoundaries(newNode.Position, candidate.Position, startIsNode: true, endIsNode: true);
                if (!boundaries.HasValue)
                {
                    // Debug.Log($"[PlanarForest]   -> SKIP: Edge too short");
                    continue;
                }

                var curve = CreateSimpleCurve(boundaries.Value.start, boundaries.Value.end, state.Random);
                var polyline = GenerateSCurvePolyline(boundaries.Value.start, boundaries.Value.end, state.Random);
                if (!IsPolylineValid(state, polyline, new List<int> { newNode.Id, candidate.Id }, null, true))
                {
                    // Debug.Log($"[PlanarForest]   -> SKIP: Polyline validation failed");
                    continue;
                }

                // Expand candidate's capacity if needed (when allowForceCapacity is true)
                if (!candidate.HasCapacity())
                {
                    candidate.MaxDegree++;
                    // Debug.Log($"[PlanarForest]   -> Expanded node {candidate.Id} capacity to {candidate.MaxDegree}");
                }

                var edge = new Edge
                {
                    Id = state.NextEdgeId++,
                    NodeA = newNode.Id,
                    NodeB = candidate.Id,
                    Curve = curve,
                    PolylinePoints = polyline,
                    Partial = false,
                    GhostCenter = null
                };

                state.Edges.Add(edge);
                newNode.AddEdge(edge.Id, validSourceAngle);
                candidate.AddEdge(edge.Id, validTargetAngle);

                // Debug.Log($"[PlanarForest]   -> SUCCESS: Created edge {edge.Id} from node {newNode.Id} to node {candidate.Id}");

                // Mark as cross-connection if connecting to non-parent node
                if (!parentNodeId.HasValue || candidate.Id != parentNodeId.Value)
                {
                    state.HasCrossConnection = true;
                }

                return true;
            }

            // Debug.Log($"[PlanarForest] TryConnect FAILED: All {candidates.Count} candidates exhausted for node {newNode.Id}");
            return false;
        }

        /// <summary>
        /// Ensures at least one cross-connection exists by forcing nodes to connect to non-parents.
        /// Tries multiple nodes in reverse order until successful.
        /// Temporarily increases node capacity if needed.
        /// </summary>
        private static void EnsureCrossConnection(ForestMapState state)
        {
            if (state.HasCrossConnection)
            {
                // Debug.Log($"[PlanarForest] EnsureCrossConnection: Already has cross-connection, skipping");
                return;
            }
            if (state.Nodes.Count < 3)
            {
                // Debug.Log($"[PlanarForest] EnsureCrossConnection: Only {state.Nodes.Count} nodes, need at least 3");
                return;
            }

            // Debug.Log($"[PlanarForest] EnsureCrossConnection: Attempting to create cross-connection with {state.Nodes.Count} nodes");

            // First try nodes that already have capacity
            var nodesWithCapacity = state.Nodes.OrderByDescending(n => n.Id)
                                               .Where(n => n.HasCapacity())
                                               .ToList();

            foreach (var node in nodesWithCapacity)
            {
                if (TryForceConnection(state, node))
                {
                    // Debug.Log($"[PlanarForest] EnsureCrossConnection: Created cross-connection from node {node.Id}");
                    return; // Success!
                }
            }

            // If no nodes have capacity, temporarily increase capacity for pairs of nodes
            var allNodes = state.Nodes.OrderByDescending(n => n.Id).ToList();

            // Try each source node with temporarily increased capacity
            foreach (var sourceNode in allNodes)
            {
                // Find this node's parent
                var parentEdge = state.Edges.FirstOrDefault(e =>
                    e.NodeB.HasValue && e.NodeB.Value == sourceNode.Id);
                int? parentNodeId = parentEdge?.NodeA;

                // Get potential target nodes (excluding parent)
                var targetNodes = state.Nodes
                    .Where(n => n.Id != sourceNode.Id)
                    .Where(n => !parentNodeId.HasValue || n.Id != parentNodeId.Value)
                    .OrderBy(n => state.Random.Next())
                    .ToList();

                // Temporarily increase source capacity
                int origSourceCapacity = sourceNode.MaxDegree;
                sourceNode.MaxDegree++;

                // Try each target node, temporarily increasing its capacity too
                foreach (var targetNode in targetNodes)
                {
                    int origTargetCapacity = targetNode.MaxDegree;
                    targetNode.MaxDegree++;

                    if (TryForceConnection(state, sourceNode))
                    {
                        // Debug.Log($"[PlanarForest] EnsureCrossConnection: Created cross-connection from node {sourceNode.Id} (expanded capacity)");
                        return; // Success! Keep both increased capacities
                    }

                    // Restore target capacity
                    targetNode.MaxDegree = origTargetCapacity;
                }

                // Restore source capacity
                sourceNode.MaxDegree = origSourceCapacity;
            }

            // Debug.LogWarning($"[PlanarForest] Could not force cross-connection even with increased capacity");
        }

        private static bool TryForceConnection(ForestMapState state, Node node)
        {
            // Find this node's parent (the node it was originally grown from)
            var parentEdge = state.Edges.FirstOrDefault(e =>
                e.NodeB.HasValue && e.NodeB.Value == node.Id);

            int? parentNodeId = parentEdge?.NodeA;

            // Try to force a connection to any existing node except parent
            // Use allowForceCapacity to expand target node capacity if needed
            return TryConnectToExisting(state, node, parentNodeId, parentNodeId, allowForceCapacity: true);
        }

        private static HashSet<int> GetConnectedNodeIds(ForestMapState state, int nodeId)
        {
            var connected = new HashSet<int>();
            foreach (var edge in state.Edges)
            {
                if (!edge.NodeB.HasValue)
                {
                    continue;
                }

                if (edge.NodeA == nodeId)
                {
                    connected.Add(edge.NodeB.Value);
                }
                else if (edge.NodeB.Value == nodeId)
                {
                    connected.Add(edge.NodeA);
                }
            }

            return connected;
        }

        // Edge merging parameters
        private const float MERGE_DISTANCE = 3.0f; // Distance below which edges should merge (walls can overlap beyond this)
        private const float NODE_PROXIMITY_SKIP = NODE_RADIUS + 1.0f; // Don't merge points this close to shared nodes
        private const float MAX_DIVERGENCE_ANGLE = 30.0f; // Maximum angle (degrees) at any turn during divergence
        private const float DIVERGENCE_SEGMENT_LENGTH = 2.0f; // Length of segments when building divergence path

        /// <summary>
        /// Merges nearby edge segments that are too close together to fit wall/tree models between them.
        /// Only modifies edges with index >= firstNewEdgeIndex (new edges created this step).
        /// Existing edges are never modified to prevent wall gaps.
        /// </summary>
        public static void MergeNearbyEdges(ForestMapState state, int firstNewEdgeIndex)
        {
            if (state.Edges.Count < 2)
                return;

            int mergeCount = 0;
            int pass = 0;
            const int MAX_PASSES = 20; // Safety limit to prevent infinite loops

            // Loop until no more merges are needed
            while (pass < MAX_PASSES)
            {
                pass++;
                bool anyMerged = false;

                // Only iterate over NEW edges (index >= firstNewEdgeIndex)
                // These are the only edges that can be modified
                for (int newEdgeIdx = firstNewEdgeIndex; newEdgeIdx < state.Edges.Count; newEdgeIdx++)
                {
                    var newEdge = state.Edges[newEdgeIdx];
                    if (newEdge.PolylinePoints.Count < 2)
                        continue;

                    // Check against ALL other edges (including existing ones)
                    for (int otherIdx = 0; otherIdx < state.Edges.Count; otherIdx++)
                    {
                        if (otherIdx == newEdgeIdx)
                            continue;

                        var otherEdge = state.Edges[otherIdx];
                        if (otherEdge.PolylinePoints.Count < 2)
                            continue;

                        // Get shared node if any (to skip points near it)
                        int? sharedNode = GetSharedNode(newEdge, otherEdge);
                        Vector2? sharedNodePos = null;
                        if (sharedNode.HasValue)
                        {
                            var node = state.Nodes.FirstOrDefault(n => n.Id == sharedNode.Value);
                            if (node != null)
                                sharedNodePos = node.Position;
                        }

                        // Only merge edges that share a node
                        if (sharedNodePos.HasValue)
                        {
                            // newEdge is the one that will be modified to merge onto otherEdge
                            if (TryMergeNewEdgeOntoExisting(newEdge, otherEdge, sharedNodePos.Value, ref mergeCount))
                            {
                                anyMerged = true;
                            }
                        }
                    }
                }

                if (!anyMerged)
                {
                    break;
                }
            }

            // Enforce minimum edge spacing at each node
            // Only modify new edges (index >= firstNewEdgeIndex)
            EnforceMinimumEdgeSpacing(state, firstNewEdgeIndex);
        }

        /// <summary>
        /// Minimum arc length between edge intersection points on node circumference.
        /// Edges closer than this will have wall voids between them.
        /// </summary>
        private const float MIN_EDGE_SPACING = 1.5f;

        /// <summary>
        /// Enforces minimum spacing between edges at each node.
        /// When edges connect to a node too close together on the circumference,
        /// adjusts their starting points to ensure at least MIN_EDGE_SPACING units apart.
        /// Only modifies edges with index >= firstNewEdgeIndex (new edges).
        /// IMPORTANT: Preserves the last segment direction for frontier edges (portal orientation).
        /// </summary>
        private static void EnforceMinimumEdgeSpacing(ForestMapState state, int firstNewEdgeIndex)
        {
            foreach (var node in state.Nodes)
            {
                // Find all edges connected to this node, tracking their index
                List<(Edge edge, int edgeIndex, bool startsAtNode, float angle)> connectedEdges = new List<(Edge, int, bool, float)>();

                for (int edgeIdx = 0; edgeIdx < state.Edges.Count; edgeIdx++)
                {
                    var edge = state.Edges[edgeIdx];
                    if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2) continue;

                    float distStart = Vector2.Distance(edge.PolylinePoints[0], node.Position);
                    float distEnd = Vector2.Distance(edge.PolylinePoints[edge.PolylinePoints.Count - 1], node.Position);

                    // Use <= for the closer check to handle equal distances
                    bool startsAtNode = distStart <= distEnd && distStart < NODE_RADIUS + 2f;
                    bool endsAtNode = distEnd < distStart && distEnd < NODE_RADIUS + 2f;

                    if (startsAtNode)
                    {
                        // Edge starts at this node - get direction toward second point
                        Vector2 dir = (edge.PolylinePoints[1] - node.Position).normalized;
                        float angle = Mathf.Atan2(dir.y, dir.x);
                        connectedEdges.Add((edge, edgeIdx, true, angle));
                    }
                    else if (endsAtNode)
                    {
                        // Edge ends at this node - get direction toward second-to-last point
                        Vector2 dir = (edge.PolylinePoints[edge.PolylinePoints.Count - 2] - node.Position).normalized;
                        float angle = Mathf.Atan2(dir.y, dir.x);
                        connectedEdges.Add((edge, edgeIdx, false, angle));
                    }
                }

                if (connectedEdges.Count < 2)
                {
                    continue;
                }

                // Sort by angle
                connectedEdges.Sort((a, b) => a.angle.CompareTo(b.angle));

                // Calculate minimum angular separation for MIN_EDGE_SPACING arc length
                float minAngleSeparation = MIN_EDGE_SPACING / NODE_RADIUS; // radians

                // Check each consecutive pair (including wrap-around)
                for (int i = 0; i < connectedEdges.Count; i++)
                {
                    int nextIdx = (i + 1) % connectedEdges.Count;
                    var current = connectedEdges[i];
                    var next = connectedEdges[nextIdx];

                    float angleDiff = next.angle - current.angle;
                    if (i == connectedEdges.Count - 1) // Wrap-around case
                    {
                        angleDiff = (next.angle + 2 * Mathf.PI) - current.angle;
                    }

                    // If too close, only adjust the edge if it's NEW (index >= firstNewEdgeIndex)
                    // Never modify existing edges
                    if (angleDiff < minAngleSeparation && angleDiff > 0.01f)
                    {
                        // Try to adjust the "next" edge if it's new
                        bool nextIsNew = next.edgeIndex >= firstNewEdgeIndex;
                        bool currentIsNew = current.edgeIndex >= firstNewEdgeIndex;

                        if (!nextIsNew && !currentIsNew)
                        {
                            // Both edges are existing - cannot adjust, skip
                            continue;
                        }

                        // Prefer to adjust the new edge
                        var edgeToAdjust = nextIsNew ? next : current;
                        float adjustment = minAngleSeparation - angleDiff;
                        float newAngle = nextIsNew
                            ? next.angle + adjustment
                            : current.angle - adjustment;  // Move current backward if it's the new one

                        // Adjust the edge's polyline to use the new angle
                        // Since we only adjust NEW edges, they will get walls generated
                        // as part of normal incremental rendering - no special tracking needed
                        AdjustEdgeAngleAtNode(edgeToAdjust.edge, edgeToAdjust.startsAtNode, node.Position, edgeToAdjust.angle, newAngle, state);

                        // Update the angle in our list for subsequent comparisons
                        int idxToUpdate = nextIsNew ? nextIdx : i;
                        connectedEdges[idxToUpdate] = (edgeToAdjust.edge, edgeToAdjust.edgeIndex, edgeToAdjust.startsAtNode, newAngle);
                    }
                }
            }
        }

        /// <summary>
        /// Adjusts an edge's polyline to change its angle at a node.
        /// Rotates points near the node to achieve the new angle.
        /// ALWAYS preserves the frontier endpoint (first or last point depending on direction).
        /// Rotates at most 2 points near the node to change the angle.
        /// </summary>
        private static void AdjustEdgeAngleAtNode(Edge edge, bool startsAtNode, Vector2 nodePos, float oldAngle, float newAngle, ForestMapState state)
        {
            float angleDelta = newAngle - oldAngle;
            int count = edge.PolylinePoints.Count;

            if (count < 3)
            {
                // Debug.LogWarning($"[EdgeSpacing] Edge {edge.Id} has only {count} points, cannot adjust");
                return;
            }

            if (startsAtNode)
            {
                // Edge starts at node, frontier is at end (last point)
                // We MUST rotate at least points 0 and 1 to change the angle at the node
                // (angle is determined by direction from node center to point 1)
                // NEVER rotate the frontier endpoint (last point) - it must stay fixed for wall placement
                int maxIndex = Mathf.Min(2, count - 1);  // Rotate at most 2 points, never the last

                // Debug.Log($"[EdgeSpacing] Edge {edge.Id}: rotating points 0 to {maxIndex-1} (count={count})");

                // Store old positions for fill generation
                List<Vector2> oldPositions = new List<Vector2>();
                for (int i = 0; i < maxIndex; i++)
                {
                    oldPositions.Add(edge.PolylinePoints[i]);
                }

                // Rotate points
                for (int i = 0; i < maxIndex; i++)
                {
                    Vector2 point = edge.PolylinePoints[i];
                    float dist = Vector2.Distance(point, nodePos);

                    // Rotate around node center
                    Vector2 offset = point - nodePos;
                    float pointAngle = Mathf.Atan2(offset.y, offset.x);
                    float newPointAngle = pointAngle + angleDelta;
                    Vector2 newOffset = new Vector2(Mathf.Cos(newPointAngle), Mathf.Sin(newPointAngle)) * dist;
                    edge.PolylinePoints[i] = nodePos + newOffset;
                }

                // Add fill segments from old to new positions (covers the swept area)
                for (int i = 0; i < maxIndex; i++)
                {
                    Vector2 oldPos = oldPositions[i];
                    Vector2 newPos = edge.PolylinePoints[i];
                    if (Vector2.Distance(oldPos, newPos) > 0.1f)
                    {
                        state.AdjustmentFills.Add((oldPos, newPos));
                        // Debug.Log($"[EdgeSpacing] Added fill segment from {oldPos} to {newPos}");
                    }
                }

                // Also add fill between old point 1 and new point 0 to cover corner
                if (maxIndex >= 2 && oldPositions.Count >= 2)
                {
                    state.AdjustmentFills.Add((oldPositions[1], edge.PolylinePoints[0]));
                }
            }
            else
            {
                // Edge ends at node, frontier is at start (first point)
                // We MUST rotate at least the last 2 points to change the angle at the node
                // NEVER rotate the frontier endpoint (first point) - it must stay fixed for wall placement
                int minIndex = Mathf.Max(count - 2, 1);  // Rotate at most last 2 points, never the first

                // Debug.Log($"[EdgeSpacing] Edge {edge.Id}: rotating points {minIndex} to {count-1} (count={count})");

                // Store old positions for fill generation
                List<Vector2> oldPositions = new List<Vector2>();
                for (int i = minIndex; i < count; i++)
                {
                    oldPositions.Add(edge.PolylinePoints[i]);
                }

                // Rotate points
                for (int i = count - 1; i >= minIndex; i--)
                {
                    Vector2 point = edge.PolylinePoints[i];
                    float dist = Vector2.Distance(point, nodePos);

                    Vector2 offset = point - nodePos;
                    float pointAngle = Mathf.Atan2(offset.y, offset.x);
                    float newPointAngle = pointAngle + angleDelta;
                    Vector2 newOffset = new Vector2(Mathf.Cos(newPointAngle), Mathf.Sin(newPointAngle)) * dist;
                    edge.PolylinePoints[i] = nodePos + newOffset;
                }

                // Add fill segments from old to new positions (covers the swept area)
                for (int i = 0; i < oldPositions.Count; i++)
                {
                    Vector2 oldPos = oldPositions[i];
                    Vector2 newPos = edge.PolylinePoints[minIndex + i];
                    if (Vector2.Distance(oldPos, newPos) > 0.1f)
                    {
                        state.AdjustmentFills.Add((oldPos, newPos));
                        // Debug.Log($"[EdgeSpacing] Added fill segment from {oldPos} to {newPos}");
                    }
                }

                // Also add fill between old second-to-last and new last point to cover corner
                if (oldPositions.Count >= 2)
                {
                    state.AdjustmentFills.Add((oldPositions[0], edge.PolylinePoints[count - 1]));
                }
            }
        }

        /// <summary>
        /// Returns the shared node ID if the two edges share exactly one node, null otherwise.
        /// </summary>
        private static int? GetSharedNode(Edge edge1, Edge edge2)
        {
            List<int> shared = new List<int>();

            if (edge1.NodeA == edge2.NodeA || edge1.NodeA == edge2.NodeB)
                shared.Add(edge1.NodeA);
            if (edge1.NodeB.HasValue)
            {
                if (edge1.NodeB.Value == edge2.NodeA)
                    shared.Add(edge2.NodeA);
                else if (edge1.NodeB.Value == edge2.NodeB)
                    shared.Add(edge1.NodeB.Value);
            }

            return shared.Count == 1 ? shared[0] : (int?)null;
        }

        private static bool TryMergeEdgeSegments(Edge edge1, Edge edge2, Vector2? sharedNodePos, ref int mergeCount)
        {
            // Debug.Log($"[EdgeMerge] Checking edge {edge1.Id} (nodes {edge1.NodeA}->{edge1.NodeB}) vs edge {edge2.Id} (nodes {edge2.NodeA}->{edge2.NodeB}), sharedNode={sharedNodePos}");

            // If edges share a node, try special shared-node merge first
            if (sharedNodePos.HasValue)
            {
                bool sharedMerge = TryMergeEdgesFromSharedNode(edge1, edge2, sharedNodePos.Value, ref mergeCount);
                if (sharedMerge)
                    return true;
            }

            // Try to merge edge2 onto edge1, then edge1 onto edge2
            bool merged1 = TryMergeEdgeOntoTarget(edge2, edge1, sharedNodePos, ref mergeCount);
            bool merged2 = TryMergeEdgeOntoTarget(edge1, edge2, sharedNodePos, ref mergeCount);
            return merged1 || merged2;
        }

        /// <summary>
        /// Merges a new edge onto an existing edge at a shared node.
        /// Only the newEdge is modified - existingEdge is never changed.
        /// </summary>
        private static bool TryMergeNewEdgeOntoExisting(Edge newEdge, Edge existingEdge, Vector2 sharedNodePos, ref int mergeCount)
        {
            // Call the shared node merge with newEdge as edge2 (the one that gets modified)
            // existingEdge is edge1 (the target that newEdge merges onto)
            return TryMergeEdgesFromSharedNode(existingEdge, newEdge, sharedNodePos, ref mergeCount);
        }

        /// <summary>
        /// Special merge handling for edges that share a node.
        /// Handles both diverging edges (from shared node) and converging edges (toward shared node).
        /// NOTE: This function only modifies edge2, never edge1.
        /// </summary>
        private static bool TryMergeEdgesFromSharedNode(Edge edge1, Edge edge2, Vector2 sharedNodePos, ref int mergeCount)
        {
            if (edge1.PolylinePoints.Count < 2 || edge2.PolylinePoints.Count < 2)
            {
                return false;
            }

            // Determine which end of each edge is near the shared node
            float dist1Start = Vector2.Distance(edge1.PolylinePoints[0], sharedNodePos);
            float dist1End = Vector2.Distance(edge1.PolylinePoints[edge1.PolylinePoints.Count - 1], sharedNodePos);
            float dist2Start = Vector2.Distance(edge2.PolylinePoints[0], sharedNodePos);
            float dist2End = Vector2.Distance(edge2.PolylinePoints[edge2.PolylinePoints.Count - 1], sharedNodePos);

            bool edge1StartsAtNode = dist1Start < dist1End;
            bool edge2StartsAtNode = dist2Start < dist2End;

            // Get the polylines oriented from the shared node outward
            List<Vector2> poly1 = edge1StartsAtNode
                ? new List<Vector2>(edge1.PolylinePoints)
                : edge1.PolylinePoints.AsEnumerable().Reverse().ToList();
            List<Vector2> poly2 = edge2StartsAtNode
                ? new List<Vector2>(edge2.PolylinePoints)
                : edge2.PolylinePoints.AsEnumerable().Reverse().ToList();

            // Prepend the shared node position if the polylines don't start there
            // This ensures both polylines start at the same point for proper merge detection
            float nodeProximityThreshold = 1.0f;
            if (poly1.Count == 0 || Vector2.Distance(poly1[0], sharedNodePos) > nodeProximityThreshold)
            {
                poly1.Insert(0, sharedNodePos);
            }
            if (poly2.Count == 0 || Vector2.Distance(poly2[0], sharedNodePos) > nodeProximityThreshold)
            {
                poly2.Insert(0, sharedNodePos);
            }

            // Check distances at each point of poly2 against poly1
            List<float> distances = new List<float>();
            List<Vector2> closestPointsOnPoly1 = new List<Vector2>();
            List<int> closestSegmentsOnPoly1 = new List<int>();

            for (int i = 0; i < poly2.Count; i++)
            {
                int segIdx;
                Vector2 closest = ClosestPointOnPolylineWithSegment(poly2[i], poly1, out segIdx);
                float dist = Vector2.Distance(poly2[i], closest);

                distances.Add(dist);
                closestPointsOnPoly1.Add(closest);
                closestSegmentsOnPoly1.Add(segIdx);
            }

            // Determine if this is a diverging case (close at start) or converging case (close at end)
            bool closeAtStart = distances.Count > 0 && distances[0] < MERGE_DISTANCE;
            bool closeAtEnd = distances.Count > 0 && distances[distances.Count - 1] < MERGE_DISTANCE;

            if (closeAtStart)
            {
                // DIVERGING CASE: edges share path from node, then split
                return TryMergeDivergingEdges(edge1, edge2, edge1StartsAtNode, edge2StartsAtNode, poly1, poly2,
                    distances, closestPointsOnPoly1, closestSegmentsOnPoly1, ref mergeCount);
            }
            else if (closeAtEnd)
            {
                // CONVERGING CASE: edges approach node from different directions, merge before node
                return TryMergeConvergingEdges(edge1, edge2, edge1StartsAtNode, edge2StartsAtNode, poly1, poly2,
                    distances, closestPointsOnPoly1, closestSegmentsOnPoly1, ref mergeCount);
            }

            return false;
        }

        /// <summary>
        /// Handles diverging edges that share a path from the node then split.
        /// CRITICAL: Copies exact world space coordinates from edge1.PolylinePoints, never synthetic points.
        /// </summary>
        private static bool TryMergeDivergingEdges(Edge edge1, Edge edge2, bool edge1StartsAtNode, bool edge2StartsAtNode,
            List<Vector2> poly1, List<Vector2> poly2, List<float> distances,
            List<Vector2> closestPointsOnPoly1, List<int> closestSegmentsOnPoly1, ref int mergeCount)
        {
            // Find where they diverge (first point beyond MERGE_DISTANCE after being close)
            int divergeIdx2 = -1;
            int lastCloseIdx2 = -1;

            for (int i = 0; i < distances.Count; i++)
            {
                if (distances[i] < MERGE_DISTANCE)
                {
                    lastCloseIdx2 = i;
                }
                else if (lastCloseIdx2 >= 0 && divergeIdx2 < 0)
                {
                    divergeIdx2 = i;
                    break;
                }
            }

            // Debug.Log($"[EdgeMerge] DIVERGING: lastCloseIdx2={lastCloseIdx2}, divergeIdx2={divergeIdx2}");

            // Special case: only point 0 is close (edges meet at node but immediately diverge)
            // We need to extend the shared path along edge1 until the paths are MERGE_DISTANCE apart
            if (lastCloseIdx2 == 0 && distances.Count > 1 && distances[1] >= MERGE_DISTANCE)
            {
                // Debug.Log($"[EdgeMerge] DIVERGING: Special case - only point 0 close, extending shared path");

                // Get the first point from edge1's ACTUAL polyline (exact coordinates)
                Vector2 edge1StartPoint = edge1StartsAtNode ? edge1.PolylinePoints[0] : edge1.PolylinePoints[edge1.PolylinePoints.Count - 1];
                Vector2 edge1SecondPoint = edge1StartsAtNode ? edge1.PolylinePoints[Math.Min(1, edge1.PolylinePoints.Count - 1)]
                    : edge1.PolylinePoints[Math.Max(0, edge1.PolylinePoints.Count - 2)];

                Vector2 edge1Dir = (edge1SecondPoint - edge1StartPoint).normalized;
                if (edge1Dir.magnitude < 0.01f && edge1.PolylinePoints.Count > 2)
                {
                    // Points are too close, use next segment
                    edge1SecondPoint = edge1StartsAtNode ? edge1.PolylinePoints[Math.Min(2, edge1.PolylinePoints.Count - 1)]
                        : edge1.PolylinePoints[Math.Max(0, edge1.PolylinePoints.Count - 3)];
                    edge1Dir = (edge1SecondPoint - edge1StartPoint).normalized;
                }

                // Walk along edge1 direction until we're MERGE_DISTANCE from the line to poly2[1]
                float stepSize = 0.5f;
                float maxDist = Vector2.Distance(edge1StartPoint, edge1SecondPoint);
                Vector2 branchPoint = edge1StartPoint;

                for (float d = 0; d < maxDist; d += stepSize)
                {
                    Vector2 testPoint = edge1StartPoint + edge1Dir * d;
                    float distToPoly2 = Vector2.Distance(testPoint, poly2[1]);

                    // Also check distance to the line segment poly2[0]->poly2[1]
                    float distToSegment = DistanceToLineSegment(testPoint, poly2[0], poly2[1]);

                    if (distToSegment >= MERGE_DISTANCE || distToPoly2 >= MERGE_DISTANCE * 2)
                    {
                        branchPoint = testPoint;
                        break;
                    }
                    branchPoint = testPoint;
                }

                // Debug.Log($"[EdgeMerge] BOUNDARY: branchPoint={branchPoint}");

                List<Vector2> boundaryNewPoly = new List<Vector2>();
                // Start with the EXACT coordinate from edge1
                boundaryNewPoly.Add(edge1StartPoint);

                // Add branch point if different from start
                if (Vector2.Distance(branchPoint, edge1StartPoint) > 0.1f)
                {
                    boundaryNewPoly.Add(branchPoint);
                }

                // Build smooth path from branchPoint to poly2[1] with max 30-degree turns
                // instead of using a perpendicular junction
                Vector2 boundaryTarget = poly2[1];
                float boundaryDist = Vector2.Distance(branchPoint, boundaryTarget);

                if (boundaryDist > 0.5f)
                {
                    var smoothPath = BuildSmoothDivergencePath(branchPoint, edge1Dir, boundaryTarget);
                    foreach (var pt in smoothPath)
                    {
                        if (boundaryNewPoly.Count == 0 || Vector2.Distance(boundaryNewPoly[boundaryNewPoly.Count - 1], pt) > 0.1f)
                        {
                            boundaryNewPoly.Add(pt);
                        }
                    }
                }

                // Add remaining poly2 points starting from poly2[2] (we already included poly2[1] via smoothPath)
                for (int i = 2; i < poly2.Count; i++)
                {
                    // Only add if it's meaningfully different from the last point
                    if (Vector2.Distance(boundaryNewPoly[boundaryNewPoly.Count - 1], poly2[i]) > 0.1f)
                    {
                        boundaryNewPoly.Add(poly2[i]);
                    }
                }

                if (!edge2StartsAtNode)
                {
                    boundaryNewPoly.Reverse();
                }

                edge2.PolylinePoints.Clear();
                edge2.PolylinePoints.AddRange(boundaryNewPoly);

                mergeCount++;
                // Debug.Log($"[EdgeMerge] DIVERGING SUCCESS (boundary case): extended to {branchPoint}, newPoly has {boundaryNewPoly.Count} pts");
                return true;
            }

            if (lastCloseIdx2 < 1)
            {
                // Debug.Log($"[EdgeMerge] DIVERGING: FAIL - lastCloseIdx2={lastCloseIdx2} < 1");
                return false;
            }

            if (divergeIdx2 < 0)
            {
                if (lastCloseIdx2 < poly2.Count - 1)
                    divergeIdx2 = lastCloseIdx2 + 1;
                else
                {
                    // Debug.Log($"[EdgeMerge] DIVERGING: FAIL - no divergence found");
                    return false;
                }
            }

            // Get merge point info
            int targetSegment = closestSegmentsOnPoly1[lastCloseIdx2];
            Vector2 closestOnPoly1 = closestPointsOnPoly1[lastCloseIdx2];
            Vector2 targetDir = GetSegmentDirection(poly1, targetSegment);

            // Build new polyline using EXACT coordinates from edge1.PolylinePoints
            // poly1 may have synthetic points and be reoriented - we need the actual edge coordinates
            List<Vector2> newPoly2 = new List<Vector2>();

            // Copy exact points from edge1's actual polyline up to the merge point
            // targetSegment is indexed into poly1 which is oriented from shared node outward
            // We need to translate this to edge1.PolylinePoints indices
            int edge1Count = edge1.PolylinePoints.Count;

            if (edge1StartsAtNode)
            {
                // edge1 starts at the shared node - copy points 0, 1, 2, ... up to targetSegment
                // poly1 may have had a synthetic point prepended, so we need to check
                // poly1[0] might be sharedNodePos (synthetic) or edge1.PolylinePoints[0]
                // We use edge1.PolylinePoints directly to get exact coordinates

                // Find how many edge1 points to copy by matching to targetSegment
                // targetSegment in poly1 corresponds to segment in edge1
                // If poly1 had synthetic point at 0, targetSegment in edge1 is targetSegment-1
                bool poly1HasSyntheticStart = poly1.Count > edge1Count;
                int edge1TargetSegment = poly1HasSyntheticStart ? Math.Max(0, targetSegment - 1) : targetSegment;

                for (int i = 0; i <= edge1TargetSegment && i < edge1Count; i++)
                {
                    newPoly2.Add(edge1.PolylinePoints[i]);
                }
            }
            else
            {
                // edge1 ends at the shared node - poly1 is reversed
                // poly1[i] corresponds to edge1.PolylinePoints[edge1Count - 1 - i]
                // Copy from the end of edge1 toward the start
                bool poly1HasSyntheticStart = poly1.Count > edge1Count;
                int edge1TargetSegment = poly1HasSyntheticStart ? Math.Max(0, targetSegment - 1) : targetSegment;

                for (int i = 0; i <= edge1TargetSegment; i++)
                {
                    int edge1Idx = edge1Count - 1 - i;
                    if (edge1Idx >= 0 && edge1Idx < edge1Count)
                    {
                        newPoly2.Add(edge1.PolylinePoints[edge1Idx]);
                    }
                }
            }

            // Add the closest point on the path (may be between vertices)
            if (newPoly2.Count == 0 || Vector2.Distance(newPoly2[newPoly2.Count - 1], closestOnPoly1) > 0.1f)
            {
                newPoly2.Add(closestOnPoly1);
            }

            // Build a smooth divergence path from closestOnPoly1 to poly2[divergeIdx2]
            // instead of using a perpendicular junction which creates sharp angles
            Vector2 divergeTarget = poly2[divergeIdx2];
            float distToDiverge = Vector2.Distance(closestOnPoly1, divergeTarget);

            if (distToDiverge > 0.5f)
            {
                // Get the current direction (along edge1's path)
                Vector2 currentDir = targetDir;

                // Build smooth path with max 30-degree turns
                var smoothPath = BuildSmoothDivergencePath(closestOnPoly1, currentDir, divergeTarget);

                // Add smooth path points (excluding first which is closestOnPoly1, including divergeTarget)
                foreach (var pt in smoothPath)
                {
                    if (newPoly2.Count == 0 || Vector2.Distance(newPoly2[newPoly2.Count - 1], pt) > 0.1f)
                    {
                        newPoly2.Add(pt);
                    }
                }
            }

            // Add remaining poly2 points after the divergence point
            for (int i = divergeIdx2 + 1; i < poly2.Count; i++)
            {
                if (newPoly2.Count == 0 || Vector2.Distance(newPoly2[newPoly2.Count - 1], poly2[i]) > 0.1f)
                {
                    newPoly2.Add(poly2[i]);
                }
            }

            if (!edge2StartsAtNode)
            {
                newPoly2.Reverse();
            }

            edge2.PolylinePoints.Clear();
            edge2.PolylinePoints.AddRange(newPoly2);

            mergeCount++;
            // Debug.Log($"[EdgeMerge] DIVERGING SUCCESS: merged at lastClose={lastCloseIdx2}, diverge={divergeIdx2}");
            return true;
        }

        /// <summary>
        /// Handles converging edges that approach the node from different directions and should merge before reaching it.
        /// CRITICAL: Copies exact world space coordinates from edge1.PolylinePoints, never synthetic points.
        /// </summary>
        private static bool TryMergeConvergingEdges(Edge edge1, Edge edge2, bool edge1StartsAtNode, bool edge2StartsAtNode,
            List<Vector2> poly1, List<Vector2> poly2, List<float> distances,
            List<Vector2> closestPointsOnPoly1, List<int> closestSegmentsOnPoly1, ref int mergeCount)
        {
            // Debug.Log($"[EdgeMerge] CONVERGING: poly2 has {poly2.Count} pts, searching for convergence point...");

            // Find where they converge (searching from end toward start)
            // Find the first point (from the far end) that becomes close
            int firstCloseIdx2 = -1;
            int convergeIdx2 = -1;

            for (int i = distances.Count - 1; i >= 0; i--)
            {
                if (distances[i] < MERGE_DISTANCE)
                {
                    firstCloseIdx2 = i;
                    // Debug.Log($"[EdgeMerge] CONVERGING: point {i} is close (dist={distances[i]:F2})");
                }
                else if (firstCloseIdx2 >= 0 && convergeIdx2 < 0)
                {
                    convergeIdx2 = i;
                    // Debug.Log($"[EdgeMerge] CONVERGING: point {i} diverges (dist={distances[i]:F2}), convergeIdx2={convergeIdx2}");
                    break;
                }
            }

            // Debug.Log($"[EdgeMerge] CONVERGING: firstCloseIdx2={firstCloseIdx2}, convergeIdx2={convergeIdx2}, poly2.Count={poly2.Count}");

            if (firstCloseIdx2 < 0 || firstCloseIdx2 >= poly2.Count - 1)
            {
                // Debug.Log($"[EdgeMerge] CONVERGING: FAIL - firstCloseIdx2={firstCloseIdx2} invalid (need 0 <= x < {poly2.Count - 1})");
                return false;
            }

            // If no convergence point found, they're close throughout from some point
            if (convergeIdx2 < 0)
            {
                convergeIdx2 = 0;
                // Debug.Log($"[EdgeMerge] CONVERGING: No divergence found, using convergeIdx2=0");
            }

            // Get merge point info - where poly2 should join poly1's path
            int targetSegment = closestSegmentsOnPoly1[firstCloseIdx2];
            Vector2 closestOnPoly1 = closestPointsOnPoly1[firstCloseIdx2];
            Vector2 targetDir = GetSegmentDirection(poly1, targetSegment);

            // Build new polyline: poly2's path up to convergence, smooth curve to poly1, then poly1's path to node
            List<Vector2> newPoly2 = new List<Vector2>();

            // Add poly2 points up to (but not including) convergence point
            for (int i = 0; i < convergeIdx2; i++)
            {
                newPoly2.Add(poly2[i]);
            }

            // Get direction of poly2 at the convergence point
            Vector2 poly2Dir = convergeIdx2 > 0
                ? (poly2[convergeIdx2] - poly2[convergeIdx2 - 1]).normalized
                : (poly2[1] - poly2[0]).normalized;

            // Build smooth path from poly2[convergeIdx2] to closestOnPoly1 with max 30-degree turns
            Vector2 convergeStart = poly2[convergeIdx2];
            float distToMerge = Vector2.Distance(convergeStart, closestOnPoly1);

            if (distToMerge > 0.5f)
            {
                // Add the convergence start point
                if (newPoly2.Count == 0 || Vector2.Distance(newPoly2[newPoly2.Count - 1], convergeStart) > 0.1f)
                {
                    newPoly2.Add(convergeStart);
                }

                var smoothPath = BuildSmoothDivergencePath(convergeStart, poly2Dir, closestOnPoly1);
                foreach (var pt in smoothPath)
                {
                    if (newPoly2.Count == 0 || Vector2.Distance(newPoly2[newPoly2.Count - 1], pt) > 0.1f)
                    {
                        newPoly2.Add(pt);
                    }
                }
            }
            else
            {
                // Points are very close, just add them directly
                if (newPoly2.Count == 0 || Vector2.Distance(newPoly2[newPoly2.Count - 1], convergeStart) > 0.1f)
                {
                    newPoly2.Add(convergeStart);
                }
                if (newPoly2.Count == 0 || Vector2.Distance(newPoly2[newPoly2.Count - 1], closestOnPoly1) > 0.1f)
                {
                    newPoly2.Add(closestOnPoly1);
                }
            }

            // Add poly1's path from merge point to node using EXACT coordinates from edge1.PolylinePoints
            // poly1 is oriented from shared node outward, so we need to add points from targetSegment down to 0
            // But we must use edge1.PolylinePoints (exact coordinates) rather than poly1 (may have synthetic points)
            int edge1Count = edge1.PolylinePoints.Count;
            bool poly1HasSyntheticStart = poly1.Count > edge1Count;
            int edge1TargetSegment = poly1HasSyntheticStart ? Math.Max(0, targetSegment - 1) : targetSegment;

            if (edge1StartsAtNode)
            {
                // edge1 starts at the shared node
                // poly1 is in same order as edge1, we need to add points from targetSegment down to 0
                // In edge1 terms: add points from edge1TargetSegment down to 0
                for (int i = edge1TargetSegment; i >= 0; i--)
                {
                    if (i < edge1Count)
                    {
                        if (newPoly2.Count == 0 || Vector2.Distance(newPoly2[newPoly2.Count - 1], edge1.PolylinePoints[i]) > 0.1f)
                        {
                            newPoly2.Add(edge1.PolylinePoints[i]);
                        }
                    }
                }
            }
            else
            {
                // edge1 ends at the shared node - poly1 is reversed
                // poly1[i] corresponds to edge1.PolylinePoints[edge1Count - 1 - i]
                // We want to add points toward the node (which is at end of edge1)
                // From poly1 perspective: targetSegment down to 0
                // In edge1 terms: (edge1Count-1-targetSegment) up to (edge1Count-1)
                int startIdx = edge1Count - 1 - edge1TargetSegment;
                for (int i = startIdx; i < edge1Count; i++)
                {
                    if (i >= 0)
                    {
                        if (newPoly2.Count == 0 || Vector2.Distance(newPoly2[newPoly2.Count - 1], edge1.PolylinePoints[i]) > 0.1f)
                        {
                            newPoly2.Add(edge1.PolylinePoints[i]);
                        }
                    }
                }
            }

            if (!edge2StartsAtNode)
            {
                newPoly2.Reverse();
            }

            edge2.PolylinePoints.Clear();
            edge2.PolylinePoints.AddRange(newPoly2);

            mergeCount++;
            // Debug.Log($"[EdgeMerge] CONVERGING SUCCESS: merged at firstClose={firstCloseIdx2}, converge={convergeIdx2}, newPoly2 has {newPoly2.Count} pts");
            return true;
        }

        /// <summary>
        /// Checks for parallel segments between two edges that are too close for wall tiles.
        /// When found, merges one edge's path onto the other using perpendicular junctions.
        /// </summary>
        private static bool TryMergeParallelSegments(Edge edge1, Edge edge2, Vector2? sharedNodePos, ref int mergeCount)
        {
            if (edge1.PolylinePoints.Count < 2 || edge2.PolylinePoints.Count < 2)
                return false;

            // Check each segment of edge1 against each segment of edge2
            for (int i = 0; i < edge1.PolylinePoints.Count - 1; i++)
            {
                Vector2 a1 = edge1.PolylinePoints[i];
                Vector2 a2 = edge1.PolylinePoints[i + 1];

                // Skip segments near shared node
                if (sharedNodePos.HasValue)
                {
                    if (Vector2.Distance(a1, sharedNodePos.Value) < NODE_PROXIMITY_SKIP &&
                        Vector2.Distance(a2, sharedNodePos.Value) < NODE_PROXIMITY_SKIP)
                        continue;
                }

                for (int j = 0; j < edge2.PolylinePoints.Count - 1; j++)
                {
                    Vector2 b1 = edge2.PolylinePoints[j];
                    Vector2 b2 = edge2.PolylinePoints[j + 1];

                    // Skip segments near shared node
                    if (sharedNodePos.HasValue)
                    {
                        if (Vector2.Distance(b1, sharedNodePos.Value) < NODE_PROXIMITY_SKIP &&
                            Vector2.Distance(b2, sharedNodePos.Value) < NODE_PROXIMITY_SKIP)
                            continue;
                    }

                    // Calculate minimum distance between the two segments
                    float minDist = MinDistanceBetweenSegments(a1, a2, b1, b2);

                    if (minDist < MERGE_DISTANCE && minDist > 0.01f)
                    {
                        // Found two segments that are too close - merge them
                        // Debug.Log($"[EdgeMerge] PARALLEL: edge {edge1.Id} seg {i} too close to edge {edge2.Id} seg {j} (dist={minDist:F2})");

                        // Sample points along edge1's close segment and snap them to edge2
                        // Then reroute edge1 to follow edge2 through this region
                        if (MergeParallelRegion(edge1, edge2, i, j, ref mergeCount))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Calculates the minimum distance between two line segments.
        /// </summary>
        private static float MinDistanceBetweenSegments(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            // Check endpoints of each segment against the other segment
            float d1 = DistanceToLineSegment(a1, b1, b2);
            float d2 = DistanceToLineSegment(a2, b1, b2);
            float d3 = DistanceToLineSegment(b1, a1, a2);
            float d4 = DistanceToLineSegment(b2, a1, a2);

            return Mathf.Min(Mathf.Min(d1, d2), Mathf.Min(d3, d4));
        }

        /// <summary>
        /// Merges a region where edge1's segment i is parallel and close to edge2's segment j.
        /// Reroutes edge1 to follow edge2's path through the close region.
        /// </summary>
        private static bool MergeParallelRegion(Edge edge1, Edge edge2, int seg1Idx, int seg2Idx, ref int mergeCount)
        {
            // Find the extent of the close region by checking neighboring segments
            int startSeg1 = seg1Idx;
            int endSeg1 = seg1Idx;

            // Expand backwards
            while (startSeg1 > 0)
            {
                Vector2 a1 = edge1.PolylinePoints[startSeg1 - 1];
                Vector2 a2 = edge1.PolylinePoints[startSeg1];
                float dist = MinDistancePointToPolyline(a1, edge2.PolylinePoints);
                if (dist >= MERGE_DISTANCE)
                    break;
                startSeg1--;
            }

            // Expand forwards
            while (endSeg1 < edge1.PolylinePoints.Count - 2)
            {
                Vector2 a1 = edge1.PolylinePoints[endSeg1 + 1];
                Vector2 a2 = edge1.PolylinePoints[endSeg1 + 2];
                float dist = MinDistancePointToPolyline(a2, edge2.PolylinePoints);
                if (dist >= MERGE_DISTANCE)
                    break;
                endSeg1++;
            }

            // Get entry and exit points
            Vector2 entryPoint = edge1.PolylinePoints[startSeg1];
            Vector2 exitPoint = edge1.PolylinePoints[endSeg1 + 1];

            // Find closest points on edge2 for entry and exit
            int entrySegOnE2, exitSegOnE2;
            Vector2 entryOnE2 = ClosestPointOnPolylineWithSegment(entryPoint, edge2.PolylinePoints, out entrySegOnE2);
            Vector2 exitOnE2 = ClosestPointOnPolylineWithSegment(exitPoint, edge2.PolylinePoints, out exitSegOnE2);

            // Build the new polyline for edge1
            List<Vector2> newPoly = new List<Vector2>();

            // Keep points before the close region
            for (int i = 0; i < startSeg1; i++)
            {
                newPoly.Add(edge1.PolylinePoints[i]);
            }

            // Add entry point if different from last
            if (newPoly.Count == 0 || Vector2.Distance(newPoly[newPoly.Count - 1], entryPoint) > 0.1f)
            {
                newPoly.Add(entryPoint);
            }

            // Add perpendicular junction to edge2's path
            Vector2 entryDir = GetSegmentDirection(edge2.PolylinePoints, entrySegOnE2);
            Vector2 perpEntry = CreatePerpendicularIntersection(entryPoint, entryOnE2, entryDir);
            if (Vector2.Distance(perpEntry, newPoly[newPoly.Count - 1]) > 0.1f)
            {
                newPoly.Add(perpEntry);
            }

            // Add points along edge2's path between entry and exit
            if (entrySegOnE2 <= exitSegOnE2)
            {
                for (int i = entrySegOnE2 + 1; i <= exitSegOnE2 && i < edge2.PolylinePoints.Count; i++)
                {
                    if (Vector2.Distance(edge2.PolylinePoints[i], newPoly[newPoly.Count - 1]) > 0.1f)
                    {
                        newPoly.Add(edge2.PolylinePoints[i]);
                    }
                }
            }
            else
            {
                // Reversed direction along edge2
                for (int i = entrySegOnE2; i >= exitSegOnE2 + 1 && i < edge2.PolylinePoints.Count; i--)
                {
                    if (Vector2.Distance(edge2.PolylinePoints[i], newPoly[newPoly.Count - 1]) > 0.1f)
                    {
                        newPoly.Add(edge2.PolylinePoints[i]);
                    }
                }
            }

            // Add perpendicular junction back from edge2's path to exit point
            Vector2 exitDir = GetSegmentDirection(edge2.PolylinePoints, exitSegOnE2);
            Vector2 perpExit = CreatePerpendicularIntersection(exitPoint, exitOnE2, exitDir);
            if (Vector2.Distance(perpExit, newPoly[newPoly.Count - 1]) > 0.1f)
            {
                newPoly.Add(perpExit);
            }

            // Add exit point
            if (Vector2.Distance(exitPoint, newPoly[newPoly.Count - 1]) > 0.1f)
            {
                newPoly.Add(exitPoint);
            }

            // Add points after the close region
            for (int i = endSeg1 + 2; i < edge1.PolylinePoints.Count; i++)
            {
                if (Vector2.Distance(edge1.PolylinePoints[i], newPoly[newPoly.Count - 1]) > 0.1f)
                {
                    newPoly.Add(edge1.PolylinePoints[i]);
                }
            }

            // Update edge1's polyline
            if (newPoly.Count >= 2)
            {
                edge1.PolylinePoints.Clear();
                edge1.PolylinePoints.AddRange(newPoly);
                mergeCount++;
                // Debug.Log($"[EdgeMerge] PARALLEL MERGE SUCCESS: edge {edge1.Id} rerouted through edge {edge2.Id}, new poly has {newPoly.Count} pts");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Calculates minimum distance from a point to any segment of a polyline.
        /// </summary>
        private static float MinDistancePointToPolyline(Vector2 point, List<Vector2> polyline)
        {
            float minDist = float.MaxValue;
            for (int i = 0; i < polyline.Count - 1; i++)
            {
                float dist = DistanceToLineSegment(point, polyline[i], polyline[i + 1]);
                if (dist < minDist)
                    minDist = dist;
            }
            return minDist;
        }

        /// <summary>
        /// Attempts to merge sourceEdge onto targetEdge by creating perpendicular transitions.
        /// When points on sourceEdge are too close to targetEdge, we:
        /// 1. Find where the close region starts (merge entry point)
        /// 2. Find where it ends (merge exit / divergence point)
        /// 3. Create perpendicular connectors at entry and exit
        /// 4. Replace the middle section with points on targetEdge's path
        /// </summary>
        private static bool TryMergeEdgeOntoTarget(Edge sourceEdge, Edge targetEdge, Vector2? sharedNodePos, ref int mergeCount)
        {
            if (sourceEdge.PolylinePoints.Count < 2 || targetEdge.PolylinePoints.Count < 2)
                return false;

            // Find all points in sourceEdge that are close to targetEdge
            List<int> closePointIndices = new List<int>();
            List<Vector2> closestPointsOnTarget = new List<Vector2>();
            List<int> closestSegmentIndices = new List<int>();

            for (int i = 0; i < sourceEdge.PolylinePoints.Count; i++)
            {
                Vector2 p = sourceEdge.PolylinePoints[i];

                // Skip points too close to shared node
                if (sharedNodePos.HasValue && Vector2.Distance(p, sharedNodePos.Value) < NODE_PROXIMITY_SKIP)
                    continue;

                // Find closest point on target edge's path and which segment it's on
                int closestSegIdx;
                Vector2 closestOnTarget = ClosestPointOnPolylineWithSegment(p, targetEdge.PolylinePoints, out closestSegIdx);
                float dist = Vector2.Distance(p, closestOnTarget);

                if (dist < MERGE_DISTANCE && dist > 0.01f)
                {
                    closePointIndices.Add(i);
                    closestPointsOnTarget.Add(closestOnTarget);
                    closestSegmentIndices.Add(closestSegIdx);
                }
            }

            if (closePointIndices.Count == 0)
                return false;

            // Find contiguous runs of close points
            List<(int startIdx, int endIdx)> runs = FindContiguousRuns(closePointIndices);

            bool anyMerged = false;
            int insertionOffset = 0; // Track how many points we've added/removed

            foreach (var (runStartInList, runEndInList) in runs)
            {
                int firstCloseIdx = closePointIndices[runStartInList] + insertionOffset;
                int lastCloseIdx = closePointIndices[runEndInList] + insertionOffset;

                // Get the target edge segment direction at entry and exit points
                Vector2 entryPointOnTarget = closestPointsOnTarget[runStartInList];
                Vector2 exitPointOnTarget = closestPointsOnTarget[runEndInList];
                int entrySegment = closestSegmentIndices[runStartInList];
                int exitSegment = closestSegmentIndices[runEndInList];

                // Build the new polyline section
                List<Vector2> newSection = new List<Vector2>();

                // If we're not at the start of sourceEdge, add perpendicular entry from previous point
                if (firstCloseIdx > 0)
                {
                    Vector2 prevPoint = sourceEdge.PolylinePoints[firstCloseIdx - 1];
                    Vector2 entryDir = GetSegmentDirection(targetEdge.PolylinePoints, entrySegment);

                    // Project previous point onto target edge direction to find perpendicular intersection
                    Vector2 perpEntryPoint = CreatePerpendicularIntersection(prevPoint, entryPointOnTarget, entryDir);
                    newSection.Add(perpEntryPoint);
                }
                else
                {
                    // At start of edge, just use the entry point directly
                    newSection.Add(entryPointOnTarget);
                }

                // If entry and exit are on different segments, add intermediate points along target path
                if (entrySegment != exitSegment || Vector2.Distance(entryPointOnTarget, exitPointOnTarget) > 0.5f)
                {
                    AddIntermediatePointsAlongTarget(newSection, targetEdge.PolylinePoints,
                        entryPointOnTarget, exitPointOnTarget, entrySegment, exitSegment);
                }

                // If we're not at the end of sourceEdge, add perpendicular exit to next point
                if (lastCloseIdx < sourceEdge.PolylinePoints.Count - 1)
                {
                    Vector2 nextPoint = sourceEdge.PolylinePoints[lastCloseIdx + 1];
                    Vector2 exitDir = GetSegmentDirection(targetEdge.PolylinePoints, exitSegment);

                    // Project next point onto target edge direction to find perpendicular intersection
                    Vector2 perpExitPoint = CreatePerpendicularIntersection(nextPoint, exitPointOnTarget, exitDir);

                    // Only add if different from last point in newSection
                    if (newSection.Count == 0 || Vector2.Distance(newSection[newSection.Count - 1], perpExitPoint) > 0.1f)
                    {
                        newSection.Add(perpExitPoint);
                    }
                }
                else
                {
                    // At end of edge, use exit point directly if different from last added
                    if (newSection.Count == 0 || Vector2.Distance(newSection[newSection.Count - 1], exitPointOnTarget) > 0.1f)
                    {
                        newSection.Add(exitPointOnTarget);
                    }
                }

                // Replace the close points section with our new perpendicular section
                int removeCount = lastCloseIdx - firstCloseIdx + 1;
                sourceEdge.PolylinePoints.RemoveRange(firstCloseIdx, removeCount);
                sourceEdge.PolylinePoints.InsertRange(firstCloseIdx, newSection);

                insertionOffset += newSection.Count - removeCount;
                mergeCount++;
                anyMerged = true;
            }

            return anyMerged;
        }

        /// <summary>
        /// Find contiguous runs of indices in a sorted list.
        /// </summary>
        private static List<(int startIdx, int endIdx)> FindContiguousRuns(List<int> indices)
        {
            var runs = new List<(int, int)>();
            if (indices.Count == 0) return runs;

            int runStart = 0;
            for (int i = 1; i < indices.Count; i++)
            {
                // If there's a gap of more than 1, end the current run
                if (indices[i] - indices[i - 1] > 1)
                {
                    runs.Add((runStart, i - 1));
                    runStart = i;
                }
            }
            runs.Add((runStart, indices.Count - 1));
            return runs;
        }

        /// <summary>
        /// Gets the direction vector of a segment in the polyline.
        /// </summary>
        private static Vector2 GetSegmentDirection(List<Vector2> polyline, int segmentIndex)
        {
            if (segmentIndex >= polyline.Count - 1)
                segmentIndex = polyline.Count - 2;
            if (segmentIndex < 0)
                segmentIndex = 0;

            return (polyline[segmentIndex + 1] - polyline[segmentIndex]).normalized;
        }

        /// <summary>
        /// Creates a point on the target line that forms a perpendicular intersection from sourcePoint.
        /// </summary>
        private static Vector2 CreatePerpendicularIntersection(Vector2 sourcePoint, Vector2 targetPoint, Vector2 targetDir)
        {
            // Project sourcePoint onto the line passing through targetPoint with direction targetDir
            Vector2 toSource = sourcePoint - targetPoint;
            float projection = Vector2.Dot(toSource, targetDir);
            return targetPoint + targetDir * projection;
        }

        /// <summary>
        /// Builds a smooth divergence path from startPoint to endPoint, where the path starts
        /// heading in startDirection and each turn is limited to MAX_DIVERGENCE_ANGLE degrees.
        /// Returns a list of intermediate points (not including start, but including end).
        /// </summary>
        private static List<Vector2> BuildSmoothDivergencePath(Vector2 startPoint, Vector2 startDirection, Vector2 endPoint)
        {
            var result = new List<Vector2>();
            float maxAngleRad = MAX_DIVERGENCE_ANGLE * Mathf.Deg2Rad;

            Vector2 currentPos = startPoint;
            Vector2 currentDir = startDirection.normalized;
            Vector2 toEnd = endPoint - currentPos;
            float remainingDist = toEnd.magnitude;

            // Safety limit to prevent infinite loops
            int maxIterations = 20;
            int iteration = 0;

            while (remainingDist > 0.5f && iteration < maxIterations)
            {
                iteration++;
                toEnd = endPoint - currentPos;
                remainingDist = toEnd.magnitude;
                Vector2 desiredDir = toEnd.normalized;

                // Calculate angle between current direction and desired direction
                float dot = Mathf.Clamp(Vector2.Dot(currentDir, desiredDir), -1f, 1f);
                float angleToTarget = Mathf.Acos(dot);

                // If we're already pointed close enough to the target, go straight there
                if (angleToTarget <= maxAngleRad && remainingDist <= DIVERGENCE_SEGMENT_LENGTH * 2f)
                {
                    result.Add(endPoint);
                    break;
                }

                // Determine turn direction (cross product sign)
                float cross = currentDir.x * desiredDir.y - currentDir.y * desiredDir.x;
                float turnSign = cross >= 0 ? 1f : -1f;

                // Limit the turn angle
                float turnAngle = Mathf.Min(angleToTarget, maxAngleRad) * turnSign;

                // Rotate current direction
                float cos = Mathf.Cos(turnAngle);
                float sin = Mathf.Sin(turnAngle);
                Vector2 newDir = new Vector2(
                    currentDir.x * cos - currentDir.y * sin,
                    currentDir.x * sin + currentDir.y * cos
                ).normalized;

                // Calculate segment length - shorter segments near the end for smoother approach
                float segLength = Mathf.Min(DIVERGENCE_SEGMENT_LENGTH, remainingDist * 0.5f);
                segLength = Mathf.Max(segLength, 0.5f);

                // Move to next point
                Vector2 nextPos = currentPos + newDir * segLength;
                result.Add(nextPos);

                currentPos = nextPos;
                currentDir = newDir;
            }

            // Ensure we end at the target
            if (result.Count == 0 || Vector2.Distance(result[result.Count - 1], endPoint) > 0.1f)
            {
                result.Add(endPoint);
            }

            return result;
        }

        /// <summary>
        /// Calculates the angle in degrees between two direction vectors.
        /// </summary>
        private static float AngleBetweenDirections(Vector2 dir1, Vector2 dir2)
        {
            float dot = Mathf.Clamp(Vector2.Dot(dir1.normalized, dir2.normalized), -1f, 1f);
            return Mathf.Acos(dot) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Adds intermediate points along the target polyline between entry and exit points.
        /// </summary>
        private static void AddIntermediatePointsAlongTarget(List<Vector2> result, List<Vector2> targetPolyline,
            Vector2 entryPoint, Vector2 exitPoint, int entrySegment, int exitSegment)
        {
            // Determine direction along the polyline
            bool forward = exitSegment >= entrySegment;

            if (forward)
            {
                // Add vertices between entry and exit segments
                for (int i = entrySegment + 1; i <= exitSegment && i < targetPolyline.Count; i++)
                {
                    Vector2 vertex = targetPolyline[i];
                    if (Vector2.Distance(vertex, entryPoint) > 0.1f && Vector2.Distance(vertex, exitPoint) > 0.1f)
                    {
                        if (result.Count == 0 || Vector2.Distance(result[result.Count - 1], vertex) > 0.1f)
                        {
                            result.Add(vertex);
                        }
                    }
                }
            }
            else
            {
                // Going backwards along the polyline
                for (int i = entrySegment; i >= exitSegment + 1 && i > 0; i--)
                {
                    Vector2 vertex = targetPolyline[i];
                    if (Vector2.Distance(vertex, entryPoint) > 0.1f && Vector2.Distance(vertex, exitPoint) > 0.1f)
                    {
                        if (result.Count == 0 || Vector2.Distance(result[result.Count - 1], vertex) > 0.1f)
                        {
                            result.Add(vertex);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Find the closest point on a polyline and report which segment it's on.
        /// </summary>
        private static Vector2 ClosestPointOnPolylineWithSegment(Vector2 point, List<Vector2> polyline, out int segmentIndex)
        {
            segmentIndex = 0;
            if (polyline.Count == 0)
                return point;
            if (polyline.Count == 1)
                return polyline[0];

            Vector2 closestPoint = polyline[0];
            float closestDistSq = float.MaxValue;

            for (int i = 0; i < polyline.Count - 1; i++)
            {
                Vector2 segmentClosest = ClosestPointOnSegment(point, polyline[i], polyline[i + 1]);
                float distSq = (point - segmentClosest).sqrMagnitude;
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestPoint = segmentClosest;
                    segmentIndex = i;
                }
            }

            return closestPoint;
        }

        /// <summary>
        /// Find the closest point on a polyline to a given point.
        /// </summary>
        private static Vector2 ClosestPointOnPolyline(Vector2 point, List<Vector2> polyline)
        {
            if (polyline.Count == 0)
                return point;
            if (polyline.Count == 1)
                return polyline[0];

            Vector2 closestPoint = polyline[0];
            float closestDistSq = float.MaxValue;

            for (int i = 0; i < polyline.Count - 1; i++)
            {
                Vector2 segmentClosest = ClosestPointOnSegment(point, polyline[i], polyline[i + 1]);
                float distSq = (point - segmentClosest).sqrMagnitude;
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestPoint = segmentClosest;
                }
            }

            return closestPoint;
        }

        private static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            Vector2 ap = p - a;

            if (ab.sqrMagnitude < 1e-9f)
                return a;

            float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / ab.sqrMagnitude);
            return a + ab * t;
        }

        private static float DistanceToLineSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 closest = ClosestPointOnSegment(p, a, b);
            return Vector2.Distance(p, closest);
        }

        // Curved polyline parameters
        private const float MIN_SEGMENT_LENGTH = 4.0f;
        private const float MAX_SEGMENT_LENGTH = 8.0f;
        private const float MIN_CURVE_ANGLE = 15.0f; // degrees - minimum angle between segments
        private const float MAX_CURVE_ANGLE = 35.0f; // degrees - maximum angle between segments
        private const int MIN_SEGMENTS = 3;
        private const int MAX_SEGMENTS = 5;

        private static List<Vector2> BuildCurvedPolyline(ForestMapState state, Vector2 startCenter, Vector2 endCenter,
            List<int> incidentNodes, bool isCrossConnection = false)
        {
            Vector2 overallDirection = (endCenter - startCenter).normalized;
            float totalDistance = Vector2.Distance(startCenter, endCenter);
            float corridorDistance = totalDistance - 2 * NODE_RADIUS; // Distance between node boundaries

            if (corridorDistance < MIN_CORRIDOR_LENGTH)
                return null;

            Vector2 startBoundary = startCenter + overallDirection * NODE_RADIUS;
            Vector2 endBoundary = endCenter - overallDirection * NODE_RADIUS;

            // Determine number of segments based on corridor distance
            int numSegments = Mathf.Clamp(
                Mathf.RoundToInt(corridorDistance / ((MIN_SEGMENT_LENGTH + MAX_SEGMENT_LENGTH) / 2f)),
                MIN_SEGMENTS,
                MAX_SEGMENTS
            );

            // Try to build a curved polyline
            var polyline = TryBuildCurvedPath(state, startBoundary, endBoundary, overallDirection,
                corridorDistance, numSegments, incidentNodes, isCrossConnection);

            if (polyline != null)
                return polyline;

            // Fallback: try with fewer segments
            for (int segs = numSegments - 1; segs >= 2; segs--)
            {
                polyline = TryBuildCurvedPath(state, startBoundary, endBoundary, overallDirection,
                    corridorDistance, segs, incidentNodes, isCrossConnection);
                if (polyline != null)
                    return polyline;
            }

            // Final fallback: straight line
            var straightLine = new List<Vector2> { startBoundary, endBoundary };
            if (IsPolylineValid(state, straightLine, incidentNodes, null, isCrossConnection))
                return straightLine;

            return null;
        }

        private static List<Vector2> TryBuildCurvedPath(ForestMapState state, Vector2 start, Vector2 end,
            Vector2 overallDirection, float corridorDistance, int numSegments, List<int> incidentNodes,
            bool isCrossConnection = false)
        {
            float baseSegmentLength = corridorDistance / numSegments;

            // Clamp segment length to valid range
            if (baseSegmentLength < MIN_SEGMENT_LENGTH * 0.5f || baseSegmentLength > MAX_SEGMENT_LENGTH * 2f)
                return null;

            // Try multiple random curve configurations
            for (int attempt = 0; attempt < 10; attempt++)
            {
                var polyline = GenerateCurvedPolyline(state, start, end, overallDirection,
                    baseSegmentLength, numSegments, attempt);

                if (polyline != null && IsPolylineValid(state, polyline, incidentNodes, null, isCrossConnection))
                    return polyline;
            }

            return null;
        }

        private static List<Vector2> GenerateCurvedPolyline(ForestMapState state, Vector2 start, Vector2 end,
            Vector2 overallDirection, float baseSegmentLength, int numSegments, int attempt)
        {
            var polyline = new List<Vector2> { start };
            Vector2 currentPos = start;
            Vector2 currentDirection = overallDirection;

            // Generate intermediate points with curved angles
            for (int i = 0; i < numSegments - 1; i++)
            {
                // Always apply curvature between MIN_CURVE_ANGLE and MAX_CURVE_ANGLE
                float minAngle = MIN_CURVE_ANGLE * Mathf.Deg2Rad;
                float maxAngle = MAX_CURVE_ANGLE * Mathf.Deg2Rad;

                // Random angle magnitude between min and max
                float angleMagnitude = minAngle + (float)state.Random.NextDouble() * (maxAngle - minAngle);

                // Alternating direction for S-curve pattern, with some randomness
                float bias = (i % 2 == 0) ? 1f : -1f;
                // Add variation based on attempt number for different curve patterns
                if (attempt % 2 == 1) bias = -bias; // Flip pattern on odd attempts

                // Small random variation to the direction
                float randomVariation = ((float)state.Random.NextDouble() - 0.5f) * 0.3f;
                float angleOffset = angleMagnitude * (bias + randomVariation);

                // Rotate direction by angle offset
                float cos = Mathf.Cos(angleOffset);
                float sin = Mathf.Sin(angleOffset);
                Vector2 newDirection = new Vector2(
                    currentDirection.x * cos - currentDirection.y * sin,
                    currentDirection.x * sin + currentDirection.y * cos
                ).normalized;

                // Vary segment length within bounds
                float lengthVariation = 1f + (state.Random.Next(-20, 21) / 100f);
                float segmentLength = Mathf.Clamp(
                    baseSegmentLength * lengthVariation,
                    MIN_SEGMENT_LENGTH,
                    MAX_SEGMENT_LENGTH
                );

                // Calculate next waypoint
                Vector2 nextPos = currentPos + newDirection * segmentLength;

                // Ensure we don't overshoot the end
                float remainingDist = Vector2.Distance(nextPos, end);
                float requiredDist = (numSegments - i - 1) * MIN_SEGMENT_LENGTH;
                if (remainingDist < requiredDist)
                {
                    // Adjust to not overshoot
                    Vector2 toEnd = (end - currentPos).normalized;
                    nextPos = currentPos + toEnd * (Vector2.Distance(currentPos, end) - requiredDist);
                }

                polyline.Add(nextPos);
                currentPos = nextPos;

                // Gradually steer back toward end point
                Vector2 toEndDir = (end - currentPos).normalized;
                currentDirection = Vector2.Lerp(newDirection, toEndDir, 0.3f).normalized;
            }

            // Add final point
            polyline.Add(end);

            // Validate segment angles don't exceed max curve
            for (int i = 1; i < polyline.Count - 1; i++)
            {
                Vector2 prevDir = (polyline[i] - polyline[i - 1]).normalized;
                Vector2 nextDir = (polyline[i + 1] - polyline[i]).normalized;
                float dot = Vector2.Dot(prevDir, nextDir);
                float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;

                if (angle > MAX_CURVE_ANGLE * 1.5f) // Allow some tolerance
                    return null; // Angle too sharp
            }

            // Validate segment lengths
            for (int i = 0; i < polyline.Count - 1; i++)
            {
                float len = Vector2.Distance(polyline[i], polyline[i + 1]);
                if (len < MIN_SEGMENT_LENGTH * 0.3f) // Very short segments are problematic
                    return null;
            }

            return polyline;
        }

        private static bool IsPolylineValid(ForestMapState state, List<Vector2> polyline,
            List<int> incidentNodes, Vector2? ghostPos = null, bool isCrossConnection = false)
        {
            if (polyline.Count < 2)
                return false;

            // Check against all existing nodes
            foreach (var node in state.Nodes)
            {
                bool isIncident = incidentNodes.Contains(node.Id);

                // Skip proximity check for incident nodes entirely - edges start from them by definition
                if (isIncident)
                    continue;

                float dist = PolylineToNodeDistance(polyline, node.Position, false);
                float minRequired = R_KEEP + PATH_RADIUS - 0.4f + WALL_BUFFER;

                if (dist < minRequired - 1e-6f)
                {
                    Debug.Log($"[IsPolylineValid] REJECTED: too close to node {node.Id} at {node.Position}, dist={dist:F2}, required={minRequired:F2}");
                    return false;
                }
            }

            // Check against ghost centers
            foreach (var ghost in state.GhostCenters)
            {
                if (ghostPos.HasValue && Vector2.Distance(ghost, ghostPos.Value) < 1e-6f)
                    continue;

                float dist = PolylineToNodeDistance(polyline, ghost, false);
                float minRequired = R_KEEP + PATH_RADIUS - 0.4f + WALL_BUFFER;

                if (dist < minRequired - 1e-6f)
                {
                    Debug.Log($"[IsPolylineValid] REJECTED: too close to ghost at {ghost}, dist={dist:F2}, required={minRequired:F2}");
                    return false;
                }
            }

            // Check against all existing edges
            // Edges CAN cross, but must do so at a steep enough angle
            foreach (var edge in state.Edges)
            {
                if (edge.PolylinePoints.Count < 2)
                    continue;

                // Skip proximity check for edges incident to source nodes
                // (new edges from a node naturally start close to existing edges from that node)
                bool isIncidentEdge = incidentNodes.Contains(edge.NodeA) ||
                                      (edge.NodeB.HasValue && incidentNodes.Contains(edge.NodeB.Value));
                if (isIncidentEdge)
                    continue;

                // Check for intersections and validate crossing angles
                var crossings = FindPolylineCrossings(polyline, edge.PolylinePoints);

                if (crossings.Count > 0)
                {
                    // Crossings are allowed - validate each crossing angle
                    foreach (var crossing in crossings)
                    {
                        if (!IsCrossingAngleValid(crossing.angle))
                        {
                            // Crossing angle too shallow - would create gap > MAX_CROSSING_GAP
                            return false;
                        }
                    }
                }
                else
                {
                    // No crossing - check parallel proximity
                    // Use MERGE_DISTANCE for non-cross-connections, smaller buffer for intentional cross-connections
                    float edgeBuffer = isCrossConnection ? PATH_WIDTH * 0.3f : MERGE_DISTANCE;
                    float dist = PolylineToPolylineDistance(polyline, edge.PolylinePoints);

                    if (dist < edgeBuffer - 1e-6f)
                    {
                        Debug.Log($"[IsPolylineValid] REJECTED: too close to edge {edge.Id} (NodeA={edge.NodeA}, NodeB={edge.NodeB}), dist={dist:F2}, required={edgeBuffer:F2}");
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Represents a crossing between two polylines.
        /// </summary>
        private struct PolylineCrossing
        {
            public Vector2 point;
            public float angle; // Crossing angle in degrees (0-90)
            public int segment1Index;
            public int segment2Index;
        }

        /// <summary>
        /// Finds all crossing points between two polylines and their crossing angles.
        /// </summary>
        private static List<PolylineCrossing> FindPolylineCrossings(List<Vector2> poly1, List<Vector2> poly2)
        {
            var crossings = new List<PolylineCrossing>();

            for (int i = 0; i < poly1.Count - 1; i++)
            {
                Vector2 a1 = poly1[i];
                Vector2 a2 = poly1[i + 1];
                Vector2 dir1 = (a2 - a1).normalized;

                for (int j = 0; j < poly2.Count - 1; j++)
                {
                    Vector2 b1 = poly2[j];
                    Vector2 b2 = poly2[j + 1];
                    Vector2 dir2 = (b2 - b1).normalized;

                    Vector2? intersection = GetSegmentIntersection(a1, a2, b1, b2);
                    if (intersection.HasValue)
                    {
                        // Calculate crossing angle (0-90 degrees)
                        float dot = Mathf.Abs(Vector2.Dot(dir1, dir2));
                        float angle = Mathf.Acos(Mathf.Clamp(dot, 0, 1)) * Mathf.Rad2Deg;
                        // Convert to acute angle (0-90)
                        if (angle > 90f) angle = 180f - angle;

                        crossings.Add(new PolylineCrossing
                        {
                            point = intersection.Value,
                            angle = angle,
                            segment1Index = i,
                            segment2Index = j
                        });
                    }
                }
            }

            return crossings;
        }

        /// <summary>
        /// Gets the intersection point of two line segments, or null if they don't intersect.
        /// </summary>
        private static Vector2? GetSegmentIntersection(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            Vector2 d1 = a2 - a1;
            Vector2 d2 = b2 - b1;

            float cross = d1.x * d2.y - d1.y * d2.x;
            if (Mathf.Abs(cross) < 1e-6f)
                return null; // Parallel

            Vector2 d3 = b1 - a1;
            float t = (d3.x * d2.y - d3.y * d2.x) / cross;
            float u = (d3.x * d1.y - d3.y * d1.x) / cross;

            if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
            {
                return a1 + d1 * t;
            }

            return null;
        }

        /// <summary>
        /// Checks if a crossing angle is steep enough to keep the gap under MAX_CROSSING_GAP.
        /// The triangular gap area at a crossing is approximately:
        /// A = (edgeWidth/2)² / tan(angle/2)
        /// We want A <= MAX_CROSSING_GAP, so angle >= 2*atan((edgeWidth/2)² / MAX_CROSSING_GAP)
        /// </summary>
        private static bool IsCrossingAngleValid(float angleDegrees)
        {
            float minAngle = BezierCurveFactory.MinCrossingAngleForGap(PATH_WIDTH, MAX_CROSSING_GAP);
            return angleDegrees >= minAngle;
        }

        private static bool IsAngleValid(Node node, float angle)
        {
            return IsAngleValid(node, angle, ANGLE_MIN_SEPARATION);
        }

        private static bool IsAngleValid(Node node, float angle, float minSeparationDegrees)
        {
            float minSeparation = minSeparationDegrees * Mathf.Deg2Rad;
            foreach (float usedAngle in node.UsedAngles)
            {
                float diff = Mathf.Abs(angle - usedAngle);
                diff = Mathf.Min(diff, 2 * Mathf.PI - diff);
                if (diff < minSeparation)
                    return false;
            }
            return true;
        }

        private static bool IsGhostPositionValid(ForestMapState state, Vector2 ghostPos, int sourceNodeId)
        {
            foreach (var node in state.Nodes)
            {
                if (Vector2.Distance(node.Position, ghostPos) < 2 * R_KEEP - 1e-6f)
                    return false;
            }

            foreach (var ghost in state.GhostCenters)
            {
                if (Vector2.Distance(ghost, ghostPos) < 2 * R_KEEP - 1e-6f)
                    return false;
            }

            foreach (var edge in state.Edges)
            {
                if (edge.PolylinePoints.Count < 2)
                    continue;

                if (edge.NodeA == sourceNodeId || (edge.NodeB.HasValue && edge.NodeB.Value == sourceNodeId))
                    continue;

                float dist = PolylineToNodeDistance(edge.PolylinePoints, ghostPos, false);
                float minRequired = NODE_RADIUS + PATH_RADIUS + WALL_BUFFER;

                if (dist < minRequired - 1e-6f)
                    return false;
            }

            return true;
        }

        private static ValidationResult ValidateMaze(ForestMapState state, char[,] grid, int gridWidth, int gridHeight,
            float scale, Vector2 offset)
        {
            var result = new ValidationResult { IsValid = true };

            // Track actual node positions from grid markers ('H' and 'N')
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    if (grid[y, x] == 'H')
                    {
                        // Root node (ID 0)
                        result.ActualNodePositions[0] = new Vector2(x, y);
                    }
                    else if (grid[y, x] == 'N')
                    {
                        // Find which node this corresponds to
                        Vector2 gridPos = new Vector2(x, y);
                        float minDist = float.MaxValue;
                        int closestNodeId = -1;

                        for (int i = 1; i < state.Nodes.Count; i++)
                        {
                            Vector2 expectedPos = state.Nodes[i].Position * scale + offset;
                            float dist = Vector2.Distance(gridPos, expectedPos);
                            if (dist < minDist)
                            {
                                minDist = dist;
                                closestNodeId = i;
                            }
                        }

                        if (closestNodeId >= 0)
                        {
                            result.ActualNodePositions[closestNodeId] = gridPos;
                        }
                    }
                }
            }

            // Verify all nodes were found
            if (result.ActualNodePositions.Count != state.Nodes.Count)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Expected {state.Nodes.Count} nodes, found {result.ActualNodePositions.Count} markers";
                return result;
            }

            // Analyze connectivity using BFS from each node
            var connections = new Dictionary<int, HashSet<int>>();
            foreach (var node in state.Nodes)
            {
                connections[node.Id] = FindConnectedNodes(grid, gridWidth, gridHeight,
                    result.ActualNodePositions[node.Id], result.ActualNodePositions);
            }

            // Check for duplicate edges (multiple paths between same node pair)
            var edgeCounts = new Dictionary<(int, int), int>();
            foreach (var edge in state.Edges.Where(e => e.IsComplete()))
            {
                int nodeA = edge.NodeA;
                int nodeB = edge.NodeB.Value;
                var key = nodeA < nodeB ? (nodeA, nodeB) : (nodeB, nodeA);

                if (!edgeCounts.ContainsKey(key))
                    edgeCounts[key] = 0;
                edgeCounts[key]++;
            }

            // Report duplicate edges
            var duplicates = edgeCounts.Where(kvp => kvp.Value > 1).ToList();
            if (duplicates.Count > 0)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Found {duplicates.Count} duplicate edge(s): " +
                    string.Join(", ", duplicates.Select(d => $"nodes {d.Key.Item1}-{d.Key.Item2} ({d.Value} edges)"));
                return result;
            }

            // Verify all complete edges have actual walkable connections
            foreach (var edge in state.Edges.Where(e => e.IsComplete()))
            {
                int nodeA = edge.NodeA;
                int nodeB = edge.NodeB.Value;

                bool aConnectsToB = connections[nodeA].Contains(nodeB);
                bool bConnectsToA = connections[nodeB].Contains(nodeA);

                if (!aConnectsToB || !bConnectsToA)
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"Edge {edge.Id} (nodes {nodeA}-{nodeB}) has no walkable path in grid";
                    return result;
                }

                result.DetectedConnections.Add(nodeA < nodeB ? (nodeA, nodeB) : (nodeB, nodeA));
            }

            // Verify all nodes are reachable from root
            var reachable = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(0); // Start from root
            reachable.Add(0);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int neighbor in connections[current])
                {
                    if (!reachable.Contains(neighbor))
                    {
                        reachable.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            if (reachable.Count != state.Nodes.Count)
            {
                var unreachable = state.Nodes.Where(n => !reachable.Contains(n.Id)).Select(n => n.Id).ToList();
                result.IsValid = false;
                result.ErrorMessage = $"Found {unreachable.Count} unreachable node(s): {string.Join(", ", unreachable)}";
                return result;
            }

            return result;
        }

        private static HashSet<int> FindConnectedNodes(char[,] grid, int width, int height,
            Vector2 startPos, Dictionary<int, Vector2> allNodePositions)
        {
            var connected = new HashSet<int>();
            var visited = new HashSet<(int, int)>();
            var queue = new Queue<(int x, int y)>();

            int startX = Mathf.RoundToInt(startPos.x);
            int startY = Mathf.RoundToInt(startPos.y);

            queue.Enqueue((startX, startY));
            visited.Add((startX, startY));

            // BFS to find all walkable tiles reachable from start
            int[] dx = { 0, 0, 1, -1, 1, 1, -1, -1 }; // 8-directional
            int[] dy = { 1, -1, 0, 0, 1, -1, 1, -1 };

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();

                // Check if this position is a node marker
                foreach (var kvp in allNodePositions)
                {
                    int nodeId = kvp.Key;
                    Vector2 nodePos = kvp.Value;

                    if (Mathf.Abs(x - nodePos.x) < 0.5f && Mathf.Abs(y - nodePos.y) < 0.5f)
                    {
                        connected.Add(nodeId);
                        break;
                    }
                }

                // Explore neighbors
                for (int i = 0; i < 8; i++)
                {
                    int nx = x + dx[i];
                    int ny = y + dy[i];

                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                        continue;

                    if (visited.Contains((nx, ny)))
                        continue;

                    if (!IsWalkableTile(grid[ny, nx]))
                        continue;

                    visited.Add((nx, ny));
                    queue.Enqueue((nx, ny));
                }
            }

            return connected;
        }

        private static void BackfillStateFromGrid(ForestMapState state, ValidationResult validation,
            float scale, Vector2 offset)
        {
            // Update node positions to match actual grid positions
            foreach (var kvp in validation.ActualNodePositions)
            {
                int nodeId = kvp.Key;
                Vector2 gridPos = kvp.Value;

                // Convert grid position back to world space
                Vector2 worldPos = (gridPos - offset) / scale;

                if (nodeId < state.Nodes.Count)
                {
                    var node = state.Nodes[nodeId];
                    if (Vector2.Distance(node.Position, worldPos) > 0.1f)
                    {
                        node.Position = worldPos;
                    }
                }
            }

            // Update edge polylines to be direct paths between actual node positions
            foreach (var edge in state.Edges.Where(e => e.IsComplete()))
            {
                int nodeA = edge.NodeA;
                int nodeB = edge.NodeB.Value;

                if (validation.ActualNodePositions.ContainsKey(nodeA) &&
                    validation.ActualNodePositions.ContainsKey(nodeB))
                {
                    Vector2 posA = (validation.ActualNodePositions[nodeA] - offset) / scale;
                    Vector2 posB = (validation.ActualNodePositions[nodeB] - offset) / scale;

                    // Calculate boundary points
                    Vector2 direction = (posB - posA).normalized;
                    Vector2 startBoundary = posA + direction * NODE_RADIUS;
                    Vector2 endBoundary = posB - direction * NODE_RADIUS;

                    // Update to straight path
                    edge.PolylinePoints.Clear();
                    edge.PolylinePoints.Add(startBoundary);
                    edge.PolylinePoints.Add(endBoundary);
                }
            }
        }

        private static int? SelectFrontierEdgeBiased(ForestMapState state)
        {
            if (state.Frontier.Count == 0)
                return null;

            Vector2 rootPos = state.Nodes[0].Position;
            int? closestEdgeId = null;
            float closestDist = float.MaxValue;

            foreach (int edgeId in state.Frontier)
            {
                var edge = state.Edges[edgeId];
                if (!edge.GhostCenter.HasValue)
                    continue;

                float dist = Vector2.Distance(edge.GhostCenter.Value, rootPos);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestEdgeId = edgeId;
                }
            }

            // Fallback to random if no valid ghost centers found
            if (!closestEdgeId.HasValue)
                return state.Frontier[state.Random.Next(state.Frontier.Count)];

            return closestEdgeId.Value;
        }

        private static float PolylineToNodeDistance(List<Vector2> polyline, Vector2 center, bool isIncident)
        {
            if (polyline.Count < 2)
                return float.MaxValue;

            float minDist = float.MaxValue;
            int startIdx = isIncident ? 1 : 0;
            int endIdx = isIncident ? polyline.Count - 2 : polyline.Count - 1;

            for (int i = startIdx; i < endIdx; i++)
            {
                float dist = PointToSegmentDistance(center, polyline[i], polyline[i + 1]);
                minDist = Mathf.Min(minDist, dist);
            }

            return minDist;
        }

        private static float PolylineToPolylineDistance(List<Vector2> poly1, List<Vector2> poly2)
        {
            if (poly1.Count < 2 || poly2.Count < 2)
                return float.MaxValue;

            float minDist = float.MaxValue;

            for (int i = 0; i < poly1.Count - 1; i++)
            {
                for (int j = 0; j < poly2.Count - 1; j++)
                {
                    float dist = SegmentToSegmentDistance(poly1[i], poly1[i + 1], poly2[j], poly2[j + 1]);
                    minDist = Mathf.Min(minDist, dist);
                }
            }

            return minDist;
        }

        private static float PointToSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            Vector2 ap = p - a;

            if (ab.sqrMagnitude < 1e-9f)
                return ap.magnitude;

            float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / ab.sqrMagnitude);
            Vector2 closest = a + ab * t;
            return Vector2.Distance(p, closest);
        }

        private static float SegmentToSegmentDistance(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            if (SegmentsIntersect(a1, a2, b1, b2))
                return 0f;

            float[] dists = {
                PointToSegmentDistance(a1, b1, b2),
                PointToSegmentDistance(a2, b1, b2),
                PointToSegmentDistance(b1, a1, a2),
                PointToSegmentDistance(b2, a1, a2)
            };

            return dists.Min();
        }

        private static bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            float o1 = Orientation(a1, a2, b1);
            float o2 = Orientation(a1, a2, b2);
            float o3 = Orientation(b1, b2, a1);
            float o4 = Orientation(b1, b2, a2);

            if (o1 == 0f && OnSegment(a1, b1, a2))
                return true;
            if (o2 == 0f && OnSegment(a1, b2, a2))
                return true;
            if (o3 == 0f && OnSegment(b1, a1, b2))
                return true;
            if (o4 == 0f && OnSegment(b1, a2, b2))
                return true;

            return (o1 > 0f) != (o2 > 0f) && (o3 > 0f) != (o4 > 0f);
        }

        private static float Orientation(Vector2 a, Vector2 b, Vector2 c)
        {
            float value = (b.y - a.y) * (c.x - b.x) - (b.x - a.x) * (c.y - b.y);
            if (Mathf.Abs(value) < 1e-6f)
                return 0f;
            return value > 0f ? 1f : -1f;
        }

        private static bool OnSegment(Vector2 a, Vector2 b, Vector2 c)
        {
            return b.x <= Mathf.Max(a.x, c.x) + 1e-6f &&
                   b.x >= Mathf.Min(a.x, c.x) - 1e-6f &&
                   b.y <= Mathf.Max(a.y, c.y) + 1e-6f &&
                   b.y >= Mathf.Min(a.y, c.y) - 1e-6f;
        }

        private static string RasterizeToGrid(ForestMapState state, int gridWidth, int gridHeight)
        {
            // Find bounds of the generated graph
            float minX = state.Nodes.Min(n => n.Position.x) - R_KEEP;
            float maxX = state.Nodes.Max(n => n.Position.x) + R_KEEP;
            float minY = state.Nodes.Min(n => n.Position.y) - R_KEEP;
            float maxY = state.Nodes.Max(n => n.Position.y) + R_KEEP;

            float graphWidth = maxX - minX;
            float graphHeight = maxY - minY;

            // Scale derived from a fixed node size in tiles to keep clearings consistent across grid sizes.
            // With NODE_RADIUS = 3.0, targetNodeDiameterTiles = 24 gives scale = 4.0, radius = 12 grid cells = 3.0 world units (at tileSize 0.25)
            const float targetNodeDiameterTiles = 24f;
            float targetScale = (targetNodeDiameterTiles / 2f) / NODE_RADIUS;

            // Clamp to fit-to-grid scale to avoid clipping if the fixed scale would exceed the grid.
            float scaleX = (gridWidth - 4) / graphWidth;
            float scaleY = (gridHeight - 4) / graphHeight;
            float fitToGridScale = Mathf.Min(scaleX, scaleY);
            float scale = Mathf.Min(targetScale, fitToGridScale);

            Vector2 offset = new Vector2(
                (gridWidth - graphWidth * scale) / 2 - minX * scale,
                (gridHeight - graphHeight * scale) / 2 - minY * scale
            );

            // Store scale and offset for later dynamic growth
            state.Scale = scale;
            state.Offset = offset;

            // Initialize grid with forest
            char[,] grid = new char[gridHeight, gridWidth];
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    grid[y, x] = '#';
                }
            }

            // Draw paths (edges) - use narrow 1-cell wide paths
            int pathWidth = 1;  // Always 1 cell wide for clean pathfinding
            foreach (var edge in state.Edges.Where(e => e.PolylinePoints.Count > 1))
            {
                for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                {
                    Vector2 p1 = edge.PolylinePoints[i] * scale + offset;
                    Vector2 p2 = edge.PolylinePoints[i + 1] * scale + offset;

                    // Always include endpoints for 1-cell wide paths to ensure connectivity
                    bool includeEndPoint = true;

                    DrawLineOnGrid(grid, p1, p2, '.', gridWidth, gridHeight, includeEndPoint, pathWidth);
                }
            }
            // Draw clearings (nodes)
            foreach (var node in state.Nodes)
            {
                Vector2 center = node.Position * scale + offset;
                float radius = NODE_RADIUS * scale;

                DrawCircleOnGrid(grid, center, radius, '.', gridWidth, gridHeight);
            }

            // Mark root node with 'H' (heart)
            if (state.Nodes.Count > 0)
            {
                Vector2 rootCenter = state.Nodes[0].Position * scale + offset;
                int cx = Mathf.RoundToInt(rootCenter.x);
                int cy = Mathf.RoundToInt(rootCenter.y);

                if (cx >= 0 && cx < gridWidth && cy >= 0 && cy < gridHeight)
                {
                    grid[cy, cx] = 'H';
                }
            }

            // Mark non-root node centers with 'N' (node hazard)
            int nodeHazardCount = 0;
            for (int i = 1; i < state.Nodes.Count; i++)
            {
                var node = state.Nodes[i];
                Vector2 nodeCenter = node.Position * scale + offset;
                int cx = Mathf.RoundToInt(nodeCenter.x);
                int cy = Mathf.RoundToInt(nodeCenter.y);

                if (cx >= 0 && cx < gridWidth && cy >= 0 && cy < gridHeight)
                {
                    if (grid[cy, cx] == '.')
                    {
                        grid[cy, cx] = 'N';
                        nodeHazardCount++;
                    }
                }
            }

            // Mark unconnected edge endpoints with unique spawn IDs (A-Z excluding H/N, then a-z, then digits)
            int entranceExitCount = 0;
            int partialEndpointCount = state.Edges.Count(e => e.Partial && e.PolylinePoints.Count > 0);
            var spawnIdQueue = new Queue<char>(GenerateSpawnIds());
            int availableSpawnIdCount = spawnIdQueue.Count;
            bool spawnIdsExhausted = false;

            foreach (var edge in state.Edges.Where(e => e.Partial && e.PolylinePoints.Count > 0))
            {
                var connectedNode = state.Nodes.First(n => n.Id == edge.NodeA);
                Vector2 nodeCenter = connectedNode.Position * scale + offset;
                int nx = Mathf.RoundToInt(nodeCenter.x);
                int ny = Mathf.RoundToInt(nodeCenter.y);

                // Find the endpoint farthest from the connected node
                // (Usually last point, but check both ends to be safe)
                Vector2 firstPoint = edge.PolylinePoints[0];
                Vector2 lastPoint = edge.PolylinePoints[edge.PolylinePoints.Count - 1];
                float distFirst = Vector2.Distance(firstPoint, connectedNode.Position);
                float distLast = Vector2.Distance(lastPoint, connectedNode.Position);

                Vector2 endPoint = (distLast >= distFirst ? lastPoint : firstPoint) * scale + offset;
                int ex = Mathf.RoundToInt(endPoint.x);
                int ey = Mathf.RoundToInt(endPoint.y);

                bool endpointUsable =
                    ex >= 0 && ex < gridWidth &&
                    ey >= 0 && ey < gridHeight &&
                    grid[ey, ex] != 'H' &&
                    grid[ey, ex] != 'N' &&
                    !IsSpawnPointChar(grid[ey, ex]) &&
                    HasAdjacentWalkableTile(grid, ex, ey, gridWidth, gridHeight);

                if (endpointUsable)
                {
                    if (spawnIdQueue.Count == 0)
                    {
                        spawnIdsExhausted = true;
                        break;
                    }

                    char spawnId = spawnIdQueue.Dequeue();
                    grid[ey, ex] = spawnId;
                    entranceExitCount++;
                    continue; // Done with this edge.
                }

                int targetX = -1;
                int targetY = -1;
                float minDistanceFromEndpoint = float.MaxValue;

                // Fallback: Search for walkable cell CLOSEST to the chosen endpoint
                // Search within small radius to stay near the actual edge end
                const int searchRadius = 3; // Only search 3 cells around endpoint
                for (int dy = -searchRadius; dy <= searchRadius; dy++)
                {
                    for (int dx = -searchRadius; dx <= searchRadius; dx++)
                    {
                        int px = ex + dx;
                        int py = ey + dy;

                        if (px < 0 || px >= gridWidth || py < 0 || py >= gridHeight)
                        {
                            continue;
                        }

                        char candidateTile = grid[py, px];
                        if (!IsWalkableTile(candidateTile) || candidateTile == 'H' || candidateTile == 'N')
                        {
                            continue;
                        }

                        // Find CLOSEST walkable cell to the endpoint (not farthest from node!)
                        float distanceFromEndpoint = (px - ex) * (px - ex) + (py - ey) * (py - ey);
                        if (distanceFromEndpoint < minDistanceFromEndpoint)
                        {
                            minDistanceFromEndpoint = distanceFromEndpoint;
                            targetX = px;
                            targetY = py;
                        }
                    }
                }

                if (targetX >= 0)
                {
                    if (spawnIdQueue.Count == 0)
                    {
                        spawnIdsExhausted = true;
                        break;
                    }

                    char spawnId = spawnIdQueue.Dequeue();
                    grid[targetY, targetX] = spawnId;
                    entranceExitCount++;
                }
            }

            // Ensure border is always forest where there's no walkable tile
            for (int x = 0; x < gridWidth; x++)
            {
                if (!IsWalkableTile(grid[0, x]))
                    grid[0, x] = '#';

                if (!IsWalkableTile(grid[gridHeight - 1, x]))
                    grid[gridHeight - 1, x] = '#';
            }

            for (int y = 0; y < gridHeight; y++)
            {
                if (!IsWalkableTile(grid[y, 0]))
                    grid[y, 0] = '#';

                if (!IsWalkableTile(grid[y, gridWidth - 1]))
                    grid[y, gridWidth - 1] = '#';
            }

            // Ensure all edge walkable tiles have at least one adjacent walkable tile
            EnsureEdgeTilesAreWalkable(grid, gridWidth, gridHeight);

            // Ensure all edge endpoints are reachable from node centers by progressively converting walls to paths
            EnsureEdgeConnectivity(state, grid, gridWidth, gridHeight);

            // Add entrances
            AddEntrance(grid, gridWidth, gridHeight, state.Random);

            // Validate the generated maze
            var validation = ValidateMaze(state, grid, gridWidth, gridHeight, scale, offset);
            state.ValidationPassed = validation.IsValid;
            state.ValidationError = validation.ErrorMessage;

            if (!validation.IsValid)
            {
                // Debug.LogWarning($"Maze validation failed: {validation.ErrorMessage}");
                // Return grid anyway, GenerateMaze will handle retry
            }
            else
            {
                // Debug.Log($"Maze validation passed: {state.Nodes.Count} nodes, {validation.DetectedConnections.Count} connections");

                // Backfill state with actual grid positions
                BackfillStateFromGrid(state, validation, scale, offset);
            }

            return GridToString(grid, gridWidth, gridHeight);
        }

        /// <summary>
        /// Rasterizes specific nodes and their connected edges to an existing grid.
        /// Used for dynamic maze growth to add newly created nodes without regenerating the entire maze.
        /// </summary>
        /// <param name="state">The forest map state containing scale and offset</param>
        /// <param name="grid">The existing grid to update</param>
        /// <param name="nodeIds">List of node IDs to rasterize</param>
        /// <param name="gridWidth">Grid width</param>
        /// <param name="gridHeight">Grid height</param>
        public static void RasterizeNodesToGrid(ForestMapState state, char[,] grid, List<int> nodeIds, int gridWidth, int gridHeight)
        {
            float scale = state.Scale;
            Vector2 offset = state.Offset;

            // Calculate path width - use narrow 1-cell wide paths
            int pathWidth = 1;  // Always 1 cell wide for clean pathfinding

            // Rasterize edges connected to these nodes
            var edgesToRasterize = state.Edges.Where(e =>
                nodeIds.Contains(e.NodeA) || (e.NodeB.HasValue && nodeIds.Contains(e.NodeB.Value))
            ).ToList();

            foreach (var edge in edgesToRasterize.Where(e => e.PolylinePoints.Count > 1))
            {
                for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                {
                    Vector2 p1 = edge.PolylinePoints[i] * scale + offset;
                    Vector2 p2 = edge.PolylinePoints[i + 1] * scale + offset;

                    // Always include endpoints for 1-cell wide paths to ensure connectivity
                    bool includeEndPoint = true;

                    DrawLineOnGrid(grid, p1, p2, '.', gridWidth, gridHeight, includeEndPoint, pathWidth);
                }
            }

            // Rasterize node clearings
            foreach (int nodeId in nodeIds)
            {
                var node = state.Nodes.FirstOrDefault(n => n.Id == nodeId);
                if (node == null) continue;

                Vector2 center = node.Position * scale + offset;
                float radius = NODE_RADIUS * scale;

                DrawCircleOnGrid(grid, center, radius, '.', gridWidth, gridHeight);

                // Mark node center with 'N' (unless it's the root)
                if (node.Kind != "root")
                {
                    int cx = Mathf.RoundToInt(center.x);
                    int cy = Mathf.RoundToInt(center.y);

                    if (cx >= 0 && cx < gridWidth && cy >= 0 && cy < gridHeight)
                    {
                        grid[cy, cx] = 'N';
                    }
                }
            }

            // Note: Gap-filling is NOT done here for dynamic growth.
            // DynamicMazeGrowth will call EnsureEdgeConnectivityPublic AFTER endpoint marking
            // to ensure endpoints are in their final positions before gap-filling.
        }

        private static void DrawLineOnGrid(char[,] grid, Vector2 p1, Vector2 p2, char ch, int width, int height, bool includeEndPoint = true, int lineWidth = 2)
        {
            int x0 = Mathf.RoundToInt(p1.x);
            int y0 = Mathf.RoundToInt(p1.y);
            int x1 = Mathf.RoundToInt(p2.x);
            int y1 = Mathf.RoundToInt(p2.y);

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;

            // Local helper for setting grid cells
            void Set(int x, int y)
            {
                if (!includeEndPoint && x == x1 && y == y1)
                    return;

                SetGridCell(grid, x, y, ch, width, height);
            }

            // For diagonal paths, use linear interpolation with narrow corridor
            if (dx > 0 && dy > 0)
            {
                // Calculate number of steps based on the distance
                int steps = Mathf.Max(dx, dy);
                int offsetX = 0;
                int offsetY = 0;
                if (dx >= dy)
                {
                    offsetY = sy == 0 ? 1 : sy;
                }
                else
                {
                    offsetX = sx == 0 ? 1 : sx;
                }

                for (int i = 0; i <= steps; i++)
                {
                    // Linear interpolation
                    float t = steps > 0 ? (float)i / steps : 0;
                    int x = Mathf.RoundToInt(x0 + t * (x1 - x0));
                    int y = Mathf.RoundToInt(y0 + t * (y1 - y0));

                    bool atEnd = (i == steps);
                    if (!atEnd || includeEndPoint)
                    {
                        // Draw a diagonal corridor with scaled width
                        int halfWidth = lineWidth / 2;
                        for (int wx = -halfWidth; wx <= halfWidth; wx++)
                        {
                            for (int wy = -halfWidth; wy <= halfWidth; wy++)
                            {
                                Set(x + wx, y + wy);
                            }
                        }
                    }
                }
            }
            else
            {
                // Horizontal or vertical path - use Bresenham with perpendicular width
                int err = dx - dy;
                int halfWidth = lineWidth / 2;

                while (true)
                {
                    bool atEnd = (x0 == x1 && y0 == y1);

                    if (!atEnd || includeEndPoint)
                    {
                        // Add perpendicular width based on lineWidth
                        if (dx >= dy)
                        {
                            // Vertical path - widen horizontally
                            for (int w = -halfWidth; w <= halfWidth; w++)
                            {
                                Set(x0, y0 + w);
                            }
                        }
                        else
                        {
                            // Horizontal path - widen vertically
                            for (int w = -halfWidth; w <= halfWidth; w++)
                            {
                                Set(x0 + w, y0);
                            }
                        }
                    }

                    if (atEnd)
                        break;

                    int e2 = 2 * err;
                    if (e2 > -dy)
                    {
                        err -= dy;
                        x0 += sx;
                    }
                    if (e2 < dx)
                    {
                        err += dx;
                        y0 += sy;
                    }
                }
            }
        }

        private static void SetGridCell(char[,] grid, int x, int y, char ch, int width, int height)
        {
            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                // Don't overwrite heart, node hazards, or spawn point markers (letters/digits)
                char existing = grid[y, x];
                if (existing != 'H' && existing != 'N' && !IsSpawnPointChar(existing))
                    grid[y, x] = ch;
            }
        }

        private static bool IsSpawnPointChar(char c)
        {
            // Spawn points are letters or digits (excluding heart/node hazard markers and non-walkable symbols)
            return (char.IsLetterOrDigit(c)) && c != 'H' && c != 'N' && c != '#' && c != '.';
        }

        private static IEnumerable<char> GenerateSpawnIds()
        {
            // Uppercase letters excluding heart/node hazards
            for (char c = 'A'; c <= 'Z'; c++)
            {
                if (c != 'H' && c != 'N')
                    yield return c;
            }

            // Lowercase letters excluding heart/node hazards
            for (char c = 'a'; c <= 'z'; c++)
            {
                if (c != 'h' && c != 'n')
                    yield return c;
            }

            // Digits
            for (char c = '0'; c <= '9'; c++)
            {
                yield return c;
            }
        }

        private static void EnsureEdgeTilesAreWalkable(char[,] grid, int width, int height)
        {
            // For each walkable edge tile, ensure it has at least one orthogonally adjacent walkable tile

            // Top and bottom edges
            for (int x = 0; x < width; x++)
            {
                // Top edge
                if (IsWalkableTile(grid[0, x]))
                {
                    if (!HasAdjacentWalkableTile(grid, x, 0, width, height))
                    {
                        // Make tile below walkable (don't overwrite special tiles)
                        if (!IsWalkableTile(grid[1, x]))
                            grid[1, x] = '.';
                    }
                }

                // Bottom edge
                if (IsWalkableTile(grid[height - 1, x]))
                {
                    if (!HasAdjacentWalkableTile(grid, x, height - 1, width, height))
                    {
                        // Make tile above walkable (don't overwrite special tiles)
                        if (!IsWalkableTile(grid[height - 2, x]))
                            grid[height - 2, x] = '.';
                    }
                }
            }

            // Left and right edges
            for (int y = 0; y < height; y++)
            {
                // Left edge
                if (IsWalkableTile(grid[y, 0]))
                {
                    if (!HasAdjacentWalkableTile(grid, 0, y, width, height))
                    {
                        // Make tile to the right walkable (don't overwrite special tiles)
                        if (!IsWalkableTile(grid[y, 1]))
                            grid[y, 1] = '.';
                    }
                }

                // Right edge
                if (IsWalkableTile(grid[y, width - 1]))
                {
                    if (!HasAdjacentWalkableTile(grid, width - 1, y, width, height))
                    {
                        // Make tile to the left walkable (don't overwrite special tiles)
                        if (!IsWalkableTile(grid[y, width - 2]))
                            grid[y, width - 2] = '.';
                    }
                }
            }
        }

        private static bool HasAdjacentWalkableTile(char[,] grid, int x, int y, int width, int height)
        {
            // Check all 4 orthogonal neighbors
            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];

                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    char tile = grid[ny, nx];
                    if (IsWalkableTile(tile))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsWalkableTile(char c)
        {
            // Check if tile is walkable: path, heart, node hazard, or spawn point
            return c == '.' || c == 'H' || c == 'N' || IsSpawnPointChar(c);
        }

        private static void DrawCircleOnGrid(char[,] grid, Vector2 center, float radius, char ch, int width, int height)
        {
            int cx = Mathf.RoundToInt(center.x);
            int cy = Mathf.RoundToInt(center.y);
            int r = Mathf.CeilToInt(radius);

            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x >= 0 && x < width && y >= 0 && y < height)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), center);
                        if (dist <= radius)
                        {
                            if (grid[y, x] != 'H') // Don't overwrite heart
                                grid[y, x] = ch;
                        }
                    }
                }
            }
        }

        private static void AddEntrance(char[,] grid, int width, int height, System.Random random)
        {
            // Find walkable tiles near edges (check for all walkable tile types)
            var topCandidates = new List<(int x, int y)>();
            var bottomCandidates = new List<(int x, int y)>();

            for (int x = 1; x < width - 1; x++)
            {
                if (IsWalkableTile(grid[1, x]))
                    topCandidates.Add((x, 0));

                if (IsWalkableTile(grid[height - 2, x]))
                    bottomCandidates.Add((x, height - 1));
            }

            // Add at least one entrance (only if not already walkable)
            if (topCandidates.Count > 0)
            {
                var entrance = topCandidates[random.Next(topCandidates.Count)];
                if (!IsWalkableTile(grid[entrance.y, entrance.x]))
                    grid[entrance.y, entrance.x] = '.';
            }

            if (bottomCandidates.Count > 0)
            {
                var entrance = bottomCandidates[random.Next(bottomCandidates.Count)];
                if (!IsWalkableTile(grid[entrance.y, entrance.x]))
                    grid[entrance.y, entrance.x] = '.';
            }
        }

        private static string GridToString(char[,] grid, int width, int height)
        {
            var sb = new StringBuilder(height * (width + 1));

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    sb.Append(grid[y, x]);
                }

                if (y < height - 1)
                    sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Public wrapper for gap-filling a single edge endpoint to node center connection.
        /// Used by DynamicMazeGrowth to ensure frontier edges are reachable after endpoint marking.
        /// </summary>
        public static void EnsureEdgeConnectivityPublic(char[,] grid, int gridWidth, int gridHeight, Vector2Int start, Vector2Int end)
        {
            EnsureDirectPathExists(grid, gridWidth, gridHeight, start, end);
        }

        /// <summary>
        /// Ensures all edge endpoints are reachable from their connected node centers
        /// by progressively converting wall tiles to paths along the direct vector.
        /// Creates straight corridors from portals to nodes instead of winding paths.
        /// </summary>
        private static void EnsureEdgeConnectivity(ForestMapState state, char[,] grid, int gridWidth, int gridHeight)
        {
            float scale = state.Scale;
            Vector2 offset = state.Offset;

            foreach (var edge in state.Edges)
            {
                Vector2Int startGrid, endGrid;

                if (edge.Partial)
                {
                    // For frontier edges: connect node center to endpoint
                    var connectedNode = state.Nodes[edge.NodeA];
                    Vector2 nodeCenter = connectedNode.Position * scale + offset;
                    Vector2Int nodeCenterGrid = new Vector2Int(Mathf.RoundToInt(nodeCenter.x), Mathf.RoundToInt(nodeCenter.y));

                    Vector2 endpoint = edge.PolylinePoints[edge.PolylinePoints.Count - 1] * scale + offset;
                    Vector2Int endpointGrid = new Vector2Int(Mathf.RoundToInt(endpoint.x), Mathf.RoundToInt(endpoint.y));

                    startGrid = nodeCenterGrid;
                    endGrid = endpointGrid;

                    // Also ensure connectivity to second-to-last point for full polyline connection
                    if (edge.PolylinePoints.Count >= 2)
                    {
                        Vector2 secondToLast = edge.PolylinePoints[edge.PolylinePoints.Count - 2] * scale + offset;
                        Vector2Int secondToLastGrid = new Vector2Int(Mathf.RoundToInt(secondToLast.x), Mathf.RoundToInt(secondToLast.y));
                        EnsureDirectPathExists(grid, gridWidth, gridHeight, nodeCenterGrid, secondToLastGrid);
                    }
                }
                else if (edge.NodeB.HasValue)
                {
                    // For connected edges: connect node centers
                    var nodeA = state.Nodes[edge.NodeA];
                    var nodeB = state.Nodes[edge.NodeB.Value];

                    Vector2 centerA = nodeA.Position * scale + offset;
                    Vector2 centerB = nodeB.Position * scale + offset;

                    startGrid = new Vector2Int(Mathf.RoundToInt(centerA.x), Mathf.RoundToInt(centerA.y));
                    endGrid = new Vector2Int(Mathf.RoundToInt(centerB.x), Mathf.RoundToInt(centerB.y));
                }
                else
                {
                    continue; // Skip edges without a second node
                }

                // Progressively convert walls to paths along the direct vector until reachable
                EnsureDirectPathExists(grid, gridWidth, gridHeight, startGrid, endGrid);
            }
        }

        private static int CountWalkableTilesInGrid(char[,] grid, int width, int height)
        {
            int count = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    char c = grid[y, x];
                    if (c == '.' || c == 'N' || c == 'H' || (char.IsUpper(c) && c != 'H' && c != 'N'))
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Ensures a path exists between start and end by converting ALL wall tiles
        /// along the direct vector to walkable tiles. This creates a straight corridor.
        /// </summary>
        private static void EnsureDirectPathExists(char[,] grid, int width, int height, Vector2Int start, Vector2Int end)
        {
            // Skip if start or end is out of bounds
            if (start.x < 0 || start.x >= width || start.y < 0 || start.y >= height)
            {
                // Debug.LogWarning($"[EnsureDirectPath] Start position ({start.x},{start.y}) is out of bounds (grid: {width}x{height})");
                return;
            }
            if (end.x < 0 || end.x >= width || end.y < 0 || end.y >= height)
            {
                // Debug.LogWarning($"[EnsureDirectPath] End position ({end.x},{end.y}) is out of bounds (grid: {width}x{height})");
                return;
            }

            // Use Bresenham's line algorithm to trace direct path from start to end
            int x0 = start.x;
            int y0 = start.y;
            int x1 = end.x;
            int y1 = end.y;

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            int wallsRemoved = 0;
            int x = x0;
            int y = y0;

            while (true)
            {
                // Convert wall tiles to paths along the direct line
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    char tile = grid[y, x];
                    if (tile == '#')  // Wall tile - convert to path
                    {
                        grid[y, x] = '.';
                        wallsRemoved++;
                    }
                }

                // Check if we've reached the end
                if (x == x1 && y == y1)
                    break;

                // Bresenham step
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }

        }

        /// <summary>
        /// Finds the furthest walkable point from start along the vector toward end.
        /// </summary>
        private static Vector2Int FindFurthestWalkableAlongVector(char[,] grid, int width, int height, Vector2Int start, Vector2Int end)
        {
            Vector2 direction = new Vector2(end.x - start.x, end.y - start.y);
            float distance = direction.magnitude;
            if (distance < 0.001f)
                return start;

            direction /= distance; // Normalize

            Vector2Int furthest = start;
            float furthestDist = 0;

            // Walk along the vector checking each grid cell
            for (float t = 0; t <= distance; t += 0.5f)
            {
                int x = Mathf.RoundToInt(start.x + direction.x * t);
                int y = Mathf.RoundToInt(start.y + direction.y * t);

                if (x < 0 || x >= width || y < 0 || y >= height)
                    break;

                if (IsWalkableTile(grid[y, x]))
                {
                    // Check if this point is reachable from start
                    Vector2Int point = new Vector2Int(x, y);
                    if (IsReachable(grid, width, height, start, point))
                    {
                        if (t > furthestDist)
                        {
                            furthestDist = t;
                            furthest = point;
                        }
                    }
                }
            }

            return furthest;
        }

        /// <summary>
        /// Finds the next wall tile along the vector from start to end that should be removed.
        /// </summary>
        private static Vector2Int? FindNextWallAlongVector(char[,] grid, int width, int height, Vector2Int start, Vector2Int end)
        {
            Vector2 direction = new Vector2(end.x - start.x, end.y - start.y);
            float distance = direction.magnitude;
            if (distance < 0.001f)
                return null;

            direction /= distance; // Normalize

            // Walk along the vector from start toward end
            for (float t = 1.0f; t <= distance; t += 0.5f)
            {
                int x = Mathf.RoundToInt(start.x + direction.x * t);
                int y = Mathf.RoundToInt(start.y + direction.y * t);

                if (x < 0 || x >= width || y < 0 || y >= height)
                    continue;

                // Found a wall tile - this is the one to remove
                if (!IsWalkableTile(grid[y, x]))
                {
                    return new Vector2Int(x, y);
                }
            }

            return null; // No wall found along vector
        }

        /// <summary>
        /// Checks if end is reachable from start using flood fill.
        /// </summary>
        private static bool IsReachable(char[,] grid, int width, int height, Vector2Int start, Vector2Int end)
        {
            // Check bounds first
            if (start.x < 0 || start.x >= width || start.y < 0 || start.y >= height)
                return false;
            if (end.x < 0 || end.x >= width || end.y < 0 || end.y >= height)
                return false;

            if (start == end)
                return true;

            if (!IsWalkableTile(grid[start.y, start.x]) || !IsWalkableTile(grid[end.y, end.x]))
                return false;

            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            visited.Add(start);

            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();

                if (current == end)
                    return true;

                foreach (var dir in directions)
                {
                    Vector2Int neighbor = current + dir;

                    if (neighbor.x < 0 || neighbor.x >= width || neighbor.y < 0 || neighbor.y >= height)
                        continue;

                    if (visited.Contains(neighbor))
                        continue;

                    if (!IsWalkableTile(grid[neighbor.y, neighbor.x]))
                        continue;

                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            return false; // End not reachable from start
        }

    }
}
