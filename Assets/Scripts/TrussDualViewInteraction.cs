using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

/// <summary>
/// Creates synchronized original/stress viewports and provides element
/// selection plus a simplified offline point-load preview.
/// </summary>
[DefaultExecutionOrder(10000)]
public class TrussDualViewInteraction : MonoBehaviour
{
    public enum LoadDirection
    {
        LocalPositiveX,
        LocalNegativeX,
        LocalPositiveZ,
        LocalNegativeZ,
        LocalPositiveY,
        LocalNegativeY
    }

    private const int OriginalModelLayer = 28;
    private const int StressModelLayer = 29;

    [Header("Offline preview material model")]
    [SerializeField]
    private float elasticModulusGPa = 200f;

    [SerializeField]
    private float minimumSectionSize = 0.001f;

    private Camera originalCamera;
    private Camera stressCamera;
    private Transform originalRoot;
    private Transform stressRoot;
    private FourPointStressController stressController;
    private WebSocketFourPointReceiver receiver;
    private Rect originalCameraRect;
    private int originalCameraMask;
    private bool initialized;
    private float nextRefreshTime;
    private int refreshPasses;
    private int knownElementCount = -1;
    private bool lastOnlineState;
    private Bounds originalModelBounds;
    private Vector3 savedStressRootLocalPosition;
    private Quaternion savedStressRootLocalRotation;
    private Vector3 savedStressRootLocalScale;
    private bool hasSavedStressRootTransform;
    private readonly Dictionary<Transform, int> savedLayers =
        new Dictionary<Transform, int>();
    private readonly List<BoxCollider> collidersAddedAtRuntime =
        new List<BoxCollider>();

    private FourPointBeamElementVisual selectedElement;
    private LoadDirection selectedDirection = LoadDirection.LocalNegativeZ;
    private string lastStatus = "SELECT AN ELEMENT IN THE STRESS VIEW";
    private float lastAppliedForceKN;
    private string loadedElementId;

    private GameObject selectionObject;
    private Mesh selectionMesh;
    private Material selectionMaterial;
    private GameObject forceArrowObject;
    private Mesh forceArrowMesh;
    private Material forceArrowMaterial;

    public Camera OriginalCamera
    {
        get { return originalCamera; }
    }

    public Camera StressCamera
    {
        get { return stressCamera; }
    }

    public FourPointBeamElementVisual SelectedElement
    {
        get { return selectedElement; }
    }

    public LoadDirection SelectedDirection
    {
        get { return selectedDirection; }
    }

    public bool IsOnline
    {
        get { return receiver != null && receiver.IsConnected; }
    }

    public float ElasticModulusGPa
    {
        get { return elasticModulusGPa; }
    }

    public float LastAppliedForceKN
    {
        get { return lastAppliedForceKN; }
    }

    public string LastStatus
    {
        get { return lastStatus; }
    }

