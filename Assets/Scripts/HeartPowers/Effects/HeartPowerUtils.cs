using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FaeMaze.Props;
using FaeMaze.Visitors;
using FaeMaze.Systems;
using FaeMaze.Audio;
using FaeMaze.Roguelike;
using ForestMaze;

namespace FaeMaze.HeartPowers
{
    /// <summary>
    /// Utility methods shared across multiple heart power effects.
    /// </summary>
    public static class HeartPowerUtils
    {
        #region Visitor Detection

        /// <summary>
        /// Finds the first visitor within a radius of a position that is in an active state.
        /// Excludes visitors in Consumed, Escaping, Grabbed, or Dazed states.
        /// </summary>
        /// <param name="position">Center position to search from</param>
        /// <param name="radius">Search radius</param>
        /// <param name="excludeList">Optional list of visitors to exclude from search</param>
        /// <returns>First matching visitor or null</returns>
        public static VisitorControllerBase FindVisitorInRadius(Vector3 position, float radius, ICollection<VisitorControllerBase> excludeList = null)
        {
            var visitors = VisitorRegistry.All;
            Vector2 pos2D = new Vector2(position.x, position.y);

            foreach (var visitor in visitors)
            {
                if (visitor == null) continue;
                if (!IsVisitorTargetable(visitor)) continue;
                if (excludeList != null && excludeList.Contains(visitor)) continue;

                Vector2 visitorPos2D = new Vector2(visitor.transform.position.x, visitor.transform.position.y);
                float distance = Vector2.Distance(visitorPos2D, pos2D);

                if (distance <= radius)
                {
                    return visitor;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds all visitors within a radius of a position that are in active states.
        /// </summary>
        public static List<VisitorControllerBase> FindAllVisitorsInRadius(Vector3 position, float radius, ICollection<VisitorControllerBase> excludeList = null)
        {
            var result = new List<VisitorControllerBase>();
            var visitors = VisitorRegistry.All;
            Vector2 pos2D = new Vector2(position.x, position.y);

            foreach (var visitor in visitors)
            {
                if (visitor == null) continue;
                if (!IsVisitorTargetable(visitor)) continue;
                if (excludeList != null && excludeList.Contains(visitor)) continue;

                Vector2 visitorPos2D = new Vector2(visitor.transform.position.x, visitor.transform.position.y);
                float distance = Vector2.Distance(visitorPos2D, pos2D);

                if (distance <= radius)
                {
                    result.Add(visitor);
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if a visitor is in a state that can be targeted by powers.
        /// </summary>
        public static bool IsVisitorTargetable(VisitorControllerBase visitor)
        {
            if (visitor == null) return false;

            var state = visitor.State;
            return state != VisitorControllerBase.VisitorState.Consumed &&
                   state != VisitorControllerBase.VisitorState.Escaping &&
                   state != VisitorControllerBase.VisitorState.Grabbed;
        }

        #endregion

        #region Animator Control

        /// <summary>
        /// Sets an animator to a specific frame by calculating normalized time.
        /// Stops the animator and forces the frame update.
        /// </summary>
        /// <param name="animator">The animator to control</param>
        /// <param name="frame">Target frame number (0-based)</param>
        /// <param name="totalFrames">Total frames in the animation</param>
        /// <param name="stateName">Optional state name to play (uses current state if null)</param>
        public static void SetAnimatorFrame(Animator animator, int frame, int totalFrames, string stateName = null)
        {
            if (animator == null) return;

            // Clamp to 0.999 to avoid looping back to frame 0 when at last frame
            float normalizedTime = Mathf.Min(frame / (float)totalFrames, 0.999f);

            animator.speed = 0f;

            if (stateName != null)
            {
                animator.Play(stateName, 0, normalizedTime);
            }
            else
            {
                var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                animator.Play(stateInfo.fullPathHash, 0, normalizedTime);
            }

            animator.Update(0f);
        }

        #endregion

        #region Tile Effects

        /// <summary>
        /// Applies a shake effect to a collection of game objects.
        /// </summary>
        /// <param name="objects">Objects to shake</param>
        /// <param name="originalPositions">Dictionary of original positions</param>
        /// <param name="intensity">Shake intensity (default 0.03)</param>
        public static void ApplyShakeEffect(IEnumerable<GameObject> objects, Dictionary<GameObject, Vector3> originalPositions, float intensity = 0.03f)
        {
            foreach (var obj in objects)
            {
                if (obj == null) continue;

                if (originalPositions.TryGetValue(obj, out Vector3 originalPos))
                {
                    float offsetX = (RandomManager.Value - 0.5f) * 2f * intensity;
                    float offsetY = (RandomManager.Value - 0.5f) * 2f * intensity;
                    obj.transform.position = originalPos + new Vector3(offsetX, offsetY, 0f);
                }
            }
        }

        /// <summary>
        /// Resets objects to their original positions.
        /// </summary>
        public static void ResetToOriginalPositions(IEnumerable<GameObject> objects, Dictionary<GameObject, Vector3> originalPositions)
        {
            foreach (var obj in objects)
            {
                if (obj != null && originalPositions.TryGetValue(obj, out Vector3 originalPos))
                {
                    obj.transform.position = originalPos;
                }
            }
        }

        /// <summary>
        /// Finds path/node tiles within a radius using physics overlap.
        /// </summary>
        /// <param name="center">Center position</param>
        /// <param name="radius">Search radius</param>
        /// <param name="tiles">Output list of found tiles</param>
        /// <param name="originalPositions">Output dictionary of original positions</param>
        public static void FindPathTilesInRadius(Vector3 center, float radius, List<GameObject> tiles, Dictionary<GameObject, Vector3> originalPositions)
        {
            tiles.Clear();
            originalPositions.Clear();

            Collider[] colliders = Physics.OverlapSphere(center, radius + 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            Vector2 center2D = new Vector2(center.x, center.y);

            foreach (var collider in colliders)
            {
                string objName = collider.gameObject.name;

                // Include path tiles: WorldTile_. (dot = path), WorldTile_H, WorldTile_N, etc. but NOT WorldTile_# (walls)
                bool isPathTile = objName.StartsWith("WorldTile_") && !objName.StartsWith("WorldTile_#");

                // Include node columns/cylinders
                bool isNode = collider.CompareTag("MazeNode") ||
                              objName.Contains("NodeColumn") ||
                              objName.Contains("NodeCylinder");

                if (isPathTile || isNode)
                {
                    Vector2 tilePos2D = new Vector2(collider.transform.position.x, collider.transform.position.y);
                    float distFromCenter = Vector2.Distance(tilePos2D, center2D);

                    if (distFromCenter <= radius)
                    {
                        tiles.Add(collider.gameObject);
                        originalPositions[collider.gameObject] = collider.transform.position;
                    }
                }
            }
        }

        /// <summary>
        /// Finds wall tiles within a radius using physics overlap.
        /// </summary>
        public static void FindWallTilesInRadius(Vector3 center, float radius, List<GameObject> walls, Dictionary<GameObject, Vector3> originalPositions)
        {
            walls.Clear();
            originalPositions.Clear();

            Collider[] colliders = Physics.OverlapSphere(center, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            foreach (var collider in colliders)
            {
                if (collider.gameObject.name.StartsWith("WorldTile_#"))
                {
                    walls.Add(collider.gameObject);
                    originalPositions[collider.gameObject] = collider.transform.position;
                }
            }
        }

        #endregion

        #region Visitor Visibility

        /// <summary>
        /// Sets the visibility of a visitor by enabling/disabling all renderers.
        /// </summary>
        public static void SetVisitorVisible(VisitorControllerBase visitor, bool visible)
        {
            if (visitor == null) return;

            var renderers = visitor.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                renderer.enabled = visible;
            }
        }

        #endregion

        #region Particle System Helpers

        /// <summary>
        /// Creates a basic particle system with common settings.
        /// </summary>
        /// <param name="parent">Parent game object</param>
        /// <param name="name">Name of the particle system object</param>
        /// <param name="position">World position</param>
        /// <param name="emissionRate">Particles per second</param>
        /// <param name="startSize">Particle start size range</param>
        /// <param name="startSpeed">Particle start speed range</param>
        /// <param name="lifetime">Particle lifetime range</param>
        /// <param name="color1">First color for gradient</param>
        /// <param name="color2">Second color for gradient</param>
        /// <param name="shapeRadius">Emission shape radius</param>
        /// <returns>The created ParticleSystem</returns>
        public static ParticleSystem CreateBasicParticleSystem(
            GameObject parent,
            string name,
            Vector3 position,
            float emissionRate,
            Vector2 startSize,
            Vector2 startSpeed,
            Vector2 lifetime,
            Color color1,
            Color color2,
            float shapeRadius)
        {
            GameObject particleObj = new GameObject(name);
            particleObj.transform.SetParent(parent.transform);
            particleObj.transform.position = position;

            ParticleSystem particles = particleObj.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(startSpeed.x, startSpeed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(startSize.x, startSize.y);
            main.startColor = new ParticleSystem.MinMaxGradient(color1, color2);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 500;

            var emission = particles.emission;
            emission.rateOverTime = emissionRate;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = shapeRadius;

            // Size over lifetime - fade out
            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Use default particle material
            var renderer = particleObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));

            particles.Play();

            return particles;
        }

        /// <summary>
        /// Safely destroys a particle system and its game object.
        /// </summary>
        public static void DestroyParticleSystem(ref ParticleSystem particles)
        {
            if (particles != null)
            {
                particles.Stop();
                Object.Destroy(particles.gameObject);
                particles = null;
            }
        }

        #endregion
    }
}
