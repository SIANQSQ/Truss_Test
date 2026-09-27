using UnityEngine;

/// <summary>
/// One finite-element display segment. The four strain values belong to
/// four corners of the local X/Z cross-section:
/// P1 (-X,+Z), P2 (+X,+Z), P3 (+X,-Z), P4 (-X,-Z).
/// Local Y is the element axis from start to end.
/// </summary>
public class FourPointBeamElementVisual : MonoBehaviour
{
    public string beamId;
    public string elementId;
    public string startNodeId;
    public string endNodeId;

    public Vector3 startPosition;
    public Vector3 endPosition;

    public float strainP1;
    public float strainP2;
    public float strainP3;
    public float strainP4;

    [SerializeField]
    private float maxAbsoluteStrain = 0.001f;

    private MeshFilter meshFilter;
    private MeshRenderer targetRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Mesh mesh;
    private Color[] vertexColors;
    private float sectionWidth;
    private float sectionHeight;
    private Color startP1Color;
    private Color startP2Color;
    private Color startP3Color;
    private Color startP4Color;
    private Color endP1Color;
    private Color endP2Color;
    private Color endP3Color;
    private Color endP4Color;

    public float SectionWidth
    {
        get
        {
            EnsureRuntimeReferences();
            return sectionWidth;
        }
    }

    public float SectionHeight
    {
        get
        {
            EnsureRuntimeReferences();
            return sectionHeight;
        }
    }

    public float ElementLength
    {
        get { return Vector3.Distance(startPosition, endPosition); }
    }

    public float MinimumStrain
    {
        get { return Mathf.Min(strainP1, strainP2, strainP3, strainP4); }
    }

    public float MaximumStrain
    {
        get { return Mathf.Max(strainP1, strainP2, strainP3, strainP4); }
    }

    public float AverageStrain
    {
        get { return (strainP1 + strainP2 + strainP3 + strainP4) * 0.25f; }
    }

    public float PeakAbsoluteStrain
    {
        get
        {
            return Mathf.Max(
                Mathf.Abs(strainP1),
                Mathf.Abs(strainP2),
                Mathf.Abs(strainP3),
                Mathf.Abs(strainP4));
        }
    }

    private void Awake()
    {
        EnsureRuntimeReferences();
        if (mesh != null)
        {
            SetCornerStrains(
                strainP1,
                strainP2,
                strainP3,
                strainP4);
        }
    }

