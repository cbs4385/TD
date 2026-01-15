using UnityEngine;

namespace FaeMaze.Props
{
    /// <summary>
    /// Controls a sphere within the FairyRing prefab.
    /// Each sphere cycles through rainbow colors at its own random rate
    /// and flies erratically within the parent cylinder's bounding box.
    /// </summary>
    [ExecuteAlways]
    public class FairyRingSphere : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Color Cycling")]
        [Tooltip("Starting rainbow color index (0-6: Red, Orange, Yellow, Green, Cyan, Blue, Violet)")]
        [Range(0, 6)]
        [SerializeField] private int startingColorIndex = 0;

        [Tooltip("Duration to cycle through all rainbow colors (randomized between min and max on start)")]
        [SerializeField] private float minCycleDuration = 1f;

        [Tooltip("Maximum cycle duration")]
        [SerializeField] private float maxCycleDuration = 3f;

        [Header("Movement")]
        [Tooltip("How erratic the movement is (higher = more chaotic)")]
        [SerializeField] private float movementSpeed = 2f;

        [Tooltip("How often the sphere changes direction (seconds)")]
        [SerializeField] private float directionChangeInterval = 0.5f;

        [Tooltip("Smoothing factor for movement (higher = smoother)")]
        [SerializeField] private float movementSmoothing = 3f;

        #endregion

        #region Private Fields

        // Rainbow hues (0-1 range) - 7 colors of the rainbow (same as LanternGlow)
        private static readonly float[] RainbowHues = new float[]
        {
            0.00f,  // Red
            0.08f,  // Orange
            0.16f,  // Yellow
            0.33f,  // Green
            0.50f,  // Cyan
            0.66f,  // Blue
            0.80f,  // Violet
        };

        private float cycleDuration;
        private float colorTimeOffset;
        private float movementTimeOffset;

        private TrailRenderer trailRenderer;
        private Renderer sphereRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Light sphereLight;

        // Cylinder bounds (local space of parent cylinder)
        private Transform cylinderTransform;
        private Vector3 cylinderCenter;
        private float cylinderRadius;
        private float cylinderHalfHeight;

        // Movement
        private Vector3 currentVelocity;
        private Vector3 targetVelocity;
        private float nextDirectionChangeTime;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            // Randomize cycle duration
            cycleDuration = Random.Range(minCycleDuration, maxCycleDuration);

            // Calculate time offset so each sphere starts at its assigned color
            float colorHoldDuration = cycleDuration / RainbowHues.Length;
            colorTimeOffset = startingColorIndex * colorHoldDuration;

            // Random movement offset for variation
            movementTimeOffset = Random.value * 1000f;

            // Ensure we have a proper 3D sphere mesh (not a flat disk)
            EnsureSphereMesh();

            // Cache components - look for TrailRenderer in children first
            trailRenderer = GetComponentInChildren<TrailRenderer>();

            // Ensure we have a trail renderer (creates one if needed)
            EnsureTrailRenderer();

            // Add point light for glowing effect
            EnsureSphereLight();

            sphereRenderer = GetComponent<Renderer>();
            propertyBlock = new MaterialPropertyBlock();

            // Find the cylinder parent
            FindCylinderBounds();

