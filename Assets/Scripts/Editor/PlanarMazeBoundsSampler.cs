using UnityEditor;
using UnityEngine;
using ForestMaze;

namespace FaeMaze.Editor
{
    public static class PlanarMazeBoundsSampler
    {
        private const int DefaultSampleCount = 100;
        private const int DefaultTurns = 20;
        private const int DefaultNodeCount = 26;

        [MenuItem("FaeMaze/Diagnostics/Sample Planar Maze Bounds")]
        public static void SampleBounds()
        {
            int sampleCount = DefaultSampleCount;
            float minWidth = float.MaxValue;
            float maxWidth = float.MinValue;
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            float sumWidth = 0f;
            float sumHeight = 0f;

            for (int seed = 0; seed < sampleCount; seed++)
            {
                var bounds = PlanarForestMazeGenerator.ComputeBoundsForSeed(
                    DefaultTurns,
                    seed,
                    DefaultNodeCount);

                float width = bounds.maxX - bounds.minX;
                float height = bounds.maxY - bounds.minY;

                sumWidth += width;
                sumHeight += height;
                minWidth = Mathf.Min(minWidth, width);
                maxWidth = Mathf.Max(maxWidth, width);
                minHeight = Mathf.Min(minHeight, height);
                maxHeight = Mathf.Max(maxHeight, height);
            }

            float averageWidth = sumWidth / sampleCount;
            float averageHeight = sumHeight / sampleCount;

            Debug.Log(
                "[PlanarMazeBoundsSampler] " +
                $"Samples={sampleCount}, Turns={DefaultTurns}, NodeCount={DefaultNodeCount} | " +
                $"Width avg={averageWidth:F2} (min {minWidth:F2}, max {maxWidth:F2}) | " +
                $"Height avg={averageHeight:F2} (min {minHeight:F2}, max {maxHeight:F2})");
        }
    }
}
