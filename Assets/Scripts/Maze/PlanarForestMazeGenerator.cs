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

        private class Node
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

        private class Edge
        {
            public int Id;
            public int NodeA;
            public int? NodeB; // null if partial (open endpoint)
            public List<Vector2> PolylinePoints = new List<Vector2>();
            public bool Partial = true;
            public Vector2? GhostCenter; // Reserved position for future node

            public bool IsComplete() => !Partial && NodeB.HasValue;
        }

        private class ForestMapState
        {
            public List<Node> Nodes = new List<Node>();
            public List<Edge> Edges = new List<Edge>();
            public List<int> Frontier = new List<int>(); // Partial edge IDs
            public List<Vector2> GhostCenters = new List<Vector2>();
            public int NextNodeId = 0;
            public int NextEdgeId = 0;
            public int TurnCount = 0;
            public System.Random Random;
        }

        /// <summary>
        /// Generate a planar organic forest maze.
        /// </summary>
        /// <param name="gridWidth">Target grid width (map will be sized to fit)</param>
        /// <param name="gridHeight">Target grid height (map will be sized to fit)</param>
        /// <param name="turns">Number of growth turns (more turns = more nodes)</param>
        /// <param name="seed">Random seed</param>
        /// <returns>Character grid representing the maze</returns>
        public static string GenerateMaze(int gridWidth, int gridHeight, int turns = 20, int? seed = null)
        {
            var state = new ForestMapState
            {
                Random = seed.HasValue ? new System.Random(seed.Value) : new System.Random()
            };

            // Initialize with root and first node
            Initialize(state);

            // Grow to ensure minimum node count, then preserve open endpoints
            int minNodeCount = 6; // Root + at least 5 normal nodes
            int minOpenEndpoints = 5; // Preserve at least 5 open endpoints for spawn points (was 4)

            Debug.Log($"[PlanarForestMaze] Starting growth: turns={turns}, minNodes={minNodeCount}, minOpenEndpoints={minOpenEndpoints}");

            // Phase 1: Grow until we have minimum nodes
            for (int i = 0; i < turns && state.Nodes.Count < minNodeCount; i++)
            {
                if (state.Frontier.Count == 0 || !Step(state))
                    break;
            }

            // Phase 2: Continue growing but preserve minimum open endpoints
            for (int i = 0; i < turns && state.Frontier.Count > minOpenEndpoints; i++)
            {
                if (!Step(state))
                    break;
            }
            Debug.Log($"[PlanarForestMaze] Growth complete: nodes={state.Nodes.Count}, openEndpoints={state.Frontier.Count}");

            // Rasterize the graph to a grid
            return RasterizeToGrid(state, gridWidth, gridHeight);
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

            int maxDegree = state.Random.Next(1, 5); // 1-4
            var node1 = new Node
            {
                Id = state.NextNodeId++,
                Position = node1Pos,
                Kind = "normal",
                MaxDegree = maxDegree
            };
            state.Nodes.Add(node1);

            // Create edge between root and node1 (straight line for simplicity)
            Vector2 direction = (node1Pos - root.Position).normalized;
            Vector2 startBoundary = root.Position + direction * NODE_RADIUS;
            Vector2 endBoundary = node1Pos - direction * NODE_RADIUS;

            var edge = new Edge
            {
                Id = state.NextEdgeId++,
                NodeA = root.Id,
                NodeB = node1.Id,
                PolylinePoints = new List<Vector2> { startBoundary, endBoundary },
                Partial = false,
                GhostCenter = null
            };

            state.Edges.Add(edge);
            root.AddEdge(edge.Id, angle);
            float reverseAngle = (angle + Mathf.PI) % (2 * Mathf.PI);
            node1.AddEdge(edge.Id, reverseAngle);

            // Fill node1's remaining capacity with partial edges
            while (node1.HasCapacity())
            {
                AddPartialEdge(state, node1);
            }
        }

        private static bool Step(ForestMapState state)
        {
            // Select a frontier edge using center-biased selection
            int? edgeId = SelectFrontierEdgeBiased(state);
            if (!edgeId.HasValue)
                return false;

            var edge = state.Edges[edgeId.Value];
            if (!edge.GhostCenter.HasValue)
                return false;

            // Create new node at ghost position
            int maxDegree = state.Random.Next(1, 5);
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
                // Try to connect to existing node
                if (state.Random.NextDouble() < CONNECT_PROB)
                {
                    if (TryConnectToExisting(state, newNode))
                        continue;
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
            float length0 = (float)(state.Random.NextDouble() * 7.0 + 3.0);

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
                while (length >= MIN_CORRIDOR_LENGTH)
                {
                    Vector2 direction = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
                    Vector2 ghostCenter = node.Position + direction * (2 * NODE_RADIUS + length);

                    if (!IsGhostPositionValid(state, ghostCenter))
                    {
                        length -= SHORTEN_STEP;
                        continue;
                    }

                    // Build polyline
                    Vector2 nodeBoundary = node.Position + direction * NODE_RADIUS;
                    Vector2 ghostBoundary = ghostCenter - direction * NODE_RADIUS;

                    float chordLength = Vector2.Distance(nodeBoundary, ghostBoundary);
                    float kmax = CURVE_STRENGTH * chordLength;

                    Vector2 perp = new Vector2(-direction.y, direction.x);

                    float[] kFactors = { 0.0f, 0.5f, -0.5f, 1.0f, -1.0f };
                    foreach (float kFactor in kFactors)
                    {
                        float k = kFactor * kmax;
                        Vector2 control1 = nodeBoundary + (ghostBoundary - nodeBoundary) * 0.33f + perp * k;
                        Vector2 control2 = nodeBoundary + (ghostBoundary - nodeBoundary) * 0.66f - perp * (0.7f * k);

                        var polyline = new List<Vector2> { nodeBoundary, control1, control2, ghostBoundary };

                        if (IsPolylineValid(state, polyline, new List<int> { node.Id }, ghostCenter))
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
                    }

                    length -= SHORTEN_STEP;
                }
            }

            return false;
        }

        private static bool TryConnectToExisting(ForestMapState state, Node newNode)
        {
            var candidates = state.Nodes
                .Where(n => n.Id != newNode.Id && n.HasCapacity())
                .Where(n => !state.Edges.Any(e =>
                    (e.NodeA == newNode.Id && e.NodeB == n.Id) ||
                    (e.NodeA == n.Id && e.NodeB == newNode.Id)))
                .ToList();

            if (candidates.Count == 0)
                return false;

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
                Vector2 direction = (candidate.Position - newNode.Position).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x);
                angle = (angle + 2 * Mathf.PI) % (2 * Mathf.PI);

                if (!IsAngleValid(newNode, angle))
                    continue;

                float reverseAngle = (angle + Mathf.PI) % (2 * Mathf.PI);
                if (!IsAngleValid(candidate, reverseAngle))
                    continue;

                var polyline = BuildCurvedPolyline(state, newNode.Position, candidate.Position,
                    new List<int> { newNode.Id, candidate.Id });

                if (polyline == null)
                    continue;

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

                return true;
            }

            return false;
        }

        private static List<Vector2> BuildCurvedPolyline(ForestMapState state, Vector2 startCenter, Vector2 endCenter,
            List<int> incidentNodes)
        {
            Vector2 direction = (endCenter - startCenter).normalized;
            Vector2 startBoundary = startCenter + direction * NODE_RADIUS;
            Vector2 endBoundary = endCenter - direction * NODE_RADIUS;

            float chordLength = Vector2.Distance(startBoundary, endBoundary);
            float kmax = CURVE_STRENGTH * chordLength;

            float[] kValues = {
                0.0f,
                0.25f * kmax, -0.25f * kmax,
                0.45f * kmax, -0.45f * kmax,
                0.65f * kmax, -0.65f * kmax,
                0.85f * kmax, -0.85f * kmax,
                1.0f * kmax, -1.0f * kmax
            };

            Vector2 perp = new Vector2(-direction.y, direction.x);

            foreach (float k in kValues)
            {
                Vector2 control1 = startBoundary + (endBoundary - startBoundary) * 0.33f + perp * k;
                Vector2 control2 = startBoundary + (endBoundary - startBoundary) * 0.66f - perp * (0.7f * k);

                var polyline = new List<Vector2> { startBoundary, control1, control2, endBoundary };

                if (IsPolylineValid(state, polyline, incidentNodes))
                {
                    return polyline;
                }
            }

            return null;
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
                float minRequired = isIncident ? NODE_RADIUS + PATH_RADIUS : R_KEEP + PATH_RADIUS - 0.4f;

                if (dist < minRequired - 1e-6f)
                    return false;
            }

            // Check against ghost centers
            foreach (var ghost in state.GhostCenters)
            {
                if (ghostPos.HasValue && Vector2.Distance(ghost, ghostPos.Value) < 1e-6f)
                    continue;

                float dist = PolylineToNodeDistance(polyline, ghost, false);
                float minRequired = R_KEEP + PATH_RADIUS - 0.4f;

                if (dist < minRequired - 1e-6f)
                    return false;
            }

            // Check against all existing edges
            foreach (var edge in state.Edges)
            {
                if (edge.PolylinePoints.Count < 2)
                    continue;

                float dist = PolylineToPolylineDistance(polyline, edge.PolylinePoints);
                float minRequired = PATH_WIDTH * 0.8f;

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

        private static bool IsGhostPositionValid(ForestMapState state, Vector2 ghostPos)
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

            return true;
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
            const float targetNodeDiameterTiles = 7f;
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

            // Initialize grid with forest
            char[,] grid = new char[gridHeight, gridWidth];
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    grid[y, x] = '#';
                }
            }

            // Draw paths (edges)
            foreach (var edge in state.Edges.Where(e => e.PolylinePoints.Count > 1))
            {
                for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                {
                    Vector2 p1 = edge.PolylinePoints[i] * scale + offset;
                    Vector2 p2 = edge.PolylinePoints[i + 1] * scale + offset;

                    // For partial (open) edges, do not rasterize the very final endpoint cell.
                    // Adjacency is guaranteed by the previously rasterized step(s).
                    bool isLastSegment = (i == edge.PolylinePoints.Count - 2);
                    bool includeEndPoint = !(edge.Partial && isLastSegment);

                    DrawLineOnGrid(grid, p1, p2, '.', gridWidth, gridHeight, includeEndPoint);
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

                // Prefer the true endpoint (last polyline point) for spawn placement.
                // This endpoint cell is intentionally NOT rasterized for partial edges; it is only "placed" as a spawn marker.
                Vector2 endPoint = edge.PolylinePoints[edge.PolylinePoints.Count - 1] * scale + offset;
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
                float maxDistance = -1f;

                // Bias the spawn point toward the farthest walkable cell from the connected node center.
                for (int i = edge.PolylinePoints.Count - 1; i >= 0; i--)
                {
                    Vector2 point = edge.PolylinePoints[i] * scale + offset;
                    int px = Mathf.RoundToInt(point.x);
                    int py = Mathf.RoundToInt(point.y);

                    if (px < 0 || px >= gridWidth || py < 0 || py >= gridHeight)
                    {
                        continue;
                    }

                    char candidateTile = grid[py, px];
                    if (!IsWalkableTile(candidateTile) || candidateTile == 'H' || candidateTile == 'N')
                    {
                        continue;
                    }

                    float distance = (px - nx) * (px - nx) + (py - ny) * (py - ny);
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        targetX = px;
                        targetY = py;
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

            // Add entrances
            AddEntrance(grid, gridWidth, gridHeight, state.Random);

            return GridToString(grid, gridWidth, gridHeight);
        }

        private static void DrawLineOnGrid(char[,] grid, Vector2 p1, Vector2 p2, char ch, int width, int height, bool includeEndPoint = true)
        {
            int x0 = Mathf.RoundToInt(p1.x);
            int y0 = Mathf.RoundToInt(p1.y);
            int x1 = Mathf.RoundToInt(p2.x);
            int y1 = Mathf.RoundToInt(p2.y);

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            // Local helper that also prevents neighbor "spill" into the end cell when endpoint rasterization is disabled.
            void Set(int x, int y)
            {
                if (!includeEndPoint && x == x1 && y == y1)
                    return;

                SetGridCell(grid, x, y, ch, width, height);
            }

            while (true)
            {
                bool atEnd = (x0 == x1 && y0 == y1);

                // Only draw at the end if includeEndPoint is true.
                if (!atEnd || includeEndPoint)
                {
                    // Draw center pixel and adjacent pixels for wider path
                    Set(x0, y0);

                    // Draw orthogonal neighbors to ensure path is always walkable
                    Set(x0 + 1, y0);
                    Set(x0 - 1, y0);
                    Set(x0, y0 + 1);
                    Set(x0, y0 - 1);
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
    }
}
