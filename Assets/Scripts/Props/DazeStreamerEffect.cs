using UnityEngine;
using FaeMaze.Systems;

namespace FaeMaze.Props
{
    /// <summary>
    /// Manages grey streamer effects around dazed visitors.
    /// Each streamer is a trail renderer + point light that flies erratically within
    /// a 1-unit radius of the visitor, similar to LanternStreamerEffect but grey-themed.
    /// </summary>
    public class DazeStreamerEffect : MonoBehaviour
    {
        private const int STREAMER_COUNT = 3;
        private const float CONSTRAINT_RADIUS = 1.0f;
        private const float MOVEMENT_SPEED = 1.5f;
        private const float DIRECTION_CHANGE_INTERVAL = 0.3f;
        private const float TRAIL_DURATION = 1.5f;
        private const float TRAIL_START_WIDTH = 0.10f;
        private const float TRAIL_END_WIDTH = 0.02f;
        private const float LIGHT_RANGE = 0.8f;
        private const float LIGHT_INTENSITY = 2f;
        private const float Z_MIN = -0.5f;
        private const float Z_MAX = 0f;

        // Grey brightness values to cycle through
        private static readonly float[] GreyValues = new float[]
        {
            0.7f,   // Light grey
            0.5f,   // Medium grey
            0.8f,   // Silver
            0.35f,  // Dark grey
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
            Vector2 xy = new Vector2(localPos.x, localPos.y);
            if (xy.magnitude > CONSTRAINT_RADIUS)
            {
                xy = xy.normalized * CONSTRAINT_RADIUS;
                localPos.x = xy.x;
                localPos.y = xy.y;

                Vector2 velXY = new Vector2(currentVelocity.x, currentVelocity.y);
                velXY = -velXY;
                currentVelocity.x = velXY.x;
                currentVelocity.y = velXY.y;
                targetVelocity = GetRandomVelocity();
            }

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
            Color currentColor = EvaluateGreyCycle(t);

            if (trailRenderer != null)
            {
                Color brightColor = currentColor * 1.5f;

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

        private Color EvaluateGreyCycle(float timeSeconds)
        {
            int colorCount = GreyValues.Length;
            float holdDuration = cycleDuration / colorCount;
            float totalCycleDuration = colorCount * holdDuration;

            if (holdDuration <= 0.0001f) return new Color(GreyValues[0], GreyValues[0], GreyValues[0]);

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

            float fromV = GreyValues[currentColorIndex];
            float toV = GreyValues[(currentColorIndex + 1) % colorCount];
            float v = Mathf.Lerp(fromV, toV, transitionT);

            return new Color(v, v, v);
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
        /// Spawns daze streamer effects as children of the given visitor transform.
        /// </summary>
        public static void SpawnStreamers(Transform visitorTransform)
        {
            if (visitorTransform == null) return;

            // Don't double-spawn
            if (visitorTransform.GetComponentInChildren<DazeStreamerEffect>() != null) return;

            for (int i = 0; i < STREAMER_COUNT; i++)
            {
                GameObject streamerObj = new GameObject($"DazeStreamer_{i}");
                streamerObj.transform.SetParent(visitorTransform);

                float angle = RandomManager.Range(0f, Mathf.PI * 2f);
                float radius = RandomManager.Range(0.2f, CONSTRAINT_RADIUS * 0.8f);
                float z = RandomManager.Range(Z_MIN * 0.5f, Z_MAX);
                streamerObj.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    z
                );

                streamerObj.AddComponent<DazeStreamerEffect>();
            }
        }

        /// <summary>
        /// Destroys all daze streamer effects on the given visitor transform.
        /// </summary>
        public static void DestroyStreamers(Transform visitorTransform)
        {
            if (visitorTransform == null) return;

            var streamers = visitorTransform.GetComponentsInChildren<DazeStreamerEffect>();
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
