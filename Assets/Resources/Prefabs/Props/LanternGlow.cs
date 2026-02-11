using UnityEngine;
using FaeMaze.Visitors;
using FaeMaze.Systems;

/// <summary>
/// Adds a cycling pastel rainbow glow effect to the lantern via a point light.
/// Manages idle/reacting states based on visitor proximity.
/// When a visitor enters the lantern's node, fires a spotlight and triggers fascination test.
/// </summary>
[ExecuteAlways]
public class LanternGlow : MonoBehaviour
{
    #region Enums

    public enum LanternState
    {
        Idle,
        Reacting
    }

    #endregion

    #region Serialized Fields

    [Header("Glow Settings")]
    [Tooltip("Duration each color is held before transitioning (seconds)")]
    [SerializeField] private float colorHoldSeconds = 5f;

    [Header("Light Settings")]
    [Tooltip("Enable point light for scene illumination")]
    [SerializeField] private bool enableLight = true;

    [Tooltip("Point light intensity")]
    [SerializeField] private float lightIntensity = 0.8f;

    [Tooltip("Point light range")]
    [SerializeField] private float lightRange = 5f;

    [Tooltip("Y offset for the light position (to place it inside the lantern)")]
    [SerializeField] private float lightYOffset = 0f;

    [Header("Reaction Settings")]
    [Tooltip("Distance at which lantern begins reacting to visitors")]
    [SerializeField] private float reactionRadius = 3.5f;

    [Tooltip("Time in seconds before lantern returns to idle if visitor doesn't enter node")]
    [SerializeField] private float reactionTimeout = 5f;

    [Tooltip("Distance threshold for considering visitor 'at the node'")]
    [SerializeField] private float nodeEntryRadius = 1.0f;

    [Header("Spotlight Settings")]
    [Tooltip("Duration of the spotlight effect in seconds")]
    [SerializeField] private float spotlightDuration = 3f;

    [Tooltip("Spotlight intensity")]
    [SerializeField] private float spotlightIntensity = 2f;

    [Tooltip("Spotlight range")]
    [SerializeField] private float spotlightRange = 8f;

    [Tooltip("Spotlight cone angle")]
    [SerializeField] private float spotlightAngle = 45f;

    #endregion

    #region Private Fields

    // Rainbow hues (0-1 range) - 7 colors of the rainbow
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

    private float timeOffset;
    private Light pointLight;
    private Light spotLight;
    private GameObject spotLightObj;

    // State tracking
    private LanternState currentState = LanternState.Idle;
    private VisitorControllerBase targetVisitor;
    private float reactionTimer;
    private bool spotlightActive;
    private float spotlightTimer;
    private Quaternion idleRotation;

    // The correct base orientation for the lantern model (180° around X)
    // Confirmed by user manual testing in editor
    private static readonly Quaternion BaseOrientation = Quaternion.Euler(180f, 0f, 0f);

    // Track current glow color for spotlight sync
    private Color currentGlowColor;

    #endregion

    #region Properties

    /// <summary>Gets the current state of the lantern</summary>
    public LanternState State => currentState;

    /// <summary>Gets the current glow color</summary>
    public Color CurrentGlowColor => currentGlowColor;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        // Add per-instance time offset so multiple lanterns don't sync
        timeOffset = Mathf.Abs(gameObject.GetInstanceID()) * 0.0001f;

        // Use the hardcoded base orientation (-90° X) instead of capturing transform.rotation
        // Capturing at OnEnable was unreliable - the rotation could be wrong if OnEnable fires
        // before prefab overrides are fully applied or if [ExecuteAlways] interferes
        idleRotation = BaseOrientation;

        // Disable any existing lights from the model (except our own)
        DisableModelLights();

        // Disable all emissive materials on the model (they're baked into the GLB)
        DisableModelEmissions();

