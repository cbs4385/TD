using UnityEngine;
using FaeMaze.Systems;

namespace FaeMaze.Props
{
    /// <summary>
    /// Manages warm amber/gold streamer effects around visitors mesmerized by lanterns.
    /// Each streamer is a trail renderer + point light that flies erratically within
    /// a 1-unit radius of the visitor, similar to FairyRingSphere but lantern-themed.
    /// </summary>
    public class LanternStreamerEffect : MonoBehaviour
    {
        private const string STREAMER_TAG = "LanternStreamer";
        private const int STREAMER_COUNT = 3;
        private const float CONSTRAINT_RADIUS = 1.0f;
        private const float MOVEMENT_SPEED = 1.5f;
        private const float DIRECTION_CHANGE_INTERVAL = 0.3f;
        private const float TRAIL_DURATION = 1.5f;
        private const float TRAIL_START_WIDTH = 0.10f;
        private const float TRAIL_END_WIDTH = 0.02f;
        private const float LIGHT_RANGE = 0.8f;
        private const float LIGHT_INTENSITY = 3f;
        private const float Z_MIN = -0.5f;
        private const float Z_MAX = 0f;

        // Warm amber/gold hues for lantern-themed colors
        private static readonly float[] WarmHues = new float[]
        {
            0.12f,  // Warm yellow
            0.08f,  // Amber
            0.10f,  // Gold
            0.06f,  // Orange-gold
        };

        private Vector3 currentVelocity;
        private Vector3 targetVelocity;
        private float nextDirectionChangeTime;
        private float cycleDuration;
        private float colorTimeOffset;

        private TrailRenderer trailRenderer;
        private Light streamerLight;

        private void OnEnable()
        {
            cycleDuration = RandomManager.Range(1.0f, 2.5f);
            colorTimeOffset = RandomManager.Value * 100f;
            nextDirectionChangeTime = Time.time + DIRECTION_CHANGE_INTERVAL;

            EnsureTrailRenderer();
            EnsureLight();

            targetVelocity = GetRandomVelocity();
            currentVelocity = targetVelocity;
        }

        private void Update()
        {
            UpdateColor();

            if (Application.isPlaying)
            {
                UpdateMovement();
            }
        }

        private void UpdateMovement()
        {
            if (Time.time >= nextDirectionChangeTime)
            {
                targetVelocity = GetRandomVelocity();
                nextDirectionChangeTime = Time.time + DIRECTION_CHANGE_INTERVAL + RandomManager.Range(-0.1f, 0.1f);
            }

            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, Time.deltaTime * 3f);

            Vector3 newLocalPos = transform.localPosition + currentVelocity * Time.deltaTime;
            newLocalPos = ConstrainToSphere(newLocalPos);
            transform.localPosition = newLocalPos;
        }

        private Vector3 GetRandomVelocity()
        {
            Vector3 randomDir = new Vector3(
                RandomManager.Range(-1f, 1f),
                RandomManager.Range(-1f, 1f),
                RandomManager.Range(-0.3f, 0.3f)
            ).normalized;

            return randomDir * MOVEMENT_SPEED;
        }

        private Vector3 ConstrainToSphere(Vector3 localPos)
        {
            // Constrain XY to circle of CONSTRAINT_RADIUS
            Vector2 xy = new Vector2(localPos.x, localPos.y);
            if (xy.magnitude > CONSTRAINT_RADIUS)
            {
                xy = xy.normalized * CONSTRAINT_RADIUS;
                localPos.x = xy.x;
                localPos.y = xy.y;

                // Bounce
                Vector2 velXY = new Vector2(currentVelocity.x, currentVelocity.y);
                velXY = -velXY;
                currentVelocity.x = velXY.x;
                currentVelocity.y = velXY.y;
                targetVelocity = GetRandomVelocity();
            }

            // Constrain Z (above ground, below camera in game's -Z up system)
            localPos.z = Mathf.Clamp(localPos.z, Z_MIN, Z_MAX);
            if (localPos.z <= Z_MIN + 0.01f || localPos.z >= Z_MAX - 0.01f)
            {
                currentVelocity.z = -currentVelocity.z;
                targetVelocity = GetRandomVelocity();
            }

            return localPos;
        }

