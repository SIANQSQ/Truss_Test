using UnityEngine;

public class BeamColorTestCaller : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private VirtualElementGenerator beamGenerator;

    [SerializeField]
    private BeamElementColorController colorController;

    [Header("Test Result")]
    [SerializeField]
    private string testElementId = "Beam_001_E037";

    [SerializeField]
    private float testStrain = 0.00045f;

    [SerializeField]
    private bool generateBeforeApply = true;

    [SerializeField]
    private bool applyOnStart = true;

    private void Start()
    {
        if (applyOnStart)
        {
            // 延迟执行，确保 Unity 完成初始化。
            Invoke(
                nameof(ApplyTestStrain),
                0.2f);
        }
    }

    [ContextMenu("Apply Test Strain")]
    public void ApplyTestStrain()
    {
        if (beamGenerator == null)
        {
            UnityEngine.Debug.LogError(
                "Beam Generator is not assigned.");

            return;
        }

        if (colorController == null)
        {
            UnityEngine.Debug.LogError(
                "Color Controller is not assigned.");

            return;
        }

        if (generateBeforeApply)
        {
            // 先生成所有子梁单元。
            beamGenerator.GenerateAllBeams();
        }

        // 重新识别生成出来的子梁。
        colorController.RefreshElements();

        // 给指定单元设置应变并改变颜色。
        colorController.SetElementStrain(
            testElementId,
            testStrain);

        UnityEngine.Debug.Log(
            "Applied strain " +
            testStrain +
            " to element " +
            testElementId);
    }

    [ContextMenu("Set Element Red")]
    public void SetElementRed()
    {
        if (colorController == null)
        {
            UnityEngine.Debug.LogError(
                "Color Controller is not assigned.");

            return;
        }

        colorController.RefreshElements();

        colorController.SetElementColor(
            testElementId,
            Color.red);
    }

    [ContextMenu("Reset All Element Colors")]
    public void ResetAllColors()
    {
        if (colorController == null)
        {
            UnityEngine.Debug.LogError(
                "Color Controller is not assigned.");

            return;
        }

        colorController.ResetAllColors();
    }
}