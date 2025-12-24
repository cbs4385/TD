using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Factory for creating PBR materials for 3D maze rendering.
    /// Converts color-based sprite materials to URP/Lit PBR equivalents
    /// while preserving the original color palette.
    /// </summary>
    public static class PBRMaterialFactory
    {
        #region Public Methods

        /// <summary>
        /// Creates a URP/Lit PBR material with the specified base color.
        /// </summary>
        /// <param name="baseColor">The base/albedo color for the material</param>
        /// <param name="materialName">Optional name for the material</param>
        /// <param name="enableEmission">Whether to enable emission (glow)</param>
        /// <param name="emissionColor">Emission color (if enabled)</param>
        /// <param name="metallic">Metallic value (0-1)</param>
        /// <param name="smoothness">Smoothness value (0-1)</param>
        /// <returns>A new URP/Lit material</returns>
        public static Material CreateLitMaterial(
            Color baseColor,
            string materialName = "PBR_Material",
            bool enableEmission = false,
            Color? emissionColor = null,
            float metallic = 0f,
            float smoothness = 0.5f)
        {
            // Find the URP/Lit shader
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null)
            {
                Debug.LogWarning("[PBRMaterialFactory] URP/Lit shader not found! Falling back to Standard shader.");
                litShader = Shader.Find("Standard");
            }

            if (litShader == null)
            {
                Debug.LogError("[PBRMaterialFactory] Neither URP/Lit nor Standard shader found! Using default shader.");
                return new Material(Shader.Find("Diffuse")) { color = baseColor, name = materialName };
            }

            // Create the material
            Material material = new Material(litShader)
            {
                name = materialName
            };

            // Set base color (albedo)
            material.SetColor("_BaseColor", baseColor);

            // Set metallic and smoothness
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);

            // Enable emission if requested
            if (enableEmission)
            {
                material.EnableKeyword("_EMISSION");
                Color emissionValue = emissionColor ?? baseColor;
                material.SetColor("_EmissionColor", emissionValue);
            }

            return material;
        }

        /// <summary>
        /// Creates a simple unlit material for 3D rendering (no lighting calculations).
        /// </summary>
        /// <param name="baseColor">The base color for the material</param>
        /// <param name="materialName">Optional name for the material</param>
        /// <returns>A new URP/Unlit material</returns>
        public static Material CreateUnlitMaterial(Color baseColor, string materialName = "PBR_Unlit")
        {
            // Find the URP/Unlit shader
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null)
            {
                Debug.LogWarning("[PBRMaterialFactory] URP/Unlit shader not found! Falling back to Unlit/Color.");
                unlitShader = Shader.Find("Unlit/Color");
            }

            if (unlitShader == null)
            {
                Debug.LogError("[PBRMaterialFactory] No unlit shader found! Using default shader.");
                return new Material(Shader.Find("Diffuse")) { color = baseColor, name = materialName };
            }

            Material material = new Material(unlitShader)
            {
                name = materialName
            };

            material.SetColor("_BaseColor", baseColor);

            return material;
        }

        /// <summary>
        /// Creates a wall material (tree bramble) with appropriate PBR properties.
        /// </summary>
        /// <param name="baseColor">The wall color</param>
        /// <returns>A PBR material for walls</returns>
        public static Material CreateWallMaterial(Color baseColor)
        {
            return CreateLitMaterial(
                baseColor,
                "PBR_Wall",
                enableEmission: false,
                metallic: 0.0f,
                smoothness: 0.2f  // Rough surface for organic walls
            );
        }

        /// <summary>
        /// Creates an undergrowth material with appropriate PBR properties.
        /// </summary>
        /// <param name="baseColor">The undergrowth color</param>
        /// <returns>A PBR material for undergrowth</returns>
        public static Material CreateUndergrowthMaterial(Color baseColor)
        {
            return CreateLitMaterial(
                baseColor,
                "PBR_Undergrowth",
                enableEmission: false,
                metallic: 0.0f,
                smoothness: 0.3f  // Slightly rough for vegetation
            );
        }

        /// <summary>
        /// Creates a water material with appropriate PBR properties.
        /// </summary>
        /// <param name="baseColor">The water color</param>
        /// <returns>A PBR material for water</returns>
        public static Material CreateWaterMaterial(Color baseColor)
        {
            return CreateLitMaterial(
                baseColor,
                "PBR_Water",
                enableEmission: false,
                metallic: 0.1f,      // Slightly metallic for reflectivity
                smoothness: 0.9f     // Very smooth for water surface
            );
        }

        /// <summary>
        /// Creates a path material with appropriate PBR properties.
        /// </summary>
        /// <param name="baseColor">The path color</param>
        /// <returns>A PBR material for paths</returns>
        public static Material CreatePathMaterial(Color baseColor)
        {
            return CreateLitMaterial(
                baseColor,
                "PBR_Path",
                enableEmission: false,
                metallic: 0.0f,
                smoothness: 0.4f  // Medium roughness for ground
            );
        }

        /// <summary>
        /// Creates an emissive material for glowing objects (like the heart).
        /// </summary>
        /// <param name="baseColor">The base color</param>
        /// <param name="emissionColor">The emission color</param>
        /// <param name="emissionIntensity">Emission intensity multiplier</param>
        /// <returns>A PBR material with emission</returns>
        public static Material CreateEmissiveMaterial(
            Color baseColor,
            Color emissionColor,
            float emissionIntensity = 1.0f)
        {
            Color finalEmission = emissionColor * emissionIntensity;

            return CreateLitMaterial(
                baseColor,
                "PBR_Emissive",
                enableEmission: true,
                emissionColor: finalEmission,
                metallic: 0.0f,
                smoothness: 0.6f
            );
        }

        /// <summary>
        /// Creates an emissive material with texture support for glowing objects.
        /// Preserves the base texture from the original material if available.
        /// </summary>
        /// <param name="baseColor">The base color tint</param>
        /// <param name="emissionColor">The emission color</param>
        /// <param name="emissionIntensity">Emission intensity multiplier</param>
        /// <param name="baseTexture">Optional base texture to apply</param>
        /// <param name="materialName">Optional name for the material</param>
        /// <returns>A PBR material with emission and texture</returns>
        public static Material CreateEmissiveMaterialWithTexture(
            Color baseColor,
            Color emissionColor,
            float emissionIntensity = 1.0f,
            Texture baseTexture = null,
            string materialName = "PBR_Emissive_Textured")
        {
            Color finalEmission = emissionColor * emissionIntensity;

            Material material = CreateLitMaterial(
                baseColor,
                materialName,
                enableEmission: true,
                emissionColor: finalEmission,
                metallic: 0.0f,
                smoothness: 0.6f
            );

            // Apply texture if provided
            if (baseTexture != null)
            {
                material.SetTexture("_BaseMap", baseTexture);
                Debug.Log($"[PBRMaterialFactory] Applied texture {baseTexture.name} to material {materialName}");
            }

            return material;
        }

        #endregion
    }
}
