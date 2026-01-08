"""
Visualization utilities for the Forest Map Generator

Provides functions to draw and save forest map snapshots showing:
- Node circles (clearings) with padding rings
- Ghost node reservations (dotted rings)
- Path corridors (curved polylines)
"""

import matplotlib.pyplot as plt
import matplotlib.patches as patches
from matplotlib.collections import LineCollection
import math
from forest_map_generator import ForestMapGenerator, NODE_RADIUS, R_KEEP, PATH_WIDTH


class ForestVisualizer:
    """Visualizer for forest maps"""

    def __init__(self, generator: ForestMapGenerator):
        self.generator = generator

    def draw_map(self, title: str = "Forest Map", filename: str = None, show: bool = False):
        """
        Draw the current state of the forest map.

        Args:
            title: Title for the plot
            filename: If provided, save to this file
            show: If True, display the plot
        """
        fig, ax = plt.subplots(figsize=(12, 12))
        ax.set_aspect('equal')
        ax.grid(True, alpha=0.3)
        ax.set_title(title)

        # Draw all edges (corridors)
        for edge in self.generator.edges:
            self._draw_edge(ax, edge)

        # Draw all nodes (clearings)
        for node in self.generator.nodes:
            self._draw_node(ax, node)

        # Draw ghost centers
        for ghost in self.generator.ghost_centers:
            self._draw_ghost(ax, ghost)

        # Calculate bounds
        all_x = [n.position.x for n in self.generator.nodes]
        all_y = [n.position.y for n in self.generator.nodes]

        if all_x and all_y:
            margin = 20
            ax.set_xlim(min(all_x) - margin, max(all_x) + margin)
            ax.set_ylim(min(all_y) - margin, max(all_y) + margin)

        # Add legend
        legend_elements = [
            patches.Circle((0, 0), 1, facecolor='lightgreen', edgecolor='darkgreen',
                          linewidth=2, label='Clearing (radius 3)'),
            patches.Circle((0, 0), 1, facecolor='none', edgecolor='orange',
                          linewidth=1, linestyle='--', label='Padding (radius 4)'),
            patches.Circle((0, 0), 1, facecolor='none', edgecolor='gray',
                          linewidth=1, linestyle=':', label='Ghost reservation'),
            plt.Line2D([0], [0], color='brown', linewidth=3, label='Path (width 1)'),
        ]
        ax.legend(handles=legend_elements, loc='upper right')

        # Add info text
        info_text = (
            f"Turn: {self.generator.turn_count}\n"
            f"Nodes: {len(self.generator.nodes)}\n"
            f"Edges: {len(self.generator.edges)}\n"
            f"Frontier: {len(self.generator.frontier)}"
        )
        ax.text(0.02, 0.98, info_text, transform=ax.transAxes,
                verticalalignment='top', bbox=dict(boxstyle='round', facecolor='wheat', alpha=0.5))

        if filename:
            plt.savefig(filename, dpi=150, bbox_inches='tight')

        if show:
            plt.show()
        else:
            plt.close(fig)

    def _draw_node(self, ax, node):
        """Draw a node with its boundary and padding"""
        x, y = node.position.x, node.position.y

        # Draw padding ring (radius 4)
        padding_circle = patches.Circle(
            (x, y), R_KEEP,
            facecolor='none',
            edgecolor='orange',
            linewidth=1,
            linestyle='--',
            alpha=0.5
        )
        ax.add_patch(padding_circle)

        # Draw node circle (radius 3)
        color = 'lightblue' if node.kind == 'root' else 'lightgreen'
        edge_color = 'darkblue' if node.kind == 'root' else 'darkgreen'

        node_circle = patches.Circle(
            (x, y), NODE_RADIUS,
            facecolor=color,
            edgecolor=edge_color,
            linewidth=2,
            alpha=0.7
        )
        ax.add_patch(node_circle)

        # Draw node ID
        ax.text(x, y, str(node.id), ha='center', va='center',
                fontsize=10, fontweight='bold')

    def _draw_ghost(self, ax, ghost):
        """Draw a ghost node reservation"""
        x, y = ghost.x, ghost.y

        # Draw ghost circle with dotted line
        ghost_circle = patches.Circle(
            (x, y), R_KEEP,
            facecolor='none',
            edgecolor='gray',
            linewidth=1,
            linestyle=':',
            alpha=0.6
        )
        ax.add_patch(ghost_circle)

        # Small center marker
        ax.plot(x, y, 'x', color='gray', markersize=8, alpha=0.6)

    def _draw_edge(self, ax, edge):
        """Draw an edge as a curved polyline"""
        if len(edge.polyline_points) < 2:
            return

        # Extract x, y coordinates
        xs = [p.x for p in edge.polyline_points]
        ys = [p.y for p in edge.polyline_points]

        # Draw polyline
        color = 'brown' if edge.is_complete() else 'gray'
        linestyle = '-' if edge.is_complete() else '--'
        alpha = 0.8 if edge.is_complete() else 0.5

        ax.plot(xs, ys, color=color, linewidth=3, linestyle=linestyle,
                alpha=alpha, solid_capstyle='round')

        # Draw control points (for debugging)
        # ax.plot(xs, ys, 'o', color='red', markersize=3, alpha=0.3)

    def create_snapshot_series(self, turns_list: list, base_filename: str = "forest_turn"):
        """
        Generate and save snapshots at specific turn counts.

        Args:
            turns_list: List of turn numbers to capture (e.g., [1, 5, 20])
            base_filename: Base name for output files
        """
        current_turn = self.generator.turn_count

        for target_turn in sorted(turns_list):
            # Generate up to target turn
            while self.generator.turn_count < target_turn:
                if not self.generator.step():
                    break

            # Save snapshot
            filename = f"{base_filename}_{self.generator.turn_count}.png"
            self.draw_map(
                title=f"Forest Map - Turn {self.generator.turn_count}",
                filename=filename,
                show=False
            )


