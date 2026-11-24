using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// The live encounter keeps its original model. Only the next encounter loads this update.
internal static class NimbAttackLearning
{
    [Serializable]
    private sealed class Profile
    {
        public int version = 1;
        public string baseRevision;
        public NimbAttackModel.Weights[] attacks;
        public List<string> appliedEncounters;
    }

    private struct Gradient
    {
        public Vector4 sum;
        public float squaredNormSum;
    }

    private static string DirectoryPath
    {
        get
        {
            if (AppDomain.CurrentDomain.GetData("NimbLearning.TestDirectory") is string testDirectory)
                return testDirectory;
            if (!Application.isPlaying || NimbAttackCsv.IsAutomatedRun) return null;
            return Path.Combine(Application.persistentDataPath, "NimbLearning");
        }
    }

    internal static bool HasSavedProfile =>
        DirectoryPath is string directory && File.Exists(Path.Combine(directory, "profile.json"));

    internal static NimbAttackModel LoadSaved(NimbAttackModel packaged, string revision)
    {
        string directory = DirectoryPath;
        if (directory == null) return packaged;
        try
        {
            return ReadModel(Path.Combine(directory, "profile.json"), packaged, revision, out _);
        }
        catch (Exception error) when (IsProfileError(error))
        {
            Debug.LogWarning("Nimb uses packaged weights: " + error.Message);
            return packaged;
        }
    }

    internal static bool TryResetToPackaged()
    {
        string directory = DirectoryPath;
        if (directory == null) return false;
        try
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "profile.json");
            using var profileLock = LockProfile(path);
            if (!File.Exists(path)) return true;

