using UnityEngine;

namespace FaeMaze.Debug
{
    /// <summary>
    /// Debug utility wrapper for Unity's Debug class.
    /// Provides a centralized logging interface with optional conditional compilation.
    /// </summary>
    public static class Debug
    {
        /// <summary>
        /// Logs a message to the Unity Console.
        /// </summary>
        public static void Log(object message)
        {
            UnityEngine.Debug.Log(message);
        }

        /// <summary>
        /// Logs a message to the Unity Console with a context object.
        /// </summary>
        public static void Log(object message, Object context)
        {
            UnityEngine.Debug.Log(message, context);
        }

        /// <summary>
        /// Logs a warning message to the Unity Console.
        /// </summary>
        public static void LogWarning(object message)
        {
            UnityEngine.Debug.LogWarning(message);
        }

        /// <summary>
        /// Logs a warning message to the Unity Console with a context object.
        /// </summary>
        public static void LogWarning(object message, Object context)
        {
            UnityEngine.Debug.LogWarning(message, context);
        }

        /// <summary>
        /// Logs an error message to the Unity Console.
        /// </summary>
        public static void LogError(object message)
        {
            UnityEngine.Debug.LogError(message);
        }

        /// <summary>
        /// Logs an error message to the Unity Console with a context object.
        /// </summary>
        public static void LogError(object message, Object context)
        {
            UnityEngine.Debug.LogError(message, context);
        }

        /// <summary>
        /// Logs an exception to the Unity Console.
        /// </summary>
        public static void LogException(System.Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
        }

        /// <summary>
        /// Logs an exception to the Unity Console with a context object.
        /// </summary>
        public static void LogException(System.Exception exception, Object context)
        {
            UnityEngine.Debug.LogException(exception, context);
        }

        /// <summary>
        /// Logs an assertion message to the Unity Console.
        /// </summary>
        public static void LogAssertion(object message)
        {
            UnityEngine.Debug.LogAssertion(message);
        }

        /// <summary>
        /// Logs an assertion message to the Unity Console with a context object.
        /// </summary>
        public static void LogAssertion(object message, Object context)
        {
            UnityEngine.Debug.LogAssertion(message, context);
        }

        /// <summary>
        /// Logs a formatted message to the Unity Console.
        /// </summary>
        public static void LogFormat(string format, params object[] args)
        {
            UnityEngine.Debug.LogFormat(format, args);
        }

        /// <summary>
        /// Logs a formatted warning message to the Unity Console.
        /// </summary>
        public static void LogWarningFormat(string format, params object[] args)
        {
            UnityEngine.Debug.LogWarningFormat(format, args);
        }

        /// <summary>
        /// Logs a formatted error message to the Unity Console.
        /// </summary>
        public static void LogErrorFormat(string format, params object[] args)
        {
            UnityEngine.Debug.LogErrorFormat(format, args);
        }
    }
}