    public void Initialize(
        string currentBeamId,
        string currentElementId,
        string currentStartNodeId,
        string currentEndNodeId,
        Vector3 currentStartPosition,
        Vector3 currentEndPosition,
        float width,
        float height,
        float currentMaxAbsoluteStrain)
    {
        beamId = currentBeamId;
        elementId = currentElementId;
        startNodeId = currentStartNodeId;
        endNodeId = currentEndNodeId;
        startPosition = currentStartPosition;
        endPosition = currentEndPosition;
        maxAbsoluteStrain = Mathf.Max(
            Mathf.Abs(currentMaxAbsoluteStrain),
            0.000001f);
        sectionWidth = Mathf.Max(Mathf.Abs(width), 0.000001f);
        sectionHeight = Mathf.Max(Mathf.Abs(height), 0.000001f);

        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        targetRenderer = GetComponent<MeshRenderer>();
        if (targetRenderer == null)
        {
            targetRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        propertyBlock = new MaterialPropertyBlock();

        BuildBoxMesh(width, height);
        SetCornerStrains(0f, 0f, 0f, 0f);
    }

    public void SetCornerStrains(
        float p1,
        float p2,
        float p3,
        float p4)
    {
        strainP1 = p1;
        strainP2 = p2;
        strainP3 = p3;
        strainP4 = p4;

        EnsureRuntimeReferences();

        if (mesh == null || vertexColors == null)
        {
            UnityEngine.Debug.LogWarning(
                "Stress mesh is unavailable on element: " + elementId);
            return;
        }

        Color c1 = StrainToColor(strainP1);
        Color c2 = StrainToColor(strainP2);
        Color c3 = StrainToColor(strainP3);
        Color c4 = StrainToColor(strainP4);
        startP1Color = c1;
        startP2Color = c2;
        startP3Color = c3;
        startP4Color = c4;
        endP1Color = c1;
        endP2Color = c2;
        endP3Color = c3;
        endP4Color = c4;
        Color average = (c1 + c2 + c3 + c4) * 0.25f;

        // Vertex layout is four vertices per face:
        // top, right, bottom, left, front cap, back cap.
        SetFaceColors(0, c1, c2, c2, c1); // +Z side
        SetFaceColors(4, c2, c3, c3, c2); // +X side
        SetFaceColors(8, c3, c4, c4, c3); // -Z side
        SetFaceColors(12, c4, c1, c1, c4); // -X side
        SetFaceColors(16, average, average, average, average);
        SetFaceColors(20, average, average, average, average);

        mesh.colors = vertexColors;

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<MeshRenderer>();
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        ApplyShaderColors();
    }

    public void SetJointEndColors(
        bool atStart,
        Color p1,
        Color p2,
        Color p3,
        Color p4)
    {
        EnsureRuntimeReferences();

        if (atStart)
        {
            startP1Color = p1;
            startP2Color = p2;
            startP3Color = p3;
            startP4Color = p4;
        }
        else
        {
            endP1Color = p1;
            endP2Color = p2;
            endP3Color = p3;
            endP4Color = p4;
        }

        ApplyShaderColors();
    }

    public void ResetJointEndColors()
    {
        startP1Color = StrainToColor(strainP1);
        startP2Color = StrainToColor(strainP2);
        startP3Color = StrainToColor(strainP3);
        startP4Color = StrainToColor(strainP4);
        endP1Color = startP1Color;
        endP2Color = startP2Color;
        endP3Color = startP3Color;
        endP4Color = startP4Color;
        ApplyShaderColors();
    }

    public bool IsStartEndCloserTo(Vector3 worldPoint)
    {
        return (startPosition - worldPoint).sqrMagnitude <=
               (endPosition - worldPoint).sqrMagnitude;
    }

    public Vector3[] GetCrossSectionCornerWorldPoints(bool atStart)
    {
        EnsureRuntimeReferences();
        float localY = atStart ? -0.5f : 0.5f;
        float halfWidth = sectionWidth * 0.5f;
        float halfHeight = sectionHeight * 0.5f;

        return new[]
        {
            transform.TransformPoint(
                new Vector3(-halfWidth, localY, halfHeight)),
            transform.TransformPoint(
                new Vector3(halfWidth, localY, halfHeight)),
            transform.TransformPoint(
                new Vector3(halfWidth, localY, -halfHeight)),
            transform.TransformPoint(
                new Vector3(-halfWidth, localY, -halfHeight))
        };
    }

    public Color SampleColorAtWorldPoint(Vector3 worldPoint)
    {
        EnsureRuntimeReferences();

        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        float safeWidth = Mathf.Max(sectionWidth, 0.000001f);
        float safeHeight = Mathf.Max(sectionHeight, 0.000001f);
        float u = Mathf.Clamp01(localPoint.x / safeWidth + 0.5f);
        float v = Mathf.Clamp01(localPoint.z / safeHeight + 0.5f);

        Color c1 = StrainToColor(strainP1);
        Color c2 = StrainToColor(strainP2);
        Color c3 = StrainToColor(strainP3);
        Color c4 = StrainToColor(strainP4);
        Color bottomColor = Color.Lerp(c4, c3, u);
        Color topColor = Color.Lerp(c1, c2, u);
        return Color.Lerp(bottomColor, topColor, v);
    }

    private void EnsureRuntimeReferences()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<MeshRenderer>();
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        if (mesh == null && meshFilter != null)
        {
            mesh = meshFilter.sharedMesh;
        }

        if (mesh == null)
        {
            return;
        }

        if (sectionWidth <= 0f)
        {
            sectionWidth = Mathf.Max(mesh.bounds.size.x, 0.000001f);
        }

        if (sectionHeight <= 0f)
        {
            sectionHeight = Mathf.Max(mesh.bounds.size.z, 0.000001f);
        }

        if (vertexColors == null || vertexColors.Length != mesh.vertexCount)
        {
            Color[] existingColors = mesh.colors;
            if (existingColors != null &&
                existingColors.Length == mesh.vertexCount)
            {
                vertexColors = existingColors;
            }
            else
            {
                vertexColors = new Color[mesh.vertexCount];
            }
        }
    }

