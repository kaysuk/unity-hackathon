using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// A cast occupies one row. Only its trailing status and hit label change after the decision.
internal sealed class NimbAttackCsv : IDisposable
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly NimbAttack[] Attacks = (NimbAttack[])Enum.GetValues(typeof(NimbAttack));
    private static readonly string Header = BuildHeader();
    private readonly string directory;
    private readonly string encounterId = Guid.NewGuid().ToString("N");
    private FileStream file;
    private long outcomeOffset = -1;
    private bool hit;
    private bool closed;
    private int castIndex;
    private double firstDecisionTime;

    internal static bool IsAutomatedRun
    {
        get
        {
            if (Equals(AppDomain.CurrentDomain.GetData("NimbTelemetry.AutomatedRun"), true))
                return true;
            foreach (var arg in Environment.GetCommandLineArgs())
                if (string.Equals(arg, "-runTests", StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }

    internal static string DefaultDirectory
    {
        get
        {
            string path = Path.Combine(Application.persistentDataPath, "NimbTelemetry");
            return IsAutomatedRun ? Path.Combine(path, "Automated") : path;
        }
    }

    internal string FilePath { get; private set; }

    internal NimbAttackCsv(string directory)
    {
        this.directory = directory;
    }

    internal void Begin(NimbAttack attack, string mode, NimbAttack requestedAttack,
        float distanceMeters, bool inCover, int recentCoverCount, bool dashAvailable,
        bool handAvailable, NimbAttack? excludedRepeat, float? roll,
        float[] hitChances, float[] selectionChances)
    {
        if (outcomeOffset >= 0) Finish(true);
        if (closed) return;
        try
        {
            var utc = DateTime.UtcNow;
            if (file == null)
            {
                Directory.CreateDirectory(directory);
                var stamp = utc.ToString("yyyyMMdd'T'HHmmssfff'Z'", Invariant);
                FilePath = Path.Combine(directory, $"nimb-{stamp}-{encounterId}.csv");
                file = new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                Write(Header);
                firstDecisionTime = Time.timeAsDouble;
                Debug.Log("Nimb attack CSV: " + FilePath);
            }

            var row = new StringBuilder(400);
            row.Append("1,").Append(encounterId).Append(',').Append((++castIndex).ToString(Invariant))
                .Append(',').Append(utc.ToString("o", Invariant))
                .Append(',').Append((Time.timeAsDouble - firstDecisionTime).ToString("R", Invariant))
                .Append(',').Append(mode).Append(',').Append(requestedAttack).Append(',').Append(attack)
                .Append(',').Append(Number(distanceMeters)).Append(',').Append(inCover ? '1' : '0')
                .Append(',').Append(recentCoverCount.ToString(Invariant))
                .Append(',').Append(dashAvailable ? '1' : '0').Append(',').Append(handAvailable ? '1' : '0')
                .Append(',').Append(excludedRepeat.ToString()).Append(',').Append(roll.HasValue ? Number(roll.Value) : "");
            foreach (var candidate in Attacks)
                row.Append(',').Append(hitChances == null ? "" : Number(hitChances[(int)candidate]));
            float lowerBound = 0f;
            bool hasLaterCandidate = false;
            foreach (var candidate in Attacks)
            {
                float chance = selectionChances == null ? (candidate == attack ? 1f : 0f) : selectionChances[(int)candidate];
                row.Append(',').Append(Number(chance));
                if ((int)candidate < (int)attack) lowerBound += chance;
                if ((int)candidate > (int)attack && chance > 0f) hasLaterCandidate = true;
            }

            string reason;
            if (roll.HasValue) reason = "weighted_draw";
            else if (mode == "combo") reason = "halo_followup";
            else if (requestedAttack == NimbAttack.HeadDash && attack == NimbAttack.EyeSeal) reason = "blocked_dash_replaced";
            else reason = mode == "model_fallback" ? "model_fallback" : "fixed_order";
            row.Append(',').Append(reason);
            if (roll.HasValue && selectionChances != null)
            {
                float upperBound = hasLaterCandidate ? Mathf.Clamp01(lowerBound + selectionChances[(int)attack]) : 1f;
                row.Append(',').Append(Number(lowerBound)).Append(',').Append(Number(upperBound));
            }
            else row.Append(",,");
            row.Append(',');
            Write(row.ToString());
            outcomeOffset = file.Position;
            hit = false;
            WriteOutcome("pending", "");
        }
        catch (Exception ex) when (IsFileError(ex)) { Disable(ex); }
    }

    internal void MarkHit()
    {
        if (outcomeOffset < 0 || hit) return;
        hit = true;
        try { WriteOutcome("pending", "1"); }
        catch (Exception ex) when (IsFileError(ex)) { Disable(ex); }
    }

    internal void Finish(bool interrupted)
    {
        if (outcomeOffset < 0) return;
        try { WriteOutcome(interrupted ? "interrupted" : "completed", hit ? "1" : interrupted ? "" : "0"); }
        catch (Exception ex) when (IsFileError(ex)) { Disable(ex); }
        outcomeOffset = -1;
    }

    private static string BuildHeader()
    {
        var header = new StringBuilder("schema_version,encounter_id,cast_index,decision_utc,elapsed_game_seconds,"
            + "mode,requested_attack,chosen_attack,distance_m,in_cover,recent_cover_count,"
            + "dash_available,hand_available,excluded_repeat,roll_01");
        foreach (var attack in Attacks) header.Append(',').Append(attack).Append("_hit_probability");
        foreach (var attack in Attacks) header.Append(',').Append(attack).Append("_selection_probability");
        return header.Append(",choice_reason,roll_from,roll_to,status,hit\n").ToString();
    }

    private static string Number(float value) => value.ToString("R", Invariant);

    private void Write(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        file.Write(bytes, 0, bytes.Length);
    }

    private void WriteOutcome(string status, string label)
    {
        file.Position = outcomeOffset;
        Write(status + "," + label + "\n");
        file.Flush();
    }

    private static bool IsFileError(Exception ex) => ex is IOException || ex is UnauthorizedAccessException
        || ex is System.Security.SecurityException || ex is ArgumentException || ex is NotSupportedException;

    private void Disable(Exception error)
    {
        closed = true;
        outcomeOffset = -1;
        try { file?.Dispose(); }
        catch (Exception ex) when (IsFileError(ex)) { }
        file = null;
        Debug.LogWarning("Nimb attack CSV could not be written: " + error.Message);
    }

    public void Dispose()
    {
        if (closed) return;
        Finish(true);
        closed = true;
        try { file?.Dispose(); }
        catch (Exception ex) when (IsFileError(ex)) { Debug.LogWarning("Nimb attack CSV could not close: " + ex.Message); }
        file = null;
    }
}
