#!/usr/bin/env python3
"""
Demo script for the Planar Organic Growing Forest Map Generator

This script demonstrates the generator by:
1. Creating snapshots at turns 1, 5, and 20
2. Exporting state data as JSON
3. Running validation tests
"""

import json
import sys
from forest_map_generator import ForestMapGenerator
from forest_visualizer import ForestVisualizer, create_comparison_visualization


def run_snapshot_demo(seed: int = 42):
    """Generate snapshots at specific turns"""
    # Create generator
    gen = ForestMapGenerator(seed)
    gen.initialize()

    # Create visualizer
    viz = ForestVisualizer(gen)

    # Generate snapshots at turns 1, 5, 20
    turns = [1, 5, 20]

    for turn in turns:
        # Generate up to target turn
        while gen.turn_count < turn:
            success = gen.step()
            if not success:
                break

        # Save snapshot
        filename = f"forest_turn_{gen.turn_count}.png"
        viz.draw_map(
            title=f"Forest Map - Turn {gen.turn_count}",
            filename=filename,
            show=False
        )

        # Export state
        state = gen.get_state()
        json_filename = f"forest_turn_{gen.turn_count}.json"
        with open(json_filename, 'w') as f:
            json.dump(state, f, indent=2)

    return gen


def run_validation_tests(gen: ForestMapGenerator):
    """Run validation tests on the generated map"""

    passed = 0
    failed = 0

    # Test 1: Connectivity
    connectivity_ok = True
    for edge in gen.edges:
        if edge.is_complete():
            if edge.node_a is None or edge.node_b is None:
                connectivity_ok = False
        else:
            if edge.node_a is None:
                connectivity_ok = False
            if edge.ghost_center is None:
                connectivity_ok = False

    if connectivity_ok:
        passed += 1
    else:
        failed += 1

    # Test 2: Node spacing
    spacing_ok = True
    from forest_map_generator import R_KEEP, NODE_RADIUS

    for i, node_a in enumerate(gen.nodes):
        for node_b in gen.nodes[i + 1:]:
            dist = node_a.position.distance_to(node_b.position)

            # Check if they share an edge (parent-child relationship)
            share_edge = False
            for edge in gen.edges:
                if (edge.node_a == node_a.id and edge.node_b == node_b.id) or \
                   (edge.node_a == node_b.id and edge.node_b == node_a.id):
                    share_edge = True
                    break

            # Connected nodes need at least 2*NODE_RADIUS spacing (circles don't overlap)
            # Non-connected nodes need at least 2*R_KEEP spacing (keep-out zones don't overlap)
            if share_edge:
                min_dist = 2 * NODE_RADIUS
            else:
                min_dist = 2 * R_KEEP
            if dist < min_dist - 1e-6:
                spacing_ok = False

    if spacing_ok:
        passed += 1
    else:
        failed += 1

    # Test 3: Planarity (simplified check)
    planarity_ok = True

    from forest_map_generator import GeometryUtils, PATH_WIDTH

    for i, edge_a in enumerate(gen.edges):
        if len(edge_a.polyline_points) < 2:
            continue

        for edge_b in gen.edges[i + 1:]:
            if len(edge_b.polyline_points) < 2:
                continue

            # Check if edges share a node
            share_node = (edge_a.node_a == edge_b.node_a or
                         edge_a.node_a == edge_b.node_b or
                         edge_a.node_b == edge_b.node_a or
                         edge_a.node_b == edge_b.node_b)

            if not share_node:
                dist = GeometryUtils.polyline_to_polyline_distance(
                    edge_a.polyline_points,
                    edge_b.polyline_points,
                    share_endpoint=False
                )

                if dist < PATH_WIDTH - 1e-6:
                    planarity_ok = False

    if planarity_ok:
        passed += 1
    else:
        failed += 1

    # Test 4: Growth
    if len(gen.nodes) > 1:
        passed += 1
    else:
        failed += 1

    # Test 5: Node degree constraints
    degree_ok = True
    for node in gen.nodes:
        if len(node.incident_edges) > node.max_degree:
            degree_ok = False

    if degree_ok:
        passed += 1
    else:
        failed += 1

    return failed == 0


def run_center_bias_demo(seed: int = 42):
    """Demonstrate center-biased vs uniform selection"""
    create_comparison_visualization(seed, 20, "forest_comparison.png")



def analyze_center_bias_behavior(base_seed: int = 42, turns: int = 20, trials: int = 10):
    """Analyze and report on center bias behavior across multiple trials"""
    import forest_map_generator as fmg

    original_bias = fmg.CENTER_BIAS

    uniform_avg_dists = []
    biased_avg_dists = []

    for trial in range(trials):
        seed = base_seed + trial * 1000  # Use different seeds for each trial

        # Test with uniform selection
        fmg.CENTER_BIAS = 0.0
        gen_uniform = ForestMapGenerator(seed)
        gen_uniform.initialize()
        gen_uniform.generate(turns)

        root_pos = gen_uniform.nodes[0].position
        distances_uniform = [node.position.distance_to(root_pos)
                            for node in gen_uniform.nodes[1:]]  # Skip root
        if distances_uniform:
            uniform_avg_dists.append(sum(distances_uniform) / len(distances_uniform))

        # Test with center bias
        fmg.CENTER_BIAS = 0.75
        gen_biased = ForestMapGenerator(seed)
        gen_biased.initialize()
        gen_biased.generate(turns)

        root_pos = gen_biased.nodes[0].position
        distances_biased = [node.position.distance_to(root_pos)
                           for node in gen_biased.nodes[1:]]  # Skip root
        if distances_biased:
            biased_avg_dists.append(sum(distances_biased) / len(distances_biased))

    # Restore
    fmg.CENTER_BIAS = original_bias

    # Calculate overall statistics
    overall_uniform = sum(uniform_avg_dists) / len(uniform_avg_dists) if uniform_avg_dists else 0
    overall_biased = sum(biased_avg_dists) / len(biased_avg_dists) if biased_avg_dists else 0
    return overall_uniform, overall_biased



def main():
    """Main demo entry point"""
    seed = 42

    # Accept seed as command line argument
    if len(sys.argv) > 1:
        try:
            seed = int(sys.argv[1])
        except ValueError:
            sys.exit(1)

    # Run snapshot demo
    gen = run_snapshot_demo(seed)

    # Run validation tests
    all_passed = run_validation_tests(gen)

    # Run center bias analysis
    analyze_center_bias_behavior(seed, 20)

    # Create comparison visualization
    run_center_bias_demo(seed)

    if all_passed:
        return 0
    else:
        return 1


if __name__ == "__main__":
    sys.exit(main())
