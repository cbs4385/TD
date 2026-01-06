"""
Planar Organic Growing Forest Map Generator

This module implements a procedural generator that grows a planar, organic forest map
where nodes represent clearings (circles) and edges represent paths (curved corridors).

Key Parameters:
- NODE_RADIUS = 3.0 (clearing radius)
- NODE_PADDING = 1.0 (safety padding)
- R_KEEP = 4.0 (effective keep-out radius)
- PATH_WIDTH = 1.0 (corridor width)
- PATH_RADIUS = 0.5 (corridor radius for collision detection)

Tuning Parameters:
- centerBias: [0..1] Controls how much to favor spawning near the root (recommended 0.6-0.85)
  0 = uniform random, 1 = maximum bias toward center
- biasPower: [1.5..3.0] Controls falloff rate of distance weighting
  Higher values = stronger preference for close nodes
- biasFloor: [0.05..0.15] Minimum weight for far nodes (prevents starvation)
- angleMinSeparation: Minimum degrees between edges at a node (recommended 25°)
- curveStrength: Controls how curved the paths are (recommended 0.3-0.6)
- rotateStep: Angle increment when searching for valid placement (recommended 6-10°)
- shortenStep: Length decrement when searching for valid placement (recommended 0.3-0.5)
"""

import math
import random
from typing import List, Tuple, Optional, Set
from dataclasses import dataclass, field
import json

# Constants
NODE_RADIUS = 3.0
NODE_PADDING = 0.7  # Reduced from 1.0 to allow tighter packing
R_KEEP = NODE_RADIUS + NODE_PADDING  # 3.7
PATH_WIDTH = 1.0
PATH_RADIUS = PATH_WIDTH / 2.0  # 0.5

# Tunable parameters
CONNECT_PROB = 0.25
CENTER_BIAS = 0.75
BIAS_POWER = 2.0
BIAS_FLOOR = 0.1
ANGLE_MIN_SEPARATION = 20.0  # degrees (reduced from 25 to allow more placement options)
CURVE_STRENGTH = 0.25  # Reduced from 0.4 to make paths straighter and more reliable
ROTATE_STEP = 6.0  # degrees (reduced from 8 to try more angles)
SHORTEN_STEP = 0.3  # (reduced from 0.4 to try more lengths)
MIN_CORRIDOR_LENGTH = 0.3  # (reduced from 0.5 to allow shorter corridors)


@dataclass
class Vector2:
    """2D vector with basic operations"""
    x: float
    y: float

    def __add__(self, other):
        return Vector2(self.x + other.x, self.y + other.y)

    def __sub__(self, other):
        return Vector2(self.x - other.x, self.y - other.y)

    def __mul__(self, scalar):
        return Vector2(self.x * scalar, self.y * scalar)

    def __rmul__(self, scalar):
        return self * scalar

    def length(self) -> float:
        return math.sqrt(self.x * self.x + self.y * self.y)

    def normalize(self):
        l = self.length()
        if l < 1e-9:
            return Vector2(0, 0)
        return Vector2(self.x / l, self.y / l)

    def distance_to(self, other) -> float:
        return (self - other).length()

    def dot(self, other) -> float:
        return self.x * other.x + self.y * other.y

    def perpendicular(self):
        """Return perpendicular vector (90° rotation)"""
        return Vector2(-self.y, self.x)

    def to_tuple(self) -> Tuple[float, float]:
        return (self.x, self.y)


@dataclass
class Node:
    """Represents a clearing in the forest"""
    id: int
    position: Vector2
    kind: str  # "root" or "normal"
    max_degree: int
    incident_edges: List[int] = field(default_factory=list)
    used_angles: List[float] = field(default_factory=list)  # in radians

    def has_capacity(self) -> bool:
        return len(self.incident_edges) < self.max_degree

    def add_edge(self, edge_id: int, angle: float):
        self.incident_edges.append(edge_id)
        self.used_angles.append(angle)


@dataclass
class Edge:
    """Represents a path between clearings"""
    id: int
    node_a: int
    node_b: Optional[int]  # None if partial (open endpoint)
    polyline_points: List[Vector2] = field(default_factory=list)
    partial: bool = True
    ghost_center: Optional[Vector2] = None  # Reserved position for future node

    def is_complete(self) -> bool:
        return not self.partial and self.node_b is not None


