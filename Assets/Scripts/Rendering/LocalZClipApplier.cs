using UnityEngine;

[DisallowMultipleComponent]
public class LocalZClipApplier : MonoBehaviour
{
    [Tooltip("Leave empty to auto-find Custom/LocalZClipURPUnlitCutout")]
    [SerializeField] private Shader clipShader;

    [Tooltip("Object-space Z threshold. 0 = clip everything below the model origin plane.")]
    [SerializeField] private float clipZ = 0f;

    [Tooltip("If true, applies to all child Renderers (SkinnedMeshRenderer included).")]
    [SerializeField] private bool applyToChildren = true;

    private Renderer[] _renderers;
    private Material _sharedClipMaterial;
    private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();

    private void Awake()
    {
        if (clipShader == null)
            clipShader = Shader.Find("Custom/LocalZClipURPUnlitCutout");

        if (clipShader == null)
        {
            Debug.LogError("LocalZClipApplier: Could not find shader 'Custom/LocalZClipURPUnlitCutout'. Did you create it?");
            enabled = false;
            return;
        }

        _sharedClipMaterial = new Material(clipShader) { enableInstancing = true };

        _renderers = applyToChildren
            ? GetComponentsInChildren<Renderer>(true)
            : GetComponents<Renderer>();

        ApplySharedMaterials();
        PushClipValue();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        PushClipValue();
    }

    private void OnDestroy()
    {
        if (_sharedClipMaterial == null) return;

        if (Application.isPlaying)
            Destroy(_sharedClipMaterial);
        else
            DestroyImmediate(_sharedClipMaterial);
    }

    private void ApplySharedMaterials()
    {
        foreach (var r in _renderers)
        {
            if (r == null) continue;

            var originalMats = r.sharedMaterials;
            var sharedClipMats = new Material[originalMats.Length];

            for (int i = 0; i < originalMats.Length; i++)
            {
                sharedClipMats[i] = _sharedClipMaterial;

                var src = originalMats[i];

                Texture baseTex = null;
                if (src != null)
                {
                    if (src.HasProperty("_BaseMap")) baseTex = src.GetTexture("_BaseMap");
                    else if (src.HasProperty("_MainTex")) baseTex = src.GetTexture("_MainTex");
                }

                Color baseCol = Color.white;
                if (src != null)
                {
                    if (src.HasProperty("_BaseColor")) baseCol = src.GetColor("_BaseColor");
                    else if (src.HasProperty("_Color")) baseCol = src.GetColor("_Color");
                }

                _propertyBlock.Clear();
                _propertyBlock.SetFloat("_ClipZ", clipZ);
                _propertyBlock.SetTexture("_BaseMap", baseTex);
                _propertyBlock.SetColor("_BaseColor", baseCol);

                r.SetPropertyBlock(_propertyBlock, i);
            }

            r.sharedMaterials = sharedClipMats;
        }
    }

    private void PushClipValue()
    {
        if (_renderers == null) return;

        foreach (var r in _renderers)
        {
            if (r == null) continue;
            var materials = r.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                _propertyBlock.Clear();
                r.GetPropertyBlock(_propertyBlock, i);
                _propertyBlock.SetFloat("_ClipZ", clipZ);
                r.SetPropertyBlock(_propertyBlock, i);
            }
        }
    }
}