def create_comparison_visualization(seed: int, turns: int, output_file: str = "comparison.png"):
    """
    Create a side-by-side comparison of uniform vs center-biased growth.

    Args:
        seed: Random seed for reproducibility
        turns: Number of turns to generate
        output_file: Output filename
    """
    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(20, 10))

    # Generate with uniform selection (bias = 0)
    from forest_map_generator import CENTER_BIAS, BIAS_POWER, BIAS_FLOOR
    import forest_map_generator as fmg

    # Uniform
    original_bias = fmg.CENTER_BIAS
    fmg.CENTER_BIAS = 0.0

    gen1 = ForestMapGenerator(seed)
    gen1.initialize()
    gen1.generate(turns)

    # Center-biased
    fmg.CENTER_BIAS = 0.75

    gen2 = ForestMapGenerator(seed)
    gen2.initialize()
    gen2.generate(turns)

    # Restore original
    fmg.CENTER_BIAS = original_bias

    # Draw both
    _draw_on_axis(ax1, gen1, "Uniform Selection (bias=0.0)")
    _draw_on_axis(ax2, gen2, "Center-Biased Selection (bias=0.75)")

    plt.tight_layout()
    plt.savefig(output_file, dpi=150, bbox_inches='tight')
    plt.close(fig)


def _draw_on_axis(ax, generator: ForestMapGenerator, title: str):
    """Helper to draw generator state on a specific axis"""
    ax.set_aspect('equal')
    ax.grid(True, alpha=0.3)
    ax.set_title(title)

    # Draw edges
    for edge in generator.edges:
        if len(edge.polyline_points) < 2:
            continue

        xs = [p.x for p in edge.polyline_points]
        ys = [p.y for p in edge.polyline_points]

        color = 'brown' if edge.is_complete() else 'gray'
        linestyle = '-' if edge.is_complete() else '--'
        alpha = 0.8 if edge.is_complete() else 0.5

        ax.plot(xs, ys, color=color, linewidth=3, linestyle=linestyle,
                alpha=alpha, solid_capstyle='round')

    # Draw nodes
    for node in generator.nodes:
        x, y = node.position.x, node.position.y

        # Padding ring
        padding_circle = patches.Circle(
            (x, y), R_KEEP,
            facecolor='none',
            edgecolor='orange',
            linewidth=1,
            linestyle='--',
            alpha=0.3
        )
        ax.add_patch(padding_circle)

        # Node circle
        color = 'lightblue' if node.kind == 'root' else 'lightgreen'
        edge_color = 'darkblue' if node.kind == 'root' else 'darkgreen'

        node_circle = patches.Circle(
            (x, y), NODE_RADIUS,
            facecolor=color,
            edgecolor=edge_color,
            linewidth=2,
            alpha=0.7
        )
        ax.add_patch(node_circle)

        ax.text(x, y, str(node.id), ha='center', va='center',
                fontsize=8, fontweight='bold')

    # Draw ghosts
    for ghost in generator.ghost_centers:
        x, y = ghost.x, ghost.y
        ghost_circle = patches.Circle(
            (x, y), R_KEEP,
            facecolor='none',
            edgecolor='gray',
            linewidth=1,
            linestyle=':',
            alpha=0.4
        )
        ax.add_patch(ghost_circle)

    # Set bounds
    all_x = [n.position.x for n in generator.nodes]
    all_y = [n.position.y for n in generator.nodes]

    if all_x and all_y:
        margin = 15
        ax.set_xlim(min(all_x) - margin, max(all_x) + margin)
        ax.set_ylim(min(all_y) - margin, max(all_y) + margin)

    # Add info
    info_text = (
        f"Turn: {generator.turn_count}\n"
        f"Nodes: {len(generator.nodes)}\n"
        f"Edges: {len(generator.edges)}\n"
        f"Frontier: {len(generator.frontier)}"
    )
    ax.text(0.02, 0.98, info_text, transform=ax.transAxes,
            verticalalignment='top', bbox=dict(boxstyle='round', facecolor='wheat', alpha=0.5),
            fontsize=9)