            var packaged = NimbAttackModel.LoadPackaged(out string revision);
            ReadModel(path, packaged, revision, out var profile);
            // Retain processed encounter IDs so a retried save cannot undo the reset.
            profile ??= new Profile { baseRevision = revision, appliedEncounters = new List<string>() };
            profile.attacks = packaged.attacks;
            Save(path, profile);
            return true;
        }
        catch (Exception error) when (IsProfileError(error))
        {
            Debug.LogWarning("Nimb weights could not reset: " + error.Message);
            return false;
        }
    }

    private static FileStream LockProfile(string path) =>
        new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);

    private static NimbAttackModel ReadModel(string path, NimbAttackModel packaged, string revision, out Profile profile)
    {
        profile = null;
        if (!File.Exists(path)) return packaged;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        try
        {
            var saved = JsonUtility.FromJson<Profile>(reader.ReadToEnd());
            if (saved == null || saved.version != 1 || string.IsNullOrEmpty(saved.baseRevision)
                || saved.appliedEncounters == null)
                throw new InvalidDataException("Invalid Nimb learning profile: " + path);
            // New bundled tuning starts a new history.
            if (saved.baseRevision != revision) return packaged;
            var model = NimbAttackModel.FromWeights(saved.attacks);
            profile = saved;
            return model;
        }
        catch (Exception error) when (error is ArgumentException || error is InvalidDataException)
        {
            Debug.LogWarning("Nimb restarting from packaged weights: " + error.Message);
            return packaged;
        }
    }

    internal static bool ApplyEncounter(string csvPath)
    {
        string directory = DirectoryPath;
        if (directory == null || string.IsNullOrEmpty(csvPath)) return false;
        try
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "profile.json");
            // Separate processes may have loaded the same weights. Read the latest profile under the lock.
            using var profileLock = LockProfile(path);
            var packaged = NimbAttackModel.LoadPackaged(out string revision);
            var model = ReadModel(path, packaged, revision, out var profile);
            profile ??= new Profile { baseRevision = revision, appliedEncounters = new List<string>() };
            var gradients = new Gradient[model.attacks.Length];
            int count = ReadGradients(csvPath, model, gradients, out string id);
            if (count == 0 || profile.appliedEncounters.Contains(id)) return false;
            for (int i = 0; i < gradients.Length; i++)
            {
                var gradient = gradients[i];
                if (gradient.squaredNormSum == 0f) continue;

                var weights = model.attacks[i];
                Vector4 current = Scaled(weights);
                // One damped batch step on logistic loss. The L2 term pulls toward the shipped model.
                // 0.25 * sum(|x|²) bounds data curvature; damping limits the influence of a short fight.
                Vector4 step = (gradient.sum + Scaled(packaged.attacks[i]) - current)
                    / (8f + 1f + .25f * gradient.squaredNormSum);
                Vector4 next = current + step;
                weights.distanceMeters = next.y / 10f;
                weights.bias = next.x - next.y;
                weights.recentCoverCount = next.z;
                weights.inCover = next.w;
            }
            profile.attacks = model.attacks;
            profile.appliedEncounters.Add(id);
            Save(path, profile);
            Debug.Log($"Nimb updated weights from {count} labelled attacks in {id}: {path}");
            return true;
        }
        catch (Exception error) when (IsProfileError(error))
        {
            Debug.LogWarning("Nimb weights could not update: " + error.Message);
            return false;
        }
    }

    // Match offline training's centered distance coordinate, (distance - 10 m) / 10 m.
    private static Vector4 Scaled(NimbAttackModel.Weights w) =>
        new Vector4(w.bias + 10f * w.distanceMeters, 10f * w.distanceMeters, w.recentCoverCount, w.inCover);

    private static int ReadGradients(string path, NimbAttackModel model, Gradient[] gradients, out string id)
    {
        using var reader = new StreamReader(path);
        var header = (reader.ReadLine() ?? "").Split(',');
        int Column(string name)
        {
            int column = Array.IndexOf(header, name);
            if (column < 0) throw new InvalidDataException("Missing Nimb CSV field: " + name);
            return column;
        }
        int version = Column("schema_version"), encounter = Column("encounter_id"), cast = Column("cast_index");
        int mode = Column("mode"), attackName = Column("chosen_attack"), distance = Column("distance_m");
        int cover = Column("in_cover"), memory = Column("recent_cover_count"), status = Column("status"), hit = Column("hit");
        int count = 0, previousCast = 0;
        id = null;
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            var row = line.Split(',');
            if (row.Length != header.Length || row[version] != "1"
                || !Guid.TryParseExact(row[encounter], "N", out _) || (id != null && id != row[encounter])
                || !int.TryParse(row[cast], out int index) || index <= previousCast)
                throw new InvalidDataException("Invalid Nimb encounter or duplicate cast: " + path);
            id = row[encounter];
            previousCast = index;
            if (row[mode] != "adaptive" || (row[hit] != "1" && row[hit] != "0")) continue;
            if (row[status] != "completed" && !(row[status] == "interrupted" && row[hit] == "1")) continue;
            if (!Enum.TryParse<NimbAttack>(row[attackName], out var attack)
                || !Enum.IsDefined(typeof(NimbAttack), attack) || attack.ToString() != row[attackName]
                || !float.TryParse(row[distance], NumberStyles.Float, CultureInfo.InvariantCulture, out float metres)
                || !(metres >= 0f) || float.IsInfinity(metres)
                || !int.TryParse(row[memory], out int reactions) || reactions < 0 || reactions > 3
                || (row[cover] != "0" && row[cover] != "1")) continue;

            bool inCover = row[cover] == "1";
            var x = new Vector4(1f, (metres - 10f) / 10f, reactions, inCover ? 1f : 0f);
            float norm = x.sqrMagnitude;
            if (float.IsInfinity(norm)) continue;
            float error = (row[hit] == "1" ? 1f : 0f) - model.Predict(attack, metres, reactions, inCover);
            gradients[(int)attack].sum += error * x;
            gradients[(int)attack].squaredNormSum += norm;
            count++;
        }
        return count;
    }

    private static void Save(string path, Profile profile)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(JsonUtility.ToJson(profile, true));
                writer.Flush();
                stream.Flush(true);
            }
            // Publish the weights and encounter ID together, or leave the old profile intact.
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static bool IsProfileError(Exception error) => error is IOException || error is InvalidDataException
        || error is UnauthorizedAccessException || error is System.Security.SecurityException
        || error is ArgumentException || error is NotSupportedException;
}
