using System;
using System.Collections.Generic;
using UnityEngine;

public class VirtualElementGenerator : MonoBehaviour
{
    private const float MillimeterToMeter = 0.001f;

    [Serializable]
    public class BeamDefinition
    {
        [Header("Beam Identity")]
        public string beamId = "Beam_001";

        [Header("Beam Nodes")]
        public Transform startNode;
        public Transform endNode;
    }

    [Header("Beams")]
    [SerializeField]
    private List<BeamDefinition> beams =
        new List<BeamDefinition>();

    [Header("Element Size in Millimeters")]
    [Tooltip("Length of each finite element along the beam.")]
    [SerializeField]
    private float segmentLengthMm = 10.0f;

    [Tooltip("Rectangle section width.")]
    [SerializeField]
    private float sectionWidthMm = 30.0f;

    [Tooltip("Rectangle section height.")]
    [SerializeField]
    private float sectionHeightMm = 400.0f;

    [Header("Output")]
    [SerializeField]
    private Transform outputRoot;

    [SerializeField]
    private Material baseMaterial;

    [Header("Generation")]
    [SerializeField]
    private bool generateOnStart = false;

    [SerializeField]
    private bool clearBeforeGenerate = true;

    [Header("Strain Visualization")]
    [Tooltip("Absolute strain corresponding to the strongest color.")]
    [SerializeField]
    private float maxAbsoluteStrain = 0.001f;

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateAllBeams();
        }
    }

    [ContextMenu("Generate All Beams")]
    public void GenerateAllBeams()
    {
        if (outputRoot == null)
        {
            outputRoot = transform;
        }

        if (segmentLengthMm <= 0.0f)
        {
            UnityEngine.Debug.LogError(
                "Segment Length must be greater than zero.");

            return;
        }

        if (sectionWidthMm <= 0.0f)
        {
            UnityEngine.Debug.LogError(
                "Section Width must be greater than zero.");

            return;
        }

        if (sectionHeightMm <= 0.0f)
        {
            UnityEngine.Debug.LogError(
                "Section Height must be greater than zero.");

            return;
        }

        if (clearBeforeGenerate)
        {
            ClearAllGeneratedElements();
        }

        HashSet<string> beamIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        int totalGeneratedCount = 0;

        foreach (BeamDefinition beam in beams)
        {
            if (beam == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(beam.beamId))
            {
                UnityEngine.Debug.LogWarning(
                    "A beam has an empty Beam ID.");

                continue;
            }

            if (!beamIds.Add(beam.beamId))
            {
                UnityEngine.Debug.LogWarning(
                    "Duplicate Beam ID: " +
                    beam.beamId);

                continue;
            }

            if (beam.startNode == null)
            {
                UnityEngine.Debug.LogWarning(
                    beam.beamId +
                    ": Start Node is not assigned.");

                continue;
            }

            if (beam.endNode == null)
            {
                UnityEngine.Debug.LogWarning(
                    beam.beamId +
                    ": End Node is not assigned.");

                continue;
            }

            int generatedCount =
                GenerateOneBeam(beam);

            totalGeneratedCount +=
                generatedCount;
        }

        UnityEngine.Debug.Log(
            "Generated " +
            totalGeneratedCount +
            " elements for " +
            beamIds.Count +
            " beams.");
    }

    private int GenerateOneBeam(
        BeamDefinition beam)
    {
        float segmentLength =
            segmentLengthMm *
            MillimeterToMeter;

        float sectionWidth =
            sectionWidthMm *
            MillimeterToMeter;

        float sectionHeight =
            sectionHeightMm *
            MillimeterToMeter;

        Vector3 start =
            beam.startNode.position;

        Vector3 end =
            beam.endNode.position;

        Vector3 axis =
            end - start;

        float totalLength =
            axis.magnitude;

        if (totalLength < 0.000001f)
        {
            UnityEngine.Debug.LogWarning(
                beam.beamId +
                ": Start Node and End Node are too close.");

            return 0;
        }

        Vector3 direction =
            axis.normalized;

        int elementCount =
            Mathf.Max(
                1,
                Mathf.CeilToInt(
                    totalLength /
                    segmentLength -
                    0.00001f));

        int generatedCount = 0;

        for (int i = 0; i < elementCount; i++)
        {
            float distanceStart =
                i *
                segmentLength;

            float distanceEnd =
                Mathf.Min(
                    (i + 1) *
                    segmentLength,
                    totalLength);

            Vector3 elementStart =
                start +
                direction *
                distanceStart;

            Vector3 elementEnd =
                start +
                direction *
                distanceEnd;

            string elementId =
                beam.beamId +
                "_E" +
                (i + 1).ToString("D3");

            GameObject element =
                CreateElement(
                    beam.beamId,
                    elementId,
                    beam.startNode.name,
                    beam.endNode.name,
                    elementStart,
                    elementEnd,
                    distanceStart,
                    distanceEnd,
                    sectionWidth,
                    sectionHeight);

            if (element != null)
            {
                generatedCount++;
            }
        }

        UnityEngine.Debug.Log(
            beam.beamId +
            ": generated " +
            generatedCount +
            " elements.");

        return generatedCount;
    }

    private GameObject CreateElement(
        string beamId,
        string elementId,
        string startNodeId,
        string endNodeId,
        Vector3 elementStart,
        Vector3 elementEnd,
        float distanceStart,
        float distanceEnd,
        float sectionWidth,
        float sectionHeight)
    {
        Vector3 direction =
            elementEnd -
            elementStart;

        float length =
            direction.magnitude;

        if (length < 0.000001f)
        {
            return null;
        }

        GameObject element =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube);

        element.name =
            elementId;

        if (outputRoot == null)
        {
            outputRoot = transform;
        }

        element.transform.SetParent(
            outputRoot,
            true);

        // 单元中心位于起点和终点的中点。
        element.transform.position =
            (elementStart + elementEnd) *
            0.5f;

        // Cube 的本地 Y 轴作为梁长度方向。
        element.transform.rotation =
            Quaternion.FromToRotation(
                Vector3.up,
                direction.normalized);

        // Unity 默认 Cube 尺寸为 1×1×1。
        //
        // X：30 mm 截面宽度
        // Y：当前单元长度，通常是 10 mm
        // Z：400 mm 截面高度
        element.transform.localScale =
            new Vector3(
                sectionWidth,
                length,
                sectionHeight);

        MeshRenderer renderer =
            element.GetComponent<MeshRenderer>();

        if (renderer != null &&
            baseMaterial != null)
        {
            renderer.sharedMaterial =
                baseMaterial;
        }

        BeamElementVisual visual =
            element.AddComponent<BeamElementVisual>();

        visual.Initialize(
            beamId,
            elementId,
            startNodeId,
            endNodeId,
            elementStart,
            elementEnd,
            distanceStart,
            distanceEnd,
            maxAbsoluteStrain);

        return element;
    }

    [ContextMenu("Clear All Generated Elements")]
    public void ClearAllGeneratedElements()
    {
        if (outputRoot == null)
        {
            outputRoot = transform;
        }

        List<GameObject> objectsToDelete =
            new List<GameObject>();

        foreach (Transform child in outputRoot)
        {
            if (child == null)
            {
                continue;
            }

            BeamElementVisual visual =
                child.GetComponent<BeamElementVisual>();

            if (visual != null)
            {
                objectsToDelete.Add(
                    child.gameObject);
            }
        }

        foreach (GameObject element in objectsToDelete)
        {
            if (UnityEngine.Application.isPlaying)
            {
                Destroy(element);
            }
            else
            {
                DestroyImmediate(element);
            }
        }

        UnityEngine.Debug.Log(
            "Cleared " +
            objectsToDelete.Count +
            " generated elements.");
    }

    public void ApplyStrain(
        string elementId,
        float strain)
    {
        if (outputRoot == null)
        {
            UnityEngine.Debug.LogWarning(
                "Output Root is not assigned.");

            return;
        }

        Transform element =
            outputRoot.Find(elementId);

        if (element == null)
        {
            UnityEngine.Debug.LogWarning(
                "Element not found: " +
                elementId);

            return;
        }

        BeamElementVisual visual =
            element.GetComponent<BeamElementVisual>();

        if (visual == null)
        {
            UnityEngine.Debug.LogWarning(
                "BeamElementVisual is missing on: " +
                elementId);

            return;
        }

        visual.SetStrain(strain);
    }
}


