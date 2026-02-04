using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Centralized random number generator that ensures all game systems use the same seed.
    /// This allows for deterministic/reproducible gameplay when using a fixed seed.
    /// </summary>
    public static class RandomManager
    {
        private static System.Random systemRandom;
        private static int currentSeed;
        private static bool isInitialized;

        /// <summary>
        /// Gets the current seed being used.
        /// </summary>
        public static int CurrentSeed => currentSeed;

        /// <summary>
        /// Gets whether the RandomManager has been initialized.
        /// </summary>
        public static bool IsInitialized => isInitialized;

        /// <summary>
        /// Initializes the random manager with the seed from GameSettings.
        /// Call this once at game start before any random numbers are needed.
        /// </summary>
        public static void Initialize()
        {
            if (GameSettings.UseFixedSeed && GameSettings.RandomSeed != 0)
            {
                Initialize(GameSettings.RandomSeed);
            }
            else
            {
                // Generate a seed from current time
                int timeSeed = System.Environment.TickCount;
                Initialize(timeSeed);
            }
        }

        /// <summary>
        /// Initializes the random manager with a specific seed.
        /// </summary>
        /// <param name="seed">The seed to use for random number generation.</param>
        public static void Initialize(int seed)
        {
            currentSeed = seed;
            systemRandom = new System.Random(seed);

            // Also seed Unity's Random for any code that uses it directly
            UnityEngine.Random.InitState(seed);

            isInitialized = true;
        }

        /// <summary>
        /// Ensures the random manager is initialized. If not, initializes with default settings.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (!isInitialized)
            {
                Initialize();
            }
        }

        /// <summary>
        /// Returns a random float between 0.0 (inclusive) and 1.0 (exclusive).
        /// Equivalent to UnityEngine.Random.value
        /// </summary>
        public static float Value
        {
            get
            {
                EnsureInitialized();
                return (float)systemRandom.NextDouble();
            }
        }

        /// <summary>
        /// Returns a random integer between min (inclusive) and max (exclusive).
        /// Equivalent to UnityEngine.Random.Range(int, int)
        /// </summary>
        public static int Range(int min, int max)
        {
            EnsureInitialized();
            if (min >= max) return min;
            return systemRandom.Next(min, max);
        }

        /// <summary>
        /// Returns a random float between min (inclusive) and max (inclusive).
        /// Equivalent to UnityEngine.Random.Range(float, float)
        /// </summary>
        public static float Range(float min, float max)
        {
            EnsureInitialized();
            if (min >= max) return min;
            return min + (float)systemRandom.NextDouble() * (max - min);
        }

        /// <summary>
        /// Returns a random point inside a unit circle (radius 1).
        /// Equivalent to UnityEngine.Random.insideUnitCircle
        /// </summary>
        public static Vector2 InsideUnitCircle
        {
            get
            {
                EnsureInitialized();
                // Use rejection sampling for uniform distribution
                float x, y;
                do
                {
                    x = (float)(systemRandom.NextDouble() * 2.0 - 1.0);
                    y = (float)(systemRandom.NextDouble() * 2.0 - 1.0);
                } while (x * x + y * y > 1.0f);
                return new Vector2(x, y);
            }
        }

        /// <summary>
        /// Returns a random point inside a unit sphere (radius 1).
        /// Equivalent to UnityEngine.Random.insideUnitSphere
        /// </summary>
        public static Vector3 InsideUnitSphere
        {
            get
            {
                EnsureInitialized();
                // Use rejection sampling for uniform distribution
                float x, y, z;
                do
                {
                    x = (float)(systemRandom.NextDouble() * 2.0 - 1.0);
                    y = (float)(systemRandom.NextDouble() * 2.0 - 1.0);
                    z = (float)(systemRandom.NextDouble() * 2.0 - 1.0);
                } while (x * x + y * y + z * z > 1.0f);
                return new Vector3(x, y, z);
            }
        }

        /// <summary>
        /// Returns a random point on the surface of a unit sphere.
        /// Equivalent to UnityEngine.Random.onUnitSphere
        /// </summary>
        public static Vector3 OnUnitSphere
        {
            get
            {
                EnsureInitialized();
                // Use Marsaglia's method for uniform distribution on sphere surface
                float u = (float)(systemRandom.NextDouble() * 2.0 - 1.0);
                float theta = (float)(systemRandom.NextDouble() * 2.0 * System.Math.PI);
                float sqrtOneMinusU2 = Mathf.Sqrt(1f - u * u);
                return new Vector3(
                    sqrtOneMinusU2 * Mathf.Cos(theta),
                    sqrtOneMinusU2 * Mathf.Sin(theta),
                    u
                );
            }
        }

        /// <summary>
        /// Returns a random rotation.
        /// Equivalent to UnityEngine.Random.rotation
        /// </summary>
        public static Quaternion Rotation
        {
            get
            {
                EnsureInitialized();
                // Generate uniform random quaternion using Shoemake's algorithm
                float u1 = (float)systemRandom.NextDouble();
                float u2 = (float)systemRandom.NextDouble();
                float u3 = (float)systemRandom.NextDouble();

                float sqrt1MinusU1 = Mathf.Sqrt(1f - u1);
                float sqrtU1 = Mathf.Sqrt(u1);
                float twoPiU2 = 2f * Mathf.PI * u2;
                float twoPiU3 = 2f * Mathf.PI * u3;

                return new Quaternion(
                    sqrt1MinusU1 * Mathf.Sin(twoPiU2),
                    sqrt1MinusU1 * Mathf.Cos(twoPiU2),
                    sqrtU1 * Mathf.Sin(twoPiU3),
                    sqrtU1 * Mathf.Cos(twoPiU3)
                );
            }
        }

        /// <summary>
        /// Creates a new System.Random instance seeded from the main random stream.
        /// Use this when you need a separate random stream that won't affect the main sequence.
        /// </summary>
        public static System.Random CreateSubRandom()
        {
            EnsureInitialized();
            return new System.Random(systemRandom.Next());
        }

        /// <summary>
        /// Gets the next seed value from the random stream.
        /// Useful for seeding other systems that need their own System.Random instance.
        /// </summary>
        public static int NextSeed()
        {
            EnsureInitialized();
            return systemRandom.Next();
        }

        /// <summary>
        /// Resets the random manager to uninitialized state.
        /// Call Initialize() again to start a new random sequence.
        /// </summary>
        public static void Reset()
        {
            isInitialized = false;
            systemRandom = null;
            currentSeed = 0;
        }
    }
}
