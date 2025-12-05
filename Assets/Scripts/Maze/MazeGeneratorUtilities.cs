using System;
using System.Collections.Generic;

namespace ForestMaze
{
    /// <summary>
    /// Shared utilities for maze generation algorithms.
    /// Provides common data structures and helper methods used across different maze generators.
    /// </summary>
    public static class MazeGeneratorUtilities
    {
        /// <summary>
        /// Disjoint-set (Union-Find) data structure for Kruskal's algorithm.
        /// Supports efficient set membership queries and union operations.
        /// </summary>
        public sealed class DisjointSet
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

            /// <summary>
            /// Finds the root of the set containing x with path compression.
            /// </summary>
            public int Find(int x)
            {
                if (_parent[x] != x)
                {
                    _parent[x] = Find(_parent[x]);
                }

                return _parent[x];
            }

            /// <summary>
            /// Unions the sets containing x and y. Returns true if a merge happened.
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

        /// <summary>
        /// Represents an edge between two cells in a maze graph.
        /// </summary>
        public struct Edge
        {
            public int A;
            public int B;

            public Edge(int a, int b)
            {
                A = a;
                B = b;
            }
        }

        /// <summary>
        /// Fisher-Yates shuffle for randomizing a list in-place.
        /// </summary>
        /// <typeparam name="T">Type of elements in the list</typeparam>
        /// <param name="list">List to shuffle</param>
        /// <param name="random">Random number generator</param>
        public static void Shuffle<T>(IList<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        /// <summary>
        /// Checks if a character represents a path-like tile (walkable).
        /// </summary>
        /// <param name="c">Character to check</param>
        /// <returns>True if the character represents a walkable tile</returns>
        public static bool IsPathLike(char c)
        {
            return c == '.' || c == 'H';
        }
    }
}
