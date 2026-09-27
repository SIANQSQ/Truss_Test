using System;
using System.Collections.Generic;
using UnityEngine;

public class FourPointBeamGenerator : MonoBehaviour
{
    private const float MillimeterToMeter = 0.001f;

    private class JointCandidate
    {
        public Vector3 center;
        public string elementId;
        public Vector3 memberDirection;
    }

    private class JointCluster
    {
        public Vector3 centerSum;
        public readonly List<JointCandidate> candidates =
            new List<JointCandidate>();

        public Vector3 Center => centerSum / candidates.Count;

        public void Add(JointCandidate candidate)
        {
            candidates.Add(candidate);
            centerSum += candidate.center;
        }
    }

    [Serializable]
    public class BeamDefinition
    {
        public string beamId = "Beam_001";
        public Transform startNode;
        public Transform endNode;
    }

    [SerializeField]
    private List<BeamDefinition> beams = new List<BeamDefinition>();

    [Header("Geometry in millimeters")]
    [SerializeField]
    private float segmentLengthMm = 10f;

    [SerializeField]
    private float sectionWidthMm = 30f;

    [SerializeField]
    private float sectionHeightMm = 400f;

    [SerializeField]
    private Transform outputRoot;

    [SerializeField]
    private Material vertexColorMaterial;

    [SerializeField]
    private float maxAbsoluteStrain = 0.001f;

    [Header("Joint blocks in millimeters")]
    [SerializeField]
    private bool generateJointBlocks = true;

    [SerializeField]
    private float jointSizeMm = 30f;

    [SerializeField]
    private float jointMergeToleranceMm = 2f;

    [SerializeField]
    private int minimumJointConnections = 2;

    [SerializeField]
    [Range(2, 24)]
    private int jointSurfaceSubdivisions = 8;

    [SerializeField]
    private bool generateOnStart;

    [SerializeField]
    private bool clearBeforeGenerate = true;

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

        if (segmentLengthMm <= 0f ||
            sectionWidthMm <= 0f ||
            sectionHeightMm <= 0f)
        {
            UnityEngine.Debug.LogError(
                "Segment length and section dimensions must be positive.");
            return;
        }

        if (clearBeforeGenerate)
        {
            ClearGeneratedElements();
        }

        int total = 0;
        List<JointCandidate> jointCandidates =
            new List<JointCandidate>();

        foreach (BeamDefinition beam in beams)
        {
            if (beam == null ||
                string.IsNullOrWhiteSpace(beam.beamId) ||
                beam.startNode == null ||
                beam.endNode == null)
            {
                UnityEngine.Debug.LogWarning(
                    "Skipped an incomplete beam definition.");
                continue;
            }

            int generatedCount = GenerateBeam(beam);
            total += generatedCount;

            if (generateJointBlocks && generatedCount > 0)
            {
                AddJointCandidates(
                    beam,
                    generatedCount,
                    jointCandidates);
            }
        }

        int jointCount = generateJointBlocks
            ? GenerateJointBlocks(jointCandidates)
            : 0;

        UnityEngine.Debug.Log(
            "Generated " + total + " four-point beam elements and " +
            jointCount + " interpolated joint blocks.");

