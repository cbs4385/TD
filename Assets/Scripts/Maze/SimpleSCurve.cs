using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForestMaze
{
    /// <summary>
    /// A simple curve that creates a nearly-straight path with a subtle sine-based S-curve.
    /// Much simpler than Bezier curves - just a straight line with small perpendicular offset.
    /// </summary>
    public class SimpleSCurve
    {
        public Vector2 Start { get; private set; }
        public Vector2 End { get; private set; }

        private Vector2 _direction;
        private Vector2 _perpendicular;
        private float _length;
        private float _amplitude;

        /// <summary>
        /// Creates a simple S-curve from start to end.
        /// </summary>
        /// <param name="start">Start point</param>
        /// <param name="end">End point</param>
        /// <param name="amplitude">Max perpendicular offset (default 0.3 for subtle curve)</param>
        public SimpleSCurve(Vector2 start, Vector2 end, float amplitude = 0.3f)
        {
            Start = start;
            End = end;
            _amplitude = amplitude;

            _direction = (end - start).normalized;
            _perpendicular = new Vector2(-_direction.y, _direction.x);
            _length = Vector2.Distance(start, end);
        }

        /// <summary>
        /// Evaluates the curve at parameter t (0 to 1).
        /// Creates a subtle S-curve using sine function.
        /// </summary>
        public Vector2 Evaluate(float t)
        {
            t = Mathf.Clamp01(t);

            // Linear interpolation along the main direction
            Vector2 linearPos = Vector2.Lerp(Start, End, t);

            // Add subtle sine offset perpendicular to direction
            // sin(t * PI) creates a single hump, peaking at t=0.5
            float sineOffset = _amplitude * Mathf.Sin(t * Mathf.PI);

            return linearPos + _perpendicular * sineOffset;
        }

        /// <summary>
        /// Calculates the tangent (direction) at parameter t.
        /// </summary>
        public Vector2 Tangent(float t)
        {
            t = Mathf.Clamp01(t);

            // Derivative of the curve
            // d/dt [start + t*(end-start) + amplitude*sin(t*PI)*perp]
            // = direction*length + amplitude*PI*cos(t*PI)*perp
            Vector2 tangent = _direction * _length + _perpendicular * (_amplitude * Mathf.PI * Mathf.Cos(t * Mathf.PI));

            return tangent.normalized;
        }

        /// <summary>
        /// Calculates the normal (perpendicular to tangent) at parameter t.
        /// </summary>
        public Vector2 Normal(float t)
        {
            Vector2 tan = Tangent(t);
            return new Vector2(-tan.y, tan.x);
        }

        /// <summary>
        /// Approximates the arc length of the curve.
        /// </summary>
        public float ApproximateLength(int segments = 16)
        {
            float length = 0f;
            Vector2 prev = Start;

            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector2 current = Evaluate(t);
                length += Vector2.Distance(prev, current);
                prev = current;
            }

            return length;
        }

        /// <summary>
        /// Samples the curve with position, tangent, and normal at each point.
        /// </summary>
        public List<CurveSample> SampleWithOrientations(float spacing, float maxGap = 0.25f)
        {
            var samples = new List<CurveSample>();
            float length = ApproximateLength();

            float actualSpacing = Mathf.Min(spacing, maxGap);
            int numSamples = Mathf.Max(2, Mathf.CeilToInt(length / actualSpacing) + 1);

            for (int i = 0; i <= numSamples; i++)
            {
                float t = (float)i / numSamples;
                samples.Add(new CurveSample(Evaluate(t), Tangent(t), Normal(t)));
            }

            return samples;
        }

        /// <summary>
        /// Converts the curve to a polyline with the specified number of segments.
        /// </summary>
        public List<Vector2> ToPolyline(int segments = 8)
        {
            var polyline = new List<Vector2>();

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                polyline.Add(Evaluate(t));
            }

            return polyline;
        }
    }
}
