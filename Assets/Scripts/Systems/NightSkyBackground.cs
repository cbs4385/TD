using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Creates a full-screen background quad with a night sky gradient.
    /// Dark forest color at the bottom/edges, night sky color at the top.
    /// </summary>
    public class NightSkyBackground : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField]
        [Tooltip("Use a uniform debug color instead of the gradient")]
        private bool useDebugColor = false;

        [SerializeField]
        [Tooltip("Uniform debug background color")]
        private Color debugColor = new Color(0.2f, 0.4f, 0.8f, 1f); // Uniform blue

        [Header("Colors")]
        [SerializeField]
        [Tooltip("Dark forest color at bottom")]
        private Color bottomColor = new Color(0.039f, 0.039f, 0.039f, 1f); // #0a0a0a

        [SerializeField]
        [Tooltip("Night sky color at top")]
        private Color topColor = new Color(0.05f, 0.05f, 0.15f, 1f); // Deep blue-purple

        [Header("Gradient Settings")]
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Vertical position of gradient center (0=bottom, 1=top)")]
        private float gradientCenter = 0.3f;

        [SerializeField]
        [Range(0.01f, 1f)]
        [Tooltip("How soft/gradual the transition is")]
        private float gradientSoftness = 0.4f;

        [Header("Position")]
        [SerializeField]
        [Tooltip("Z position of the background (should be far behind everything)")]
        private float zPosition = 500f;

        [SerializeField]
        [Tooltip("Size of the background quad")]
        private float quadSize = 10000f;

        private GameObject backgroundQuad;
        private Material gradientMaterial;

        private void Start()
        {
            CreateBackgroundQuad();
        }

        private void CreateBackgroundQuad()
        {
            // Create the quad
            backgroundQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backgroundQuad.name = "NightSkyBackground";
            backgroundQuad.transform.SetParent(transform);

            // Position it far behind everything
            backgroundQuad.transform.localPosition = new Vector3(0f, 0f, zPosition);
            backgroundQuad.transform.localScale = new Vector3(quadSize, quadSize, 1f);

            // Face the camera (rotate to face negative Z)
            backgroundQuad.transform.localRotation = Quaternion.identity;

            // Remove collider - not needed
            var collider = backgroundQuad.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            // Create and apply gradient material
            if (useDebugColor)
            {
                // Use simple unlit color for debugging
                var unlitShader = Shader.Find("Unlit/Color");
                gradientMaterial = new Material(unlitShader);
                gradientMaterial.color = debugColor;
            }
            else
            {
                var shader = Shader.Find("Custom/NightSkyGradient");
                if (shader == null)
                {
                    // Fallback to unlit color if shader not found
                    shader = Shader.Find("Unlit/Color");
                    gradientMaterial = new Material(shader);
                    gradientMaterial.color = bottomColor;
                }
                else
                {
                    gradientMaterial = new Material(shader);
                    UpdateMaterialProperties();
                }
            }

            var renderer = backgroundQuad.GetComponent<Renderer>();
            renderer.material = gradientMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void UpdateMaterialProperties()
        {
            if (gradientMaterial == null) return;

            gradientMaterial.SetColor("_BottomColor", bottomColor);
            gradientMaterial.SetColor("_TopColor", topColor);
            gradientMaterial.SetFloat("_GradientCenter", gradientCenter);
            gradientMaterial.SetFloat("_GradientSoftness", gradientSoftness);
        }

        private void OnValidate()
        {
            // Update material when values change in inspector
            if (Application.isPlaying && gradientMaterial != null)
            {
                if (useDebugColor)
                {
                    gradientMaterial.color = debugColor;
                }
                else
                {
                    UpdateMaterialProperties();
                }
            }
        }

        private void OnDestroy()
        {
            if (gradientMaterial != null)
            {
                Destroy(gradientMaterial);
            }
            if (backgroundQuad != null)
            {
                Destroy(backgroundQuad);
            }
        }
    }
}
