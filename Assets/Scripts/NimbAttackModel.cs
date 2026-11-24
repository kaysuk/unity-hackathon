using System;
using UnityEngine;

internal enum NimbAttack { EyeSeal, HaloWave, LightArrows, HeadDash, HandTouch }

internal sealed class NimbAttackModel
{
    [Serializable]
    private sealed class ModelFile
    {
        public int version = 0;
        public Weights[] attacks = null;
    }

    [Serializable]
    internal sealed class Weights
    {
        public string attack = "";
        public float bias = 0f;
        public float distanceMeters = 0f;
        public float recentCoverCount = 0f;
        public float inCover = 0f;
    }

    internal readonly Weights[] attacks;
    // The telemetry writer consumes these arrays before the next decision overwrites them.
    internal float[] HitChances { get; }
    internal float[] SelectionChances { get; }

    private NimbAttackModel(Weights[] attacks)
    {
        this.attacks = attacks;
        HitChances = new float[attacks.Length];
        SelectionChances = new float[attacks.Length];
    }

    internal static NimbAttackModel Load()
    {
        try
        {
            var packaged = LoadPackaged(out string revision);
            return NimbAttackLearning.LoadSaved(packaged, revision);
        }
        catch (ArgumentException error)
        {
            Debug.LogWarning("Nimb attack model could not load. " + error.Message);
            return null;
        }
    }

    internal static NimbAttackModel LoadPackaged(out string revision)
    {
        var asset = Resources.Load<TextAsset>("NimbModel");
        if (asset == null) throw new ArgumentException("Missing Resources/NimbModel.json.");
        var file = JsonUtility.FromJson<ModelFile>(asset.text);
        if (file == null || file.version != 1)
            throw new ArgumentException("Expected model version 1.");
        revision = Hash128.Compute(asset.text).ToString();
        return FromWeights(file.attacks);
    }

    internal static NimbAttackModel FromWeights(Weights[] entries)
    {
        int count = Enum.GetValues(typeof(NimbAttack)).Length;
        if (entries?.Length != count) throw new ArgumentException("Expected one entry for each attack.");
        var ordered = new Weights[count];
        foreach (var weights in entries)
        {
            if (weights == null || !Enum.TryParse<NimbAttack>(weights.attack, out var attack)
                || attack.ToString() != weights.attack || !Enum.IsDefined(typeof(NimbAttack), attack)
                || ordered[(int)attack] != null)
                throw new ArgumentException("Invalid or duplicate attack name.");
            if (!Finite(weights.bias) || !Finite(weights.distanceMeters)
                || !Finite(weights.recentCoverCount) || !Finite(weights.inCover))
                throw new ArgumentException("Non-finite coefficient for " + attack + ".");
            ordered[(int)attack] = weights;
        }
        return new NimbAttackModel(ordered);
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    internal float Predict(NimbAttack attack, float distanceMeters, int recentCoverCount, bool inCover)
    {
        var weights = attacks[(int)attack];
        float logit = weights.bias + weights.distanceMeters * Mathf.Max(0f, distanceMeters)
            + weights.recentCoverCount * Mathf.Clamp(recentCoverCount, 0, 3)
            + (inCover ? weights.inCover : 0f);
        float exp = Mathf.Exp(-Mathf.Abs(logit));
        return logit >= 0f ? 1f / (1f + exp) : exp / (1f + exp);
    }

    internal NimbAttack Choose(float distanceMeters, int recentCoverCount, bool inCover,
        bool dashAvailable, bool handAvailable, NimbAttack? excludedRepeat, float sample01)
    {
        ScoreHits(distanceMeters, recentCoverCount, inCover);
        float total = 0f;
        for (int i = 0; i < attacks.Length; i++)
        {
            var attack = (NimbAttack)i;
            bool available = attack != excludedRepeat && (attack != NimbAttack.HeadDash || dashAvailable)
                && (attack != NimbAttack.HandTouch || handAvailable);
            SelectionChances[i] = available ? Mathf.Max(.01f, HitChances[i]) : 0f;
            total += SelectionChances[i];
        }

        for (int i = 0; i < SelectionChances.Length; i++)
            SelectionChances[i] /= total;

        // Use the same cumulative intervals we report in the CSV. Random.value can also be 1.
        float draw = Mathf.Clamp01(sample01);
        float upperBound = 0f;
        var selected = NimbAttack.EyeSeal;
        for (int i = 0; i < SelectionChances.Length; i++)
        {
            if (SelectionChances[i] <= 0f) continue;
            selected = (NimbAttack)i;
            upperBound += SelectionChances[i];
            if (draw < upperBound) break;
        }
        return selected;
    }

    internal void ScoreFixedDecision(float distanceMeters, int recentCoverCount, bool inCover, NimbAttack chosen)
    {
        ScoreHits(distanceMeters, recentCoverCount, inCover);
        Array.Clear(SelectionChances, 0, SelectionChances.Length);
        SelectionChances[(int)chosen] = 1f;
    }

    private void ScoreHits(float distanceMeters, int recentCoverCount, bool inCover)
    {
        for (int i = 0; i < attacks.Length; i++)
            HitChances[i] = Predict((NimbAttack)i, distanceMeters, recentCoverCount, inCover);
    }
}
