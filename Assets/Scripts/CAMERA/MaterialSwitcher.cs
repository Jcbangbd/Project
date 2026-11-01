//#define Define_OutLine
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Camera))]
[AddComponentMenu("Camera/MaterialSwitcher")]
public class MaterialSwitcher : MonoBehaviour 
{
    public Color outterColor = new Color(0.133f, 1, 0, 1);
#if Define_OutLine
    public int Iterations = 3;
    public float Spread = 0.7f;
    public Camera outterLineCamera;
    private RenderTexture outterLineTexture = null;

    public Shader compositeShader;
    Material m_CompositeMaterial = null;
    protected Material compositeMaterial
    {
        get
        {
            if (m_CompositeMaterial == null)
            {
                m_CompositeMaterial = new Material(compositeShader);
                m_CompositeMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            return m_CompositeMaterial;
        }
    }

    public Shader blurShader;
    Material m_BlurMaterial = null;
    protected Material blurMaterial
    {
        get
        {
            if (m_BlurMaterial == null)
            {
                m_BlurMaterial = new Material(blurShader);
                m_BlurMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            return m_BlurMaterial;
        }
    }

    public Shader cutoffShader;
    Material m_cutoffMaterial = null;
    protected Material cutoffMaterial
    {
        get
        {
            if (m_cutoffMaterial == null)
            {
                m_cutoffMaterial = new Material(cutoffShader);
                m_cutoffMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            return m_cutoffMaterial;
        }
    }

    public Shader outterLineShader;
    private Material m_outterLineMat = null;
    protected Material outterLineMat
    {
        get
        {
            if (m_outterLineMat == null)
            {
                m_outterLineMat = new Material(outterLineShader);
                m_outterLineMat.hideFlags = HideFlags.HideAndDontSave;
            }
            return m_outterLineMat;
        }
    }
#endif

    public Shader[] swithableShaders;
    //private Shader[] availableShaders;

    [System.NonSerialized]
    public static MaterialSwitcher Instance;
    private MaterialSwitcher()
    {
        // this function will be called whenever an instance of the SpawnController class is made
        // first, we check that an instance does not already exist (this is a singleton, afterall!)
        if (Instance != null)
        {
            // drop out if instance exists, to avoid generating duplicates
            Debug.LogWarning("Tried to generate more than one instance of singleton SpawnController.");
            return;
        }
        // as no instance already exists, we can safely set instance to this one
        Instance = this;
    }


    void Awake()
    {
#if Define_OutLine
        if (!SystemInfo.supportsImageEffects)
        {
            enabled = false;
            return;
        }

        if (!blurMaterial.shader.isSupported)
            enabled = false;
        if (!compositeMaterial.shader.isSupported)
            enabled = false;
        if (!cutoffMaterial.shader.isSupported)
            enabled = false;
        if (!outterLineMat.shader.isSupported)
            enabled = false;
        if (!outterLineTexture)
        {
            outterLineTexture = new RenderTexture((int)GetComponent<Camera>().pixelWidth, (int)GetComponent<Camera>().pixelHeight, 16);
            outterLineTexture.hideFlags = HideFlags.DontSave;
        }
#endif
        if (this.swithableShaders.Length == 0)
            enabled = false;
        foreach (Shader shader in this.swithableShaders)
        {
            if (!shader.isSupported)
                enabled = false;
        }
    }
#if Define_OutLine
    void OnDisable()
    {

        if (outterLineTexture)
        {
            DestroyImmediate(outterLineTexture);
            outterLineTexture = null;
        }
        if (m_CompositeMaterial)
        {
            DestroyImmediate(m_CompositeMaterial);
        }
        if (m_BlurMaterial)
        {
            DestroyImmediate(m_BlurMaterial);
        }
        if (m_outterLineMat)
        {
            DestroyImmediate(m_outterLineMat);
        }
    }
#endif

#if Define_OutLine
    public void FourTapCone(RenderTexture source, RenderTexture dest, int iteration)
    {
        float off = 0.5f + iteration * Spread;
        Graphics.BlitMultiTap(source, dest, blurMaterial,
            new Vector2(off, off),
            new Vector2(-off, off),
            new Vector2(off, -off),
            new Vector2(-off, -off)
        );
    }

    void OnPreRender()
    {
        outterLineCamera.targetTexture = outterLineTexture;
        outterLineCamera.RenderWithShader(outterLineMat.shader, "");
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Iterations = Mathf.Clamp(Iterations, 0, 15);
        Spread = Mathf.Clamp(Spread, 0.5f, 6.0f);

        RenderTexture buffer = RenderTexture.GetTemporary(outterLineTexture.width, outterLineTexture.height, 0);
        RenderTexture buffer2 = RenderTexture.GetTemporary(outterLineTexture.width, outterLineTexture.height, 0);

        Graphics.Blit(outterLineTexture, buffer);

        bool oddEven = true;
        for (int i = 0; i < Iterations; i++)
        {
            if (oddEven)
                FourTapCone(buffer, buffer2, i);
            else
                FourTapCone(buffer2, buffer, i);
            oddEven = !oddEven;
        }
        Graphics.Blit(source, destination);
        if (oddEven)
        {
            Graphics.Blit(outterLineTexture, buffer, cutoffMaterial);
            Graphics.Blit(buffer, destination, compositeMaterial);
        }
        else
        {
            Graphics.Blit(outterLineTexture, buffer2, cutoffMaterial);
            Graphics.Blit(buffer2, destination, compositeMaterial);
        }

        RenderTexture.ReleaseTemporary(buffer);
        RenderTexture.ReleaseTemporary(buffer2);
    }
#endif

    public void Switch(Renderer renderer, int methodID)
    {
        if (!enabled)
            return;
        Material[] sharedMaterials = renderer.sharedMaterials;
        int materialLength = sharedMaterials.Length;
        Material[] materialList = new Material[materialLength];//

        Shader switchShader;
        if (methodID < this.swithableShaders.Length)
            switchShader = this.swithableShaders[methodID];
        else
            switchShader = this.swithableShaders[0];
        for (int i = 0; i < materialLength; i++)
        {
            Material material = new Material(sharedMaterials[i]);
            material.hideFlags = HideFlags.HideAndDontSave;
            material.shader = switchShader;//rimLightSpecBumpShader
            material.SetColor("_RimColor", outterColor);
            //material.name = sharedMaterials[i].name;

            materialList[i] = material;
        }
        renderer.sharedMaterials = materialList;
    }
}
