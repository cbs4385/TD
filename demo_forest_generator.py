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
    print("=" * 60)
    print("Forest Map Generator - Snapshot Demo")
    print("=" * 60)
    print(f"Seed: {seed}")
    print()

    # Create generator
    gen = ForestMapGenerator(seed)
    gen.initialize()

    print(f"Initialized: {len(gen.nodes)} nodes, {len(gen.edges)} edges")
    print()

    # Create visualizer
    viz = ForestVisualizer(gen)

    # Generate snapshots at turns 1, 5, 20
    turns = [1, 5, 20]

    for turn in turns:
        # Generate up to target turn
        while gen.turn_count < turn:
            success = gen.step()
            if not success:
                print(f"Warning: Generation stopped at turn {gen.turn_count}")
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

        print(f"Turn {gen.turn_count}:")
        print(f"  Nodes: {len(gen.nodes)}")
        print(f"  Edges: {len(gen.edges)}")
        print(f"  Frontier: {len(gen.frontier)}")
        print(f"  Saved: {filename}, {json_filename}")
        print()

    print("Snapshot demo complete!")
    print()

    return gen


def run_validation_tests(gen: ForestMapGenerator):
    """Run validation tests on the generated map"""
    print("=" * 60)
    print("Validation Tests")
    print("=" * 60)

    passed = 0
    failed = 0

    # Test 1: Connectivity
    print("Test 1: Connectivity")
    connectivity_ok = True
    for edge in gen.edges:
        if edge.is_complete():
            if edge.node_a is None or edge.node_b is None:
                print(f"  FAIL: Complete edge {edge.id} missing node reference")
                connectivity_ok = False
        else:
            if edge.node_a is None:
                print(f"  FAIL: Partial edge {edge.id} missing node_a")
                connectivity_ok = False
            if edge.ghost_center is None:
                print(f"  FAIL: Partial edge {edge.id} missing ghost_center")
                connectivity_ok = False

    if connectivity_ok:
        print("  PASS: All edges properly connected")
        passed += 1
    else:
        failed += 1
    print()

    # Test 2: Node spacing
    print("Test 2: Node spacing")
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
                if share_edge:
                    print(f"  FAIL: Connected nodes {node_a.id} and {node_b.id} overlap: {dist:.2f} < {min_dist:.2f}")
                else:
                    print(f"  FAIL: Nodes {node_a.id} and {node_b.id} too close: {dist:.2f} < {min_dist:.2f}")
                spacing_ok = False

    if spacing_ok:
        print(f"  PASS: All node pairs properly spaced")
        passed += 1
    else:
        failed += 1
    print()

    # Test 3: Planarity (simplified check)
    print("Test 3: Planarity (simplified)")
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
                    print(f"  FAIL: Edges {edge_a.id} and {edge_b.id} too close: {dist:.3f} < {PATH_WIDTH}")
                    planarity_ok = False

    if planarity_ok:
        print(f"  PASS: No edge collisions (min {PATH_WIDTH})")
        passed += 1
    else:
        failed += 1
    print()

    # Test 4: Growth
    print("Test 4: Growth")
    if len(gen.nodes) > 1:
        print(f"  PASS: Map grew to {len(gen.nodes)} nodes")
        passed += 1
    else:
        print(f"  FAIL: Map did not grow")
        failed += 1
    print()

    # Test 5: Node degree constraints
    print("Test 5: Node degree constraints")
    degree_ok = True
    for node in gen.nodes:
        if len(node.incident_edges) > node.max_degree:
            print(f"  FAIL: Node {node.id} exceeds max degree: {len(node.incident_edges)} > {node.max_degree}")
            degree_ok = False

    if degree_ok:
        print("  PASS: All nodes respect degree constraints")
        passed += 1
    else:
        failed += 1
    print()

    # Summary
    print("=" * 60)
    print(f"Results: {passed} passed, {failed} failed")
    print("=" * 60)
    print()

    return failed == 0


def run_center_bias_demo(seed: int = 42):
    """Demonstrate center-biased vs uniform selection"""
    print("=" * 60)
    print("Center Bias Comparison Demo")
    print("=" * 60)
    print()

    create_comparison_visualization(seed, 20, "forest_comparison.png")

    print("Comparison complete!")
    print()


def analyze_center_bias_behavior(base_seed: int = 42, turns: int = 20, trials: int = 10):
    """Analyze and report on center bias behavior across multiple trials"""
    print("=" * 60)
    print("Center Bias Analysis")
    print("=" * 60)
    print()

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

    print(f"Tested {trials} trials with {turns} turns each")
    print()
    print(f"Uniform selection (bias=0.0):")
    print(f"  Average distance from root: {overall_uniform:.2f}")
    print()

    print(f"Center-biased selection (bias=0.75):")
    print(f"  Average distance from root: {overall_biased:.2f}")
    print()

    if overall_biased < overall_uniform:
        reduction_pct = ((overall_uniform - overall_biased) / overall_uniform) * 100
        print("✓ Center bias successfully reduces average spawn distance")
        print(f"  Reduction: {overall_uniform - overall_biased:.2f} units ({reduction_pct:.1f}%)")
    else:
        print("✗ Center bias did not reduce average spawn distance")

    print()


def main():
    """Main demo entry point"""
    seed = 42

    # Accept seed as command line argument
    if len(sys.argv) > 1:
        try:
            seed = int(sys.argv[1])
        except ValueError:
            print(f"Invalid seed: {sys.argv[1]}")
            sys.exit(1)

    print()
    print("╔" + "═" * 58 + "╗")
    print("║" + " " * 10 + "PLANAR ORGANIC FOREST MAP GENERATOR" + " " * 13 + "║")
    print("╚" + "═" * 58 + "╝")
    print()

    # Run snapshot demo
    gen = run_snapshot_demo(seed)

    # Run validation tests
    all_passed = run_validation_tests(gen)

    # Run center bias analysis
    analyze_center_bias_behavior(seed, 20)

    # Create comparison visualization
    run_center_bias_demo(seed)

    # Final summary
    print("=" * 60)
    print("Demo Complete!")
    print("=" * 60)
    print()
    print("Generated files:")
    print("  - forest_turn_1.png, forest_turn_1.json")
    print("  - forest_turn_5.png, forest_turn_5.json")
    print("  - forest_turn_20.png, forest_turn_20.json")
    print("  - forest_comparison.png")
    print()

    if all_passed:
        print("✓ All validation tests passed!")
        return 0
    else:
        print("✗ Some validation tests failed")
        return 1


if __name__ == "__main__":
    sys.exit(main())