        if (UnityEngine.Application.isPlaying)
        {
            FourPointStressController controller =
                FindFirstObjectByType<FourPointStressController>();
            if (controller != null)
            {
                controller.RefreshElements();
            }
        }
    }

    private int GenerateBeam(BeamDefinition beam)
    {
        Vector3 start = beam.startNode.position;
        Vector3 end = beam.endNode.position;
        Vector3 axis = end - start;
        float totalLength = axis.magnitude;

        if (totalLength < 0.000001f)
        {
            UnityEngine.Debug.LogWarning(
                beam.beamId + " has zero length.");
            return 0;
        }

        Vector3 direction = axis / totalLength;
        float segmentLength = segmentLengthMm * MillimeterToMeter;
        float width = sectionWidthMm * MillimeterToMeter;
        float height = sectionHeightMm * MillimeterToMeter;
        int count = Mathf.Max(1, Mathf.CeilToInt(
            totalLength / segmentLength - 0.00001f));
        int generated = 0;

        for (int i = 0; i < count; i++)
        {
            float d0 = i * segmentLength;
            float d1 = Mathf.Min((i + 1) * segmentLength, totalLength);
            Vector3 elementStart = start + direction * d0;
            Vector3 elementEnd = start + direction * d1;
            string elementId = beam.beamId + "_E" + (i + 1).ToString("D3");

            GameObject element = new GameObject(elementId);
            element.transform.SetParent(outputRoot, true);
            element.layer = outputRoot.gameObject.layer;
            element.transform.position = (elementStart + elementEnd) * 0.5f;
            element.transform.rotation = Quaternion.FromToRotation(
                Vector3.up, (elementEnd - elementStart).normalized);
            element.transform.localScale = new Vector3(
                1f,
                (elementEnd - elementStart).magnitude,
                1f);

            MeshFilter filter = element.AddComponent<MeshFilter>();
            MeshRenderer renderer = element.AddComponent<MeshRenderer>();
            FourPointBeamElementVisual visual =
                element.AddComponent<FourPointBeamElementVisual>();

            if (vertexColorMaterial != null)
            {
                renderer.sharedMaterial = vertexColorMaterial;
            }

            visual.Initialize(
                beam.beamId,
                elementId,
                beam.startNode.name,
                beam.endNode.name,
                elementStart,
                elementEnd,
                width,
                height,
                maxAbsoluteStrain);

            BoxCollider collider = element.AddComponent<BoxCollider>();
            collider.center = filter.sharedMesh != null
                ? filter.sharedMesh.bounds.center
                : Vector3.zero;
            collider.size = filter.sharedMesh != null
                ? filter.sharedMesh.bounds.size
                : new Vector3(width, 1f, height);

            // Initialize() creates and assigns the mesh. Keep the component
            // reference explicit so missing MeshFilter errors are obvious.
            if (filter.sharedMesh == null)
            {
                UnityEngine.Debug.LogError(
                    "Mesh was not created for " + elementId);
                DestroyImmediate(element);
                continue;
            }

            generated++;
        }

        return generated;
    }

    private void AddJointCandidates(
        BeamDefinition beam,
        int elementCount,
        List<JointCandidate> candidates)
    {
        Vector3 start = beam.startNode.position;
        Vector3 end = beam.endNode.position;
        Vector3 direction = (end - start).normalized;
        float halfJointSize = Mathf.Max(
            jointSizeMm * MillimeterToMeter * 0.5f,
            0.000001f);

        candidates.Add(new JointCandidate
        {
            center = start - direction * halfJointSize,
            elementId = beam.beamId + "_E001",
            memberDirection = direction
        });

        candidates.Add(new JointCandidate
        {
            center = end + direction * halfJointSize,
            elementId = beam.beamId + "_E" +
                        elementCount.ToString("D3"),
            memberDirection = -direction
        });
    }

    private int GenerateJointBlocks(List<JointCandidate> candidates)
    {
        float jointSize = Mathf.Max(
            jointSizeMm * MillimeterToMeter,
            0.000001f);
        float mergeTolerance = Mathf.Max(
            jointMergeToleranceMm * MillimeterToMeter,
            0.000001f);
        int requiredConnections = Mathf.Max(2, minimumJointConnections);
        List<JointCluster> clusters = new List<JointCluster>();

        foreach (JointCandidate candidate in candidates)
        {
            JointCluster nearestCluster = null;
            float nearestDistance = float.MaxValue;

            foreach (JointCluster cluster in clusters)
            {
                float distance = Vector3.Distance(
                    cluster.Center,
                    candidate.center);
                if (distance <= mergeTolerance &&
                    distance < nearestDistance)
                {
                    nearestCluster = cluster;
                    nearestDistance = distance;
                }
            }

            if (nearestCluster == null)
            {
                nearestCluster = new JointCluster();
                clusters.Add(nearestCluster);
            }

            nearestCluster.Add(candidate);
        }

        clusters.Sort((a, b) =>
        {
            int yComparison = a.Center.y.CompareTo(b.Center.y);
            if (yComparison != 0)
            {
                return yComparison;
            }

            int xComparison = a.Center.x.CompareTo(b.Center.x);
            return xComparison != 0
                ? xComparison
                : a.Center.z.CompareTo(b.Center.z);
        });

        int generatedJointCount = 0;
        foreach (JointCluster cluster in clusters)
        {
            Dictionary<string, Vector3> connectedMembers =
                new Dictionary<string, Vector3>(
                StringComparer.OrdinalIgnoreCase);
            foreach (JointCandidate candidate in cluster.candidates)
            {
                if (!connectedMembers.ContainsKey(candidate.elementId))
                {
                    connectedMembers.Add(
                        candidate.elementId,
                        candidate.memberDirection.normalized);
                }
            }

            if (connectedMembers.Count < requiredConnections)
            {
                continue;
            }

            generatedJointCount++;
            string jointId = "Joint_" +
                             generatedJointCount.ToString("D3");
            GameObject joint = new GameObject(jointId);
            joint.transform.SetParent(outputRoot, true);
            joint.layer = outputRoot.gameObject.layer;
            joint.transform.position = cluster.Center;
            joint.transform.rotation = Quaternion.identity;
            joint.transform.localScale = Vector3.one;

            joint.AddComponent<MeshFilter>();
            MeshRenderer renderer = joint.AddComponent<MeshRenderer>();
            if (vertexColorMaterial != null)
            {
                renderer.sharedMaterial = vertexColorMaterial;
            }

            string[] elementIds = new string[connectedMembers.Count];
            connectedMembers.Keys.CopyTo(elementIds, 0);
            Array.Sort(elementIds, StringComparer.OrdinalIgnoreCase);
            Vector3[] memberDirections = new Vector3[elementIds.Length];
            for (int i = 0; i < elementIds.Length; i++)
            {
                memberDirections[i] = connectedMembers[elementIds[i]];
            }

            FourPointJointVisual visual =
                joint.AddComponent<FourPointJointVisual>();
            visual.Initialize(
                jointId,
                jointSize,
                elementIds,
                memberDirections,
                jointSurfaceSubdivisions < 2
                    ? 8
                    : Mathf.Clamp(jointSurfaceSubdivisions, 2, 24));
        }

        return generatedJointCount;
    }

    [ContextMenu("Clear Generated Elements")]
    public void ClearGeneratedElements()
    {
        if (outputRoot == null)
        {
            outputRoot = transform;
        }

        for (int i = outputRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = outputRoot.GetChild(i);
            if (child != null &&
                (child.GetComponent<FourPointBeamElementVisual>() != null ||
                 child.GetComponent<FourPointJointVisual>() != null))
            {
                if (UnityEngine.Application.isPlaying)
                {
                    child.SetParent(null, true);
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}
