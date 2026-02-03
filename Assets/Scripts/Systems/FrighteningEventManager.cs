using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Visitors;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Manages frightening events that can scare nearby visitors.
    /// Systems register active events, and visitors check for nearby events to become frightened.
    /// </summary>
    public class FrighteningEventManager : SingletonMonoBehaviour<FrighteningEventManager>
    {
        /// <summary>
        /// Gets the instance, auto-creating if not found (lazy singleton).
        /// </summary>
        public static FrighteningEventManager GetOrCreate()
        {
            if (Instance != null) return Instance;

            var found = FindFirstObjectByType<FrighteningEventManager>();
            if (found != null) return found;

            // Auto-create if not found
            GameObject go = new GameObject("FrighteningEventManager");
            return go.AddComponent<FrighteningEventManager>();
        }

        #region Event Types

        /// <summary>
        /// Types of frightening events that can scare visitors
        /// </summary>
        public enum EventType
        {
            PukaDrowning,       // Puka actively drowning a visitor
            HeartTongue,        // Heart tongue reaching/grabbing
            HeartwardGrasp,     // HeartwardGrasp power active
            DevouringMaw,       // DevouringMaw power active
            WispLure            // Wisp actively luring another visitor
        }

        /// <summary>
        /// Represents an active frightening event
        /// </summary>
        public class FrighteningEvent
        {
            public EventType Type;
            public Vector3 Position;
            public float Radius;
            public object Source;  // Reference to the source object
            public float StartTime;

            public FrighteningEvent(EventType type, Vector3 position, float radius, object source)
            {
                Type = type;
                Position = position;
                Radius = radius;
                Source = source;
                StartTime = Time.time;
            }
        }

        #endregion

        #region Configuration

        [Header("Detection Settings")]
        [SerializeField]
        [Tooltip("Default radius for frightening events if not specified")]
        private float defaultEventRadius = 5f;

        [SerializeField]
        [Tooltip("How often visitors check for frightening events (seconds)")]
        private float checkInterval = 0.25f;

        [Header("Event-Specific Radii")]
        [SerializeField]
        private float pukaDrowningRadius = 4f;

        [SerializeField]
        private float heartTongueRadius = 5f;

        [SerializeField]
        private float heartwardGraspRadius = 5f;

        [SerializeField]
        private float devouringMawRadius = 6f;

        [SerializeField]
        private float wispLureRadius = 3f;

        #endregion

        #region Private Fields

        private List<FrighteningEvent> activeEvents = new List<FrighteningEvent>();
        private float lastCheckTime;

        #endregion

        #region Properties

        /// <summary>Gets all currently active frightening events</summary>
        public IReadOnlyList<FrighteningEvent> ActiveEvents => activeEvents;

        /// <summary>Check interval for visitor detection</summary>
        public float CheckInterval => checkInterval;

        #endregion

        #region Unity Lifecycle

        // Singleton lifecycle handled by base class

        #endregion

        #region Event Registration

        /// <summary>
        /// Registers a frightening event at the specified position.
        /// Returns a handle that should be used to unregister the event.
        /// </summary>
        public FrighteningEvent RegisterEvent(EventType type, Vector3 position, object source, float? customRadius = null)
        {
            float radius = customRadius ?? GetDefaultRadiusForType(type);
            var evt = new FrighteningEvent(type, position, radius, source);
            activeEvents.Add(evt);
            return evt;
        }

        /// <summary>
        /// Updates the position of an existing event.
        /// </summary>
        public void UpdateEventPosition(FrighteningEvent evt, Vector3 newPosition)
        {
            if (evt != null && activeEvents.Contains(evt))
            {
                evt.Position = newPosition;
            }
        }

        /// <summary>
        /// Unregisters a frightening event.
        /// </summary>
        public void UnregisterEvent(FrighteningEvent evt)
        {
            if (evt != null)
            {
                activeEvents.Remove(evt);
            }
        }

        /// <summary>
        /// Unregisters all events from a specific source.
        /// </summary>
        public void UnregisterAllFromSource(object source)
        {
            activeEvents.RemoveAll(e => e.Source == source);
        }

        /// <summary>
        /// Gets the default radius for a given event type.
        /// </summary>
        private float GetDefaultRadiusForType(EventType type)
        {
            return type switch
            {
                EventType.PukaDrowning => pukaDrowningRadius,
                EventType.HeartTongue => heartTongueRadius,
                EventType.HeartwardGrasp => heartwardGraspRadius,
                EventType.DevouringMaw => devouringMawRadius,
                EventType.WispLure => wispLureRadius,
                _ => defaultEventRadius
            };
        }

        #endregion

        #region Visitor Checking

        /// <summary>
        /// Checks if a visitor at the given position can see any frightening events.
        /// Returns the nearest event if found, null otherwise.
        /// </summary>
        public FrighteningEvent GetNearestVisibleEvent(Vector3 visitorPosition, VisitorControllerBase excludeIfSource = null)
        {
            FrighteningEvent nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var evt in activeEvents)
            {
                // Don't frighten the visitor being affected by the event
                if (excludeIfSource != null && ReferenceEquals(evt.Source, excludeIfSource))
                    continue;

                // Check distance in XY plane (game uses -Z as up)
                Vector2 visitorXY = new Vector2(visitorPosition.x, visitorPosition.y);
                Vector2 eventXY = new Vector2(evt.Position.x, evt.Position.y);
                float distance = Vector2.Distance(visitorXY, eventXY);

                if (distance <= evt.Radius && distance < nearestDistance)
                {
                    // Optional: Add line-of-sight check here if walls should block visibility
                    // For now, we assume events are visible if within radius

                    nearestDistance = distance;
                    nearest = evt;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Frightens all visitors within range of active events.
        /// Called by visitors during their update cycle.
        /// </summary>
        public void CheckAndFrightenVisitor(VisitorControllerBase visitor)
        {
            if (visitor == null || activeEvents.Count == 0)
                return;

            // Don't frighten visitors that are already in certain states
            // Note: Drowning state was consolidated into Escaping state
            if (visitor.State == VisitorControllerBase.VisitorState.Consumed ||
                visitor.State == VisitorControllerBase.VisitorState.Escaping ||
                visitor.State == VisitorControllerBase.VisitorState.Grabbed ||
                visitor.State == VisitorControllerBase.VisitorState.Frightened)
            {
                return;
            }

            var nearestEvent = GetNearestVisibleEvent(visitor.transform.position, visitor);
            if (nearestEvent != null)
            {
                // Frighten the visitor, fleeing from the event
                visitor.SetFrightened(nearestEvent.Position);
            }
        }

        #endregion

        #region Debug

        private void OnDrawGizmosSelected()
        {
            if (activeEvents == null || activeEvents.Count == 0)
                return;

            foreach (var evt in activeEvents)
            {
                // Draw event radius
                Gizmos.color = evt.Type switch
                {
                    EventType.PukaDrowning => new Color(0f, 0.5f, 1f, 0.3f),
                    EventType.HeartTongue => new Color(1f, 0f, 0f, 0.3f),
                    EventType.HeartwardGrasp => new Color(0.5f, 0f, 0.5f, 0.3f),
                    EventType.DevouringMaw => new Color(0.8f, 0f, 0.2f, 0.3f),
                    EventType.WispLure => new Color(0.2f, 1f, 0.2f, 0.3f),
                    _ => new Color(1f, 1f, 0f, 0.3f)
                };

                Gizmos.DrawWireSphere(evt.Position, evt.Radius);

                // Draw center point
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(evt.Position, 0.2f);
            }
        }

        #endregion
    }
}
