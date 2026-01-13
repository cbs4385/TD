using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForestMaze
{
    /// <summary>
    /// Represents a cubic Bezier curve with utilities for evaluation, sampling, and tangent calculation.
    /// Supports 2-3 control points placed equidistantly along the curve on alternating sides.
    /// </summary>
    public class BezierCurve
    {
        /// <summary>Start point of the curve</summary>
        public Vector2 P0 { get; private set; }

        /// <summary>First control point</summary>
        public Vector2 P1 { get; private set; }

        /// <summary>Second control point (for cubic curves)</summary>
        public Vector2 P2 { get; private set; }

        /// <summary>End point of the curve</summary>
        public Vector2 P3 { get; private set; }

        /// <summary>Whether this is a cubic (true) or quadratic (false) Bezier</summary>
        public bool IsCubic { get; private set; }

        /// <summary>
        /// Creates a cubic Bezier curve with 2 control points.
        /// </summary>
        public BezierCurve(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
        {
            P0 = start;
            P1 = control1;
            P2 = control2;
            P3 = end;
            IsCubic = true;
        }

        /// <summary>
        /// Creates a quadratic Bezier curve with 1 control point (internally converted to cubic).
        /// </summary>
        public BezierCurve(Vector2 start, Vector2 control, Vector2 end)
        {
            // Convert quadratic to cubic for uniform handling
            // Cubic control points: P1 = P0 + 2/3*(C - P0), P2 = P3 + 2/3*(C - P3)
            P0 = start;
            P1 = start + (2f / 3f) * (control - start);
            P2 = end + (2f / 3f) * (control - end);
            P3 = end;
            IsCubic = true;
        }

        /// <summary>
        /// Evaluates the curve at parameter t (0 to 1).
        /// </summary>
        public Vector2 Evaluate(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            // Cubic Bezier: B(t) = (1-t)³P0 + 3(1-t)²tP1 + 3(1-t)t²P2 + t³P3
            return uuu * P0 + 3f * uu * t * P1 + 3f * u * tt * P2 + ttt * P3;
        }

        /// <summary>
        /// Calculates the tangent (first derivative) at parameter t.
        /// </summary>
        public Vector2 Tangent(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;

            // First derivative: B'(t) = 3(1-t)²(P1-P0) + 6(1-t)t(P2-P1) + 3t²(P3-P2)
            Vector2 tangent = 3f * u * u * (P1 - P0) + 6f * u * t * (P2 - P1) + 3f * t * t * (P3 - P2);
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
        /// Approximates the arc length of the curve using numerical integration.
        /// </summary>
        public float ApproximateLength(int segments = 32)
        {
            float length = 0f;
            Vector2 prev = P0;

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
        /// Samples the curve at uniform arc-length intervals.
        /// Returns points spaced approximately 'spacing' units apart.
        /// </summary>
        public List<Vector2> SampleByArcLength(float spacing, float maxGap = 0.25f)
        {
            var points = new List<Vector2>();
            float length = ApproximateLength();

            if (length < spacing)
            {
                points.Add(P0);
                points.Add(P3);
                return points;
            }

            // Use finer spacing to ensure max gap is respected
            float actualSpacing = Mathf.Min(spacing, maxGap);
            int numSamples = Mathf.Max(2, Mathf.CeilToInt(length / actualSpacing) + 1);

            for (int i = 0; i <= numSamples; i++)
            {
                float t = (float)i / numSamples;
                points.Add(Evaluate(t));
            }

            return points;
        }

        /// <summary>
        /// Samples the curve returning both positions and tangent directions.
        /// Useful for wall placement perpendicular to the path.
        /// </summary>
        public List<(Vector2 position, Vector2 tangent, Vector2 normal)> SampleWithOrientations(float spacing, float maxGap = 0.25f)
        {
            var samples = new List<(Vector2, Vector2, Vector2)>();
            float length = ApproximateLength();

            float actualSpacing = Mathf.Min(spacing, maxGap);
            int numSamples = Mathf.Max(2, Mathf.CeilToInt(length / actualSpacing) + 1);

            for (int i = 0; i <= numSamples; i++)
            {
                float t = (float)i / numSamples;
                samples.Add((Evaluate(t), Tangent(t), Normal(t)));
            }

            return samples;
        }

        /// <summary>
        /// Calculates the maximum curvature angle (deviation from straight line) along the curve.
        /// </summary>
        public float MaxCurvatureAngle(int segments = 16)
        {
            float maxAngle = 0f;
            Vector2 prevTangent = Tangent(0);

            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector2 currentTangent = Tangent(t);

                float dot = Mathf.Clamp(Vector2.Dot(prevTangent, currentTangent), -1f, 1f);
                float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
                maxAngle = Mathf.Max(maxAngle, angle);

                prevTangent = currentTangent;
            }

            return maxAngle;
        }

        /// <summary>
        /// Converts the Bezier curve to a polyline with the specified number of segments.
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

        /// <summary>
        /// Finds the parameter t at which the curve is closest to a given point.
        /// </summary>
        public float ClosestParameter(Vector2 point, int iterations = 16)
        {
            float bestT = 0f;
            float bestDistSq = float.MaxValue;

            // Coarse search
            for (int i = 0; i <= iterations; i++)
            {
                float t = (float)i / iterations;
                float distSq = (Evaluate(t) - point).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestT = t;
                }
            }

            // Refine with binary search
            float step = 1f / iterations / 2f;
            for (int i = 0; i < 8; i++)
            {
                float tLeft = Mathf.Max(0, bestT - step);
                float tRight = Mathf.Min(1, bestT + step);

                float distLeft = (Evaluate(tLeft) - point).sqrMagnitude;
                float distRight = (Evaluate(tRight) - point).sqrMagnitude;

                if (distLeft < bestDistSq)
                {
                    bestDistSq = distLeft;
                    bestT = tLeft;
                }
                if (distRight < bestDistSq)
                {
                    bestDistSq = distRight;
                    bestT = tRight;
                }

                step *= 0.5f;
            }

            return bestT;
        }

        /// <summary>
        /// Calculates the minimum distance from a point to the curve.
        /// </summary>
        public float DistanceToPoint(Vector2 point)
        {
            float t = ClosestParameter(point);
            return Vector2.Distance(Evaluate(t), point);
        }
    }

    /// <summary>
    /// Factory for creating Bezier curves with gentle curvature for maze edges.
    /// </summary>
    public static class BezierCurveFactory
    {
        private const float MAX_CURVE_ANGLE = 15f;  // Maximum deviation in degrees
        private const float TYPICAL_MIN_ANGLE = 5f;  // Minimum typical angle
        private const float TYPICAL_MAX_ANGLE = 10f; // Maximum typical angle (2/3 of curves)

        /// <summary>
        /// Creates a gentle Bezier curve between two points with 2-3 control points.
        /// Control points are placed equidistantly on alternating sides.
        /// </summary>
        /// <param name="start">Start point of the curve</param>
        /// <param name="end">End point of the curve</param>
        /// <param name="random">Random generator for curve variation</param>
        /// <param name="numControlPoints">Number of control points (2 or 3)</param>
        /// <returns>A Bezier curve or null if invalid</returns>
        public static BezierCurve CreateGentleCurve(Vector2 start, Vector2 end, System.Random random, int numControlPoints = 2)
        {
            Vector2 direction = (end - start).normalized;
            float length = Vector2.Distance(start, end);

            if (length < 0.1f)
                return null;

            // Perpendicular direction for control point offset
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);

            // Determine if this should be a gentle curve (2/3 probability) or sharper (1/3)
            bool isGentleCurve = random.NextDouble() < 0.667;
            float minAngle = isGentleCurve ? TYPICAL_MIN_ANGLE : TYPICAL_MAX_ANGLE;
            float maxAngle = isGentleCurve ? TYPICAL_MAX_ANGLE : MAX_CURVE_ANGLE;

            // Generate curve angle within bounds
            float curveAngle = minAngle + (float)random.NextDouble() * (maxAngle - minAngle);

            // Convert angle to offset distance
            // For a gentle curve, offset ≈ length * tan(angle/2) at the midpoint
            float offsetMagnitude = length * Mathf.Tan(curveAngle * Mathf.Deg2Rad * 0.5f);

            // Random direction (left or right)
            int startSide = random.Next(2) == 0 ? 1 : -1;

            if (numControlPoints == 2)
            {
                // Two control points at 1/3 and 2/3 along the line, on alternating sides
                Vector2 p1Base = Vector2.Lerp(start, end, 1f / 3f);
                Vector2 p2Base = Vector2.Lerp(start, end, 2f / 3f);

                Vector2 control1 = p1Base + perpendicular * offsetMagnitude * startSide;
                Vector2 control2 = p2Base + perpendicular * offsetMagnitude * (-startSide);

                return new BezierCurve(start, control1, control2, end);
            }
            else // 3 control points - create as two connected quadratics (composite curve)
            {
                // For 3 control points, we'll use a cubic approximation
                // Place control points at 1/4, 1/2, and 3/4 positions
                Vector2 p1Base = Vector2.Lerp(start, end, 0.25f);
                Vector2 p2Base = Vector2.Lerp(start, end, 0.5f);
                Vector2 p3Base = Vector2.Lerp(start, end, 0.75f);

                // Alternate sides: side1, -side1, side1
                float offset1 = offsetMagnitude * startSide * 0.7f;
                float offset2 = offsetMagnitude * (-startSide) * 1.0f;
                float offset3 = offsetMagnitude * startSide * 0.7f;

                Vector2 control1 = p1Base + perpendicular * offset1;
                Vector2 control3 = p3Base + perpendicular * offset3;

                // For cubic Bezier, we use only 2 control points
                // Blend the 3 desired points into 2 cubic control points
                Vector2 cubicControl1 = control1;
                Vector2 cubicControl2 = control3;

                return new BezierCurve(start, cubicControl1, cubicControl2, end);
            }
        }

        /// <summary>
        /// Creates a curve that avoids specified positions by adjusting control points.
        /// </summary>
        public static BezierCurve CreateCurveAvoidingPositions(
            Vector2 start,
            Vector2 end,
            System.Random random,
            List<Vector2> avoidPositions,
            float avoidRadius,
            int maxAttempts = 10)
        {
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                int numControls = random.Next(2) == 0 ? 2 : 3;
                var curve = CreateGentleCurve(start, end, random, numControls);

                if (curve == null)
                    continue;

                bool valid = true;
                foreach (var pos in avoidPositions)
                {
                    if (curve.DistanceToPoint(pos) < avoidRadius)
                    {
                        valid = false;
                        break;
                    }
                }

                if (valid && curve.MaxCurvatureAngle() <= MAX_CURVE_ANGLE)
                    return curve;
            }

            // Fallback to straight line
            Vector2 midpoint = (start + end) * 0.5f;
            return new BezierCurve(start, midpoint, end);
        }

        /// <summary>
        /// Adjusts control points and optionally the endpoint to ensure crossing angle constraint.
        /// Used when edges cross to ensure gap ≤ 0.25 square units.
        /// </summary>
        /// <param name="curve">The curve to adjust</param>
        /// <param name="crossingPoint">Point where edges cross</param>
        /// <param name="otherEdgeTangent">Tangent of the other edge at crossing</param>
        /// <param name="minCrossingAngle">Minimum angle in degrees for crossing</param>
        /// <returns>Adjusted curve or null if impossible</returns>
        public static BezierCurve AdjustForCrossing(
            BezierCurve curve,
            Vector2 crossingPoint,
            Vector2 otherEdgeTangent,
            float minCrossingAngle = 45f)
        {
            float t = curve.ClosestParameter(crossingPoint);
            Vector2 currentTangent = curve.Tangent(t);

            // Calculate current crossing angle
            float dot = Mathf.Abs(Vector2.Dot(currentTangent, otherEdgeTangent));
            float currentAngle = Mathf.Acos(Mathf.Clamp(dot, 0, 1)) * Mathf.Rad2Deg;

            if (currentAngle >= minCrossingAngle)
                return curve; // Already valid

            // Need to adjust - rotate the control points to increase angle
            float neededRotation = (minCrossingAngle - currentAngle) * Mathf.Deg2Rad;

            // Calculate rotation direction (whichever gets us closer to perpendicular)
            float cross = currentTangent.x * otherEdgeTangent.y - currentTangent.y * otherEdgeTangent.x;
            float rotationSign = cross >= 0 ? 1f : -1f;

            // Rotate control points around curve midpoint
            Vector2 midpoint = curve.Evaluate(0.5f);
            float cos = Mathf.Cos(neededRotation * rotationSign);
            float sin = Mathf.Sin(neededRotation * rotationSign);

            Vector2 newP1 = RotateAround(curve.P1, midpoint, cos, sin);
            Vector2 newP2 = RotateAround(curve.P2, midpoint, cos, sin);

            return new BezierCurve(curve.P0, newP1, newP2, curve.P3);
        }

        /// <summary>
        /// Adjusts the endpoint of a curve (for frontier placement) to achieve better crossing angle.
        /// </summary>
        public static (BezierCurve curve, Vector2 newEndpoint) AdjustEndpointForCrossing(
            Vector2 start,
            Vector2 originalEnd,
            Vector2 crossingPoint,
            Vector2 otherEdgeTangent,
            System.Random random,
            float minCrossingAngle = 45f)
        {
            // Try adjusting the endpoint in various directions
            float originalLength = Vector2.Distance(start, originalEnd);
            Vector2 originalDir = (originalEnd - start).normalized;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                // Rotate the endpoint direction
                float rotation = (attempt + 1) * 15f * Mathf.Deg2Rad;
                float sign = attempt % 2 == 0 ? 1f : -1f;

                float cos = Mathf.Cos(rotation * sign);
                float sin = Mathf.Sin(rotation * sign);
                Vector2 newDir = new Vector2(
                    originalDir.x * cos - originalDir.y * sin,
                    originalDir.x * sin + originalDir.y * cos
                );

                Vector2 newEnd = start + newDir * originalLength;
                var curve = CreateGentleCurve(start, newEnd, random, 2);

                if (curve == null) continue;

                // Check crossing angle
                float t = curve.ClosestParameter(crossingPoint);
                Vector2 tangent = curve.Tangent(t);
                float dot = Mathf.Abs(Vector2.Dot(tangent, otherEdgeTangent));
                float angle = Mathf.Acos(Mathf.Clamp(dot, 0, 1)) * Mathf.Rad2Deg;

                if (angle >= minCrossingAngle)
                    return (curve, newEnd);
            }

            // Fallback - return original
            var fallbackCurve = CreateGentleCurve(start, originalEnd, random, 2);
            return (fallbackCurve, originalEnd);
        }

        private static Vector2 RotateAround(Vector2 point, Vector2 center, float cos, float sin)
        {
            Vector2 offset = point - center;
            return new Vector2(
                offset.x * cos - offset.y * sin + center.x,
                offset.x * sin + offset.y * cos + center.y
            );
        }

        /// <summary>
        /// Calculates the minimum crossing angle needed to keep gap below threshold.
        /// Gap = (edgeWidth / 2) / tan(angle/2), solve for angle given maxGap.
        /// </summary>
        public static float MinCrossingAngleForGap(float edgeWidth, float maxGapArea)
        {
            // For two edges of width w crossing at angle θ, the triangular gap area is:
            // A = (w/2)² / tan(θ/2)
            // Solving for θ: tan(θ/2) = (w/2)² / A
            // θ = 2 * atan((w/2)² / A)

            float halfWidth = edgeWidth * 0.5f;
            float tanHalfAngle = (halfWidth * halfWidth) / maxGapArea;
            float halfAngle = Mathf.Atan(tanHalfAngle);
            return 2f * halfAngle * Mathf.Rad2Deg;
        }
    }
}