class GeometryUtils:
    """Geometric utility functions for collision detection"""

    @staticmethod
    def point_to_segment_distance(p: Vector2, a: Vector2, b: Vector2) -> float:
        """Minimum distance from point p to line segment ab"""
        ab = b - a
        ap = p - a

        if ab.length() < 1e-9:
            return ap.length()

        t = ap.dot(ab) / ab.dot(ab)
        t = max(0.0, min(1.0, t))  # Clamp to [0, 1]

        closest = a + ab * t
        return p.distance_to(closest)

    @staticmethod
    def segment_to_segment_distance(a1: Vector2, a2: Vector2, b1: Vector2, b2: Vector2) -> float:
        """Minimum distance between two line segments"""
        # Check all combinations of endpoint to segment
        dists = [
            GeometryUtils.point_to_segment_distance(a1, b1, b2),
            GeometryUtils.point_to_segment_distance(a2, b1, b2),
            GeometryUtils.point_to_segment_distance(b1, a1, a2),
            GeometryUtils.point_to_segment_distance(b2, a1, a2),
        ]

        # Check if segments intersect
        if GeometryUtils.segments_intersect(a1, a2, b1, b2):
            return 0.0

        return min(dists)

    @staticmethod
    def segments_intersect(a1: Vector2, a2: Vector2, b1: Vector2, b2: Vector2) -> bool:
        """Check if two line segments intersect"""
        def ccw(A: Vector2, B: Vector2, C: Vector2) -> bool:
            return (C.y - A.y) * (B.x - A.x) > (B.y - A.y) * (C.x - A.x)

        return (ccw(a1, b1, b2) != ccw(a2, b1, b2) and
                ccw(a1, a2, b1) != ccw(a1, a2, b2))

    @staticmethod
    def polyline_to_node_distance(polyline: List[Vector2], center: Vector2,
                                   is_incident: bool = False) -> float:
        """
        Minimum distance from a polyline to a node.
        If is_incident, only check middle segments (allow endpoint overlap).
        """
        if len(polyline) < 2:
            return float('inf')

        min_dist = float('inf')
        start_idx = 1 if is_incident else 0
        end_idx = len(polyline) - 2 if is_incident else len(polyline) - 1

        for i in range(start_idx, end_idx):
            dist = GeometryUtils.point_to_segment_distance(center, polyline[i], polyline[i + 1])
            min_dist = min(min_dist, dist)

        return min_dist

    @staticmethod
    def polyline_to_polyline_distance(poly1: List[Vector2], poly2: List[Vector2],
                                      share_endpoint: bool = False) -> float:
        """
        Minimum distance between two polylines.
        If share_endpoint, exclude first/last segments from checks.
        """
        if len(poly1) < 2 or len(poly2) < 2:
            return float('inf')

        min_dist = float('inf')

        # Determine segment ranges to check
        start1 = 1 if share_endpoint else 0
        end1 = len(poly1) - 2 if share_endpoint else len(poly1) - 1
        start2 = 1 if share_endpoint else 0
        end2 = len(poly2) - 2 if share_endpoint else len(poly2) - 1

        for i in range(start1, end1):
            for j in range(start2, end2):
                dist = GeometryUtils.segment_to_segment_distance(
                    poly1[i], poly1[i + 1],
                    poly2[j], poly2[j + 1]
                )
                min_dist = min(min_dist, dist)

        return min_dist