    public void Initialize(
        Camera sourceCamera,
        Transform currentOriginalRoot,
        Transform currentStressRoot,
        FourPointStressController controller,
        WebSocketFourPointReceiver currentReceiver)
    {
        if (initialized)
        {
            return;
        }

        if (sourceCamera == null ||
            currentOriginalRoot == null ||
            currentStressRoot == null)
        {
            UnityEngine.Debug.LogWarning(
                "Dual view could not start because a camera or model root is missing.");
            return;
        }

        originalCamera = sourceCamera;
        originalRoot = currentOriginalRoot;
        stressRoot = currentStressRoot;
        stressController = controller;
        receiver = currentReceiver;

        savedStressRootLocalPosition = stressRoot.localPosition;
        savedStressRootLocalRotation = stressRoot.localRotation;
        savedStressRootLocalScale = stressRoot.localScale;
        hasSavedStressRootTransform = true;

        originalCameraRect = originalCamera.rect;
        originalCameraMask = originalCamera.cullingMask;

        CaptureLayers(originalRoot);
        CaptureLayers(stressRoot);
        SetLayerRecursively(originalRoot, OriginalModelLayer);
        SetLayerRecursively(stressRoot, StressModelLayer);
        CreateStressCamera();
        CreateSelectionVisuals();
        RefreshSelectableElements();

        initialized = true;
        lastOnlineState = IsOnline;
        nextRefreshTime = Time.unscaledTime + 0.25f;
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + 1f;
            int currentCount = stressRoot == null
                ? 0
                : stressRoot.GetComponentsInChildren<FourPointBeamElementVisual>(
                    true).Length;
            if (refreshPasses < 3 || currentCount != knownElementCount)
            {
                RefreshSelectableElements();
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            TrySelectFromPointer();
        }

        if (lastOnlineState != IsOnline)
        {
            lastOnlineState = IsOnline;
            if (IsOnline)
            {
                if (stressController != null)
                {
                    stressController.ResetAllElements();
                }

                lastAppliedForceKN = 0f;
                loadedElementId = null;
            }

            lastStatus = IsOnline
                ? "LIVE DATA / READ ONLY"
                : "OFFLINE POINT-LOAD PREVIEW";
            UpdateForceArrowVisibility();
        }

        if (selectedElement == null && selectionObject != null)
        {
            selectionObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (!initialized || originalCamera == null || stressCamera == null)
        {
            return;
        }

        stressCamera.orthographic = originalCamera.orthographic;
        stressCamera.fieldOfView = originalCamera.fieldOfView;
        stressCamera.nearClipPlane = originalCamera.nearClipPlane;
        stressCamera.farClipPlane = originalCamera.farClipPlane;
        SyncStressCameraView();

        if (selectedElement != null &&
            selectionObject != null &&
            selectionObject.activeSelf)
        {
            SyncSelectionTransform();
        }
    }

    private void CreateStressCamera()
    {
        GameObject stressCameraObject = Instantiate(originalCamera.gameObject);
        stressCameraObject.SetActive(false);
        stressCameraObject.name = "Stress Result Camera";
        stressCameraObject.tag = "Untagged";

        TrussCameraController duplicateController =
            stressCameraObject.GetComponent<TrussCameraController>();
        if (duplicateController != null)
        {
            duplicateController.enabled = false;
            Destroy(duplicateController);
        }

        AudioListener duplicateListener =
            stressCameraObject.GetComponent<AudioListener>();
        if (duplicateListener != null)
        {
            duplicateListener.enabled = false;
            Destroy(duplicateListener);
        }

        stressCamera = stressCameraObject.GetComponent<Camera>();
        stressCameraObject.transform.SetParent(originalCamera.transform, false);
        stressCameraObject.transform.localPosition = Vector3.zero;
        stressCameraObject.transform.localRotation = Quaternion.identity;
        stressCameraObject.transform.localScale = Vector3.one;
        stressCamera.rect = new Rect(0.5f, 0f, 0.5f, 1f);
        stressCamera.depth = originalCamera.depth;
        stressCamera.cullingMask =
            (originalCameraMask | (1 << StressModelLayer)) &
            ~(1 << OriginalModelLayer);

        originalCamera.rect = new Rect(0f, 0f, 0.5f, 1f);
        originalCamera.cullingMask =
            (originalCameraMask | (1 << OriginalModelLayer)) &
            ~(1 << StressModelLayer);
        stressCameraObject.SetActive(true);
    }

    public void RefreshSelectableElements()
    {
        if (stressRoot == null)
        {
            return;
        }

        SetLayerRecursively(stressRoot, StressModelLayer);
        FourPointBeamElementVisual[] elements =
            stressRoot.GetComponentsInChildren<FourPointBeamElementVisual>(true);
        knownElementCount = elements.Length;
        refreshPasses++;

        foreach (FourPointBeamElementVisual element in elements)
        {
            if (element == null)
            {
                continue;
            }

            element.gameObject.layer = StressModelLayer;
            BoxCollider collider = element.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = element.gameObject.AddComponent<BoxCollider>();
                collidersAddedAtRuntime.Add(collider);
            }

            MeshFilter filter = element.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                collider.center = filter.sharedMesh.bounds.center;
                collider.size = filter.sharedMesh.bounds.size;
            }
        }

        if (stressController != null)
        {
            stressController.RefreshElements();
        }