            // Initialize random velocity
            targetVelocity = GetRandomVelocity();
            currentVelocity = targetVelocity;
            nextDirectionChangeTime = Time.time + directionChangeInterval;
        }

        /// <summary>
        /// Ensures the object has a proper 3D sphere mesh instead of a flat disk.
        /// </summary>
        private void EnsureSphereMesh()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                // No mesh filter - add one with a sphere mesh
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            // Check if the current mesh is flat (like a quad or plane)
            // A sphere mesh has many vertices, a flat disk has few
            bool needsSphereMesh = false;
            if (meshFilter.sharedMesh == null)
            {
                needsSphereMesh = true;
            }
            else
            {
                // Check mesh bounds - a flat disk will have near-zero extent in one axis
                var bounds = meshFilter.sharedMesh.bounds;
                float minExtent = Mathf.Min(bounds.extents.x, bounds.extents.y, bounds.extents.z);
                if (minExtent < 0.01f)
                {
                    needsSphereMesh = true;
                }
            }

            if (needsSphereMesh)
            {
                // Create a sphere primitive and steal its mesh
                var tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                meshFilter.sharedMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;

                // Clean up the temp object
                if (Application.isPlaying)
                {
                    Destroy(tempSphere);
                }
                else
                {
                    DestroyImmediate(tempSphere);
                }

                // Ensure we have a MeshRenderer
                var meshRenderer = GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                {
                    meshRenderer = gameObject.AddComponent<MeshRenderer>();
                }

                // Create a transparent material with emission support for glowing specks
                var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                {
                    material = new Material(Shader.Find("Particles/Standard Unlit"));
                }
                if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                {
                    material = new Material(Shader.Find("Sprites/Default"));
                }

                // Configure for transparency
                material.SetFloat("_Surface", 1); // Transparent
                material.SetFloat("_Blend", 0); // Alpha blend
                material.renderQueue = 3000; // Transparent queue
                meshRenderer.material = material;
            }
        }

        /// <summary>
        /// Ensures the object has a TrailRenderer component for visual effects.
        /// Trail must be on this object (not a child) to render properly when moving.
        /// </summary>
        private void EnsureTrailRenderer()
        {
            // TrailRenderer must be on this object, not a child, to render trails when moving
            trailRenderer = GetComponent<TrailRenderer>();

            // Remove any child Trail objects - they don't work for moving objects
            var childTrail = transform.Find("Trail");
            if (childTrail != null)
            {
                var childTrailRenderer = childTrail.GetComponent<TrailRenderer>();
                if (childTrailRenderer != null && trailRenderer == null)
                {
                    // Copy settings from child before destroying
                    trailRenderer = gameObject.AddComponent<TrailRenderer>();
                    trailRenderer.time = childTrailRenderer.time;
                    trailRenderer.startWidth = childTrailRenderer.startWidth;
                    trailRenderer.endWidth = childTrailRenderer.endWidth;
                    trailRenderer.minVertexDistance = childTrailRenderer.minVertexDistance;
                }

                if (Application.isPlaying)
                {
                    Destroy(childTrail.gameObject);
                }
            }

            if (trailRenderer == null)
            {
                trailRenderer = gameObject.AddComponent<TrailRenderer>();
            }

            // Configure trail for long exposure light streak effect
            trailRenderer.time = 2f;  // Long trail for light painting effect
            trailRenderer.startWidth = 0.15f;  // Wider, more visible light streak
            trailRenderer.endWidth = 0.03f;
            trailRenderer.minVertexDistance = 0.01f;
            trailRenderer.emitting = true;
            trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trailRenderer.receiveShadows = false;
            trailRenderer.sortingOrder = 100;
            trailRenderer.allowOcclusionWhenDynamic = false;

            // Always ensure the material supports vertex colors for color cycling
            SetupTrailMaterial();
        }

        /// <summary>
        /// Sets up a material that supports vertex colors for the trail renderer.
        /// Uses additive blending for a luminous glow effect like light painting.
        /// </summary>
        private void SetupTrailMaterial()
        {
            if (trailRenderer == null)
                return;

            // Try URP Particles shader first (best for this project)
            var trailMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (trailMaterial.shader == null || trailMaterial.shader.name == "Hidden/InternalErrorShader")
            {
                trailMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
            }
            if (trailMaterial.shader == null || trailMaterial.shader.name == "Hidden/InternalErrorShader")
            {
                trailMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            // Configure for additive blending - makes lights glow and blend together
            trailMaterial.SetFloat("_Surface", 1); // Transparent
            trailMaterial.SetFloat("_Blend", 4); // Additive blend mode
            trailMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            trailMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            trailMaterial.SetInt("_ZWrite", 0); // Disable depth write for proper blending
            trailMaterial.renderQueue = 3500; // Render after other transparents
            trailMaterial.SetColor("_BaseColor", Color.white);
            trailMaterial.SetColor("_Color", Color.white);

            // Enable keywords for additive blending
            trailMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            trailMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");

            trailRenderer.material = trailMaterial;
        }

        /// <summary>
        /// Adds a point light to the sphere for a glowing effect.
        /// </summary>
        private void EnsureSphereLight()
        {
            sphereLight = GetComponent<Light>();
            if (sphereLight == null)
            {
                sphereLight = gameObject.AddComponent<Light>();
            }

            sphereLight.type = LightType.Point;
            sphereLight.range = 1.2f;  // Visible glow on nearby surfaces
            sphereLight.intensity = 5f;  // Bright like a light source
            sphereLight.shadows = LightShadows.None;
            sphereLight.renderMode = LightRenderMode.Auto;

            // Hide the sphere mesh - we only want the light and trail visible
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }
        }

        private void Update()
        {
            UpdateColor();

            if (Application.isPlaying)
            {
                UpdateMovement();
            }
        }

        #endregion

        #region Color Cycling

        private void UpdateColor()
        {
#if UNITY_EDITOR
            float t = Application.isPlaying ? Time.time : (float)UnityEditor.EditorApplication.timeSinceStartup;
#else
            float t = Time.time;
#endif
            t += colorTimeOffset;

            Color currentColor = EvaluateRainbowCycle(t, cycleDuration / RainbowHues.Length);

            // Update trail renderer - bright glowing light streak like light painting
            if (trailRenderer != null)
            {
                // HDR-like bright colors for the light painting effect
                // Multiply color values above 1.0 for bloom/glow effect
                Color brightColor = currentColor * 2f;  // Boost brightness for HDR glow

                // Bright, vibrant trail that fades smoothly like a light painting
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(brightColor, 0f),      // Bright head
                        new GradientColorKey(currentColor, 0.4f),   // Slightly dimmer
                        new GradientColorKey(currentColor * 0.5f, 1f)  // Faded tail
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(1f, 0f),     // Full brightness at head
                        new GradientAlphaKey(0.8f, 0.2f), // Stay bright longer
                        new GradientAlphaKey(0.4f, 0.5f), // Gradual fade
                        new GradientAlphaKey(0f, 1f)      // Fade to nothing
                    }
                );
                trailRenderer.colorGradient = gradient;

                trailRenderer.startColor = brightColor;
                trailRenderer.endColor = new Color(currentColor.r * 0.5f, currentColor.g * 0.5f, currentColor.b * 0.5f, 0f);
            }

            // Update point light color - this creates the glow on nearby surfaces
            if (sphereLight != null)
            {
                sphereLight.color = currentColor;
            }
        }

        /// <summary>
        /// Returns a smoothly cycling color through the rainbow.
        /// Based on LanternGlow's EvaluateRainbowCycle.
        /// </summary>
        private static Color EvaluateRainbowCycle(float timeSeconds, float holdDuration)
        {
            int colorCount = RainbowHues.Length;
            float totalCycleDuration = colorCount * holdDuration;

            if (holdDuration <= 0.0001f) return Color.HSVToRGB(RainbowHues[0], 0.8f, 1f);

            // Get position in the full cycle
            float cycleTime = Mathf.Repeat(timeSeconds, totalCycleDuration);

            // Which color segment are we in?
            int currentColorIndex = Mathf.FloorToInt(cycleTime / holdDuration);
            currentColorIndex = Mathf.Clamp(currentColorIndex, 0, colorCount - 1);

            // Time within this color's hold period (0 to holdDuration)
            float timeInSegment = cycleTime - (currentColorIndex * holdDuration);

            // Normalize to 0-1 within segment
            float segmentProgress = timeInSegment / holdDuration;

            // Transition happens in the second half (0.5 to 1.0)
            float transitionT = 0f;
            if (segmentProgress > 0.5f)
            {
                float transitionProgress = (segmentProgress - 0.5f) * 2f;
                transitionT = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * transitionProgress);
            }

            // Get current and next hues
            float fromH = RainbowHues[currentColorIndex];
            float toH = RainbowHues[(currentColorIndex + 1) % colorCount];

            // Interpolate hue (shortest path around color wheel)
            float hDiff = toH - fromH;
            if (hDiff > 0.5f) hDiff -= 1f;
            else if (hDiff < -0.5f) hDiff += 1f;
            float h = fromH + hDiff * transitionT;
            if (h < 0f) h += 1f;
            if (h > 1f) h -= 1f;

            // Higher saturation than LanternGlow for more vivid fairy lights
            const float saturation = 0.8f;
            const float value = 1.0f;

            return Color.HSVToRGB(h, saturation, value);
        }

        #endregion

        #region Movement

        private void FindCylinderBounds()
        {
            // Look for Cylinder in parent hierarchy
            Transform parent = transform.parent;
            while (parent != null)
            {
                if (parent.name.Contains("Cylinder") || parent.GetComponent<CapsuleCollider>() != null)
                {
                    cylinderTransform = parent;
                    break;
                }
                parent = parent.parent;
            }

            if (cylinderTransform == null)
            {
                // Fallback: use grandparent (ring) as reference
                cylinderTransform = transform.parent?.parent ?? transform.parent;
            }

            if (cylinderTransform != null)
            {
                // Try to get bounds from collider
                var capsule = cylinderTransform.GetComponent<CapsuleCollider>();
                var meshFilter = cylinderTransform.GetComponent<MeshFilter>();

                if (capsule != null)
                {
                    cylinderCenter = capsule.center;
                    cylinderRadius = capsule.radius * Mathf.Max(cylinderTransform.lossyScale.x, cylinderTransform.lossyScale.z) * 0.9f;
                    cylinderHalfHeight = (capsule.height / 2f) * cylinderTransform.lossyScale.y * 0.9f;
                }
                else if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    var bounds = meshFilter.sharedMesh.bounds;
                    cylinderCenter = bounds.center;
                    cylinderRadius = Mathf.Max(bounds.extents.x, bounds.extents.z) * Mathf.Max(cylinderTransform.lossyScale.x, cylinderTransform.lossyScale.z) * 0.9f;
                    cylinderHalfHeight = bounds.extents.y * cylinderTransform.lossyScale.y * 0.9f;
                }
                else
                {
                    // Default bounds
                    cylinderCenter = Vector3.zero;
                    cylinderRadius = 1f;
                    cylinderHalfHeight = 0.5f;
                }
            }
            else
            {
                cylinderCenter = Vector3.zero;
                cylinderRadius = 1f;
                cylinderHalfHeight = 0.5f;
            }
        }

        private void UpdateMovement()
        {
            // Change direction periodically
            if (Time.time >= nextDirectionChangeTime)
            {
                targetVelocity = GetRandomVelocity();
                nextDirectionChangeTime = Time.time + directionChangeInterval + Random.Range(-0.2f, 0.2f);
            }

            // Smoothly interpolate velocity
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, Time.deltaTime * movementSmoothing);

            // Calculate new position
            Vector3 newLocalPos = transform.localPosition + currentVelocity * Time.deltaTime;

            // Constrain to cylinder bounds
            newLocalPos = ConstrainToCylinder(newLocalPos);

            transform.localPosition = newLocalPos;
        }

        private Vector3 GetRandomVelocity()
        {
            // Random direction in 3D space
            Vector3 randomDir = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ).normalized;

            return randomDir * movementSpeed;
        }

        private Vector3 ConstrainToCylinder(Vector3 localPos)
        {
            if (cylinderTransform == null)
                return localPos;

            // Get position relative to cylinder center
            Vector3 relativePos = localPos - cylinderCenter;

            // Constrain height (Y axis)
            relativePos.y = Mathf.Clamp(relativePos.y, -cylinderHalfHeight, cylinderHalfHeight);

            // Constrain to cylinder radius (XZ plane)
            Vector2 xz = new Vector2(relativePos.x, relativePos.z);
            if (xz.magnitude > cylinderRadius)
            {
                xz = xz.normalized * cylinderRadius;
                relativePos.x = xz.x;
                relativePos.z = xz.y;

                // Bounce off the edge by reversing XZ velocity component
                Vector2 velXZ = new Vector2(currentVelocity.x, currentVelocity.z);
                velXZ = -velXZ;
                currentVelocity.x = velXZ.x;
                currentVelocity.z = velXZ.y;
                targetVelocity = GetRandomVelocity();
            }

            // Check if we hit the top or bottom
            if (Mathf.Abs(relativePos.y) >= cylinderHalfHeight * 0.99f)
            {
                currentVelocity.y = -currentVelocity.y;
                targetVelocity = GetRandomVelocity();
            }

            return relativePos + cylinderCenter;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the starting color index for this sphere.
        /// Call this before OnEnable or use the inspector.
        /// </summary>
        public void SetStartingColorIndex(int index)
        {
            startingColorIndex = Mathf.Clamp(index, 0, RainbowHues.Length - 1);
        }

        #endregion
    }
}
