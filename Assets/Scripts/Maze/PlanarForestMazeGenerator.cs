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

        // Tunable parameters
        private const float CONNECT_PROB = 0.25f;
        private const float CENTER_BIAS = 0.75f;
        private const float BIAS_POWER = 2.0f;
        private const float BIAS_FLOOR = 0.1f;
        private const float ANGLE_MIN_SEPARATION = 20.0f; // degrees
        private const float CURVE_STRENGTH = 0.25f;
        private const float ROTATE_STEP = 6.0f; // degrees
        private const float SHORTEN_STEP = 0.3f;
        private const float MIN_CORRIDOR_LENGTH = 0.3f;
        private const float WALL_BUFFER = 1.0f; // Minimum wall buffer (in world units) between elements

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

            public bool IsComplete() => !Partial && NodeB.HasValue;
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
        /// <returns>Tuple of (empty string for legacy compatibility, generation state with world-space positions)</returns>
        public static (string maze, ForestMapState state) GenerateMazeWithState(int gridWidth, int gridHeight, int turns = 20, int? seed = null)
        {
            const int minNodeCount = 6; // Root + at least 5 normal nodes
            const int minOpenEndpoints = 5; // Preserve at least 5 open endpoints for spawn points
            int baseSeed = seed.HasValue ? seed.Value : System.Environment.TickCount;

            var state = new ForestMapState
            {
                Random = new System.Random(baseSeed),
                ValidationPassed = true // Always valid in world-space mode
            };

            // Initialize with root and first node
            Initialize(state);

            // Phase 1: Grow until we have minimum nodes
            for (int i = 0; i < turns && state.Nodes.Count < minNodeCount; i++)
            {
                if (state.Frontier.Count == 0 || !Step(state))
                    break;
            }

            // Ensure at least one cross-connection exists after initial growth
            EnsureCrossConnection(state);

            // Phase 2: Continue growing but preserve minimum open endpoints
            for (int i = 0; i < turns && state.Frontier.Count > minOpenEndpoints; i++)
            {
                if (!Step(state))
                    break;
            }

            Debug.Log($"[PlanarForest] Generated graph with {state.Nodes.Count} nodes, {state.Edges.Count} edges, {state.Frontier.Count} frontier edges");

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

            // Create first normal node
            float angle = (float)(state.Random.NextDouble() * 2.0 * Math.PI);
            float length = (float)(state.Random.NextDouble() * 7.0 + 3.0);
            float distance = 2 * NODE_RADIUS + length;

            Vector2 node1Pos = new Vector2(
                Mathf.Cos(angle) * distance,
                Mathf.Sin(angle) * distance
            );

            int maxDegree = state.Random.Next(2, 5); // 2-4
            var node1 = new Node
            {
                Id = state.NextNodeId++,
                Position = node1Pos,
                Kind = "normal",
                MaxDegree = maxDegree
            };
            state.Nodes.Add(node1);

            // Create edge between root and node1 with curved polyline
            var initialPolyline = BuildCurvedPolyline(state, root.Position, node1Pos,
                new List<int> { root.Id, node1.Id });

            // Fallback to straight line if curved path fails
            if (initialPolyline == null)
            {
                Vector2 direction = (node1Pos - root.Position).normalized;
                Vector2 startBoundary = root.Position + direction * NODE_RADIUS;
                Vector2 endBoundary = node1Pos - direction * NODE_RADIUS;
                initialPolyline = new List<Vector2> { startBoundary, endBoundary };
            }

            var edge = new Edge
            {
                Id = state.NextEdgeId++,
                NodeA = root.Id,
                NodeB = node1.Id,
                PolylinePoints = initialPolyline,
                Partial = false,
                GhostCenter = null
            };

            state.Edges.Add(edge);
            root.AddEdge(edge.Id, angle);
            float reverseAngle = (angle + Mathf.PI) % (2 * Mathf.PI);
            node1.AddEdge(edge.Id, reverseAngle);

            // Fill node1's remaining capacity with edges
            // Ensure at least one edge tries to connect to existing node (root)
            bool isFirstEdge = true;
            while (node1.HasCapacity())
            {
                // First edge always tries to connect, others have CONNECT_PROB chance
                bool tryConnect = isFirstEdge || state.Random.NextDouble() < CONNECT_PROB;

                if (tryConnect)
                {
                    // Try to connect to existing node (not root, which is the parent)
                    if (TryConnectToExisting(state, node1, root.Id, root.Id))
                    {
                        isFirstEdge = false;
                        continue;
                    }
                }

                // Otherwise add partial edge
                if (!AddPartialEdge(state, node1))
                    break;

                isFirstEdge = false;
            }
        }

        /// <summary>
        /// Executes one growth step: selects a frontier edge and creates a new node.
        /// This is used by both initial generation and dynamic growth.
        /// </summary>
        public static bool Step(ForestMapState state)
        {
            // Select a frontier edge using center-biased selection
            int? edgeId = SelectFrontierEdgeBiased(state);
            if (!edgeId.HasValue)
                return false;

            var edge = state.Edges[edgeId.Value];
            if (!edge.GhostCenter.HasValue)
                return false;

            // Create new node at ghost position
            int maxDegree = state.Random.Next(2, 5); // 2-4
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

            // Fill new node's remaining capacity
            while (newNode.HasCapacity())
            {
                // Try to connect to existing node (25% chance)
                if (state.Random.NextDouble() < CONNECT_PROB)
                {
                    // Allow forcing connection even to nodes at capacity during growth
                    if (TryConnectToExisting(state, newNode, edge.NodeA, edge.NodeA, allowForceCapacity: true))
                    {
                        Debug.Log($"[PlanarForest] Growth: Created cross-connection from node {newNode.Id}");
                        continue;
                    }
                }

                // Otherwise add partial edge
                if (!AddPartialEdge(state, newNode))
                    break;
            }

            state.TurnCount++;
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

                theta = theta % (2 * Mathf.PI);

                if (!IsAngleValid(node, theta))
                    continue;

                float length = length0;
                while (length >= MIN_SEGMENT_LENGTH * 2)
                {
                    Vector2 direction = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
                    Vector2 ghostCenter = node.Position + direction * (2 * NODE_RADIUS + length);

                    if (!IsGhostPositionValid(state, ghostCenter, node.Id))
                    {
                        length -= SHORTEN_STEP * 2;
                        continue;
                    }

                    // Try to build a curved polyline to the ghost position
                    var polyline = BuildCurvedPolylineToGhost(state, node.Position, ghostCenter,
                        new List<int> { node.Id });

                    if (polyline != null && IsPolylineValid(state, polyline, new List<int> { node.Id }, ghostCenter))
                    {
                        // Success! Create the partial edge
                        var edge = new Edge
                        {
                            Id = state.NextEdgeId++,
                            NodeA = node.Id,
                            NodeB = null,
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
                Vector2 start = nodeCenter + overallDirection * NODE_RADIUS;
                Vector2 end = ghostCenter - overallDirection * NODE_RADIUS;
                return new List<Vector2> { start, end };
            }

            Vector2 startBoundary = nodeCenter + overallDirection * NODE_RADIUS;
            Vector2 endBoundary = ghostCenter - overallDirection * NODE_RADIUS;

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

            // First try nodes with capacity
            var candidates = state.Nodes
                .Where(n => n.Id != newNode.Id && n.HasCapacity())
                .Where(n => !prohibitedNodeId.HasValue || n.Id != prohibitedNodeId.Value)
                .Where(n => !connectedNodeIds.Contains(n.Id))
                .ToList();

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
                return false;
            }

            // Shuffle candidates
            for (int i = 0; i < candidates.Count; i++)
            {
                int j = state.Random.Next(i, candidates.Count);
                var temp = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = temp;
            }

            foreach (var candidate in candidates)
            {
                if (connectedNodeIds.Contains(candidate.Id))
                {
                    continue;
                }

                Vector2 direction = (candidate.Position - newNode.Position).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x);
                angle = (angle + 2 * Mathf.PI) % (2 * Mathf.PI);

                if (!IsAngleValid(newNode, angle))
                {
                    continue;
                }

                float reverseAngle = (angle + Mathf.PI) % (2 * Mathf.PI);
                if (!IsAngleValid(candidate, reverseAngle))
                {
                    continue;
                }

                var polyline = BuildCurvedPolyline(state, newNode.Position, candidate.Position,
                    new List<int> { newNode.Id, candidate.Id });

                if (polyline == null)
                {
                    continue;
                }

                // Expand candidate's capacity if needed (when allowForceCapacity is true)
                if (!candidate.HasCapacity())
                {
                    candidate.MaxDegree++;
                    Debug.Log($"[PlanarForest] Expanded node {candidate.Id} capacity to {candidate.MaxDegree} for cross-connection");
                }

                var edge = new Edge
                {
                    Id = state.NextEdgeId++,
                    NodeA = newNode.Id,
                    NodeB = candidate.Id,
                    PolylinePoints = polyline,
                    Partial = false,
                    GhostCenter = null
                };

                state.Edges.Add(edge);
                newNode.AddEdge(edge.Id, angle);
                candidate.AddEdge(edge.Id, reverseAngle);

                // Mark as cross-connection if connecting to non-parent node
                if (!parentNodeId.HasValue || candidate.Id != parentNodeId.Value)
                {
                    state.HasCrossConnection = true;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Ensures at least one cross-connection exists by forcing nodes to connect to non-parents.
        /// Tries multiple nodes in reverse order until successful.
        /// Temporarily increases node capacity if needed.
        /// </summary>
        private static void EnsureCrossConnection(ForestMapState state)
        {
            if (state.HasCrossConnection || state.Nodes.Count < 3)
            {
                return; // Already has cross-connection or too few nodes
            }

            // First try nodes that already have capacity
            var nodesWithCapacity = state.Nodes.OrderByDescending(n => n.Id)
                                               .Where(n => n.HasCapacity())
                                               .ToList();

            foreach (var node in nodesWithCapacity)
            {
                if (TryForceConnection(state, node))
                    return; // Success!
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
                        return; // Success! Keep both increased capacities
                    }

                    // Restore target capacity
                    targetNode.MaxDegree = origTargetCapacity;
                }

                // Restore source capacity
                sourceNode.MaxDegree = origSourceCapacity;
            }

            Debug.LogWarning($"[PlanarForest] Could not force cross-connection even with increased capacity");
        }

        private static bool TryForceConnection(ForestMapState state, Node node)
        {
            // Find this node's parent (the node it was originally grown from)
            var parentEdge = state.Edges.FirstOrDefault(e =>
                e.NodeB.HasValue && e.NodeB.Value == node.Id);

            int? parentNodeId = parentEdge?.NodeA;

            // Try to force a connection to any existing node except parent
            return TryConnectToExisting(state, node, parentNodeId, parentNodeId);
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

        // Curved polyline parameters
        private const float MIN_SEGMENT_LENGTH = 4.0f;
        private const float MAX_SEGMENT_LENGTH = 8.0f;
        private const float MAX_CURVE_ANGLE = 35.0f; // degrees
        private const int MIN_SEGMENTS = 3;
        private const int MAX_SEGMENTS = 5;

        private static List<Vector2> BuildCurvedPolyline(ForestMapState state, Vector2 startCenter, Vector2 endCenter,
            List<int> incidentNodes)
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
                corridorDistance, numSegments, incidentNodes);

            if (polyline != null)
                return polyline;

            // Fallback: try with fewer segments
            for (int segs = numSegments - 1; segs >= 2; segs--)
            {
                polyline = TryBuildCurvedPath(state, startBoundary, endBoundary, overallDirection,
                    corridorDistance, segs, incidentNodes);
                if (polyline != null)
                    return polyline;
            }

            // Final fallback: straight line
            var straightLine = new List<Vector2> { startBoundary, endBoundary };
            if (IsPolylineValid(state, straightLine, incidentNodes))
                return straightLine;

            return null;
        }

        private static List<Vector2> TryBuildCurvedPath(ForestMapState state, Vector2 start, Vector2 end,
            Vector2 overallDirection, float corridorDistance, int numSegments, List<int> incidentNodes)
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

                if (polyline != null && IsPolylineValid(state, polyline, incidentNodes))
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
                // Random angle deviation within limits (alternating bias for S-curves)
                float maxAngle = MAX_CURVE_ANGLE * Mathf.Deg2Rad;
                float angleOffset;

                if (attempt == 0)
                {
                    // First attempt: no curve (almost straight)
                    angleOffset = 0;
                }
                else
                {
                    // Subsequent attempts: add random curvature
                    float bias = (i % 2 == 0) ? 1f : -1f; // Alternating bias for S-curve
                    float randomFactor = (state.Random.Next(0, 100) / 100f - 0.5f) * 2f;
                    angleOffset = (bias * 0.5f + randomFactor * 0.5f) * maxAngle * (attempt / 10f);
                }

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
            List<int> incidentNodes, Vector2? ghostPos = null)
        {
            if (polyline.Count < 2)
                return false;

            // Check against all existing nodes
            foreach (var node in state.Nodes)
            {
                bool isIncident = incidentNodes.Contains(node.Id);
                float dist = PolylineToNodeDistance(polyline, node.Position, isIncident);
                float minRequired = isIncident
                    ? NODE_RADIUS + PATH_RADIUS
                    : R_KEEP + PATH_RADIUS - 0.4f + WALL_BUFFER;

                if (dist < minRequired - 1e-6f)
                    return false;
            }

            // Check against ghost centers
            foreach (var ghost in state.GhostCenters)
            {
                if (ghostPos.HasValue && Vector2.Distance(ghost, ghostPos.Value) < 1e-6f)
                    continue;

                float dist = PolylineToNodeDistance(polyline, ghost, false);
                float minRequired = R_KEEP + PATH_RADIUS - 0.4f + WALL_BUFFER;

                if (dist < minRequired - 1e-6f)
                    return false;
            }

            // Check against all existing edges
            foreach (var edge in state.Edges)
            {
                if (edge.PolylinePoints.Count < 2)
                    continue;

                float dist = PolylineToPolylineDistance(polyline, edge.PolylinePoints);
                float minRequired = PATH_WIDTH * 0.8f + WALL_BUFFER;

                if (dist < minRequired - 1e-6f)
                    return false;
            }

            return true;
        }

        private static bool IsAngleValid(Node node, float angle)
        {
            float minSeparation = ANGLE_MIN_SEPARATION * Mathf.Deg2Rad;
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
            var weights = new List<float>();

            foreach (int edgeId in state.Frontier)
            {
                var edge = state.Edges[edgeId];
                if (!edge.GhostCenter.HasValue)
                {
                    weights.Add(0.0f);
                    continue;
                }

                float dist = Vector2.Distance(edge.GhostCenter.Value, rootPos);
                float baseWeight = 1.0f / Mathf.Pow(dist + 1e-6f, BIAS_POWER);
                float weight = (1.0f - CENTER_BIAS) + CENTER_BIAS * baseWeight;
                weight = Mathf.Max(weight, BIAS_FLOOR);

                weights.Add(weight);
            }

            float totalWeight = weights.Sum();
            if (totalWeight < 1e-9f)
                return state.Frontier[state.Random.Next(state.Frontier.Count)];

            float r = (float)(state.Random.NextDouble() * totalWeight);
            float cumulative = 0.0f;

            for (int i = 0; i < weights.Count; i++)
            {
                cumulative += weights[i];
                if (r <= cumulative)
                    return state.Frontier[i];
            }

            return state.Frontier[state.Frontier.Count - 1];
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
                Debug.LogWarning($"Maze validation failed: {validation.ErrorMessage}");
                // Return grid anyway, GenerateMaze will handle retry
            }
            else
            {
                Debug.Log($"Maze validation passed: {state.Nodes.Count} nodes, {validation.DetectedConnections.Count} connections");

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
                Debug.LogWarning($"[EnsureDirectPath] Start position ({start.x},{start.y}) is out of bounds (grid: {width}x{height})");
                return;
            }
            if (end.x < 0 || end.x >= width || end.y < 0 || end.y >= height)
            {
                Debug.LogWarning($"[EnsureDirectPath] End position ({end.x},{end.y}) is out of bounds (grid: {width}x{height})");
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
