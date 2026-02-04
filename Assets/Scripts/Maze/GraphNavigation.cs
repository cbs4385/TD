using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForestMaze
{
    /// <summary>
    /// Type of segment in a graph path.
    /// </summary>
    public enum SegmentType
    {
        Edge,
        Node
    }

    /// <summary>
    /// A segment of a graph path, representing traversal of either an edge or a node.
    /// </summary>
    public struct GraphPathSegment
    {
        /// <summary>Whether this segment traverses an edge or a node.</summary>
        public SegmentType Type;

        /// <summary>Index of the edge or node in ForestMapState.</summary>
        public int Index;

        /// <summary>World position where visitor enters this segment.</summary>
        public Vector2 EntryPoint;

        /// <summary>World position where visitor exits this segment.</summary>
        public Vector2 ExitPoint;

        /// <summary>For edges: true if traversing from NodeB to NodeA (reversed direction).</summary>
        public bool Reversed;

        /// <summary>Approximate length of this segment in world units.</summary>
        public float Length;

        public override string ToString()
        {
            return $"{Type}[{Index}] {(Reversed ? "R" : "")} len={Length:F1}";
        }
    }

    /// <summary>
    /// A complete path through the maze graph, consisting of alternating node and edge segments.
    /// </summary>
    public class GraphPath
    {
        /// <summary>Ordered list of segments from start to destination.</summary>
        public List<GraphPathSegment> Segments = new List<GraphPathSegment>();

        /// <summary>Total length of the path in world units.</summary>
        public float TotalLength;

        /// <summary>Whether this path is valid and complete.</summary>
        public bool IsValid => Segments.Count > 0;
    }

    /// <summary>
    /// Graph-based navigation system that uses the actual maze geometry (Bezier curves and circular nodes)
    /// instead of tile approximations.
    /// </summary>
    public static class GraphNavigation
    {
        // Constants from CLAUDE.md
        private const float NODE_RADIUS = 3.0f;
        private const float WALKABLE_RING_MIN = 1.0f;  // Inner edge of walkable ring (unwalkable center)
        private const float WALKABLE_RING_MAX = 3.0f;  // Outer edge of walkable ring (NODE_RADIUS)
        private const float WALKABLE_RING_MID = 2.0f;  // Middle of walkable ring for arc traversal
        private const float PATH_WIDTH = 1.25f;

        #region Main Pathfinding API

        /// <summary>
        /// Finds a path through the graph from start to end position.
        /// Returns a sequence of edge and node segments to traverse.
        /// </summary>
        /// <param name="mapState">The forest map state containing nodes and edges.</param>
        /// <param name="start">Starting world position.</param>
        /// <param name="end">Destination world position.</param>
        /// <returns>A GraphPath with segments, or an invalid path if no route exists.</returns>
        public static GraphPath FindPath(
            PlanarForestMazeGenerator.ForestMapState mapState,
            Vector2 start,
            Vector2 end)
        {
            var path = new GraphPath();

            if (mapState == null || mapState.Nodes.Count == 0)
            {
                return path;
            }

            // Find which graph element contains the start position
            var startLocation = FindGraphLocation(mapState, start);
            var endLocation = FindGraphLocation(mapState, end);

            if (!startLocation.IsValid || !endLocation.IsValid)
            {
                return path;
            }

            // If start and end are on the same element, create a simple path
            if (startLocation.Type == endLocation.Type && startLocation.Index == endLocation.Index)
            {
                path.Segments.Add(CreateSegment(mapState, startLocation, start, end));
                CalculateTotalLength(path);
                return path;
            }

            // Find path through nodes using A*
            int startNodeIndex = GetNearestNodeIndex(mapState, startLocation);
            int endNodeIndex = GetNearestNodeIndex(mapState, endLocation);

            if (startNodeIndex < 0 || endNodeIndex < 0)
            {
                return path;
            }

            // A* through graph nodes
            var nodePath = FindNodePath(mapState, startNodeIndex, endNodeIndex);
            if (nodePath == null || nodePath.Count == 0)
            {
                return path;
            }

            // Build segment sequence from node path
            BuildPathSegments(mapState, path, start, end, startLocation, endLocation, nodePath);
            CalculateTotalLength(path);

            return path;
        }

        #endregion

        #region Graph Location Finding

        /// <summary>
        /// Represents a location on the graph (either on an edge or within a node).
        /// </summary>
        public struct GraphLocation
        {
            public SegmentType Type;
            public int Index;           // Edge or Node index
            public float Progress;      // 0-1 for edges, angle in radians for nodes
            public bool IsValid;

            public static GraphLocation Invalid => new GraphLocation { IsValid = false, Index = -1 };
        }

        /// <summary>
        /// Finds which graph element (node or edge) contains the given position.
        /// </summary>
        public static GraphLocation FindGraphLocation(
            PlanarForestMazeGenerator.ForestMapState mapState,
            Vector2 position)
        {
            // First check if position is within any node
            for (int i = 0; i < mapState.Nodes.Count; i++)
            {
                var node = mapState.Nodes[i];
                float dist = Vector2.Distance(position, node.Position);

                if (dist < NODE_RADIUS)
                {
                    // Within node - calculate angle from center
                    Vector2 offset = position - node.Position;
                    float angle = Mathf.Atan2(offset.y, offset.x);

                    return new GraphLocation
                    {
                        Type = SegmentType.Node,
                        Index = i,
                        Progress = angle,
                        IsValid = true
                    };
                }
            }

            // Check edges - find closest edge
            // ALWAYS use polyline - it matches the actual path where walls are placed
            float bestDist = float.MaxValue;
            int bestEdge = -1;
            float bestProgress = 0f;

            for (int i = 0; i < mapState.Edges.Count; i++)
            {
                var edge = mapState.Edges[i];

                // Use polyline exclusively (not edge.Curve which is a straight-line approximation)
                if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
                    continue;

                float t = EdgeFollower.GetProgress(edge, position);
                Vector2 closestPoint = EdgeFollower.GetPosition(edge, t);

                float dist = Vector2.Distance(position, closestPoint);

                // Check if within path width
                if (dist < PATH_WIDTH && dist < bestDist)
                {
                    bestDist = dist;
                    bestEdge = i;
                    bestProgress = t;
                }
            }

            if (bestEdge >= 0)
            {
                return new GraphLocation
                {
                    Type = SegmentType.Edge,
                    Index = bestEdge,
                    Progress = bestProgress,
                    IsValid = true
                };
            }

            // Fallback: find nearest node
            int nearestNode = FindNearestNode(mapState, position);
            if (nearestNode >= 0)
            {
                var node = mapState.Nodes[nearestNode];
                Vector2 offset = position - node.Position;
                float angle = Mathf.Atan2(offset.y, offset.x);

                return new GraphLocation
                {
                    Type = SegmentType.Node,
                    Index = nearestNode,
                    Progress = angle,
                    IsValid = true
                };
            }

            return GraphLocation.Invalid;
        }

        /// <summary>
        /// Finds the nearest node to a position.
        /// </summary>
        private static int FindNearestNode(
            PlanarForestMazeGenerator.ForestMapState mapState,
            Vector2 position)
        {
            float bestDist = float.MaxValue;
            int bestNode = -1;

            for (int i = 0; i < mapState.Nodes.Count; i++)
            {
                float dist = Vector2.Distance(position, mapState.Nodes[i].Position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestNode = i;
                }
            }

            return bestNode;
        }

        /// <summary>
        /// Gets the node index associated with a graph location.
        /// For edges, returns the closer of the two endpoint nodes.
        /// </summary>
        private static int GetNearestNodeIndex(
            PlanarForestMazeGenerator.ForestMapState mapState,
            GraphLocation location)
        {
            if (location.Type == SegmentType.Node)
                return location.Index;

            // For edge, return the closer endpoint
            var edge = mapState.Edges[location.Index];
            if (!edge.NodeB.HasValue)
            {
                // Partial edge - only NodeA exists
                return edge.NodeA;
            }

            // Return closer node based on progress
            return location.Progress < 0.5f ? edge.NodeA : edge.NodeB.Value;
        }

        #endregion

        #region Node Path A*

        /// <summary>
        /// A* pathfinding through graph nodes.
        /// Returns list of node indices from start to end.
        /// </summary>
        private static List<int> FindNodePath(
            PlanarForestMazeGenerator.ForestMapState mapState,
            int startNode,
            int endNode)
        {
            if (startNode == endNode)
                return new List<int> { startNode };

            var openSet = new SortedSet<(float fScore, int nodeId, int counter)>();
            var cameFrom = new Dictionary<int, int>();
            var gScore = new Dictionary<int, float>();
            var fScore = new Dictionary<int, float>();
            int counter = 0;

            Vector2 goalPos = mapState.Nodes[endNode].Position;

            gScore[startNode] = 0;
            fScore[startNode] = Heuristic(mapState.Nodes[startNode].Position, goalPos);
            openSet.Add((fScore[startNode], startNode, counter++));

            int maxIterations = 10000;
            int iterations = 0;

            while (openSet.Count > 0 && iterations++ < maxIterations)
            {
                var current = openSet.Min;
                openSet.Remove(current);
                int currentNode = current.nodeId;

                if (currentNode == endNode)
                {
                    // Reconstruct path
                    var path = new List<int>();
                    int node = endNode;
                    while (cameFrom.ContainsKey(node))
                    {
                        path.Add(node);
                        node = cameFrom[node];
                    }
                    path.Add(startNode);
                    path.Reverse();
                    return path;
                }

                // Get neighbors via incident edges
                var nodeData = mapState.Nodes[currentNode];
                foreach (int edgeId in nodeData.IncidentEdges)
                {
                    var edge = mapState.Edges[edgeId];
                    int neighborNode = -1;

                    if (edge.NodeA == currentNode && edge.NodeB.HasValue)
                        neighborNode = edge.NodeB.Value;
                    else if (edge.NodeB.HasValue && edge.NodeB.Value == currentNode)
                        neighborNode = edge.NodeA;

                    if (neighborNode < 0) continue;

                    // Calculate edge length from polyline (the actual path)
                    float edgeLength = CalculatePolylineLength(edge.PolylinePoints) > 0
                        ? CalculatePolylineLength(edge.PolylinePoints)
                        : Vector2.Distance(nodeData.Position, mapState.Nodes[neighborNode].Position);

                    float tentativeG = gScore[currentNode] + edgeLength;

                    if (!gScore.ContainsKey(neighborNode) || tentativeG < gScore[neighborNode])
                    {
                        cameFrom[neighborNode] = currentNode;
                        gScore[neighborNode] = tentativeG;
                        float h = Heuristic(mapState.Nodes[neighborNode].Position, goalPos);
                        fScore[neighborNode] = tentativeG + h;

                        openSet.Add((fScore[neighborNode], neighborNode, counter++));
                    }
                }
            }

            // No path found
            return null;
        }

        private static float Heuristic(Vector2 a, Vector2 b)
        {
            return Vector2.Distance(a, b);
        }

        #endregion

        #region Segment Building

        /// <summary>
        /// Builds the path segments from the node path.
        /// </summary>
        private static void BuildPathSegments(
            PlanarForestMazeGenerator.ForestMapState mapState,
            GraphPath path,
            Vector2 start,
            Vector2 end,
            GraphLocation startLocation,
            GraphLocation endLocation,
            List<int> nodePath)
        {
            if (nodePath.Count == 0) return;

            // Handle start position (might be on edge or node)
            Vector2 currentPos = start;

            // If starting on an edge, add partial edge segment to first node
            if (startLocation.Type == SegmentType.Edge)
            {
                var edge = mapState.Edges[startLocation.Index];
                int firstNodeInPath = nodePath[0];

                // Determine direction to first node
                bool reversed = edge.NodeB.HasValue && edge.NodeB.Value == firstNodeInPath;

                Vector2 entryOnNode = GetEdgeNodeConnectionPoint(mapState, startLocation.Index, firstNodeInPath);

                path.Segments.Add(new GraphPathSegment
                {
                    Type = SegmentType.Edge,
                    Index = startLocation.Index,
                    EntryPoint = start,
                    ExitPoint = entryOnNode,
                    Reversed = reversed,
                    Length = CalculatePartialEdgeLength(edge, startLocation.Progress, reversed ? 0f : 1f)
                });

                currentPos = entryOnNode;
            }

            // Add segments for each node-to-node transition
            for (int i = 0; i < nodePath.Count; i++)
            {
                int nodeIndex = nodePath[i];
                var node = mapState.Nodes[nodeIndex];

                // Is this the last node?
                bool isLastNode = (i == nodePath.Count - 1);

                if (isLastNode)
                {
                    // Handle end position
                    if (endLocation.Type == SegmentType.Node && endLocation.Index == nodeIndex)
                    {
                        // End is within this node - add node segment to end position
                        if (Vector2.Distance(currentPos, end) > 0.1f)
                        {
                            path.Segments.Add(CreateNodeSegment(mapState, nodeIndex, currentPos, end));
                        }
                    }
                    else if (endLocation.Type == SegmentType.Edge)
                    {
                        // End is on an edge from this node
                        var endEdge = mapState.Edges[endLocation.Index];

                        // Add node segment to edge connection
                        Vector2 exitToEdge = GetEdgeNodeConnectionPoint(mapState, endLocation.Index, nodeIndex);
                        if (Vector2.Distance(currentPos, exitToEdge) > 0.1f)
                        {
                            path.Segments.Add(CreateNodeSegment(mapState, nodeIndex, currentPos, exitToEdge));
                        }

                        // Add partial edge segment to end
                        bool reversed = endEdge.NodeB.HasValue && endEdge.NodeB.Value == nodeIndex;
                        path.Segments.Add(new GraphPathSegment
                        {
                            Type = SegmentType.Edge,
                            Index = endLocation.Index,
                            EntryPoint = exitToEdge,
                            ExitPoint = end,
                            Reversed = reversed,
                            Length = CalculatePartialEdgeLength(endEdge, reversed ? 1f : 0f, endLocation.Progress)
                        });
                    }
                }
                else
                {
                    // Not the last node - find edge to next node
                    int nextNodeIndex = nodePath[i + 1];
                    int connectingEdgeIndex = FindConnectingEdge(mapState, nodeIndex, nextNodeIndex);

                    if (connectingEdgeIndex < 0)
                    {
                        continue;
                    }

                    var edge = mapState.Edges[connectingEdgeIndex];

                    // Add node segment (arc through node to edge connection)
                    Vector2 exitPoint = GetEdgeNodeConnectionPoint(mapState, connectingEdgeIndex, nodeIndex);
                    if (Vector2.Distance(currentPos, exitPoint) > 0.1f)
                    {
                        path.Segments.Add(CreateNodeSegment(mapState, nodeIndex, currentPos, exitPoint));
                    }

                    // Add edge segment
                    bool reversed = edge.NodeA != nodeIndex;
                    Vector2 entryOnNextNode = GetEdgeNodeConnectionPoint(mapState, connectingEdgeIndex, nextNodeIndex);

                    path.Segments.Add(new GraphPathSegment
                    {
                        Type = SegmentType.Edge,
                        Index = connectingEdgeIndex,
                        EntryPoint = exitPoint,
                        ExitPoint = entryOnNextNode,
                        Reversed = reversed,
                        Length = CalculatePolylineLength(edge.PolylinePoints) > 0
                            ? CalculatePolylineLength(edge.PolylinePoints)
                            : Vector2.Distance(exitPoint, entryOnNextNode)
                    });

                    currentPos = entryOnNextNode;
                }
            }
        }

        /// <summary>
        /// Creates a single segment for same-element paths.
        /// </summary>
        private static GraphPathSegment CreateSegment(
            PlanarForestMazeGenerator.ForestMapState mapState,
            GraphLocation location,
            Vector2 start,
            Vector2 end)
        {
            if (location.Type == SegmentType.Edge)
            {
                var edge = mapState.Edges[location.Index];
                // Use polyline-based progress calculation to match actual path
                float startT = EdgeFollower.GetProgress(edge, start);
                float endT = EdgeFollower.GetProgress(edge, end);

                return new GraphPathSegment
                {
                    Type = SegmentType.Edge,
                    Index = location.Index,
                    EntryPoint = start,
                    ExitPoint = end,
                    Reversed = endT < startT,
                    Length = CalculatePartialEdgeLength(edge, startT, endT)
                };
            }
            else
            {
                return CreateNodeSegment(mapState, location.Index, start, end);
            }
        }

        /// <summary>
        /// Creates a node segment with arc through walkable ring.
        /// </summary>
        private static GraphPathSegment CreateNodeSegment(
            PlanarForestMazeGenerator.ForestMapState mapState,
            int nodeIndex,
            Vector2 entry,
            Vector2 exit)
        {
            var node = mapState.Nodes[nodeIndex];
            float arcLength = CalculateNodeArcLength(node.Position, entry, exit);

            return new GraphPathSegment
            {
                Type = SegmentType.Node,
                Index = nodeIndex,
                EntryPoint = entry,
                ExitPoint = exit,
                Reversed = false,
                Length = arcLength
            };
        }

        /// <summary>
        /// Finds the edge connecting two nodes.
        /// </summary>
        private static int FindConnectingEdge(
            PlanarForestMazeGenerator.ForestMapState mapState,
            int nodeA,
            int nodeB)
        {
            var node = mapState.Nodes[nodeA];
            foreach (int edgeId in node.IncidentEdges)
            {
                var edge = mapState.Edges[edgeId];
                if ((edge.NodeA == nodeA && edge.NodeB.HasValue && edge.NodeB.Value == nodeB) ||
                    (edge.NodeA == nodeB && edge.NodeB.HasValue && edge.NodeB.Value == nodeA))
                {
                    return edgeId;
                }
            }
            return -1;
        }

        /// <summary>
        /// Gets the point where an edge connects to a node (on the node boundary).
        /// </summary>
        private static Vector2 GetEdgeNodeConnectionPoint(
            PlanarForestMazeGenerator.ForestMapState mapState,
            int edgeIndex,
            int nodeIndex)
        {
            var edge = mapState.Edges[edgeIndex];
            var node = mapState.Nodes[nodeIndex];

            // Determine which end of the edge connects to this node
            bool isNodeA = edge.NodeA == nodeIndex;

            // Use polyline endpoints (the actual path where walls are placed)
            if (edge.PolylinePoints != null && edge.PolylinePoints.Count > 0)
            {
                return isNodeA ? edge.PolylinePoints[0] : edge.PolylinePoints[edge.PolylinePoints.Count - 1];
            }

            // Fallback: calculate point at NODE_RADIUS from node center
            Vector2 otherNodePos;
            if (isNodeA && edge.NodeB.HasValue)
                otherNodePos = mapState.Nodes[edge.NodeB.Value].Position;
            else
                otherNodePos = mapState.Nodes[edge.NodeA].Position;

            Vector2 dir = (otherNodePos - node.Position).normalized;
            return node.Position + dir * NODE_RADIUS;
        }

        #endregion

        #region Length Calculations

        private static void CalculateTotalLength(GraphPath path)
        {
            path.TotalLength = 0f;
            foreach (var segment in path.Segments)
            {
                path.TotalLength += segment.Length;
            }
        }

        /// <summary>
        /// Calculates the total length of a polyline.
        /// </summary>
        private static float CalculatePolylineLength(List<Vector2> points)
        {
            if (points == null || points.Count < 2)
                return 0f;

            float length = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                length += Vector2.Distance(points[i - 1], points[i]);
            }
            return length;
        }

        private static float CalculatePartialEdgeLength(
            PlanarForestMazeGenerator.Edge edge,
            float startT,
            float endT)
        {
            // Always use polyline for length calculation (matches actual path)
            if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
                return 0f;

            float totalLen = 0f;
            for (int i = 1; i < edge.PolylinePoints.Count; i++)
            {
                totalLen += Vector2.Distance(edge.PolylinePoints[i - 1], edge.PolylinePoints[i]);
            }
            return totalLen * Mathf.Abs(endT - startT);
        }

        private static float CalculateNodeArcLength(Vector2 nodeCenter, Vector2 entry, Vector2 exit)
        {
            Vector2 entryOffset = entry - nodeCenter;
            Vector2 exitOffset = exit - nodeCenter;

            float entryAngle = Mathf.Atan2(entryOffset.y, entryOffset.x);
            float exitAngle = Mathf.Atan2(exitOffset.y, exitOffset.x);

            float angleDiff = Mathf.Abs(Mathf.DeltaAngle(entryAngle * Mathf.Rad2Deg, exitAngle * Mathf.Rad2Deg)) * Mathf.Deg2Rad;

            // Arc at middle of walkable ring
            return angleDiff * WALKABLE_RING_MID;
        }

        #endregion
    }

    /// <summary>
    /// Utility class for following edges using polylines.
    /// NOTE: We use PolylinePoints exclusively, NOT edge.Curve, because:
    /// - edge.Curve is a simplified straight-line Bezier approximation
    /// - edge.PolylinePoints is the actual S-curved path where walls are placed
    /// Using the Curve would cause visitors to walk through walls.
    /// </summary>
    public static class EdgeFollower
    {
        /// <summary>
        /// Gets the progress (0-1) along an edge closest to the given position.
        /// </summary>
        public static float GetProgress(PlanarForestMazeGenerator.Edge edge, Vector2 position)
        {
            // ALWAYS use polyline - it matches the actual path where walls are placed
            // edge.Curve is a simplified straight-line approximation that doesn't match
            if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
                return 0f;

            float bestT = 0f;
            float bestDist = float.MaxValue;
            float accumulatedLength = 0f;
            float totalLength = 0f;

            // First pass: calculate total length
            for (int i = 1; i < edge.PolylinePoints.Count; i++)
            {
                totalLength += Vector2.Distance(edge.PolylinePoints[i - 1], edge.PolylinePoints[i]);
            }

            if (totalLength < 0.001f)
                return 0f;

            // Second pass: find closest point
            for (int i = 1; i < edge.PolylinePoints.Count; i++)
            {
                Vector2 a = edge.PolylinePoints[i - 1];
                Vector2 b = edge.PolylinePoints[i];
                float segmentLength = Vector2.Distance(a, b);

                // Project point onto segment
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(position - a, ab) / ab.sqrMagnitude);
                Vector2 closest = a + ab * t;
                float dist = Vector2.Distance(position, closest);

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestT = (accumulatedLength + segmentLength * t) / totalLength;
                }

                accumulatedLength += segmentLength;
            }

            return bestT;
        }

        /// <summary>
        /// Gets the world position at a given progress (0-1) along the edge.
        /// </summary>
        public static Vector2 GetPosition(PlanarForestMazeGenerator.Edge edge, float progress)
        {
            // ALWAYS use polyline - it matches the actual path where walls are placed
            if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
                return edge.PolylinePoints?.Count > 0 ? edge.PolylinePoints[0] : Vector2.zero;

            progress = Mathf.Clamp01(progress);

            float totalLength = 0f;
            for (int i = 1; i < edge.PolylinePoints.Count; i++)
            {
                totalLength += Vector2.Distance(edge.PolylinePoints[i - 1], edge.PolylinePoints[i]);
            }

            float targetLength = totalLength * progress;
            float accumulatedLength = 0f;

            for (int i = 1; i < edge.PolylinePoints.Count; i++)
            {
                Vector2 a = edge.PolylinePoints[i - 1];
                Vector2 b = edge.PolylinePoints[i];
                float segmentLength = Vector2.Distance(a, b);

                if (accumulatedLength + segmentLength >= targetLength)
                {
                    float t = (targetLength - accumulatedLength) / segmentLength;
                    return Vector2.Lerp(a, b, t);
                }

                accumulatedLength += segmentLength;
            }

            return edge.PolylinePoints[edge.PolylinePoints.Count - 1];
        }

        /// <summary>
        /// Gets the movement direction (tangent) at a given progress along the edge.
        /// </summary>
        public static Vector2 GetDirection(PlanarForestMazeGenerator.Edge edge, float progress, bool reversed)
        {
            Vector2 tangent;

            // ALWAYS use polyline tangent approximation
            if (edge.PolylinePoints != null && edge.PolylinePoints.Count >= 2)
            {
                float delta = 0.01f;
                Vector2 p1 = GetPosition(edge, Mathf.Max(0f, progress - delta));
                Vector2 p2 = GetPosition(edge, Mathf.Min(1f, progress + delta));
                tangent = (p2 - p1).normalized;
            }
            else
            {
                tangent = Vector2.right;
            }

            return reversed ? -tangent : tangent;
        }

        /// <summary>
        /// Gets the normal (perpendicular to tangent) at a given progress.
        /// </summary>
        public static Vector2 GetNormal(PlanarForestMazeGenerator.Edge edge, float progress)
        {
            // Use polyline-based tangent to calculate normal
            Vector2 tangent = GetDirection(edge, progress, false);
            return new Vector2(-tangent.y, tangent.x);
        }

        /// <summary>
        /// Gets the lateral offset from the edge centerline.
        /// Positive = to the right of the curve direction, Negative = to the left.
        /// </summary>
        public static float GetLateralOffset(PlanarForestMazeGenerator.Edge edge, float progress, Vector2 position)
        {
            Vector2 centerPos = GetPosition(edge, progress);
            Vector2 normal = GetNormal(edge, progress);
            return Vector2.Dot(position - centerPos, normal);
        }
    }

    /// <summary>
    /// Utility class for traversing nodes along the walkable ring.
    /// </summary>
    public static class NodeTraverser
    {
        private const float WALKABLE_RING_RADIUS = 2.0f; // Middle of 1.0-3.0 range

        /// <summary>
        /// Generates arc waypoints through a node from entry to exit point.
        /// The arc follows the walkable ring (avoiding the unwalkable center).
        /// </summary>
        /// <param name="node">The node to traverse.</param>
        /// <param name="entry">Entry point on the node boundary.</param>
        /// <param name="exit">Exit point on the node boundary.</param>
        /// <param name="arcSegments">Number of intermediate waypoints (0 = direct line if valid).</param>
        /// <returns>List of waypoints including entry and exit.</returns>
        public static List<Vector2> GetNodeArc(
            PlanarForestMazeGenerator.Node node,
            Vector2 entry,
            Vector2 exit,
            int arcSegments = 4)
        {
            var points = new List<Vector2>();
            points.Add(entry);

            // Calculate angles from node center
            Vector2 entryOffset = entry - node.Position;
            Vector2 exitOffset = exit - node.Position;

            float entryAngle = Mathf.Atan2(entryOffset.y, entryOffset.x);
            float exitAngle = Mathf.Atan2(exitOffset.y, exitOffset.x);

            // Calculate shortest arc direction
            float angleDiff = exitAngle - entryAngle;

            // Normalize to -PI to PI
            while (angleDiff > Mathf.PI) angleDiff -= 2f * Mathf.PI;
            while (angleDiff < -Mathf.PI) angleDiff += 2f * Mathf.PI;

            // Check if direct path is valid (doesn't pass through unwalkable center)
            float directPathMinDist = DistanceFromPointToSegment(node.Position, entry, exit);

            if (directPathMinDist >= 1.0f && arcSegments == 0)
            {
                // Direct path is valid - skip arc
                points.Add(exit);
                return points;
            }

            // Generate arc points
            float absAngleDiff = Mathf.Abs(angleDiff);
            int segments = arcSegments > 0 ? arcSegments : Mathf.CeilToInt(absAngleDiff / (Mathf.PI / 4f));
            segments = Mathf.Max(1, segments);

            for (int i = 1; i < segments; i++)
            {
                float t = (float)i / segments;
                float angle = entryAngle + angleDiff * t;

                Vector2 arcPoint = node.Position + new Vector2(
                    Mathf.Cos(angle) * WALKABLE_RING_RADIUS,
                    Mathf.Sin(angle) * WALKABLE_RING_RADIUS
                );

                points.Add(arcPoint);
            }

            points.Add(exit);
            return points;
        }

        /// <summary>
        /// Gets the position on the walkable ring at a given angle from node center.
        /// </summary>
        public static Vector2 GetPositionAtAngle(PlanarForestMazeGenerator.Node node, float angleRadians)
        {
            return node.Position + new Vector2(
                Mathf.Cos(angleRadians) * WALKABLE_RING_RADIUS,
                Mathf.Sin(angleRadians) * WALKABLE_RING_RADIUS
            );
        }

        /// <summary>
        /// Calculates the arc length through a node between two angles.
        /// </summary>
        public static float GetArcLength(float entryAngle, float exitAngle)
        {
            float angleDiff = exitAngle - entryAngle;
            while (angleDiff > Mathf.PI) angleDiff -= 2f * Mathf.PI;
            while (angleDiff < -Mathf.PI) angleDiff += 2f * Mathf.PI;

            return Mathf.Abs(angleDiff) * WALKABLE_RING_RADIUS;
        }

        /// <summary>
        /// Checks if a position is in the walkable ring of a node.
        /// </summary>
        public static bool IsInWalkableRing(PlanarForestMazeGenerator.Node node, Vector2 position)
        {
            float dist = Vector2.Distance(position, node.Position);
            return dist >= 1.0f && dist <= 3.0f;
        }

        private static float DistanceFromPointToSegment(Vector2 point, Vector2 segStart, Vector2 segEnd)
        {
            Vector2 v = segEnd - segStart;
            Vector2 w = point - segStart;

            float c1 = Vector2.Dot(w, v);
            if (c1 <= 0)
                return Vector2.Distance(point, segStart);

            float c2 = Vector2.Dot(v, v);
            if (c2 <= c1)
                return Vector2.Distance(point, segEnd);

            float b = c1 / c2;
            Vector2 pb = segStart + b * v;
            return Vector2.Distance(point, pb);
        }
    }
}
