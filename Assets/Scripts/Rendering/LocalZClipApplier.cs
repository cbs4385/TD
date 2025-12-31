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

        _renderers = applyToChildren
            ? GetComponentsInChildren<Renderer>(true)
            : GetComponents<Renderer>();

        ApplyUniqueMaterials();
        PushClipValue();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        PushClipValue();
    }

    private void ApplyUniqueMaterials()
    {
        foreach (var r in _renderers)
        {
            if (r == null) continue;

            // IMPORTANT:
            // r.materials instantiates materials per-renderer (so we don't mutate shared/imported materials).
            var mats = r.materials;

            for (int i = 0; i < mats.Length; i++)
            {
                var src = mats[i];
                if (src == null) continue;

                var dst = new Material(clipShader);

                // Copy over common base texture + color from whatever shader the importer used.
                Texture baseTex = null;
                if (src.HasProperty("_BaseMap")) baseTex = src.GetTexture("_BaseMap");
                else if (src.HasProperty("_MainTex")) baseTex = src.GetTexture("_MainTex");

                Color baseCol = Color.white;
                if (src.HasProperty("_BaseColor")) baseCol = src.GetColor("_BaseColor");
                else if (src.HasProperty("_Color")) baseCol = src.GetColor("_Color");

                dst.SetTexture("_BaseMap", baseTex);
                dst.SetColor("_BaseColor", baseCol);
                dst.SetFloat("_ClipZ", clipZ);

                mats[i] = dst;
            }

            r.materials = mats;
        }
    }

    private void PushClipValue()
    {
        if (_renderers == null) return;

        foreach (var r in _renderers)
        {
            if (r == null) continue;
            foreach (var m in r.materials)
            {
                if (m != null && m.HasProperty("_ClipZ"))
                    m.SetFloat("_ClipZ", clipZ);
            }
        }
    }
}
