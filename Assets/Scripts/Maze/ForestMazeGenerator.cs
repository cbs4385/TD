using System;
using System.Collections.Generic;
using System.Text;

namespace ForestMaze
{
    /// <summary>
    /// Generates a forest maze using Kruskal's algorithm.
    /// 
    /// Tiles:
    ///   Trees/brambles: '#'
    ///   Undergrowth   : ';'
    ///   Water         : '~'
    ///   Center (heart): 'H'
    ///   Path          : '.'
    /// 
    /// The returned maze string is height lines of width characters each.
    /// </summary>
    public static class ForestMazeGenerator
    {
        /// <summary>
        /// Generate a forest maze.
        /// </summary>
        /// <param name="width">Total width in tiles (>= 3).</param>
        /// <param name="height">Total height in tiles (>= 3).</param>
        /// <param name="numberOfEntrances">Number of entrances/exits on the outer border (>= 1).</param>
        /// <param name="seed">Optional random seed for repeatability.</param>
        /// <returns>Newline-separated string representation of the maze.</returns>
        public static string GenerateMaze(
            int width,
            int height,
            int numberOfEntrances,
            int? seed = null)
        {
            if (width < 3) throw new ArgumentOutOfRangeException(nameof(width), "Width must be >= 3.");
            if (height < 3) throw new ArgumentOutOfRangeException(nameof(height), "Height must be >= 3.");
            if (numberOfEntrances < 1) throw new ArgumentOutOfRangeException(nameof(numberOfEntrances), "Must be >= 1.");

            var random = seed.HasValue ? new Random(seed.Value) : new Random();

            // The maze grid we will return.
            var grid = new char[height, width];

            // Fill with solid forest (#) initially.
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    grid[y, x] = '#';
                }
            }

            // Logical cell grid for Kruskal: we embed cells at odd coordinates in the char grid.
            // This standard trick gives us explicit "walls" between cells.
            int cellWidth = (width - 1) / 2;
            int cellHeight = (height - 1) / 2;

            if (cellWidth <= 0 || cellHeight <= 0)
                throw new InvalidOperationException("Maze dimensions too small for cell-based generation.");

            var dsu = new DisjointSet(cellWidth * cellHeight);
            var edges = new List<Edge>();

            // Initialize cells (as path) and build edge list for Kruskal.
            for (int cy = 0; cy < cellHeight; cy++)
            {
                for (int cx = 0; cx < cellWidth; cx++)
                {
                    int cellId = cy * cellWidth + cx;

                    // Cell center position in the output grid:
                    int gx = 2 * cx + 1;
                    int gy = 2 * cy + 1;

                    if (gx < width && gy < height)
                    {
                        grid[gy, gx] = '.'; // mark logical cell centers as path
                    }

                    // Horizontal edge to cell (cx + 1, cy).
                    if (cx < cellWidth - 1)
                    {
                        int neighborId = cellId + 1;
                        edges.Add(new Edge(cellId, neighborId));
                    }

                    // Vertical edge to cell (cx, cy + 1).
                    if (cy < cellHeight - 1)
                    {
                        int neighborId = cellId + cellWidth;
                        edges.Add(new Edge(cellId, neighborId));
                    }
                }
            }

            // Shuffle edges for random Kruskal.
            Shuffle(edges, random);

            // Kruskal's algorithm: carve passages between cells.
            foreach (var edge in edges)
            {
                if (dsu.Union(edge.A, edge.B))
                {
                    // Edge connects two different sets, so we carve the wall between the two cells.
                    int ax = edge.A % cellWidth;
                    int ay = edge.A / cellWidth;
                    int bx = edge.B % cellWidth;
                    int by = edge.B / cellWidth;

                    int agx = 2 * ax + 1;
                    int agy = 2 * ay + 1;
                    int bgx = 2 * bx + 1;
                    int bgy = 2 * by + 1;

                    int wx = (agx + bgx) / 2;
                    int wy = (agy + bgy) / 2;

                    if (wx >= 0 && wx < width && wy >= 0 && wy < height)
                    {
                        grid[wy, wx] = '.'; // carve passage between cells
                    }
                }
            }

            // Mark the maze center as 'H' (heart) on top of a path tile.
            MarkCenter(grid, width, height, cellWidth, cellHeight);

            // Cut entrances/exits on the outer border.
            AddEntrances(grid, width, height, numberOfEntrances, random);

            // Decorate remaining solid forest tiles as trees, undergrowth, or water.
            DecorateTerrain(grid, width, height, random);

            // FINAL OVERRIDE: ensure all edge tiles are '#' or '.' only.
            EnforceEdgeTiles(grid, width, height);

