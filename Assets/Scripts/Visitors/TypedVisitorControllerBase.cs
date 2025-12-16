using UnityEngine;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Base class for archetype-aware visitor controllers.
    /// Provides configuration-driven behavior and archetype identification.
    /// </summary>
    public abstract class TypedVisitorControllerBase : VisitorControllerBase, IArchetypedVisitor
    {
        [Header("Archetype Configuration")]
        [SerializeField]
        [Tooltip("Archetype configuration defining this visitor's behavioral parameters")]
        protected VisitorArchetypeConfig config;

        public VisitorArchetype Archetype => config != null ? config.Archetype : VisitorArchetype.LanternDrunk;
        public VisitorArchetypeConfig ArchetypeConfig => config;

        protected override void Awake()
        {
            base.Awake();

            if (config != null)
            {
                // Apply archetype-specific settings
                moveSpeed = config.BaseSpeed;
                mesmerizedDuration = config.InitialMesmerizedDuration;
                frightenedDuration = config.FrightenedDuration;
                minLostDistance = Mathf.RoundToInt(config.LostDetourMin);
                maxLostDistance = Mathf.RoundToInt(config.LostDetourMax);
            }
        }

        /// <summary>
        /// Gets the fascination chance for this archetype.
        /// Override to apply additional modifiers (e.g., from Heart powers).
        /// </summary>
        public virtual float GetFascinationChance()
        {
            return config != null ? config.FascinationChance : 0.5f;
        }

        /// <summary>
        /// Gets the fascination duration range for this archetype.
        /// </summary>
        public virtual (float min, float max) GetFascinationDuration()
        {
            if (config != null)
                return (config.FascinationDurationMin, config.FascinationDurationMax);
            return (2f, 5f);
        }

        /// <summary>
        /// Gets the confusion/misstep chance at intersections for this archetype.
        /// </summary>
        public virtual float GetConfusionChance()
        {
            return config != null ? config.ConfusionIntersectionChance : 0.25f;
        }

        /// <summary>
        /// Gets the frightened speed multiplier for this archetype.
        /// </summary>
        public virtual float GetFrightenedSpeedMultiplier()
        {
            return config != null ? config.FrightenedSpeedMultiplier : 1.2f;
        }

        /// <summary>
        /// Returns whether frightened visitors of this archetype prefer exits over the heart.
        /// </summary>
        public virtual bool ShouldFrightenedPreferExit()
        {
            return config != null && config.FrightenedPrefersExit;
        }

        /// <summary>
        /// Gets the essence reward for consuming this visitor.
        /// </summary>
        public virtual int GetEssenceReward()
        {
            return config != null ? config.EssenceReward : 1;
        }

        #region Archetype-Aware Fascination

        /// <summary>
        /// Override to use archetype-specific fascination chance and cooldown.
        /// </summary>
        protected override void EnterFaeInfluence(FaeMaze.Props.FaeLantern lantern, Vector2Int visitorGridPosition)
        {
            // If already fascinated by this same lantern, ignore
            if (isFascinated && currentFaeLantern == lantern && fascinationLanternPosition == lantern.GridPosition)
                return;

            // Check cooldown (prevents immediate re-triggering)
            float cooldown = config != null ? config.FascinationCooldown : lantern.CooldownSec;
            if (lanternCooldowns.ContainsKey(lantern) && lanternCooldowns[lantern] > 0f)
            {
                return;
            }

            // Use archetype-specific fascination chance
            float fascinationChance = GetFascinationChance();
            float roll = Random.value;
            if (roll > fascinationChance)
            {
                // Set cooldown even on failed proc to prevent spam checks
                lanternCooldowns[lantern] = cooldown;
                return;
            }

            // Allow re-fascination by a different lantern
            isFascinated = true;
            currentFaeLantern = lantern;
            fascinationLanternPosition = lantern.GridPosition;
            hasReachedLantern = false;
            fascinationTimer = 0f; // Will be set when reaching lantern

            // Set archetype-specific cooldown for this lantern
            lanternCooldowns[lantern] = cooldown;

            // Clear path nodes from previous fascination
            fascinatedPathNodes.Clear();

            // Abandon current path and reset detour state
            path = null;
            currentPathIndex = 0;
            ResetDetourState();

            // Calculate path to lantern
            if (gameController != null && mazeGridBehaviour != null &&
                mazeGridBehaviour.WorldToGrid(transform.position, out int currentX, out int currentY))
            {
                Vector2Int currentPos = new Vector2Int(currentX, currentY);

                List<FaeMaze.Systems.MazeGrid.MazeNode> pathToLantern = new List<FaeMaze.Systems.MazeGrid.MazeNode>();
                // Fascinated visitors use normal attraction (they're already mesmerized)
                if (gameController.TryFindPath(currentPos, fascinationLanternPosition, pathToLantern, 1.0f) && pathToLantern.Count > 0)
                {
                    // Convert to Vector2Int path
                    path = new List<Vector2Int>();
                    foreach (var node in pathToLantern)
                    {
                        path.Add(new Vector2Int(node.x, node.y));
                    }

                    // Find starting waypoint
                    currentPathIndex = 0;
                    if (path.Count > 1)
                    {
                        for (int i = 1; i < path.Count && i < 3; i++)
                        {
                            Vector2Int pathPos = path[i];
                            if (pathPos.x == currentX && pathPos.y == currentY)
                            {
                                currentPathIndex = i;
                                break;
                            }
                        }
                    }

                    // Use archetype-specific fascination duration
                    var (minDuration, maxDuration) = GetFascinationDuration();
                    float duration = Random.Range(minDuration, maxDuration);

                    RefreshStateFromFlags();
                }
                else
                {
                    // Couldn't find path to lantern - cancel fascination
                    isFascinated = false;
                    currentFaeLantern = null;
                }
            }
            else
            {
                // Couldn't resolve current position - cancel fascination
                isFascinated = false;
                currentFaeLantern = null;
            }
        }

        #endregion
    }
}
