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
    /// Static events for heart power effects that notify other systems when visitors are affected.
    /// </summary>
    public static class HeartPowerEvents
    {
        /// <summary>
        /// Invoked when a visitor is grabbed by HeartwardGrasp (Power 2).
        /// Parameter is the world position where the grab occurred.
        /// </summary>
        public static event System.Action<Vector3> OnVisitorGrabbedByGrasp;

        /// <summary>
        /// Invoked when a visitor is pushed/released by HeartwardGrasp (Power 2).
        /// Parameter is the world position where the push occurred.
        /// </summary>
        public static event System.Action<Vector3> OnVisitorPushedByGrasp;

        /// <summary>
        /// Invoked when a visitor is consumed by DevouringMaw (Power 3).
        /// Parameter is the world position where consumption occurred.
        /// </summary>
        public static event System.Action<Vector3> OnVisitorConsumedByMaw;

        /// <summary>
        /// Invoke the grab event from HeartwardGrasp.
        /// </summary>
        public static void NotifyVisitorGrabbedByGrasp(Vector3 position)
        {
            OnVisitorGrabbedByGrasp?.Invoke(position);
        }

        /// <summary>
        /// Invoke the push event from HeartwardGrasp.
        /// </summary>
        public static void NotifyVisitorPushedByGrasp(Vector3 position)
        {
            OnVisitorPushedByGrasp?.Invoke(position);
        }

        /// <summary>
        /// Invoke the consumption event from DevouringMaw.
        /// </summary>
        public static void NotifyVisitorConsumedByMaw(Vector3 position)
        {
            OnVisitorConsumedByMaw?.Invoke(position);
        }
    }
}
