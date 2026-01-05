# Planar Organic Growing Forest Map Generator

A procedural generator that creates planar, organic forest maps where nodes represent clearings (circles) and edges represent paths (curved corridors). The generator maintains **absolute planarity** - paths never cross or overlap, and nodes maintain proper spacing constraints.

## Features

- **Planar Generation**: Guaranteed no path crossings or overlaps
- **Organic Growth**: Paths grow outward from a central root using curved polylines
- **Center-Biased Spawning**: New clearings favor locations closer to the root, keeping the starting area central
- **Collision Detection**: Robust geometry checks ensure valid placements
- **Configurable Parameters**: Extensive tuning options for different forest layouts

## Quick Start

```bash
# Install dependencies
pip3 install matplotlib

# Run the demo
python3 demo_forest_generator.py [seed]

# Example with specific seed
python3 demo_forest_generator.py 42
```

This will generate:
- `forest_turn_1.png`, `forest_turn_5.png`, `forest_turn_20.png` - Snapshots at different growth stages
- `forest_turn_*.json` - Exported state data
- `forest_comparison.png` - Side-by-side comparison of uniform vs center-biased growth

## Key Parameters

### Constants (in `forest_map_generator.py`)

```python
NODE_RADIUS = 3.0          # Clearing radius
NODE_PADDING = 0.7         # Safety padding around clearings
R_KEEP = 3.7               # Effective keep-out radius
PATH_WIDTH = 1.0           # Corridor width
PATH_RADIUS = 0.5          # Corridor radius for collision detection
```

### Tunable Parameters

#### Growth Behavior

- **CONNECT_PROB = 0.25**: Probability of connecting to an existing node instead of creating a new partial edge
  - Higher values create more interconnected networks
  - Lower values create more tree-like structures
  - Range: [0.0 .. 1.0]

#### Center Bias (keeps root clearing near center)

- **CENTER_BIAS = 0.75**: How much to favor spawning near the root
  - 0.0 = uniform random selection (no bias)
  - 1.0 = maximum bias toward center
  - Recommended: [0.6 .. 0.85]

- **BIAS_POWER = 2.0**: Falloff rate of distance weighting
  - Higher values = stronger preference for close nodes
  - Recommended: [1.5 .. 3.0]

- **BIAS_FLOOR = 0.1**: Minimum weight for far nodes (prevents starvation)
  - Ensures distant frontier edges can still be selected
  - Recommended: [0.05 .. 0.15]

#### Placement Constraints

- **ANGLE_MIN_SEPARATION = 20.0**: Minimum degrees between edges at a node
  - Prevents edges from clumping together
  - Lower values allow tighter packing
  - Range: [15 .. 40] degrees

- **CURVE_STRENGTH = 0.25**: Controls how curved the paths are
  - 0.0 = straight lines
  - Higher values = more pronounced S-curves
  - Recommended: [0.2 .. 0.6]
  - **Note**: Very high values may cause placement failures

#### Rotate/Shorten Solver

- **ROTATE_STEP = 6.0**: Angle increment when searching for valid placement
  - Smaller values try more angles (slower but more thorough)
  - Range: [5 .. 15] degrees

- **SHORTEN_STEP = 0.3**: Length decrement when searching for valid placement
  - Smaller values try more lengths (slower but more thorough)
  - Range: [0.2 .. 0.5]

- **MIN_CORRIDOR_LENGTH = 0.3**: Minimum corridor length before giving up
  - Lower values allow tighter spaces
  - Range: [0.2 .. 1.0]

## Architecture

### Data Structures

#### Node (Clearing)
```python
@dataclass
class Node:
    id: int
    position: Vector2
    kind: str                    # "root" or "normal"
    max_degree: int              # 1 for root, 1-4 for normal
    incident_edges: List[int]
    used_angles: List[float]     # Track edge directions
```

#### Edge (Path)
```python
@dataclass
class Edge:
    id: int
    node_a: int
    node_b: Optional[int]        # None if partial (open)
    polyline_points: List[Vector2]  # Curved corridor centerline
    partial: bool
    ghost_center: Optional[Vector2]  # Reserved future node position
```

### Growth Algorithm

1. **Initialize**: Create root at (0,0) and first normal node
2. **Growth Loop** (each turn):
   - Select a partial edge from frontier using center-biased weighting
   - Spawn new node at the edge's ghost center
   - Convert partial edge to complete
   - Fill new node's capacity:
     - With probability CONNECT_PROB: try connecting to existing node
     - Otherwise: add new partial edges using rotate/shorten solver

