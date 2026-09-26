using UnityEngine;

public class FourPointStressDemo : MonoBehaviour
{
    [SerializeField]
    private FourPointStressController controller;

    [SerializeField]
    private string elementId = "Beam_001_E001";

    [SerializeField]
    private float magnitude = 0.001f;

    [SerializeField]
    private bool runOnStart = true;

    private void Start()
    {
        if (runOnStart)
        {
            Invoke(nameof(ShowBendingExample), 0.2f);
        }
    }

    [ContextMenu("Show Bending Example")]
    public void ShowBendingExample()
    {
        if (controller == null)
        {
            UnityEngine.Debug.LogError(
                "Four-point stress controller is not assigned.");
            return;
        }

        controller.RefreshElements();

        // Looking at the local cross-section:
        // P1/P2 are the +Z side, P3/P4 are the -Z side.
        // Top is compression, bottom is tension: a bending example.
        controller.SetElementCornerStrains(
            elementId,
            -magnitude,
            -magnitude * 0.85f,
            magnitude,
            magnitude * 0.85f);

        UnityEngine.Debug.Log(
            "Applied four-point bending strain to " + elementId);
    }

    [ContextMenu("Show Axial Tension")]
    public void ShowAxialTension()
    {
        if (controller == null)
        {
            return;
        }

        controller.SetElementCornerStrains(
            elementId,
            magnitude,
            magnitude,
            magnitude,
            magnitude);
    }

    [ContextMenu("Show Axial Compression")]
    public void ShowAxialCompression()
    {
        if (controller == null)
        {
            return;
        }

        controller.SetElementCornerStrains(
            elementId,
            -magnitude,
            -magnitude,
            -magnitude,
            -magnitude);
    }
}
