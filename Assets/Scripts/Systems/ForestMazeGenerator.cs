using System;
using System.Collections.Generic;
using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Tile types for the forest maze.
    /// </summary>
    public enum TileType
    {
        Path,          // fully walkable
        Undergrowth,   // walkable but "rough"
        TreeBramble,   // solid
        Water          // usually solid or special
    }

    /// <summary>
    /// Configuration for forest maze generation.
    /// </summary>
    [System.Serializable]
    public struct ForestMazeConfig
    {
        [Tooltip("Fine grid width in tiles")]
        public int width;

        [Tooltip("Fine grid height in tiles")]
        public int height;

        [Tooltip("Number of entrances/exits on map border")]
        public int numEntrances;

        [Tooltip("Minimum corridor width in tiles (e.g. 2)")]
        public int minPathWidth;

        [Tooltip("Maximum corridor width in tiles (e.g. 5)")]
        public int maxPathWidth;

        [Tooltip("Fraction (0..1) of non-walkable cells to use as water")]
        [Range(0f, 1f)]
        public float waterCoverage;

        [Tooltip("Random seed for generation")]
        public int randomSeed;

        /// <summary>
        /// Creates a default configuration.
        /// </summary>
        public static ForestMazeConfig Default()
        {
            return new ForestMazeConfig
            {
                width = 100,
                height = 100,
                numEntrances = 2,
                minPathWidth = 2,
                maxPathWidth = 5,
                waterCoverage = 0.15f,
                randomSeed = 0
            };
        }
    }

    /// <summary>
    /// Represents a cell in the coarse maze grid.
    /// </summary>
    internal class MazeCell
    {
        public int i;  // column in coarse grid
        public int j;  // row in coarse grid

        public MazeCell(int i, int j)
        {
            this.i = i;
            this.j = j;
        }

        public override bool Equals(object obj)
        {
            if (obj is MazeCell other)
            {
                return i == other.i && j == other.j;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (i << 16) ^ j;
        }
    }

    /// <summary>
    /// Represents an edge between two maze cells.
    /// </summary>
    internal struct MazeEdge
    {
        public MazeCell cellA;
        public MazeCell cellB;

        public MazeEdge(MazeCell a, MazeCell b)
        {
            cellA = a;
            cellB = b;
        }

        public override bool Equals(object obj)
        {
            if (obj is MazeEdge other)
            {
                return (cellA.Equals(other.cellA) && cellB.Equals(other.cellB)) ||
                       (cellA.Equals(other.cellB) && cellB.Equals(other.cellA));
            }
            return false;
        }

        public override int GetHashCode()
        {
            return cellA.GetHashCode() ^ cellB.GetHashCode();
        }
    }

    /// <summary>
    /// Generates a forest-themed maze with variable-width paths,
    /// undergrowth edges, tree/bramble walls, and water features.
    /// </summary>
    public class ForestMazeGenerator
    {
        private ForestMazeConfig config;
        private System.Random rng;
        private TileType[,] tiles;

        // Coarse maze grid
        private int mazeCols;
        private int mazeRows;
        private int cellSpacing;
        private MazeCell[,] mazeCells;
        private HashSet<MazeEdge> edges;
        private List<MazeCell> chosenEntrances;
        private List<Vector2Int> entranceEdgePositions;

        /// <summary>
        /// Generates a forest maze based on the provided configuration.
        /// </summary>
        /// <param name="config">Configuration parameters</param>
        /// <returns>2D array of tile types</returns>
        public TileType[,] GenerateForestMaze(ForestMazeConfig config)
        {
            this.config = config;
            this.rng = new System.Random(config.randomSeed);
            this.edges = new HashSet<MazeEdge>();
            this.chosenEntrances = new List<MazeCell>();
            this.entranceEdgePositions = new List<Vector2Int>();

            // Step 1: Initialize the grid with solid forest
            InitializeGrid();

            // Step 2: Create coarse maze layout
            CreateCoarseMazeLayout();

            // Step 2a: Carve perfect maze using DFS
            CarveMazeDFS();

            // Step 2b: Add extra edges for loops
            AddExtraEdges();

            // Step 4: Choose entrances/exits
            ChooseEntrances();

            // Step 5: Carve corridors with variable width
            CarveCorridors();

            // Step 5c: Carve entrances to edges
            CarveEntrancesToEdges();

            // Step 6: Add water features
            AddWaterFeatures();

            return tiles;
        }

        /// <summary>
        /// Step 1: Initialize the grid with TreeBramble everywhere.
        /// </summary>
        private void InitializeGrid()
        {
            tiles = new TileType[config.width, config.height];

            for (int y = 0; y < config.height; y++)
            {
                for (int x = 0; x < config.width; x++)
                {
                    tiles[x, y] = TileType.TreeBramble;
                }
            }

            Debug.Log($"ForestMazeGenerator: Initialized {config.width}x{config.height} grid");
        }

        /// <summary>
        /// Step 2: Create the coarse maze layout.
        /// </summary>
        private void CreateCoarseMazeLayout()
        {
            // Decide spacing between maze cell centers
            // Use the widest possible corridor width to avoid over-crowding the grid and keep walls intact
            cellSpacing = Mathf.Max(config.maxPathWidth + 1, config.minPathWidth + 2);
            mazeCols = Mathf.Max(1, (config.width - 2) / cellSpacing);
            mazeRows = Mathf.Max(1, (config.height - 2) / cellSpacing);

            // Create maze cells
            mazeCells = new MazeCell[mazeCols, mazeRows];
            for (int j = 0; j < mazeRows; j++)
            {
                for (int i = 0; i < mazeCols; i++)
                {
                    mazeCells[i, j] = new MazeCell(i, j);
                }
            }

            Debug.Log($"ForestMazeGenerator: Created {mazeCols}x{mazeRows} coarse maze grid (spacing={cellSpacing})");
        }

        /// <summary>
        /// Gets neighbors of a maze cell in 4 directions.
        /// </summary>
        private List<MazeCell> GetNeighbors(int i, int j)
        {
            List<MazeCell> neighbors = new List<MazeCell>();

            if (i > 0) neighbors.Add(mazeCells[i - 1, j]);
            if (i < mazeCols - 1) neighbors.Add(mazeCells[i + 1, j]);
            if (j > 0) neighbors.Add(mazeCells[i, j - 1]);
            if (j < mazeRows - 1) neighbors.Add(mazeCells[i, j + 1]);

            return neighbors;
        }

        /// <summary>
        /// Step 2a: Carve a perfect maze using DFS.
        /// </summary>
        private void CarveMazeDFS()
        {
            HashSet<MazeCell> visited = new HashSet<MazeCell>();

            // Pick random start cell
            MazeCell startCell = mazeCells[rng.Next(0, mazeCols), rng.Next(0, mazeRows)];

            DFS(startCell, visited);

            Debug.Log($"ForestMazeGenerator: Carved perfect maze with {edges.Count} corridors");
        }

        /// <summary>
        /// Recursive DFS to carve the maze.
        /// </summary>
        private void DFS(MazeCell cell, HashSet<MazeCell> visited)
        {
            visited.Add(cell);

            List<MazeCell> neighbors = GetNeighbors(cell.i, cell.j);
            Shuffle(neighbors);

            foreach (MazeCell neighbor in neighbors)
            {
                if (!visited.Contains(neighbor))
                {
                    edges.Add(new MazeEdge(cell, neighbor));
                    DFS(neighbor, visited);
                }
            }
        }

        /// <summary>
        /// Step 2b: Add extra edges for loops and complexity.
        /// </summary>
        private void AddExtraEdges()
        {
            int extraEdgesCount = Mathf.Max(1, (mazeCols * mazeRows) / 10);

            for (int k = 0; k < extraEdgesCount; k++)
            {
                MazeCell c = mazeCells[rng.Next(0, mazeCols), rng.Next(0, mazeRows)];
                List<MazeCell> neighbors = GetNeighbors(c.i, c.j);

                if (neighbors.Count > 0)
                {
                    MazeCell n = neighbors[rng.Next(0, neighbors.Count)];
                    MazeEdge edge = new MazeEdge(c, n);
                    edges.Add(edge); // HashSet prevents duplicates
                }
            }

            Debug.Log($"ForestMazeGenerator: Added extra edges (total={edges.Count})");
        }

        /// <summary>
        /// Step 4: Choose entrance/exit cells on the border.
        /// </summary>
        private void ChooseEntrances()
        {
            List<MazeCell> borderCells = new List<MazeCell>();

            for (int j = 0; j < mazeRows; j++)
            {
                for (int i = 0; i < mazeCols; i++)
                {
                    if (i == 0 || i == mazeCols - 1 || j == 0 || j == mazeRows - 1)
                    {
                        borderCells.Add(mazeCells[i, j]);
                    }
                }
            }

            Shuffle(borderCells);

            int numToChoose = Mathf.Min(config.numEntrances, borderCells.Count);
            for (int k = 0; k < numToChoose; k++)
            {
                chosenEntrances.Add(borderCells[k]);
            }

            Debug.Log($"ForestMazeGenerator: Chose {chosenEntrances.Count} entrances");
        }

        /// <summary>
        /// Converts a coarse maze cell to fine grid world coordinates.
        /// </summary>
        private Vector2Int MazeCellToWorld(MazeCell cell)
        {
            int worldX = 1 + cell.i * cellSpacing;
            int worldY = 1 + cell.j * cellSpacing;
            return new Vector2Int(worldX, worldY);
        }

        /// <summary>
        /// Step 5: Carve corridors with variable width.
        /// </summary>
        private void CarveCorridors()
        {
            // 5a: Junction clearings at cell centers
            foreach (MazeCell cell in mazeCells)
            {
                Vector2Int center = MazeCellToWorld(cell);
                int width = rng.Next(config.minPathWidth, config.maxPathWidth + 1);
                int pathRadius = Mathf.Max(1, width / 2);
                int undergrowthRadius = pathRadius + 1;
                CarveDisk(center.x, center.y, pathRadius, undergrowthRadius);
            }

            // 5b: Corridors between neighboring maze cells
            foreach (MazeEdge edge in edges)
            {
                Vector2Int p1 = MazeCellToWorld(edge.cellA);
                Vector2Int p2 = MazeCellToWorld(edge.cellB);
                CarveCorridorBetween(p1, p2);
            }

            Debug.Log($"ForestMazeGenerator: Carved all corridors");
        }

        /// <summary>
        /// Carves a disk with inner path radius and outer undergrowth radius.
        /// </summary>
        private void CarveDisk(int cx, int cy, int radiusInner, int radiusOuter)
        {
            for (int y = cy - radiusOuter; y <= cy + radiusOuter; y++)
            {
                if (y < 0 || y >= config.height) continue;

                for (int x = cx - radiusOuter; x <= cx + radiusOuter; x++)
                {
                    if (x < 0 || x >= config.width) continue;

                    int dx = x - cx;
                    int dy = y - cy;
                    int dist2 = dx * dx + dy * dy;

                    if (dist2 <= radiusInner * radiusInner)
                    {
                        tiles[x, y] = TileType.Path;
                    }
                    else if (dist2 <= radiusOuter * radiusOuter)
                    {
                        if (tiles[x, y] == TileType.TreeBramble || tiles[x, y] == TileType.Water)
                        {
                            tiles[x, y] = TileType.Undergrowth;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Carves a thick corridor between two points.
        /// </summary>
        private void CarveCorridorBetween(Vector2Int p1, Vector2Int p2)
        {
            int x1 = p1.x;
            int y1 = p1.y;
            int x2 = p2.x;
            int y2 = p2.y;

            // Random width for this corridor
            int width = rng.Next(config.minPathWidth, config.maxPathWidth + 1);
            int pathRadius = Mathf.Max(1, width / 2 - 1);
            int undergrowthRadius = Mathf.Max(pathRadius + 1, width / 2);

            int steps = Mathf.Max(Mathf.Abs(x2 - x1), Mathf.Abs(y2 - y1));
            if (steps == 0) steps = 1;

            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps;
                int cx = Mathf.RoundToInt(Mathf.Lerp(x1, x2, t));
                int cy = Mathf.RoundToInt(Mathf.Lerp(y1, y2, t));
                CarveDisk(cx, cy, pathRadius, undergrowthRadius);
            }
        }

        /// <summary>
        /// Step 5c: Carve entrances/exits to the map edges.
        /// </summary>
        private void CarveEntrancesToEdges()
        {
            foreach (MazeCell cell in chosenEntrances)
            {
                CarveEntrance(cell);
            }

            Debug.Log($"ForestMazeGenerator: Carved {chosenEntrances.Count} entrance tunnels");
        }

        /// <summary>
        /// Carves a corridor from a maze cell to the nearest map edge.
        /// </summary>
        private void CarveEntrance(MazeCell cell)
        {
            Vector2Int center = MazeCellToWorld(cell);
            int cx = center.x;
            int cy = center.y;

            // Find nearest edge
            int distLeft = cx;
            int distRight = config.width - 1 - cx;
            int distTop = cy;
            int distBottom = config.height - 1 - cy;

            int minDist = Mathf.Min(distLeft, Mathf.Min(distRight, Mathf.Min(distTop, distBottom)));

            int ex, ey;
            if (minDist == distLeft)
            {
                ex = 0;
                ey = cy;
            }
            else if (minDist == distRight)
            {
                ex = config.width - 1;
                ey = cy;
            }
            else if (minDist == distTop)
            {
                ex = cx;
                ey = 0;
            }
            else
            {
                ex = cx;
                ey = config.height - 1;
            }

            entranceEdgePositions.Add(new Vector2Int(ex, ey));

            // Carve corridor between edge and center
            CarveCorridorBetween(new Vector2Int(ex, ey), new Vector2Int(cx, cy));
        }

        /// <summary>
        /// Step 6: Add water features to the forest.
        /// </summary>
        private void AddWaterFeatures()
        {
            // 6a: Scatter water blobs in forest
            List<Vector2Int> solidPositions = new List<Vector2Int>();
            for (int y = 0; y < config.height; y++)
            {
                for (int x = 0; x < config.width; x++)
                {
                    if (tiles[x, y] == TileType.TreeBramble)
                    {
                        solidPositions.Add(new Vector2Int(x, y));
                    }
                }
            }

            int targetWaterCount = Mathf.FloorToInt(config.waterCoverage * solidPositions.Count);
            Shuffle(solidPositions);

            int waterPlaced = 0;
            int i = 0;
            while (waterPlaced < targetWaterCount && i < solidPositions.Count)
            {
                Vector2Int start = solidPositions[i];
                i++;

                // Small random blob using random walk
                int blobSize = rng.Next(10, 40);
                int x = start.x;
                int y = start.y;

                for (int step = 0; step < blobSize; step++)
                {
                    if (tiles[x, y] == TileType.TreeBramble)
                    {
                        tiles[x, y] = TileType.Water;
                        waterPlaced++;
                        if (waterPlaced >= targetWaterCount)
                            break;
                    }

                    // Move in random direction
                    Vector2Int[] directions = new Vector2Int[]
                    {
                        new Vector2Int(1, 0),
                        new Vector2Int(-1, 0),
                        new Vector2Int(0, 1),
                        new Vector2Int(0, -1)
                    };

                    Vector2Int dir = directions[rng.Next(0, directions.Length)];
                    int nx = x + dir.x;
                    int ny = y + dir.y;

                    if (nx >= 0 && nx < config.width && ny >= 0 && ny < config.height)
                    {
                        x = nx;
                        y = ny;
                    }
                }
            }

            // 6b: Optional water hugging corridors
            for (int y = 1; y < config.height - 1; y++)
            {
                for (int x = 1; x < config.width - 1; x++)
                {
                    if (tiles[x, y] == TileType.TreeBramble)
                    {
                        // Check if adjacent to path/undergrowth
                        bool adjacentToPath = false;
                        Vector2Int[] neighbors = new Vector2Int[]
                        {
                            new Vector2Int(x + 1, y),
                            new Vector2Int(x - 1, y),
                            new Vector2Int(x, y + 1),
                            new Vector2Int(x, y - 1)
                        };

                        foreach (Vector2Int n in neighbors)
                        {
                            if (n.x >= 0 && n.x < config.width && n.y >= 0 && n.y < config.height)
                            {
                                if (tiles[n.x, n.y] == TileType.Path || tiles[n.x, n.y] == TileType.Undergrowth)
                                {
                                    adjacentToPath = true;
                                    break;
                                }
                            }
                        }

                        if (adjacentToPath && rng.NextDouble() < 0.02)
                        {
                            tiles[x, y] = TileType.Water;
                        }
                    }
                }
            }

            Debug.Log($"ForestMazeGenerator: Placed {waterPlaced} water tiles (target={targetWaterCount})");
        }

        /// <summary>
        /// Fisher-Yates shuffle for a list.
        /// </summary>
        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        /// <summary>
        /// Returns the list of carved entrance edge positions (world grid coordinates).
        /// </summary>
        public IReadOnlyList<Vector2Int> GetEntranceEdgePositions()
        {
            return entranceEdgePositions ?? new List<Vector2Int>();
        }
    }
}