        AlignStressModelToOriginal();
    }

    private void AlignStressModelToOriginal()
    {
        if (stressRoot == null || !hasSavedStressRootTransform)
        {
            return;
        }

        stressRoot.localPosition = savedStressRootLocalPosition;
        stressRoot.localRotation = savedStressRootLocalRotation;
        stressRoot.localScale = savedStressRootLocalScale;

        Bounds currentOriginalBounds;
        Bounds currentStressBounds;
        if (!TryGetRendererBounds(originalRoot, out currentOriginalBounds) ||
            !TryGetRendererBounds(stressRoot, out currentStressBounds))
        {
            return;
        }

        originalModelBounds = currentOriginalBounds;
        float originalSpan = GetLargestSpan(originalModelBounds.size);
        float stressSpan = GetLargestSpan(currentStressBounds.size);
        if (originalSpan > 0.000001f && stressSpan > 0.000001f)
        {
            float modelScale = Mathf.Clamp(
                originalSpan / stressSpan,
                0.01f,
                100f);
            stressRoot.localScale = Vector3.Scale(
                savedStressRootLocalScale,
                Vector3.one * modelScale);
        }

        if (TryGetRendererBounds(stressRoot, out currentStressBounds))
        {
            stressRoot.position +=
                originalModelBounds.center - currentStressBounds.center;
        }

        SyncStressCameraView();
    }

    private void SyncStressCameraView()
    {
        if (originalCamera == null || stressCamera == null)
        {
            return;
        }

        Transform stressTransform = stressCamera.transform;
        if (stressTransform.parent != originalCamera.transform)
        {
            stressTransform.SetParent(originalCamera.transform, false);
        }

        stressTransform.localPosition = Vector3.zero;
        stressTransform.localRotation = Quaternion.identity;
        stressTransform.localScale = Vector3.one;
        stressCamera.orthographicSize = originalCamera.orthographicSize;
    }

    private bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds();
        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        foreach (Renderer currentRenderer in renderers)
        {
            if (currentRenderer == null || !currentRenderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = currentRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(currentRenderer.bounds);
            }
        }

        return hasBounds;
    }

    private float GetLargestSpan(Vector3 size)
    {
        return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
    }

    private void TrySelectFromPointer()
    {
        if (stressCamera == null ||
            !stressCamera.pixelRect.Contains(Input.mousePosition))
        {
            return;
        }

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Ray ray = stressCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            stressCamera.farClipPlane,
            1 << StressModelLayer,
            QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (left, right) =>
            left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            FourPointBeamElementVisual element =
                hit.collider.GetComponentInParent<FourPointBeamElementVisual>();
            if (element != null)
            {
                SelectElement(element);
                return;
            }
        }
    }

    public void SelectElement(FourPointBeamElementVisual element)
    {
        selectedElement = element;
        if (selectedElement == null)
        {
            lastStatus = "SELECT AN ELEMENT IN THE STRESS VIEW";
            if (selectionObject != null)
            {
                selectionObject.SetActive(false);
            }
            return;
        }

        lastStatus = IsOnline
            ? "LIVE DATA / READ ONLY"
            : "OFFLINE POINT-LOAD PREVIEW";
        UpdateSelectionOutline();
        UpdateForceArrowVisibility();
    }

    public LoadDirection CycleDirection()
    {
        if (!string.IsNullOrEmpty(loadedElementId))
        {
            ClearSelectedBeam();
        }

        int next = ((int)selectedDirection + 1) %
                   Enum.GetValues(typeof(LoadDirection)).Length;
        selectedDirection = (LoadDirection)next;
        lastStatus = "DIRECTION CHANGED / APPLY FORCE AGAIN";
        return selectedDirection;
    }

    public string GetDirectionLabel()
    {
        switch (selectedDirection)
        {
            case LoadDirection.LocalPositiveX:
                return "局部 +X";
            case LoadDirection.LocalNegativeX:
                return "局部 -X";
            case LoadDirection.LocalPositiveZ:
                return "局部 +Z";
            case LoadDirection.LocalNegativeZ:
                return "局部 -Z";
            case LoadDirection.LocalPositiveY:
                return "局部 +Y（轴向）";
            case LoadDirection.LocalNegativeY:
                return "局部 -Y（轴向）";
            default:
                return "局部 -Z";
        }
    }

    public bool ApplyOfflineForce(float forceKN)
    {
        if (IsOnline)
        {
            lastStatus = "ONLINE MODE IS READ ONLY";
            return false;
        }

        if (selectedElement == null)
        {
            lastStatus = "SELECT AN ELEMENT FIRST";
            return false;
        }

        if (stressController == null)
        {
            lastStatus = "STRESS CONTROLLER IS UNAVAILABLE";
            return false;
        }

        float loadKN = Mathf.Abs(forceKN);
        if (float.IsNaN(loadKN) ||
            float.IsInfinity(loadKN) ||
            loadKN < 0.0001f)
        {
            lastStatus = "FORCE MUST BE GREATER THAN ZERO";
            return false;
        }

        stressController.RefreshElements();
        List<FourPointBeamElementVisual> beamElements =
            stressController.GetElementsForBeam(selectedElement.beamId);
        if (beamElements.Count == 0)
        {
            lastStatus = "NO ELEMENTS FOUND FOR " + selectedElement.beamId;
            return false;
        }

        ApplyPointLoadToBeam(beamElements, selectedElement, loadKN);
        lastAppliedForceKN = loadKN;
        loadedElementId = selectedElement.elementId;
        lastStatus = "APPLIED " + loadKN.ToString("0.###") +
                     " kN  /  " + GetDirectionLabel();
        UpdateForceArrow();
        return true;
    }

    public void ClearSelectedBeam()
    {
        if (IsOnline)
        {
            lastStatus = "ONLINE MODE IS READ ONLY";
            return;
        }

        if (selectedElement == null || stressController == null)
        {
            lastStatus = "SELECT AN ELEMENT FIRST";
            return;
        }

        stressController.RefreshElements();
        List<FourPointBeamElementVisual> beamElements =
            stressController.GetElementsForBeam(selectedElement.beamId);
        foreach (FourPointBeamElementVisual element in beamElements)
        {
            stressController.SetElementCornerStrains(
                element.elementId,
                0f,
                0f,
                0f,
                0f);
        }

        lastAppliedForceKN = 0f;
        loadedElementId = null;
        lastStatus = "CLEARED " + selectedElement.beamId;
        UpdateForceArrowVisibility();
    }

    public void ClearLoadIndicator()
    {
        lastAppliedForceKN = 0f;
        loadedElementId = null;
        lastStatus = IsOnline
            ? "LIVE DATA / READ ONLY"
            : "ALL RESULTS RESET";
        UpdateForceArrowVisibility();
    }

    private void ApplyPointLoadToBeam(
        List<FourPointBeamElementVisual> elements,
        FourPointBeamElementVisual loadElement,
        float forceKN)
    {
        float totalLength = 0f;
        float loadPosition = 0f;
        float cumulative = 0f;

        foreach (FourPointBeamElementVisual element in elements)
        {
            float elementLength = Mathf.Max(element.ElementLength, 0.000001f);
            if (element == loadElement)
            {
                loadPosition = cumulative + elementLength * 0.5f;
            }

            cumulative += elementLength;
        }

        totalLength = Mathf.Max(cumulative, 0.000001f);
        loadPosition = Mathf.Clamp(
            loadPosition,
            totalLength * 0.001f,
            totalLength * 0.999f);

        float width = Mathf.Max(loadElement.SectionWidth, minimumSectionSize);
        float height = Mathf.Max(loadElement.SectionHeight, minimumSectionSize);
        float area = width * height;
        float inertiaX = width * Mathf.Pow(height, 3f) / 12f;
        float inertiaZ = height * Mathf.Pow(width, 3f) / 12f;
        float elasticModulus = Mathf.Max(elasticModulusGPa, 0.001f) * 1e9f;
        float forceN = forceKN * 1000f;

        float forceX = 0f;
        float forceZ = 0f;
        float axialForce = 0f;
        switch (selectedDirection)
        {
            case LoadDirection.LocalPositiveX:
                forceX = forceN;
                break;
            case LoadDirection.LocalNegativeX:
                forceX = -forceN;
                break;
            case LoadDirection.LocalPositiveZ:
                forceZ = forceN;
                break;
            case LoadDirection.LocalNegativeZ:
                forceZ = -forceN;
                break;
            case LoadDirection.LocalPositiveY:
                axialForce = forceN;
                break;
            case LoadDirection.LocalNegativeY:
                axialForce = -forceN;
                break;
        }

        float[] cornerX =
        {
            -width * 0.5f,
            width * 0.5f,
            width * 0.5f,
            -width * 0.5f
        };
        float[] cornerZ =
        {
            height * 0.5f,
            height * 0.5f,
            -height * 0.5f,
            -height * 0.5f
        };

        cumulative = 0f;
        foreach (FourPointBeamElementVisual element in elements)
        {
            float elementLength = Mathf.Max(element.ElementLength, 0.000001f);
            float position = cumulative + elementLength * 0.5f;
            cumulative += elementLength;

            float influence = position <= loadPosition
                ? (totalLength - loadPosition) / totalLength * position
                : loadPosition / totalLength * (totalLength - position);
            float momentX = forceZ * influence;
            float momentZ = -forceX * influence;
            float axialStress = axialForce / area;

            float[] strains = new float[4];
            for (int corner = 0; corner < 4; corner++)
            {
                float stress = axialStress -
                               momentZ * cornerX[corner] / inertiaZ +
                               momentX * cornerZ[corner] / inertiaX;
                strains[corner] = stress / elasticModulus;
            }

            stressController.SetElementCornerStrains(
                element.elementId,
                strains[0],
                strains[1],
                strains[2],
                strains[3]);
        }
    }

    private void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private void CaptureLayers(Transform root)
    {
        if (root == null)
        {
            return;
        }

        if (!savedLayers.ContainsKey(root))
        {
            savedLayers.Add(root, root.gameObject.layer);
        }

        for (int i = 0; i < root.childCount; i++)
        {
            CaptureLayers(root.GetChild(i));
        }
    }

    private void CreateSelectionVisuals()
    {
        selectionMaterial = CreateUnlitMaterial(
            "ElementSelectionMaterial",
            new Color(1f, 0.72f, 0.08f, 1f));
        forceArrowMaterial = CreateUnlitMaterial(
            "ForceArrowMaterial",
            new Color(1f, 0.35f, 0.08f, 1f));

        selectionObject = new GameObject("SelectedElementOutline");
        selectionObject.layer = StressModelLayer;
        selectionObject.AddComponent<MeshFilter>();
        MeshRenderer selectionRenderer =
            selectionObject.AddComponent<MeshRenderer>();
        selectionRenderer.sharedMaterial = selectionMaterial;
        selectionRenderer.shadowCastingMode = ShadowCastingMode.Off;
        selectionRenderer.receiveShadows = false;
        selectionObject.SetActive(false);

        forceArrowObject = new GameObject("OfflineForceArrow");
        forceArrowObject.layer = StressModelLayer;
        forceArrowObject.AddComponent<MeshFilter>();
        MeshRenderer arrowRenderer =
            forceArrowObject.AddComponent<MeshRenderer>();
        arrowRenderer.sharedMaterial = forceArrowMaterial;
        arrowRenderer.shadowCastingMode = ShadowCastingMode.Off;
        arrowRenderer.receiveShadows = false;
        forceArrowObject.SetActive(false);
    }

    private void UpdateSelectionOutline()
    {
        if (selectedElement == null || selectionObject == null)
        {
            return;
        }

        MeshFilter sourceFilter = selectedElement.GetComponent<MeshFilter>();
        if (sourceFilter == null || sourceFilter.sharedMesh == null)
        {
            selectionObject.SetActive(false);
            return;
        }

        if (selectionMesh != null)
        {
            Destroy(selectionMesh);
        }

        Bounds bounds = sourceFilter.sharedMesh.bounds;
        Vector3 padding = new Vector3(0.002f, 0.03f, 0.002f);
        Vector3 min = bounds.min - padding;
        Vector3 max = bounds.max + padding;
        Vector3[] vertices =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
            new Vector3(min.x, max.y, max.z)
        };
        int[] indices =
        {
            0, 1, 1, 2, 2, 3, 3, 0,
            4, 5, 5, 6, 6, 7, 7, 4,
            0, 4, 1, 5, 2, 6, 3, 7
        };

        selectionMesh = new Mesh
        {
            name = "SelectedElementOutlineMesh"
        };
        selectionMesh.vertices = vertices;
        selectionMesh.SetIndices(indices, MeshTopology.Lines, 0);
        selectionMesh.RecalculateBounds();

        selectionObject.transform.SetParent(null, false);
        SyncSelectionTransform();
        selectionObject.GetComponent<MeshFilter>().sharedMesh = selectionMesh;
        selectionObject.SetActive(true);
    }

    private void SyncSelectionTransform()
    {
        if (selectedElement == null || selectionObject == null)
        {
            return;
        }

        selectionObject.transform.position = selectedElement.transform.position;
        selectionObject.transform.rotation = selectedElement.transform.rotation;
        selectionObject.transform.localScale = selectedElement.transform.lossyScale;
    }

    private void UpdateForceArrowVisibility()
    {
        bool show = !IsOnline &&
                    selectedElement != null &&
                    !string.IsNullOrEmpty(loadedElementId) &&
                    string.Equals(
                        selectedElement.elementId,
                        loadedElementId,
                        StringComparison.OrdinalIgnoreCase);
        if (forceArrowObject != null)
        {
            forceArrowObject.SetActive(show);
        }

        if (show)
        {
            UpdateForceArrow();
        }
    }

    private void UpdateForceArrow()
    {
        if (selectedElement == null || forceArrowObject == null)
        {
            return;
        }

        Vector3 localDirection = GetLocalDirection(selectedDirection);
        Vector3 direction = selectedElement.transform
            .TransformDirection(localDirection).normalized;
        Vector3 center = selectedElement.transform.position;
        float arrowLength = Mathf.Clamp(
            selectedElement.ElementLength * 10f,
            0.08f,
            0.22f);
        Vector3 start = center - direction * arrowLength * 0.65f;
        Vector3 end = center + direction * arrowLength * 0.35f;

        Vector3 side = Vector3.Cross(direction, Vector3.up);
        if (side.sqrMagnitude < 0.001f)
        {
            side = Vector3.Cross(direction, Vector3.forward);
        }
        side.Normalize();
        Vector3 headBase = end - direction * arrowLength * 0.22f;
        float headWidth = arrowLength * 0.11f;

        if (forceArrowMesh != null)
        {
            Destroy(forceArrowMesh);
        }

        forceArrowMesh = new Mesh
        {
            name = "OfflineForceArrowMesh"
        };
        forceArrowMesh.vertices = new[]
        {
            start,
            end,
            end,
            headBase + side * headWidth,
            end,
            headBase - side * headWidth
        };
        forceArrowMesh.SetIndices(
            new[] { 0, 1, 2, 3, 4, 5 },
            MeshTopology.Lines,
            0);
        forceArrowMesh.RecalculateBounds();
        forceArrowObject.transform.SetParent(null, false);
        forceArrowObject.transform.position = Vector3.zero;
        forceArrowObject.transform.rotation = Quaternion.identity;
        forceArrowObject.transform.localScale = Vector3.one;
        forceArrowObject.GetComponent<MeshFilter>().sharedMesh = forceArrowMesh;
        forceArrowObject.SetActive(true);
    }

    private Vector3 GetLocalDirection(LoadDirection direction)
    {
        switch (direction)
        {
            case LoadDirection.LocalPositiveX:
                return Vector3.right;
            case LoadDirection.LocalNegativeX:
                return Vector3.left;
            case LoadDirection.LocalPositiveZ:
                return Vector3.forward;
            case LoadDirection.LocalNegativeZ:
                return Vector3.back;
            case LoadDirection.LocalPositiveY:
                return Vector3.up;
            case LoadDirection.LocalNegativeY:
                return Vector3.down;
            default:
                return Vector3.back;
        }
    }

    private Material CreateUnlitMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.HideAndDontSave
        };
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        return material;
    }

    private void OnDestroy()
    {
        if (stressRoot != null && hasSavedStressRootTransform)
        {
            stressRoot.localPosition = savedStressRootLocalPosition;
            stressRoot.localRotation = savedStressRootLocalRotation;
            stressRoot.localScale = savedStressRootLocalScale;
        }

        if (originalCamera != null)
        {
            originalCamera.rect = originalCameraRect;
            originalCamera.cullingMask = originalCameraMask;
        }

        if (stressCamera != null)
        {
            Destroy(stressCamera.gameObject);
        }

        RestoreRootLayerHierarchy(originalRoot);
        RestoreRootLayerHierarchy(stressRoot);

        foreach (KeyValuePair<Transform, int> savedLayer in savedLayers)
        {
            if (savedLayer.Key != null)
            {
                savedLayer.Key.gameObject.layer = savedLayer.Value;
            }
        }

        foreach (BoxCollider collider in collidersAddedAtRuntime)
        {
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        if (selectionObject != null)
        {
            Destroy(selectionObject);
        }

        if (forceArrowObject != null)
        {
            Destroy(forceArrowObject);
        }

        if (selectionMesh != null)
        {
            Destroy(selectionMesh);
        }

        if (forceArrowMesh != null)
        {
            Destroy(forceArrowMesh);
        }

        if (selectionMaterial != null)
        {
            Destroy(selectionMaterial);
        }

        if (forceArrowMaterial != null)
        {
            Destroy(forceArrowMaterial);
        }
    }

    private void RestoreRootLayerHierarchy(Transform root)
    {
        if (root == null)
        {
            return;
        }

        int restoredLayer;
        if (!savedLayers.TryGetValue(root, out restoredLayer))
        {
            restoredLayer = 0;
        }

        SetLayerRecursively(root, restoredLayer);
    }
}