class ForestMapGenerator:
    """Main generator class for the organic forest map"""

    def __init__(self, seed: int = 42):
        self.seed = seed
        random.seed(seed)

        self.nodes: List[Node] = []
        self.edges: List[Edge] = []
        self.frontier: List[int] = []  # List of partial edge IDs
        self.ghost_centers: List[Vector2] = []  # List of reserved positions

        self.next_node_id = 0
        self.next_edge_id = 0
        self.turn_count = 0

    def initialize(self):
        """Initialize the map with root node and first normal node"""
        # Create root node at origin
        root = Node(
            id=self.next_node_id,
            position=Vector2(0, 0),
            kind="root",
            max_degree=1
        )
        self.nodes.append(root)
        self.next_node_id += 1

        # Create first normal node
        angle = random.uniform(0, 2 * math.pi)
        length = random.uniform(1.0, 5.0)
        distance = 2 * NODE_RADIUS + length

        node1_pos = Vector2(
            math.cos(angle) * distance,
            math.sin(angle) * distance
        )

        max_degree = random.randint(1, 4)
        node1 = Node(
            id=self.next_node_id,
            position=node1_pos,
            kind="normal",
            max_degree=max_degree
        )
        self.nodes.append(node1)
        self.next_node_id += 1

        # Create edge between root and node1
        # For the very first edge, create it directly with a straight line
        direction = (node1_pos - root.position).normalize()
        start_boundary = root.position + direction * NODE_RADIUS
        end_boundary = node1_pos - direction * NODE_RADIUS

        edge = Edge(
            id=self.next_edge_id,
            node_a=root.id,
            node_b=node1.id,
            polyline_points=[start_boundary, end_boundary],  # Straight line
            partial=False,
            ghost_center=None
        )
        self.next_edge_id += 1

        self.edges.append(edge)
        root.add_edge(edge.id, angle)
        reverse_angle = (angle + math.pi) % (2 * math.pi)
        node1.add_edge(edge.id, reverse_angle)

        # Fill node1's remaining capacity with partial edges
        while node1.has_capacity():
            self._add_partial_edge(node1)

    def _create_edge(self, node_a_id: int, node_b_id: int) -> Optional[Edge]:
        """Create a complete edge between two nodes"""
        node_a = self.nodes[node_a_id]
        node_b = self.nodes[node_b_id]

        # Build curved polyline
        polyline = self._build_curved_polyline(node_a.position, node_b.position,
                                               incident_nodes=[node_a_id, node_b_id])

        if polyline is None:
            return None

        edge = Edge(
            id=self.next_edge_id,
            node_a=node_a_id,
            node_b=node_b_id,
            polyline_points=polyline,
            partial=False,
            ghost_center=None
        )
        self.next_edge_id += 1

        return edge

    def _build_curved_polyline(self, start_center: Vector2, end_center: Vector2,
                               for_partial: bool = False,
                               ghost_pos: Optional[Vector2] = None,
                               incident_nodes: Optional[List[int]] = None) -> Optional[List[Vector2]]:
        """
        Build a curved polyline from start to end node boundaries.
        Returns None if no valid curve found.
        """
        direction = (end_center - start_center).normalize()

        # Calculate boundary points
        start_boundary = start_center + direction * NODE_RADIUS
        end_boundary = end_center - direction * NODE_RADIUS

        chord_length = start_boundary.distance_to(end_boundary)
        kmax = CURVE_STRENGTH * chord_length

        # Try different curve strengths
        k_values = [
            0.0,
            0.25 * kmax, -0.25 * kmax,
            0.45 * kmax, -0.45 * kmax,
            0.65 * kmax, -0.65 * kmax,
            0.85 * kmax, -0.85 * kmax,
            1.0 * kmax, -1.0 * kmax
        ]

        perp = direction.perpendicular()

        for k in k_values:
            # Create S-curve with 4 points
            control1 = start_boundary + (end_boundary - start_boundary) * 0.33 + perp * k
            control2 = start_boundary + (end_boundary - start_boundary) * 0.66 - perp * (0.7 * k)

            polyline = [start_boundary, control1, control2, end_boundary]

            # Check if this polyline is valid
            if self._is_polyline_valid(polyline, for_partial, ghost_pos, incident_nodes):
                return polyline

        return None

    def _is_polyline_valid(self, polyline: List[Vector2], for_partial: bool = False,
                          ghost_pos: Optional[Vector2] = None,
                          incident_nodes: Optional[List[int]] = None) -> bool:
        """Check if a polyline passes all collision tests"""
        if len(polyline) < 2:
            return False

        if incident_nodes is None:
            incident_nodes = []

        # Check against all existing nodes (except incident ones)
        for node in self.nodes:
            # Skip incident nodes - they're allowed to be close
            is_incident = node.id in incident_nodes

            dist = GeometryUtils.polyline_to_node_distance(
                polyline, node.position, is_incident=is_incident
            )
            # For incident nodes, only check they don't penetrate the circle
            # For non-incident nodes, use relaxed keep-out distance
            if is_incident:
                min_required = NODE_RADIUS + PATH_RADIUS
            else:
                min_required = R_KEEP + PATH_RADIUS - 0.4

            if dist < min_required - 1e-6:
                return False

        # Check against ghost centers
        for ghost in self.ghost_centers:
            if ghost_pos and ghost.distance_to(ghost_pos) < 1e-6:
                continue  # Skip self

            dist = GeometryUtils.polyline_to_node_distance(
                polyline, ghost, is_incident=False
            )
            min_required = R_KEEP + PATH_RADIUS - 0.4
            if dist < min_required - 1e-6:
                return False

        # Check against all existing edges
        for edge in self.edges:
            if len(edge.polyline_points) < 2:
                continue

            dist = GeometryUtils.polyline_to_polyline_distance(
                polyline, edge.polyline_points, share_endpoint=False
            )
            # Use slightly relaxed constraint for edge-to-edge
            min_required = PATH_WIDTH * 0.8
            if dist < min_required - 1e-6:
                return False

        return True

    def _add_partial_edge(self, node: Node) -> bool:
        """
        Add a partial edge (open endpoint) to a node using rotate/shorten solver.
        Returns True if successful.
        """
        if not node.has_capacity():
            return False

        # Initial random angle and length
        theta0 = random.uniform(0, 2 * math.pi)
        length0 = random.uniform(1.0, 5.0)

        # Try different angles in order of smallest deviation
        max_rotations = int(180 / ROTATE_STEP)

        for rot_step in range(max_rotations):
            # Alternate positive and negative rotations
            if rot_step == 0:
                theta = theta0
            elif rot_step % 2 == 1:
                theta = theta0 + (rot_step // 2 + 1) * math.radians(ROTATE_STEP)
            else:
                theta = theta0 - (rot_step // 2) * math.radians(ROTATE_STEP)

            theta = theta % (2 * math.pi)

            # Check angle separation from existing edges
            if not self._is_angle_valid(node, theta):
                continue

            # Try different lengths
            length = length0
            while length >= MIN_CORRIDOR_LENGTH:
                # Calculate ghost center position
                direction = Vector2(math.cos(theta), math.sin(theta))
                ghost_center = node.position + direction * (2 * NODE_RADIUS + length)

                # Check if ghost center collides with existing nodes/ghosts
                if not self._is_ghost_position_valid(ghost_center):
                    length -= SHORTEN_STEP
                    continue

                # Build polyline from node boundary to ghost boundary
                node_boundary = node.position + direction * NODE_RADIUS
                ghost_boundary = ghost_center - direction * NODE_RADIUS

                # Create simple polyline for partial edge
                chord_length = node_boundary.distance_to(ghost_boundary)
                kmax = CURVE_STRENGTH * chord_length

                # Try a few curve options
                perp = direction.perpendicular()
                for k_factor in [0.0, 0.5, -0.5, 1.0, -1.0]:
                    k = k_factor * kmax
                    control1 = node_boundary + (ghost_boundary - node_boundary) * 0.33 + perp * k
                    control2 = node_boundary + (ghost_boundary - node_boundary) * 0.66 - perp * (0.7 * k)

                    polyline = [node_boundary, control1, control2, ghost_boundary]

                    if self._is_polyline_valid(polyline, for_partial=True, ghost_pos=ghost_center,
                                              incident_nodes=[node.id]):
                        # Success! Create the partial edge
                        edge = Edge(
                            id=self.next_edge_id,
                            node_a=node.id,
                            node_b=None,
                            polyline_points=polyline,
                            partial=True,
                            ghost_center=ghost_center
                        )
                        self.next_edge_id += 1

                        self.edges.append(edge)
                        self.frontier.append(edge.id)
                        self.ghost_centers.append(ghost_center)
                        node.add_edge(edge.id, theta)

                        return True

                length -= SHORTEN_STEP

        return False

    def _is_angle_valid(self, node: Node, angle: float) -> bool:
        """Check if angle has sufficient separation from existing edges"""
        for used_angle in node.used_angles:
            diff = abs(angle - used_angle)
            # Handle wrap-around
            diff = min(diff, 2 * math.pi - diff)
            if diff < math.radians(ANGLE_MIN_SEPARATION):
                return False
        return True

    def _is_ghost_position_valid(self, ghost_pos: Vector2) -> bool:
        """Check if a ghost center position is valid"""
        # Check against all existing nodes
        for node in self.nodes:
            if node.position.distance_to(ghost_pos) < 2 * R_KEEP - 1e-6:
                return False

        # Check against all existing ghosts
        for ghost in self.ghost_centers:
            if ghost.distance_to(ghost_pos) < 2 * R_KEEP - 1e-6:
                return False

        return True

    def _select_frontier_edge_biased(self) -> Optional[int]:
        """Select a frontier edge using center-biased weighting"""
        if not self.frontier:
            return None

        root_pos = self.nodes[0].position
        weights = []

        for edge_id in self.frontier:
            edge = self.edges[edge_id]
            if edge.ghost_center is None:
                weights.append(0.0)
                continue

            dist = edge.ghost_center.distance_to(root_pos)

            # Calculate biased weight
            base = 1.0 / ((dist + 1e-6) ** BIAS_POWER)
            weight = (1.0 - CENTER_BIAS) + CENTER_BIAS * base
            weight = max(weight, BIAS_FLOOR)

            weights.append(weight)

        # Weighted random selection
        total_weight = sum(weights)
        if total_weight < 1e-9:
            return random.choice(self.frontier)

        r = random.uniform(0, total_weight)
        cumulative = 0.0

        for i, weight in enumerate(weights):
            cumulative += weight
            if r <= cumulative:
                return self.frontier[i]

        return self.frontier[-1]

    def step(self) -> bool:
        """
        Perform one growth turn.
        Returns True if successful, False if no growth possible.
        """
        # Select a frontier edge
        edge_id = self._select_frontier_edge_biased()
        if edge_id is None:
            return False

        edge = self.edges[edge_id]
        if edge.ghost_center is None:
            return False

        # Create new node at ghost position
        max_degree = random.randint(1, 4)
        new_node = Node(
            id=self.next_node_id,
            position=edge.ghost_center,
            kind="normal",
            max_degree=max_degree
        )
        self.nodes.append(new_node)
        self.next_node_id += 1

        # Convert partial edge to complete
        edge.node_b = new_node.id
        edge.partial = False

        # Calculate reverse angle for new node
        node_a = self.nodes[edge.node_a]
        direction = (new_node.position - node_a.position).normalize()
        reverse_angle = math.atan2(-direction.y, -direction.x)
        reverse_angle = (reverse_angle + 2 * math.pi) % (2 * math.pi)

        new_node.add_edge(edge_id, reverse_angle)

        # Remove from frontier and ghost list
        self.frontier.remove(edge_id)
        self.ghost_centers = [g for g in self.ghost_centers
                             if g.distance_to(edge.ghost_center) > 1e-6]
        edge.ghost_center = None

        # Fill new node's remaining capacity
        while new_node.has_capacity():
            # Try to connect to existing node with probability CONNECT_PROB
            if random.random() < CONNECT_PROB:
                if self._try_connect_to_existing(new_node):
                    continue

            # Otherwise add partial edge
            if not self._add_partial_edge(new_node):
                break  # Can't add more edges

        self.turn_count += 1
        return True

    def _try_connect_to_existing(self, new_node: Node) -> bool:
        """Try to connect new_node to an existing node"""
        # Find candidate nodes
        candidates = []
        for node in self.nodes:
            if node.id == new_node.id:
                continue
            if not node.has_capacity():
                continue
            # Check if already connected
            if any(e for e in self.edges
                   if (e.node_a == new_node.id and e.node_b == node.id) or
                      (e.node_a == node.id and e.node_b == new_node.id)):
                continue

            candidates.append(node)

        if not candidates:
            return False

        # Shuffle and try each candidate
        random.shuffle(candidates)

        for candidate in candidates:
            # Calculate angle from new_node to candidate
            direction = (candidate.position - new_node.position).normalize()
            angle = math.atan2(direction.y, direction.x)
            angle = (angle + 2 * math.pi) % (2 * math.pi)

            if not self._is_angle_valid(new_node, angle):
                continue

            # Calculate reverse angle
            reverse_angle = (angle + math.pi) % (2 * math.pi)
            if not self._is_angle_valid(candidate, reverse_angle):
                continue

            # Try to build edge
            polyline = self._build_curved_polyline(new_node.position, candidate.position,
                                                   incident_nodes=[new_node.id, candidate.id])
            if polyline is None:
                continue

            # Create edge
            edge = Edge(
                id=self.next_edge_id,
                node_a=new_node.id,
                node_b=candidate.id,
                polyline_points=polyline,
                partial=False,
                ghost_center=None
            )
            self.next_edge_id += 1

            self.edges.append(edge)
            new_node.add_edge(edge.id, angle)
            candidate.add_edge(edge.id, reverse_angle)

            return True

        return False

    def generate(self, turns: int) -> bool:
        """
        Run the generator for a specified number of turns.
        Returns True if all turns completed successfully.
        """
        for _ in range(turns):
            if not self.step():
                return False
        return True

    def get_state(self) -> dict:
        """Export current state as a dictionary"""
        return {
            'turn': self.turn_count,
            'nodes': [
                {
                    'id': n.id,
                    'position': n.position.to_tuple(),
                    'kind': n.kind,
                    'max_degree': n.max_degree,
                    'incident_edges': n.incident_edges
                }
                for n in self.nodes
            ],
            'edges': [
                {
                    'id': e.id,
                    'node_a': e.node_a,
                    'node_b': e.node_b,
                    'partial': e.partial,
                    'polyline': [p.to_tuple() for p in e.polyline_points],
                    'ghost_center': e.ghost_center.to_tuple() if e.ghost_center else None
                }
                for e in self.edges
            ],
            'frontier_count': len(self.frontier)
        }
