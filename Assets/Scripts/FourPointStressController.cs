using System;
using System.Collections.Generic;
using UnityEngine;

public class FourPointStressController : MonoBehaviour
{
    [SerializeField]
    private Transform elementRoot;

    [SerializeField]
    private bool scanOnStart = true;

    private readonly Dictionary<string, FourPointBeamElementVisual> elements =
        new Dictionary<string, FourPointBeamElementVisual>(
            StringComparer.OrdinalIgnoreCase);

    private readonly List<FourPointJointVisual> joints =
        new List<FourPointJointVisual>();

    private readonly Dictionary<string, List<FourPointJointVisual>>
        jointsByElementId =
            new Dictionary<string, List<FourPointJointVisual>>(
                StringComparer.OrdinalIgnoreCase);

    private void Start()
    {
        if (scanOnStart)
        {
            Invoke(nameof(RefreshElements), 0f);
        }
    }

    [ContextMenu("Refresh Elements")]
    public void RefreshElements()
    {
        elements.Clear();
        joints.Clear();
        jointsByElementId.Clear();
        Transform root = elementRoot != null ? elementRoot : transform;
        FourPointBeamElementVisual[] visuals =
            root.GetComponentsInChildren<FourPointBeamElementVisual>(true);

        foreach (FourPointBeamElementVisual visual in visuals)
        {
            if (visual == null || string.IsNullOrWhiteSpace(visual.elementId))
            {
                continue;
            }

            if (elements.ContainsKey(visual.elementId))
            {
                UnityEngine.Debug.LogWarning(
                    "Duplicate element ID: " + visual.elementId);
                continue;
            }

            elements.Add(visual.elementId, visual);
        }

        FourPointJointVisual[] jointVisuals =
            root.GetComponentsInChildren<FourPointJointVisual>(true);
        foreach (FourPointJointVisual joint in jointVisuals)
        {
            if (joint == null)
            {
                continue;
            }

            joints.Add(joint);
            string[] connectedIds = joint.ConnectedElementIds;
            if (connectedIds == null)
            {
                continue;
            }

            foreach (string connectedId in connectedIds)
            {
                if (string.IsNullOrWhiteSpace(connectedId))
                {
                    continue;
                }

                if (!jointsByElementId.TryGetValue(
                        connectedId,
                        out List<FourPointJointVisual> connectedJoints))
                {
                    connectedJoints = new List<FourPointJointVisual>();
                    jointsByElementId.Add(connectedId, connectedJoints);
                }

                connectedJoints.Add(joint);
            }
        }

        RefreshAllJoints();

        UnityEngine.Debug.Log(
            "Four-point elements identified: " + elements.Count +
            "; interpolated joints identified: " + joints.Count);
    }

    public bool ContainsElement(string elementId)
    {
        return !string.IsNullOrWhiteSpace(elementId) &&
               elements.ContainsKey(elementId);
    }

    public bool TryGetElement(
        string elementId,
        out FourPointBeamElementVisual visual)
    {
        if (string.IsNullOrWhiteSpace(elementId))
        {
            visual = null;
            return false;
        }

        return elements.TryGetValue(elementId, out visual);
    }

    public List<FourPointBeamElementVisual> GetElementsForBeam(string beamId)
    {
        List<FourPointBeamElementVisual> result =
            new List<FourPointBeamElementVisual>();

        if (string.IsNullOrWhiteSpace(beamId))
        {
            return result;
        }

        foreach (FourPointBeamElementVisual element in elements.Values)
        {
            if (element != null &&
                string.Equals(
                    element.beamId,
                    beamId,
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Add(element);
            }
        }

        result.Sort((left, right) =>
            string.Compare(
                left.elementId,
                right.elementId,
                StringComparison.OrdinalIgnoreCase));
        return result;
    }

    public void SetElementCornerStrains(
        string elementId,
        float p1,
        float p2,
        float p3,
        float p4)
    {
        if (!elements.TryGetValue(elementId, out FourPointBeamElementVisual visual))
        {
            UnityEngine.Debug.LogWarning(
                "Four-point element not found: " + elementId);
            return;
        }

        visual.SetCornerStrains(p1, p2, p3, p4);
        RefreshJointsForElement(elementId);
    }

    public void SetElementAverageStrain(string elementId, float strain)
    {
        SetElementCornerStrains(elementId, strain, strain, strain, strain);
    }

    public void ResetAllElements()
    {
        foreach (FourPointBeamElementVisual visual in elements.Values)
        {
            if (visual != null)
            {
                visual.SetCornerStrains(0f, 0f, 0f, 0f);
            }
        }

        RefreshAllJoints();
    }

    public void RefreshAllJoints()
    {
        ResetAllJointEndOverrides();
        foreach (FourPointJointVisual joint in joints)
        {
            RefreshJoint(joint);
        }
    }

    private void RefreshJointsForElement(string elementId)
    {
        if (!jointsByElementId.TryGetValue(
                elementId,
                out List<FourPointJointVisual> connectedJoints))
        {
            return;
        }

        foreach (FourPointJointVisual joint in connectedJoints)
        {
            RefreshJoint(joint);
        }
    }

    private void ResetAllJointEndOverrides()
    {
        foreach (FourPointBeamElementVisual element in elements.Values)
        {
            if (element != null)
            {
                element.ResetJointEndColors();
            }
        }
    }

    private void RefreshJoint(FourPointJointVisual joint)
    {
        if (joint == null)
        {
            return;
        }

        List<FourPointBeamElementVisual> connectedElements =
            new List<FourPointBeamElementVisual>();
        string[] connectedIds = joint.ConnectedElementIds;

        if (connectedIds != null)
        {
            foreach (string connectedId in connectedIds)
            {
                if (elements.TryGetValue(
                        connectedId,
                        out FourPointBeamElementVisual element))
                {
                    connectedElements.Add(element);
                }
            }
        }

        joint.RefreshColors(connectedElements);

        foreach (FourPointBeamElementVisual element in connectedElements)
        {
            bool atStart = element.IsStartEndCloserTo(
                joint.transform.position);
            Vector3[] cornerPoints =
                element.GetCrossSectionCornerWorldPoints(atStart);
            if (cornerPoints == null || cornerPoints.Length != 4)
            {
                continue;
            }

            element.SetJointEndColors(
                atStart,
                joint.SampleColorAtWorldPoint(cornerPoints[0]),
                joint.SampleColorAtWorldPoint(cornerPoints[1]),
                joint.SampleColorAtWorldPoint(cornerPoints[2]),
                joint.SampleColorAtWorldPoint(cornerPoints[3]));
        }
    }
}