public class BeamElementVisual : MonoBehaviour
{
    public string beamId;
    public string elementId;

    public string startNodeId;
    public string endNodeId;

    public Vector3 startPosition;
    public Vector3 endPosition;

    public float distanceStart;
    public float distanceEnd;

    private Renderer targetRenderer;
    private MaterialPropertyBlock propertyBlock;
    private float maxAbsoluteStrain = 0.001f;

    public void Initialize(
        string currentBeamId,
        string currentElementId,
        string currentStartNodeId,
        string currentEndNodeId,
        Vector3 currentStartPosition,
        Vector3 currentEndPosition,
        float currentDistanceStart,
        float currentDistanceEnd,
        float currentMaxAbsoluteStrain)
    {
        beamId =
            currentBeamId;

        elementId =
            currentElementId;

        startNodeId =
            currentStartNodeId;

        endNodeId =
            currentEndNodeId;

        startPosition =
            currentStartPosition;

        endPosition =
            currentEndPosition;

        distanceStart =
            currentDistanceStart;

        distanceEnd =
            currentDistanceEnd;

        maxAbsoluteStrain =
            Mathf.Max(
                Mathf.Abs(
                    currentMaxAbsoluteStrain),
                0.000001f);

        targetRenderer =
            GetComponent<Renderer>();

        propertyBlock =
            new MaterialPropertyBlock();

        // 初始显示为中性灰色。
        SetStrain(0.0f);
    }

    public void SetStrain(
        float strain)
    {
        if (targetRenderer == null)
        {
            targetRenderer =
                GetComponent<Renderer>();
        }

        if (propertyBlock == null)
        {
            propertyBlock =
                new MaterialPropertyBlock();
        }

        if (targetRenderer == null)
        {
            return;
        }

        float normalized =
            Mathf.Clamp01(
                Mathf.Abs(strain) /
                maxAbsoluteStrain);

        Color color =
            StrainToColor(
                strain,
                normalized);

        propertyBlock.Clear();

        // URP Lit Shader。
        propertyBlock.SetColor(
            "_BaseColor",
            color);

        // Built-in Standard Shader。
        propertyBlock.SetColor(
            "_Color",
            color);

        targetRenderer.SetPropertyBlock(
            propertyBlock);
    }

    private Color StrainToColor(
        float strain,
        float normalized)
    {
        Color neutral =
            new Color(
                0.72f,
                0.75f,
                0.78f,
                1.0f);

        if (strain < 0.0f)
        {
            Color compression =
                new Color(
                    0.05f,
                    0.25f,
                    1.0f,
                    1.0f);

            return Color.Lerp(
                neutral,
                compression,
                normalized);
        }

        Color tension =
            new Color(
                1.0f,
                0.05f,
                0.02f,
                1.0f);

        return Color.Lerp(
            neutral,
            tension,
            normalized);
    }
}