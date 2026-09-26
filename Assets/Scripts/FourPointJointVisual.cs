using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visual-only rigid joint block. Its vertex colors are interpolated from
/// the four-point strain colors of all connected member-end elements.
/// </summary>
public class FourPointJointVisual : MonoBehaviour
{
    public string jointId;

    [SerializeField]
    private string[] connectedElementIds;

    [SerializeField]
    private Vector3[] connectedMemberDirections;

    private MeshFilter meshFilter;
    private MeshRenderer targetRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Mesh mesh;
    private Vector3[] vertices;
    private Vector3[] normals;
    private Color[] vertexColors;
    private Color[] cornerColors;
    private float jointHalfSize;

    public string[] ConnectedElementIds => connectedElementIds;

    private void Awake()
    {
        EnsureRuntimeReferences();
        ApplyCornerShaderColors();
    }

    public void Initialize(
        string currentJointId,
        float size,
        string[] currentConnectedElementIds,
        Vector3[] currentConnectedMemberDirections,
        int surfaceSubdivisions)
    {
        jointId = currentJointId;
        connectedElementIds = currentConnectedElementIds;
        connectedMemberDirections = currentConnectedMemberDirections;
        jointHalfSize = Mathf.Max(Mathf.Abs(size) * 0.5f, 0.000001f);

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
        BuildCubeMesh(
            Mathf.Max(Mathf.Abs(size), 0.000001f),
            Mathf.Clamp(surfaceSubdivisions, 2, 24));
        SetNeutralColor();
    }

    public void RefreshColors(
        IList<FourPointBeamElementVisual> connectedElements)
    {
        EnsureRuntimeReferences();
        if (mesh == null || vertices == null || vertexColors == null)
        {
            return;
        }

        Dictionary<string, FourPointBeamElementVisual> elementById =
            new Dictionary<string, FourPointBeamElementVisual>(
                StringComparer.OrdinalIgnoreCase);
        if (connectedElements != null)
        {
            foreach (FourPointBeamElementVisual element in connectedElements)
            {
                if (element != null &&
                    !string.IsNullOrWhiteSpace(element.elementId))
                {
                    elementById[element.elementId] = element;
                }
            }
        }

        CalculateCornerColors(elementById);

        for (int i = 0; i < vertices.Length; i++)
        {
            vertexColors[i] = InterpolateCornerColors(vertices[i]);
        }

        mesh.colors = vertexColors;
        ApplyCornerShaderColors();
    }

    public void SetNeutralColor()
    {
        EnsureRuntimeReferences();
        if (mesh == null || vertexColors == null)
        {
            return;
        }

        Color neutral = NeutralColor();
        EnsureCornerColors();
        for (int i = 0; i < cornerColors.Length; i++)
        {
            cornerColors[i] = neutral;
        }

        for (int i = 0; i < vertexColors.Length; i++)
        {
            vertexColors[i] = neutral;
        }

        mesh.colors = vertexColors;
        ApplyCornerShaderColors();
    }

    public Color SampleColorAtWorldPoint(Vector3 worldPoint)
    {
        EnsureRuntimeReferences();
        EnsureCornerColors();
        return InterpolateCornerColors(
            transform.InverseTransformPoint(worldPoint));
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

        if (vertices == null || vertices.Length != mesh.vertexCount)
        {
            vertices = mesh.vertices;
        }

        if (normals == null || normals.Length != mesh.vertexCount)
        {
            normals = mesh.normals;
        }

        if (jointHalfSize <= 0f)
        {
            Bounds bounds = mesh.bounds;
            jointHalfSize = Mathf.Max(
                bounds.extents.x,
                bounds.extents.y,
                bounds.extents.z,
                0.000001f);
        }

        if (vertexColors == null || vertexColors.Length != mesh.vertexCount)
        {
            Color[] existingColors = mesh.colors;
            vertexColors = existingColors != null &&
                           existingColors.Length == mesh.vertexCount
                ? existingColors
                : new Color[mesh.vertexCount];
        }
    }