    private void ApplyShaderColors()
    {
        if (targetRenderer == null)
        {
            return;
        }

        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_StartP1Color", startP1Color);
        propertyBlock.SetColor("_StartP2Color", startP2Color);
        propertyBlock.SetColor("_StartP3Color", startP3Color);
        propertyBlock.SetColor("_StartP4Color", startP4Color);
        propertyBlock.SetColor("_EndP1Color", endP1Color);
        propertyBlock.SetColor("_EndP2Color", endP2Color);
        propertyBlock.SetColor("_EndP3Color", endP3Color);
        propertyBlock.SetColor("_EndP4Color", endP4Color);
        propertyBlock.SetFloat("_SectionWidth", sectionWidth);
        propertyBlock.SetFloat("_SectionHeight", sectionHeight);
        propertyBlock.SetFloat("_ElementLength", 1f);
        propertyBlock.SetFloat("_UseVertexColors", 0f);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    public void SetAverageStrain(float strain)
    {
        SetCornerStrains(strain, strain, strain, strain);
    }

    private void BuildBoxMesh(float width, float height)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        // The element is one unit long in local Y. The generator scales Y
        // to the actual segment length.
        Vector3[] vertices =
        {
            // +Z side (P1 -> P2)
            new Vector3(-halfWidth, -0.5f, halfHeight),
            new Vector3( halfWidth, -0.5f, halfHeight),
            new Vector3( halfWidth,  0.5f, halfHeight),
            new Vector3(-halfWidth,  0.5f, halfHeight),
            // +X side (P2 -> P3)
            new Vector3(halfWidth, -0.5f,  halfHeight),
            new Vector3(halfWidth, -0.5f, -halfHeight),
            new Vector3(halfWidth,  0.5f, -halfHeight),
            new Vector3(halfWidth,  0.5f,  halfHeight),
            // -Z side (P3 -> P4)
            new Vector3( halfWidth, -0.5f, -halfHeight),
            new Vector3(-halfWidth, -0.5f, -halfHeight),
            new Vector3(-halfWidth,  0.5f, -halfHeight),
            new Vector3( halfWidth,  0.5f, -halfHeight),
            // -X side (P4 -> P1)
            new Vector3(-halfWidth, -0.5f, -halfHeight),
            new Vector3(-halfWidth, -0.5f,  halfHeight),
            new Vector3(-halfWidth,  0.5f,  halfHeight),
            new Vector3(-halfWidth,  0.5f, -halfHeight),
            // -Y cap
            new Vector3(-halfWidth, -0.5f, -halfHeight),
            new Vector3( halfWidth, -0.5f, -halfHeight),
            new Vector3( halfWidth, -0.5f,  halfHeight),
            new Vector3(-halfWidth, -0.5f,  halfHeight),
            // +Y cap
            new Vector3(-halfWidth, 0.5f,  halfHeight),
            new Vector3( halfWidth, 0.5f,  halfHeight),
            new Vector3( halfWidth, 0.5f, -halfHeight),
            new Vector3(-halfWidth, 0.5f, -halfHeight)
        };

        Vector3[] normals =
        {
            Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
            Vector3.right, Vector3.right, Vector3.right, Vector3.right,
            Vector3.back, Vector3.back, Vector3.back, Vector3.back,
            Vector3.left, Vector3.left, Vector3.left, Vector3.left,
            Vector3.down, Vector3.down, Vector3.down, Vector3.down,
            Vector3.up, Vector3.up, Vector3.up, Vector3.up
        };

        int[] triangles =
        {
            0, 1, 2, 0, 2, 3,
            4, 5, 6, 4, 6, 7,
            8, 9, 10, 8, 10, 11,
            12, 13, 14, 12, 14, 15,
            16, 17, 18, 16, 18, 19,
            20, 21, 22, 20, 22, 23
        };

        mesh = new Mesh
        {
            name = elementId + "_StressMesh"
        };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        vertexColors = new Color[vertices.Length];
        mesh.colors = vertexColors;
        meshFilter.sharedMesh = mesh;
    }

    private void SetFaceColors(
        int offset,
        Color a,
        Color b,
        Color c,
        Color d)
    {
        vertexColors[offset] = a;
        vertexColors[offset + 1] = b;
        vertexColors[offset + 2] = c;
        vertexColors[offset + 3] = d;
    }

    private Color StrainToColor(float strain)
    {
        float normalized = Mathf.Clamp01(
            Mathf.Abs(strain) / maxAbsoluteStrain);

        Color neutral = new Color(0.92f, 0.92f, 0.92f, 1f);

        if (strain < 0f)
        {
            Color compression = new Color(0.02f, 0.15f, 1f, 1f);
            return Color.Lerp(neutral, compression, normalized);
        }

        Color tension = new Color(1f, 0.02f, 0.01f, 1f);
        return Color.Lerp(neutral, tension, normalized);
    }
}
