using System;
using System.Collections.Generic;
using UnityEngine;

public class BeamElementColorController : MonoBehaviour
{
    [Serializable]
    private class ElementRecord
    {
        public string elementId;
        public string beamId;
        public BeamElementVisual visual;
        public Renderer renderer;
    }

    [Header("Element Root")]
    [Tooltip("通常指定为 FiniteElementDisplay。")]
    [SerializeField]
    private Transform elementRoot;

    [Header("Scan")]
    [SerializeField]
    private bool scanOnStart = true;

    [SerializeField]
    private bool includeInactiveObjects = true;

    [SerializeField]
    private bool logScanResult = true;

    [Header("Strain Color")]
    [Tooltip("达到该绝对应变时显示最强颜色。")]
    [SerializeField]
    private float maxAbsoluteStrain = 0.001f;

    private readonly Dictionary<string, ElementRecord>
        elementById =
        new Dictionary<string, ElementRecord>(
            StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, List<ElementRecord>>
        elementsByBeamId =
        new Dictionary<string, List<ElementRecord>>(
            StringComparer.OrdinalIgnoreCase);

    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        propertyBlock =
            new MaterialPropertyBlock();
    }

    private void Start()
    {
        if (scanOnStart)
        {
            // 延迟一帧，确保生成器已经完成子梁创建。
            Invoke(
                nameof(RefreshElements),
                0.0f);
        }
    }

    [ContextMenu("Refresh Elements")]
    public void RefreshElements()
    {
        elementById.Clear();
        elementsByBeamId.Clear();

        if (elementRoot == null)
        {
            elementRoot = transform;
        }

        BeamElementVisual[] visuals =
            elementRoot.GetComponentsInChildren<
                BeamElementVisual>(
                    includeInactiveObjects);

        if (visuals == null ||
            visuals.Length == 0)
        {
            UnityEngine.Debug.LogWarning(
                "No BeamElementVisual was found under: " +
                elementRoot.name);

            return;
        }

        int validCount = 0;

        foreach (BeamElementVisual visual in visuals)
        {
            if (visual == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(
                    visual.elementId))
            {
                UnityEngine.Debug.LogWarning(
                    "A beam element has an empty elementId.");

                continue;
            }

            Renderer renderer =
                visual.GetComponent<Renderer>();

            if (renderer == null)
            {
                UnityEngine.Debug.LogWarning(
                    "No Renderer found on element: " +
                    visual.elementId);

                continue;
            }

            if (elementById.ContainsKey(
                    visual.elementId))
            {
                UnityEngine.Debug.LogWarning(
                    "Duplicate elementId found: " +
                    visual.elementId);

                continue;
            }

            ElementRecord record =
                new ElementRecord();

            record.elementId =
                visual.elementId;

            record.beamId =
                visual.beamId;

            record.visual =
                visual;

            record.renderer =
                renderer;

            elementById.Add(
                record.elementId,
                record);

            if (!elementsByBeamId.TryGetValue(
                    record.beamId,
                    out List<ElementRecord> beamElements))
            {
                beamElements =
                    new List<ElementRecord>();

                elementsByBeamId.Add(
                    record.beamId,
                    beamElements);
            }

            beamElements.Add(record);
            validCount++;
        }

        if (logScanResult)
        {
            UnityEngine.Debug.Log(
                "Identified " +
                validCount +
                " beam elements and " +
                elementsByBeamId.Count +
                " beams.");
        }
    }

    public void SetElementColor(
        string elementId,
        Color color)
    {
        if (!TryGetElement(
                elementId,
                out ElementRecord record))
        {
            return;
        }

        ApplyRendererColor(
            record.renderer,
            color);
    }

    public void SetBeamColor(
        string beamId,
        Color color)
    {
        if (string.IsNullOrWhiteSpace(beamId))
        {
            return;
        }

        if (!elementsByBeamId.TryGetValue(
                beamId,
                out List<ElementRecord> beamElements))
        {
            UnityEngine.Debug.LogWarning(
                "Beam not found: " +
                beamId);

            return;
        }

        foreach (ElementRecord record in beamElements)
        {
            if (record == null)
            {
                continue;
            }

            ApplyRendererColor(
                record.renderer,
                color);
        }
    }

    public void SetElementStrain(
        string elementId,
        float strain)
    {
        if (!TryGetElement(
                elementId,
                out ElementRecord record))
        {
            return;
        }

        Color color =
            StrainToColor(strain);

        ApplyRendererColor(
            record.renderer,
            color);
    }

    public void SetBeamStrain(
        string beamId,
        float strain)
    {
        if (string.IsNullOrWhiteSpace(beamId))
        {
            return;
        }

        if (!elementsByBeamId.TryGetValue(
                beamId,
                out List<ElementRecord> beamElements))
        {
            UnityEngine.Debug.LogWarning(
                "Beam not found: " +
                beamId);

            return;
        }

        Color color =
            StrainToColor(strain);

        foreach (ElementRecord record in beamElements)
        {
            if (record == null)
            {
                continue;
            }

            ApplyRendererColor(
                record.renderer,
                color);
        }
    }

    public void SetAllElementsColor(
        Color color)
    {
        foreach (ElementRecord record
                 in elementById.Values)
        {
            if (record == null)
            {
                continue;
            }

            ApplyRendererColor(
                record.renderer,
                color);
        }
    }

    public void ResetAllColors()
    {
        Color neutralColor =
            new Color(
                0.72f,
                0.75f,
                0.78f,
                1.0f);

        SetAllElementsColor(
            neutralColor);
    }

    public bool ContainsElement(
        string elementId)
    {
        if (string.IsNullOrWhiteSpace(elementId))
        {
            return false;
        }

        return elementById.ContainsKey(
            elementId);
    }

    public int GetElementCount()
    {
        return elementById.Count;
    }

    public int GetBeamCount()
    {
        return elementsByBeamId.Count;
    }

    private bool TryGetElement(
        string elementId,
        out ElementRecord record)
    {
        record = null;

        if (string.IsNullOrWhiteSpace(elementId))
        {
            UnityEngine.Debug.LogWarning(
                "Element ID is empty.");

            return false;
        }

        if (!elementById.TryGetValue(
                elementId,
                out record))
        {
            UnityEngine.Debug.LogWarning(
                "Element not found: " +
                elementId);

            return false;
        }

        if (record == null ||
            record.renderer == null)
        {
            UnityEngine.Debug.LogWarning(
                "Element Renderer is missing: " +
                elementId);

            return false;
        }

        return true;
    }

    private void ApplyRendererColor(
        Renderer renderer,
        Color color)
    {
        if (renderer == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock =
                new MaterialPropertyBlock();
        }

        propertyBlock.Clear();

        // URP Lit Shader。
        propertyBlock.SetColor(
            "_BaseColor",
            color);

        // Built-in Standard Shader。
        propertyBlock.SetColor(
            "_Color",
            color);

        // 使用属性块，不修改共享材质。
        // 因此每个子梁可以独立显示颜色。
        renderer.SetPropertyBlock(
            propertyBlock);
    }

    private Color StrainToColor(
        float strain)
    {
        float safeMaximum =
            Mathf.Max(
                Mathf.Abs(
                    maxAbsoluteStrain),
                0.000001f);

        float normalized =
            Mathf.Clamp01(
                Mathf.Abs(strain) /
                safeMaximum);

        Color neutral =
            new Color(
                0.72f,
                0.75f,
                0.78f,
                1.0f);

        if (strain < 0.0f)
        {
            // 负应变：受压，灰色到蓝色。
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

        // 正应变：受拉，灰色到红色。
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