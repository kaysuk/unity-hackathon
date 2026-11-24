using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TempleLeverBank : MonoBehaviour
{
    private const int SealCount = 6;
    private const int AllSeals = (1 << SealCount) - 1;

    [SerializeField] private TemplePassage passage;
    [SerializeField] private TempleArchiveLever[] levers;
    [SerializeField] private Shader sealShader;
    [Tooltip("Начальное свечение: бит 0 — крайний левый знак.")]
    [SerializeField, Range(1, AllSeals)] private int initialLights = 37;
    [SerializeField] private AudioClip leverSound;

    private int lights;
    private AudioSource mechanism;
    public bool IsSolved => passage.IsOpen;

    private void Awake()
    {
        lights = initialLights;
        for (int i = 0; i < levers.Length; i++)
            levers[i].Initialize(this, i, IsLit(i), sealShader);

        mechanism = gameObject.AddComponent<AudioSource>();
        GameSoundLevels.Route(mechanism, GameSoundChannel.Effects);
        mechanism.playOnAwake = false;
        mechanism.spatialBlend = 1f;
        mechanism.minDistance = 2f;
        mechanism.maxDistance = 12f;
    }

    private bool IsLit(int index) => (lights & (1 << index)) != 0;

    private static int LinkedSeals(int index)
    {
        int self = 1 << index;
        return (self | (self >> 1) | (self << 1)) & AllSeals;
    }

    internal void Pull(int index)
    {
        if (IsSolved) return;
        int linked = LinkedSeals(index);
        lights ^= linked;
        for (int i = 0; i < SealCount; i++)
            if ((linked & (1 << i)) != 0) levers[i].ShowState(IsLit(i));

        mechanism.PlayOneShot(leverSound, 0.7f);
        if (lights == 0)
        {
            GameAudio.Play(SoundCue.Seal, passage.transform.position);
            passage.SetOpen(true);
        }
    }

    internal string DescribeSolution()
    {
        var text = new StringBuilder("Каждая строка — печать, каждый столбец — рычаг слева направо. Единица в столбце означает, что рычаг меняет эту печать. Справа — её начальное свечение.\n\nA x = b   (mod 2)\n");
        var rows = new int[SealCount];
        for (int i = 0; i < SealCount; i++)
        {
            // Links are symmetric. Bit 6 holds the right-hand side of [A | b].
            int rhs = (initialLights >> i) & 1;
            rows[i] = LinkedSeals(i) | (rhs << SealCount);
            for (int j = 0; j < SealCount; j++)
                text.Append((rows[i] >> j) & 1).Append(' ');
            text.Append("| ").Append(rhs).AppendLine();
        }

        text.Append("\nМетод Гаусса. При сложении строк две единицы дают ноль.\n");
        for (int col = 0; col < SealCount; col++)
        {
            int bit = 1 << col;
            int pivot = col;
            // This six-seal matrix has full rank, so every column has a pivot.
            while ((rows[pivot] & bit) == 0) pivot++;
            if (pivot != col)
            {
                (rows[col], rows[pivot]) = (rows[pivot], rows[col]);
                text.AppendLine($"Поменять строки {col + 1} и {pivot + 1}.");
            }
            for (int row = 0; row < SealCount; row++)
            {
                if (row == col || (rows[row] & bit) == 0) continue;
                rows[row] ^= rows[col];
                text.AppendLine($"Строка {row + 1} ← строка {row + 1} + строка {col + 1}.");
            }
        }

        text.Append("\nСлева получилась единичная матрица. Справа — решение x = (");
        for (int i = 0; i < SealCount; i++)
        {
            if (i != 0) text.Append(' ');
            text.Append(rows[i] >> SealCount);
        }
        text.Append("). Каждый рычаг с единицей достаточно потянуть один раз, в любом порядке.");
        return text.ToString();
    }
}