        // Create point light
        if (enableLight)
        {
            CreatePointLight();
        }
    }

    private void Update()
    {
        // Calculate current time for color cycling
#if UNITY_EDITOR
        float t = Application.isPlaying ? Time.time : (float)UnityEditor.EditorApplication.timeSinceStartup;
#else
        float t = Time.time;
#endif
        t += timeOffset;

        // Evaluate the smooth rainbow color cycle
        currentGlowColor = EvaluateRainbowCycle(t, colorHoldSeconds);

        // Update point light color
        if (pointLight != null)
        {
            pointLight.color = currentGlowColor;
            pointLight.intensity = lightIntensity;
            pointLight.range = lightRange;
        }
        else if (Application.isPlaying && enableLight && Time.frameCount % 60 == 0)
        {
            CreatePointLight();
        }

        // Only process state logic in play mode
        if (!Application.isPlaying)
            return;

        // Update state machine
        UpdateStateMachine();

        // Update spotlight if active
        UpdateSpotlight();
    }

    private void OnDisable()
    {
        // Clean up spotlight
        if (spotLightObj != null)
        {
            if (Application.isPlaying)
                Destroy(spotLightObj);
            else
                DestroyImmediate(spotLightObj);
        }
    }

    #endregion

    #region State Machine

    private void UpdateStateMachine()
    {
        switch (currentState)
        {
            case LanternState.Idle:
                UpdateIdleState();
                break;
            case LanternState.Reacting:
                UpdateReactingState();
                break;
        }
    }

    private void UpdateIdleState()
    {
        // Spin continuously on Z axis at 1 Hz (360 degrees per second)
        float spinAngle = Time.time * 360f; // 1 full rotation per second
        transform.rotation = Quaternion.Euler(0f, 0f, spinAngle) * idleRotation;

        // Look for nearby visitors
        VisitorControllerBase closestVisitor = FindClosestVisitorInRange(reactionRadius);

        if (closestVisitor != null)
        {
            // Transition to reacting
            targetVisitor = closestVisitor;
            reactionTimer = reactionTimeout;
            currentState = LanternState.Reacting;
        }
    }

    private void UpdateReactingState()
    {
        // Check if target visitor is still valid and in range
        if (targetVisitor == null || !targetVisitor.gameObject.activeInHierarchy)
        {
            // Lost target, try to find another
            VisitorControllerBase newTarget = FindClosestVisitorInRange(reactionRadius);
            if (newTarget != null)
            {
                targetVisitor = newTarget;
                reactionTimer = reactionTimeout;
            }
            else
            {
                // No visitors nearby, return to idle
                TransitionToIdle();
                return;
            }
        }

        // Calculate distance to target
        float distanceToVisitor = Vector3.Distance(transform.position, targetVisitor.transform.position);

        // Check if visitor left reaction range
        if (distanceToVisitor > reactionRadius * 1.2f) // Small hysteresis
        {
            // Try to find another visitor
            VisitorControllerBase newTarget = FindClosestVisitorInRange(reactionRadius);
            if (newTarget != null)
            {
                targetVisitor = newTarget;
                reactionTimer = reactionTimeout;
            }
            else
            {
                TransitionToIdle();
                return;
            }
        }

        // Rotate toward visitor (only X/Y plane, no Z rotation)
        RotateTowardVisitor();

        // Start spotlight immediately when reacting (not waiting for node entry)
        if (!spotlightActive)
        {
            StartSpotlight();
        }

        // Countdown reaction timer if spotlight not active
        if (!spotlightActive)
        {
            reactionTimer -= Time.deltaTime;
            if (reactionTimer <= 0f)
            {
                // Timeout - return to idle
                TransitionToIdle();
            }
        }
    }

    private void TransitionToIdle()
    {
        currentState = LanternState.Idle;
        targetVisitor = null;
        reactionTimer = 0f;
        transform.rotation = idleRotation;
    }

    #endregion

    #region Visitor Detection

    private VisitorControllerBase FindClosestVisitorInRange(float range)
    {
        VisitorControllerBase closest = null;
        float closestDist = float.MaxValue;

        // Use VisitorRegistry which tracks ALL visitor types
        foreach (var visitor in VisitorRegistry.All)
        {
            if (visitor == null || !visitor.gameObject.activeInHierarchy)
                continue;

            // Skip already fascinated visitors
            if (visitor.IsFascinated)
                continue;

            float dist = Vector3.Distance(transform.position, visitor.transform.position);
            if (dist <= range && dist < closestDist)
            {
                closestDist = dist;
                closest = visitor;
            }
        }

        return closest;
    }

    #endregion

    #region Rotation

    private void RotateTowardVisitor()
    {
        if (targetVisitor == null)
            return;

        // Get direction to visitor in XY plane
        Vector3 toVisitor = targetVisitor.transform.position - transform.position;
        toVisitor.z = 0; // Keep in XY plane

        if (toVisitor.sqrMagnitude < 0.001f)
            return;

        // Calculate rotation to point +X toward visitor
        // +X forward means we want the angle from +X axis
        float angle = Mathf.Atan2(toVisitor.y, toVisitor.x) * Mathf.Rad2Deg;

        // Apply rotation around Z axis, then the base orientation
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle) * idleRotation;

        // Smooth rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
    }

    #endregion

    #region Spotlight

    private void StartSpotlight()
    {
        if (spotlightActive)
            return;

        spotlightActive = true;
        spotlightTimer = spotlightDuration;

        // Dim the omni-directional glow while spotlighting (don't disable completely)
        // This creates the visual effect of the glow "focusing" into the spotlight
        if (pointLight != null)
        {
            pointLight.intensity = lightIntensity * 0.2f;
        }

        // Create spotlight if needed
        CreateSpotlight();

        if (spotLight != null)
        {
            spotLight.enabled = true;
            // Spotlight intensity is 10x the glow intensity
            spotLight.intensity = lightIntensity * 10f;
            spotLight.color = currentGlowColor;
        }

    }

    private void CreateSpotlight()
    {
        if (spotLightObj != null)
        {
            spotLight = spotLightObj.GetComponent<Light>();
            return;
        }

        // Create spotlight object as child of this transform
        spotLightObj = new GameObject("SpotLight");
        spotLightObj.transform.SetParent(transform);
        spotLightObj.transform.localPosition = new Vector3(0, lightYOffset, 0);
        // Point in +X direction (local)
        spotLightObj.transform.localRotation = Quaternion.Euler(0, 90, 0);

        spotLight = spotLightObj.AddComponent<Light>();
        spotLight.type = LightType.Spot;
        spotLight.intensity = spotlightIntensity;
        spotLight.range = spotlightRange;
        spotLight.spotAngle = spotlightAngle;
        spotLight.innerSpotAngle = spotlightAngle * 0.5f;
        // Disable shadows to avoid shadow atlas overflow warnings
        spotLight.shadows = LightShadows.None;
        spotLight.enabled = false;
    }

    private void UpdateSpotlight()
    {
        if (!spotlightActive)
            return;

        // Sync spotlight color with glow
        if (spotLight != null)
        {
            spotLight.color = currentGlowColor;

            // Update spotlight direction to point at visitor
            if (targetVisitor != null)
            {
                Vector3 toVisitor = targetVisitor.transform.position - spotLightObj.transform.position;
                if (toVisitor.sqrMagnitude > 0.001f)
                {
                    spotLightObj.transform.rotation = Quaternion.LookRotation(toVisitor);
                }
            }
        }

        // Countdown
        spotlightTimer -= Time.deltaTime;
        if (spotlightTimer <= 0f)
        {
            EndSpotlight();
        }
    }

    private void EndSpotlight()
    {
        spotlightActive = false;

        if (spotLight != null)
        {
            spotLight.enabled = false;
        }

        // Restore the omni-directional glow to full intensity
        // This creates the visual effect of the focused light "relaxing" back to glow
        if (pointLight != null && enableLight)
        {
            pointLight.intensity = lightIntensity;
        }

        // Return to idle after spotlight ends
        TransitionToIdle();
    }

    #endregion

    #region Light Setup

    /// <summary>
    /// Disables any Light components that came with the imported model.
    /// </summary>
    private void DisableModelLights()
    {
        var lights = GetComponentsInChildren<Light>(true);
        foreach (var light in lights)
        {
            if (light == null) continue;

            // Don't disable our own lights
            if (light.gameObject.name == "GlowLight" || light.gameObject.name == "SpotLight")
                continue;

            // Disable the light from the model
            light.enabled = false;
        }
    }

    // Brass/antique gold color for the lantern body
    private static readonly Color BrassColor = new Color(0.72f, 0.53f, 0.20f, 1f);
    private static readonly Color BrassSpecular = new Color(0.85f, 0.65f, 0.30f, 1f);

    /// <summary>
    /// Disables emission on all materials and applies a brass tint to structural materials.
    /// Emissive/glow materials (plasma, flame, etc.) are hidden so the point light controls the glow.
    /// Structural materials get a brass color to give the lantern its metallic look.
    /// </summary>
    private void DisableModelEmissions()
    {
        // Skip in edit mode to avoid material leaks
#if UNITY_EDITOR
        if (!Application.isPlaying) return;
#endif

        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;

            // Get materials (creates instances, which is what we want at runtime)
            var materials = renderer.materials;

            foreach (var mat in materials)
            {
                if (mat == null) continue;

                // Disable emission keyword
                mat.DisableKeyword("_EMISSION");

                // Set emission color to black (Standard shader)
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", Color.black);
                }

                // Also try _EmissiveColor (some shaders use this)
                if (mat.HasProperty("_EmissiveColor"))
                {
                    mat.SetColor("_EmissiveColor", Color.black);
                }

                // Set emission intensity to 0 if available
                if (mat.HasProperty("_EmissionIntensity"))
                {
                    mat.SetFloat("_EmissionIntensity", 0f);
                }

                // Check if this is an emissive-named material (plasma, flame, etc.) - make it invisible
                string matName = mat.name.ToLower();
                bool isEmissiveMaterial = matName.Contains("plasma") || matName.Contains("flame") ||
                    matName.Contains("glow") || matName.Contains("emissive") ||
                    matName.Contains("fire") || matName.Contains("light");

                if (isEmissiveMaterial)
                {
                    // Make the material completely transparent/invisible
                    if (mat.HasProperty("_BaseColor"))
                    {
                        mat.SetColor("_BaseColor", Color.clear);
                    }
                    if (mat.HasProperty("_Color"))
                    {
                        mat.SetColor("_Color", Color.clear);
                    }

                    // Set alpha to 0
                    mat.SetFloat("_Surface", 1); // 1 = Transparent in URP
                    mat.SetFloat("_Blend", 0);   // Alpha blend
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.renderQueue = 3000;
                }
                else
                {
                    // Structural material - apply brass coloring
                    // This ensures the lantern body has a consistent brass appearance
                    // regardless of whether the GLB material loaded correctly
                    if (mat.HasProperty("_BaseColor"))
                    {
                        mat.SetColor("_BaseColor", BrassColor);
                    }
                    if (mat.HasProperty("_Color"))
                    {
                        mat.SetColor("_Color", BrassColor);
                    }

                    // Set metallic properties for a shiny brass look
                    if (mat.HasProperty("_Metallic"))
                    {
                        mat.SetFloat("_Metallic", 0.7f);
                    }
                    if (mat.HasProperty("_Smoothness"))
                    {
                        mat.SetFloat("_Smoothness", 0.6f);
                    }

                    // Ensure material is opaque
                    if (mat.HasProperty("_Surface"))
                    {
                        mat.SetFloat("_Surface", 0); // 0 = Opaque in URP
                    }
                    mat.SetOverrideTag("RenderType", "Opaque");
                    mat.renderQueue = -1; // Use shader default
                }
            }

            renderer.materials = materials;
        }
    }

    /// <summary>
    /// Applies a brass metallic tint to a material.
    /// </summary>
    private static void ApplyBrassTint(Material mat)
    {
        // Set base/albedo color to brass
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", BrassColor);
        }
        if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", BrassColor);
        }

        // Set metallic properties for a brass look
        if (mat.HasProperty("_Metallic"))
        {
            mat.SetFloat("_Metallic", 0.85f);
        }
        if (mat.HasProperty("_Smoothness"))
        {
            mat.SetFloat("_Smoothness", 0.5f);
        }

        // Set specular color if available
        if (mat.HasProperty("_SpecColor"))
        {
            mat.SetColor("_SpecColor", BrassSpecular);
        }
    }

    private void CreatePointLight()
    {
        // First, check if we already have a reference that's enabled
        if (pointLight != null && pointLight.enabled)
            return;

        // Look for existing GlowLight child (our own light)
        Transform existingLightTransform = transform.Find("GlowLight");

        if (existingLightTransform != null)
        {
            pointLight = existingLightTransform.GetComponent<Light>();
            if (pointLight != null)
            {
                pointLight.enabled = true;
                // Update position in case offset changed
                existingLightTransform.localPosition = new Vector3(0, lightYOffset, 0);
            }
        }

        // Only create a new light if we don't have one named GlowLight
        if (pointLight == null)
        {
            // Create new light
            GameObject lightObj = new GameObject("GlowLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = new Vector3(0, lightYOffset, 0);

            pointLight = lightObj.AddComponent<Light>();
        }

        pointLight.type = LightType.Point;
        pointLight.intensity = lightIntensity;
        pointLight.range = lightRange;
        // Disable shadows to avoid shadow atlas overflow warnings
        pointLight.shadows = LightShadows.None;
    }

    #endregion

    #region Color Cycling

    /// <summary>
    /// Returns a smoothly cycling color through the rainbow.
    /// Each color is held for colorHoldSeconds, with smooth transitions starting halfway through.
    /// </summary>
    private static Color EvaluateRainbowCycle(float timeSeconds, float holdDuration)
    {
        int colorCount = RainbowHues.Length;
        float totalCycleDuration = colorCount * holdDuration;

        if (holdDuration <= 0.0001f) return Color.HSVToRGB(RainbowHues[0], 0.5f, 1f);

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
        // First half (0 to 0.5): hold current color
        // Second half (0.5 to 1.0): transition to next color
        float transitionT = 0f;
        if (segmentProgress > 0.5f)
        {
            // Map 0.5-1.0 to 0-1 for transition
            float transitionProgress = (segmentProgress - 0.5f) * 2f;
            // Cosine ease for smooth transition
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

        // Pastel: lower saturation, full brightness
        const float pastelSaturation = 0.5f;
        const float pastelValue = 1.0f;

        return Color.HSVToRGB(h, pastelSaturation, pastelValue);
    }

    #endregion

    #region Debug

    private void OnDrawGizmosSelected()
    {
        // Draw reaction radius
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, reactionRadius);

        // Draw node entry radius
        Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, nodeEntryRadius);

        // Draw +X direction
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.right * 2f);
    }

    #endregion
}