            // Convert to a single string.
            return GridToString(grid, width, height);
        }

        /// <summary>
        /// Ensure that all border tiles are either '#' (forest) or '.' (entrance/exit).
        /// Any other character on the outer edge is forced back to '#'.
        /// </summary>
        private static void EnforceEdgeTiles(char[,] grid, int width, int height)
        {
            // Top and bottom rows
            for (int x = 0; x < width; x++)
            {
                if (grid[0, x] != '.')           // top edge
                    grid[0, x] = '#';

                if (grid[height - 1, x] != '.')  // bottom edge
                    grid[height - 1, x] = '#';
            }

            // Left and right columns
            for (int y = 0; y < height; y++)
            {
                if (grid[y, 0] != '.')           // left edge
                    grid[y, 0] = '#';

                if (grid[y, width - 1] != '.')   // right edge
                    grid[y, width - 1] = '#';
            }
        }

        private static bool IsPathLike(char c)
        {
            return c == '.' || c == 'H';
        }

        #region Center / Entrances / Decoration

        private static void MarkCenter(char[,] grid, int width, int height, int cellWidth, int cellHeight)
        {
            int centerCx = cellWidth / 2;
            int centerCy = cellHeight / 2;

            int gx = 2 * centerCx + 1;
            int gy = 2 * centerCy + 1;

            if (gx < 0 || gx >= width || gy < 0 || gy >= height)
                return;

            // Only override if it's already a path tile.
            if (grid[gy, gx] == '.')
            {
                grid[gy, gx] = 'H';
            }
        }

        private static void AddEntrances(char[,] grid, int width, int height, int numberOfEntrances, Random random)
        {
            if (numberOfEntrances <= 0)
                return;

            // Define the central band on each edge: indices within 25% of the center.
            // That yields a central 50% span: [width/4, width-1-width/4] (clamped to [1, width-2]).
            int marginX = width / 4;
            int minX = Math.Max(1, marginX);
            int maxX = Math.Min(width - 2, width - 1 - marginX);
            if (minX > maxX)
            {
                // Fallback for very small grids: allow the entire inner edge.
                minX = 1;
                maxX = width - 2;
            }

            int marginY = height / 4;
            int minY = Math.Max(1, marginY);
            int maxY = Math.Min(height - 2, height - 1 - marginY);
            if (minY > maxY)
            {
                // Fallback for very small grids.
                minY = 1;
                maxY = height - 2;
            }

            var topCandidates = new List<(int x, int y)>();
            var bottomCandidates = new List<(int x, int y)>();
            var leftCandidates = new List<(int x, int y)>();
            var rightCandidates = new List<(int x, int y)>();

            // Top edge (y = 0) – look one tile inside at y = 1.
            for (int x = minX; x <= maxX; x++)
            {
                if (IsPathLike(grid[1, x]))
                    topCandidates.Add((x, 0));
            }

            // Bottom edge (y = height - 1) – look one tile inside at y = height - 2.
            for (int x = minX; x <= maxX; x++)
            {
                if (IsPathLike(grid[height - 2, x]))
                    bottomCandidates.Add((x, height - 1));
            }

            // Left edge (x = 0) – look one tile inside at x = 1.
            for (int y = minY; y <= maxY; y++)
            {
                if (IsPathLike(grid[y, 1]))
                    leftCandidates.Add((0, y));
            }

            // Right edge (x = width - 1) – look one tile inside at x = width - 2.
            for (int y = minY; y <= maxY; y++)
            {
                if (IsPathLike(grid[y, width - 2]))
                    rightCandidates.Add((width - 1, y));
            }

            // Group edges; each group will contribute at most one entrance.
            var edgeGroups = new List<List<(int x, int y)>>();
            if (topCandidates.Count > 0) edgeGroups.Add(topCandidates);
            if (bottomCandidates.Count > 0) edgeGroups.Add(bottomCandidates);
            if (leftCandidates.Count > 0) edgeGroups.Add(leftCandidates);
            if (rightCandidates.Count > 0) edgeGroups.Add(rightCandidates);

            if (edgeGroups.Count == 0)
                return;

            // Randomize which edges get picked first.
            Shuffle(edgeGroups, random);

            int openings = Math.Min(numberOfEntrances, edgeGroups.Count);

            for (int i = 0; i < openings; i++)
            {
                var group = edgeGroups[i];
                var candidate = group[random.Next(group.Count)];
                grid[candidate.y, candidate.x] = '.'; // open a single entrance on this edge
            }
        }


        private static void DecorateTerrain(char[,] grid, int width, int height, Random random)
        {
            const double waterChance = 0.05;       // 5% of solid tiles become water
            const double undergrowthChance = 0.30; // 30% become undergrowth

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    char c = grid[y, x];

                    // Only decorate solid forest (#); keep paths and center untouched.
                    if (c != '#') continue;

                    double roll = random.NextDouble();

                    if (roll < waterChance)
                    {
                        grid[y, x] = '~';
                    }
                    else if (roll < waterChance + undergrowthChance)
                    {
                        grid[y, x] = ';';
                    }
                    else
                    {
                        grid[y, x] = '#';
                    }
                }
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

        #endregion

        #region Helpers (Kruskal support)

        private struct Edge
        {
            public int A;
            public int B;

            public Edge(int a, int b)
            {
                A = a;
                B = b;
            }
        }

        private sealed class DisjointSet
        {
            private readonly int[] _parent;
            private readonly byte[] _rank;

            public DisjointSet(int count)
            {
                _parent = new int[count];
                _rank = new byte[count];

                for (int i = 0; i < count; i++)
                {
                    _parent[i] = i;
                    _rank[i] = 0;
                }
            }

            private int Find(int x)
            {
                if (_parent[x] != x)
                {
                    _parent[x] = Find(_parent[x]);
                }

                return _parent[x];
            }

            /// <summary>
            /// Union sets containing x and y. Returns true if a merge happened.
            /// </summary>
            public bool Union(int x, int y)
            {
                int rx = Find(x);
                int ry = Find(y);

                if (rx == ry) return false;

                if (_rank[rx] < _rank[ry])
                {
                    _parent[rx] = ry;
                }
                else if (_rank[rx] > _rank[ry])
                {
                    _parent[ry] = rx;
                }
                else
                {
                    _parent[ry] = rx;
                    _rank[rx]++;
                }

                return true;
            }
        }

        private static void Shuffle<T>(IList<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        #endregion
    }
}