    private void ApplyCornerShaderColors()
    {
        if (targetRenderer == null || propertyBlock == null)
        {
            return;
        }

        EnsureCornerColors();
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_StartP1Color", cornerColors[2]);
        propertyBlock.SetColor("_StartP2Color", cornerColors[3]);
        propertyBlock.SetColor("_StartP3Color", cornerColors[1]);
        propertyBlock.SetColor("_StartP4Color", cornerColors[0]);
        propertyBlock.SetColor("_EndP1Color", cornerColors[6]);
        propertyBlock.SetColor("_EndP2Color", cornerColors[7]);
        propertyBlock.SetColor("_EndP3Color", cornerColors[5]);
        propertyBlock.SetColor("_EndP4Color", cornerColors[4]);
        propertyBlock.SetFloat("_SectionWidth", jointHalfSize * 2f);
        propertyBlock.SetFloat("_SectionHeight", jointHalfSize * 2f);
        propertyBlock.SetFloat("_ElementLength", jointHalfSize * 2f);
        propertyBlock.SetFloat("_UseVertexColors", 0f);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void BuildCubeMesh(float size, int subdivisions)
    {
        float h = size * 0.5f;
        List<Vector3> vertexList = new List<Vector3>();
        List<Vector3> normalList = new List<Vector3>();
        List<int> triangleList = new List<int>();

        AddSubdividedFace(
            vertexList, normalList, triangleList,
            Vector3.forward * h, Vector3.right, Vector3.up,
            Vector3.forward, size, subdivisions);
        AddSubdividedFace(
            vertexList, normalList, triangleList,
            Vector3.right * h, Vector3.back, Vector3.up,
            Vector3.right, size, subdivisions);
        AddSubdividedFace(
            vertexList, normalList, triangleList,
            Vector3.back * h, Vector3.left, Vector3.up,
            Vector3.back, size, subdivisions);
        AddSubdividedFace(
            vertexList, normalList, triangleList,
            Vector3.left * h, Vector3.forward, Vector3.up,
            Vector3.left, size, subdivisions);
        AddSubdividedFace(
            vertexList, normalList, triangleList,
            Vector3.down * h, Vector3.right, Vector3.forward,
            Vector3.down, size, subdivisions);
        AddSubdividedFace(
            vertexList, normalList, triangleList,
            Vector3.up * h, Vector3.right, Vector3.back,
            Vector3.up, size, subdivisions);

        vertices = vertexList.ToArray();
        normals = normalList.ToArray();
        int[] triangles = triangleList.ToArray();

        mesh = new Mesh
        {
            name = jointId + "_StressJointMesh"
        };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        vertexColors = new Color[vertices.Length];
        mesh.colors = vertexColors;
        meshFilter.sharedMesh = mesh;
    }

    private static void AddSubdividedFace(
        List<Vector3> vertexList,
        List<Vector3> normalList,
        List<int> triangleList,
        Vector3 faceCenter,
        Vector3 horizontalAxis,
        Vector3 verticalAxis,
        Vector3 faceNormal,
        float size,
        int subdivisions)
    {
        int firstVertex = vertexList.Count;
        for (int row = 0; row <= subdivisions; row++)
        {
            float v = (float)row / subdivisions - 0.5f;
            for (int column = 0; column <= subdivisions; column++)
            {
                float u = (float)column / subdivisions - 0.5f;
                vertexList.Add(
                    faceCenter +
                    horizontalAxis * (u * size) +
                    verticalAxis * (v * size));
                normalList.Add(faceNormal);
            }
        }

        int rowLength = subdivisions + 1;
        for (int row = 0; row < subdivisions; row++)
        {
            for (int column = 0; column < subdivisions; column++)
            {
                int a = firstVertex + row * rowLength + column;
                int b = a + 1;
                int d = a + rowLength;
                int c = d + 1;
                triangleList.Add(a);
                triangleList.Add(b);
                triangleList.Add(c);
                triangleList.Add(a);
                triangleList.Add(c);
                triangleList.Add(d);
            }
        }
    }

    private void CalculateCornerColors(
        Dictionary<string, FourPointBeamElementVisual> elementById)
    {
        EnsureCornerColors();
        Vector3[] localCorners = GetLocalCorners();
        for (int cornerIndex = 0;
             cornerIndex < localCorners.Length;
             cornerIndex++)
        {
            Vector3 worldPoint =
                transform.TransformPoint(localCorners[cornerIndex]);
            cornerColors[cornerIndex] = CalculateAveragedMemberColor(
                worldPoint,
                elementById);
        }
    }

    private Color CalculateAveragedMemberColor(
        Vector3 worldPoint,
        Dictionary<string, FourPointBeamElementVisual> elementById)
    {
        Color accumulatedColor = Color.clear;
        int validElementCount = 0;

        if (connectedElementIds == null)
        {
            return NeutralColor();
        }

        for (int i = 0; i < connectedElementIds.Length; i++)
        {
            if (!TryGetConnectedElement(
                    i,
                    elementById,
                    out FourPointBeamElementVisual element))
            {
                continue;
            }

            accumulatedColor +=
                element.SampleColorAtWorldPoint(worldPoint);
            validElementCount++;
        }

        return validElementCount > 0
            ? accumulatedColor / validElementCount
            : NeutralColor();
    }

    private Color InterpolateCornerColors(Vector3 localPoint)
    {
        EnsureCornerColors();
        float safeSize = Mathf.Max(jointHalfSize * 2f, 0.000001f);
        float x = Mathf.Clamp01(localPoint.x / safeSize + 0.5f);
        float y = Mathf.Clamp01(localPoint.y / safeSize + 0.5f);
        float z = Mathf.Clamp01(localPoint.z / safeSize + 0.5f);

        Color bottomBack = Color.Lerp(cornerColors[0], cornerColors[1], x);
        Color bottomFront = Color.Lerp(cornerColors[2], cornerColors[3], x);
        Color topBack = Color.Lerp(cornerColors[4], cornerColors[5], x);
        Color topFront = Color.Lerp(cornerColors[6], cornerColors[7], x);
        Color bottom = Color.Lerp(bottomBack, bottomFront, z);
        Color top = Color.Lerp(topBack, topFront, z);
        return Color.Lerp(bottom, top, y);
    }

    private Vector3[] GetLocalCorners()
    {
        float h = jointHalfSize;
        return new[]
        {
            new Vector3(-h, -h, -h),
            new Vector3( h, -h, -h),
            new Vector3(-h, -h,  h),
            new Vector3( h, -h,  h),
            new Vector3(-h,  h, -h),
            new Vector3( h,  h, -h),
            new Vector3(-h,  h,  h),
            new Vector3( h,  h,  h)
        };
    }

    private void EnsureCornerColors()
    {
        if (cornerColors == null || cornerColors.Length != 8)
        {
            cornerColors = new Color[8];
            Color neutral = NeutralColor();
            for (int i = 0; i < cornerColors.Length; i++)
            {
                cornerColors[i] = neutral;
            }
        }
    }

    private bool TryGetConnectedElement(
        int connectionIndex,
        Dictionary<string, FourPointBeamElementVisual> elementById,
        out FourPointBeamElementVisual element)
    {
        element = null;
        return connectedElementIds != null &&
               connectionIndex >= 0 &&
               connectionIndex < connectedElementIds.Length &&
               elementById.TryGetValue(
                   connectedElementIds[connectionIndex],
                   out element) &&
               element != null;
    }

    private static Color NeutralColor()
    {
        return new Color(0.92f, 0.92f, 0.92f, 1f);
    }
}