### Rotate/Shorten Solver

When placing a new partial edge:
1. Start with random angle θ₀ and length L₀
2. Try angles in order of smallest deviation: θ₀, θ₀±Δ, θ₀±2Δ, ...
3. For each angle, try lengths: L₀, L₀-step, L₀-2×step, ...
4. For each (θ, L) combination:
   - Compute ghost node center
   - Check node/ghost collisions
   - Build curved polyline candidates
   - Accept first valid configuration

### Collision Detection

All checks use **capsule geometry** (line segment + radius):

- **Node-to-Node**: Non-connected nodes must be ≥ 2×R_KEEP apart
- **Path-to-Node**: Corridors must maintain R_KEEP + PATH_RADIUS distance (relaxed for incident nodes)
- **Path-to-Path**: Non-sharing corridors must be ≥ PATH_WIDTH apart
- **Incident Nodes**: More lenient checks for directly connected nodes

## API Usage

### Basic Usage

```python
from forest_map_generator import ForestMapGenerator

# Create generator
gen = ForestMapGenerator(seed=42)
gen.initialize()

# Grow for 20 turns
gen.generate(20)

# Export state
state = gen.get_state()
print(f"Generated {len(gen.nodes)} nodes and {len(gen.edges)} edges")
```

### With Visualization

```python
from forest_map_generator import ForestMapGenerator
from forest_visualizer import ForestVisualizer

gen = ForestMapGenerator(seed=42)
gen.initialize()

viz = ForestVisualizer(gen)

# Grow and visualize
for turn in [1, 5, 10, 20]:
    while gen.turn_count < turn:
        if not gen.step():
            break

    viz.draw_map(
        title=f"Turn {gen.turn_count}",
        filename=f"map_turn_{gen.turn_count}.png"
    )
```

## Implementation Notes

### Why Planar?

The generator maintains planarity to ensure:
- Clear navigation (no ambiguous crossings)
- Aesthetic appeal (clean, readable layouts)
- Gameplay clarity (for game maps)

### Center Bias Behavior

The center-biased frontier selection uses weighted sampling:

```python
For each frontier edge:
    distance = distance_to_root(edge.ghost_center)
    base_weight = 1 / (distance + ε)^BIAS_POWER
    weight = lerp(1, base_weight, CENTER_BIAS)
    weight = max(weight, BIAS_FLOOR)
```

This keeps the root clearing near the center while allowing the map to grow outward.

### Limitations

- **Finite Growth**: Generation may stop before target turns if frontier is exhausted
- **Tighter Constraints = Less Growth**: Stricter collision rules limit how many nodes can be placed
- **Random Variation**: Some seeds produce more compact/sparse layouts than others

## Tuning Guidelines

### For Dense Forests
```python
NODE_PADDING = 0.5           # Reduce spacing
ANGLE_MIN_SEPARATION = 15.0  # Allow closer edges
CONNECT_PROB = 0.35          # More connections
```

### For Sparse Forests
```python
NODE_PADDING = 1.0           # Increase spacing
ANGLE_MIN_SEPARATION = 30.0  # Spread out edges
CONNECT_PROB = 0.15          # Fewer connections
```

### For Straighter Paths
```python
CURVE_STRENGTH = 0.15        # Less curvature
```

### For More Organic Paths
```python
CURVE_STRENGTH = 0.5         # More curvature
ROTATE_STEP = 5.0            # Try more angles
```

### For Compact, Central Layouts
```python
CENTER_BIAS = 0.85           # Strong center preference
BIAS_POWER = 3.0             # Steep falloff
```

## Validation

The demo includes 5 automated tests:

1. **Connectivity**: All edges properly connect nodes
2. **Node Spacing**: Proper distance constraints maintained
3. **Planarity**: No path collisions or overlaps
4. **Growth**: Map successfully grows multiple nodes
5. **Degree Constraints**: Nodes respect max_degree limits

Run with: `python3 demo_forest_generator.py`

## Files

- `forest_map_generator.py` - Core generator implementation
- `forest_visualizer.py` - Visualization utilities
- `demo_forest_generator.py` - Demo harness with tests
- `FOREST_MAP_GENERATOR_README.md` - This file

## Requirements

- Python 3.7+
- matplotlib (for visualization)
- numpy (installed with matplotlib)

## License

This implementation was created for the Tower Defense project as specified in the requirements document.
