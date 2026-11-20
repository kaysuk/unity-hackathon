using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleM4Puzzle : MonoBehaviour
{
    public enum Symbol
    {
        Eye,
        Quill,
        Seal,
        Bowl,
        Key
    }

    private static readonly Symbol[] Sequence = { Symbol.Eye, Symbol.Quill, Symbol.Seal };
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly Color Amber = new Color(1f, .61f, .23f);
    private static readonly Color Red = new Color(.91f, .20f, .12f);
    private static readonly Color Green = new Color(.44f, .76f, .40f);
    private static readonly Color Dark = new Color(.20f, .13f, .065f);

    [SerializeField] private TemplePassage shutter;
    [SerializeField] private GameObject report;
    [SerializeField] private Renderer[] indicators;

    private MaterialPropertyBlock indicatorProperties;
    private float errorUntil;

    public int Progress { get; private set; }
    public bool IsSolved => Progress == Sequence.Length;

    private void Awake()
    {
        if (shutter == null || report == null)
        {
            Debug.LogError("М4 требует створку и нишу D04.", this);
            enabled = false;
            return;
        }

        report.SetActive(false);
        indicatorProperties = new MaterialPropertyBlock();
        RefreshIndicators();
    }

    public void Press(Symbol symbol)
    {
        if (!isActiveAndEnabled || IsSolved)
            return;

        bool matches = symbol == Sequence[Progress];
        Progress = matches ? Progress + 1 : 0;
        errorUntil = matches ? 0f : Time.time + .4f;

        if (IsSolved)
        {
            shutter.SetOpen(true);
            report.SetActive(true);
        }

        RefreshIndicators();
    }

    private void Update()
    {
        if (errorUntil <= 0f || Time.time < errorUntil)
            return;

        errorUntil = 0f;
        RefreshIndicators();
    }

    private void RefreshIndicators()
    {
        for (int i = 0; i < indicators.Length; i++)
        {
            Color color = Dark;
            if (IsSolved)
                color = Green;
            else if (errorUntil > 0f)
                color = Red;
            else if (i < Progress)
                color = Amber;

            indicatorProperties.SetColor(BaseColorId, color);
            indicatorProperties.SetColor(EmissionColorId, color * .55f);
            indicators[i].SetPropertyBlock(indicatorProperties);
        }
    }
}