        private void UpdateColor()
        {
            float t = Time.time + colorTimeOffset;
            Color currentColor = EvaluateWarmCycle(t);

            if (trailRenderer != null)
            {
                Color brightColor = currentColor * 2f;

                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(brightColor, 0f),
                        new GradientColorKey(currentColor, 0.4f),
                        new GradientColorKey(currentColor * 0.5f, 1f)
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0.8f, 0.2f),
                        new GradientAlphaKey(0.4f, 0.5f),
                        new GradientAlphaKey(0f, 1f)
                    }
                );
                trailRenderer.colorGradient = gradient;
                trailRenderer.startColor = brightColor;
                trailRenderer.endColor = new Color(currentColor.r * 0.5f, currentColor.g * 0.5f, currentColor.b * 0.5f, 0f);
            }

            if (streamerLight != null)
            {
                streamerLight.color = currentColor;
            }
        }

        private Color EvaluateWarmCycle(float timeSeconds)
        {
            int colorCount = WarmHues.Length;
            float holdDuration = cycleDuration / colorCount;
            float totalCycleDuration = colorCount * holdDuration;

            if (holdDuration <= 0.0001f) return Color.HSVToRGB(WarmHues[0], 0.9f, 1f);

            float cycleTime = Mathf.Repeat(timeSeconds, totalCycleDuration);
            int currentColorIndex = Mathf.Clamp(Mathf.FloorToInt(cycleTime / holdDuration), 0, colorCount - 1);
            float timeInSegment = cycleTime - (currentColorIndex * holdDuration);
            float segmentProgress = timeInSegment / holdDuration;

            float transitionT = 0f;
            if (segmentProgress > 0.5f)
            {
                float transitionProgress = (segmentProgress - 0.5f) * 2f;
                transitionT = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * transitionProgress);
            }

            float fromH = WarmHues[currentColorIndex];
            float toH = WarmHues[(currentColorIndex + 1) % colorCount];

            float hDiff = toH - fromH;
            if (hDiff > 0.5f) hDiff -= 1f;
            else if (hDiff < -0.5f) hDiff += 1f;
            float h = fromH + hDiff * transitionT;
            if (h < 0f) h += 1f;
            if (h > 1f) h -= 1f;

            // High saturation, warm glow
            return Color.HSVToRGB(h, 0.9f, 1.0f);
        }

        private void EnsureTrailRenderer()
        {
            trailRenderer = GetComponent<TrailRenderer>();
            if (trailRenderer == null)
            {
                trailRenderer = gameObject.AddComponent<TrailRenderer>();
            }

            trailRenderer.time = TRAIL_DURATION;
            trailRenderer.startWidth = TRAIL_START_WIDTH;
            trailRenderer.endWidth = TRAIL_END_WIDTH;
            trailRenderer.minVertexDistance = 0.01f;
            trailRenderer.emitting = true;
            trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trailRenderer.receiveShadows = false;
            trailRenderer.sortingOrder = 100;
            trailRenderer.allowOcclusionWhenDynamic = false;

            SetupTrailMaterial();
        }

        private void SetupTrailMaterial()
        {
            if (trailRenderer == null) return;

            var trailMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (trailMaterial.shader == null || trailMaterial.shader.name == "Hidden/InternalErrorShader")
            {
                trailMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
            }
            if (trailMaterial.shader == null || trailMaterial.shader.name == "Hidden/InternalErrorShader")
            {
                trailMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            trailMaterial.SetFloat("_Surface", 1);
            trailMaterial.SetFloat("_Blend", 4);
            trailMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            trailMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            trailMaterial.SetInt("_ZWrite", 0);
            trailMaterial.renderQueue = 3500;
            trailMaterial.SetColor("_BaseColor", Color.white);
            trailMaterial.SetColor("_Color", Color.white);
            trailMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            trailMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");

            trailRenderer.material = trailMaterial;
        }

        private void EnsureLight()
        {
            streamerLight = GetComponent<Light>();
            if (streamerLight == null)
            {
                streamerLight = gameObject.AddComponent<Light>();
            }

            streamerLight.type = LightType.Point;
            streamerLight.range = LIGHT_RANGE;
            streamerLight.intensity = LIGHT_INTENSITY;
            streamerLight.shadows = LightShadows.None;
            streamerLight.renderMode = LightRenderMode.Auto;
        }

        /// <summary>
        /// Spawns lantern streamer effects as children of the given visitor transform.
        /// </summary>
        public static void SpawnStreamers(Transform visitorTransform)
        {
            if (visitorTransform == null) return;

            // Don't double-spawn
            if (visitorTransform.GetComponentInChildren<LanternStreamerEffect>() != null) return;

            for (int i = 0; i < STREAMER_COUNT; i++)
            {
                GameObject streamerObj = new GameObject($"LanternStreamer_{i}");
                streamerObj.transform.SetParent(visitorTransform);

                // Start at a random position within the constraint sphere
                float angle = RandomManager.Range(0f, Mathf.PI * 2f);
                float radius = RandomManager.Range(0.2f, CONSTRAINT_RADIUS * 0.8f);
                float z = RandomManager.Range(Z_MIN * 0.5f, Z_MAX);
                streamerObj.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    z
                );

                streamerObj.AddComponent<LanternStreamerEffect>();
            }
        }

        /// <summary>
        /// Destroys all lantern streamer effects on the given visitor transform.
        /// </summary>
        public static void DestroyStreamers(Transform visitorTransform)
        {
            if (visitorTransform == null) return;

            var streamers = visitorTransform.GetComponentsInChildren<LanternStreamerEffect>();
            foreach (var streamer in streamers)
            {
                if (streamer != null && streamer.gameObject != null)
                {
                    Destroy(streamer.gameObject);
                }
            }
        }
    }
}
