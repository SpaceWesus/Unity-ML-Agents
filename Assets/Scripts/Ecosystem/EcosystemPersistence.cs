using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Turtle.Ecosystem
{
    /// <summary>
    /// Owns the JSON persistence boundary. Simulation code never performs file I/O.
    /// </summary>
    public sealed class EcosystemSaveRepository
    {
        private const string CurrentFileName = "2d-ecosystem-v5.json";
        private static readonly string[] LegacyFileNames =
        {
            "2d-ecosystem-v4.json",
            "ecosystem-slice-v3.json",
            "ecosystem-slice-v2.json",
            "ecosystem-slice-v1.json"
        };

        private readonly IReadOnlyList<EcosystemGearDefinition> gearCatalog;
        private readonly string currentSavePath;
        private readonly List<string> legacyImportPaths = new();

        public EcosystemSaveRepository(
            IReadOnlyList<EcosystemGearDefinition> availableGear,
            string overridePath = null)
        {
            gearCatalog = availableGear ?? Array.Empty<EcosystemGearDefinition>();
            if (string.IsNullOrWhiteSpace(overridePath))
            {
                currentSavePath = Path.Combine(Application.persistentDataPath, CurrentFileName);
                AddLegacyImportPaths(Application.persistentDataPath);
            }
            else if (IsKnownLegacyFileName(Path.GetFileName(overridePath)))
            {
                // Supporting versioned path overrides makes migration tests and developer tools
                // useful without ever selecting an older schema as the write destination.
                var directory = Path.GetDirectoryName(overridePath) ?? string.Empty;
                currentSavePath = Path.Combine(
                    directory,
                    CurrentFileName);
                legacyImportPaths.Add(overridePath);
                AddLegacyImportPaths(directory);
            }
            else
            {
                currentSavePath = overridePath;
                AddLegacyImportPaths(Path.GetDirectoryName(overridePath) ?? string.Empty);
            }

            ActiveSavePath = currentSavePath;
        }

        public string ActiveSavePath { get; private set; }

        public EcosystemWorldState LoadOrCreate(out string status)
        {
            ActiveSavePath = currentSavePath;
            var sourcePath = File.Exists(currentSavePath) ? currentSavePath : FindLegacyImport();
            if (sourcePath == null)
            {
                status = "Created a new deterministic guild ecosystem.";
                return EcosystemWorldFactory.CreateDefaultWorld(gearCatalog);
            }

            var importingLegacyFile = !PathsEqual(sourcePath, currentSavePath);
            try
            {
                var loaded = DeserializeForValidation(
                    File.ReadAllText(sourcePath),
                    gearCatalog,
                    out var migrated);

                if (importingLegacyFile)
                {
                    if (!Save(loaded, out var saveError))
                    {
                        status =
                            $"Imported the older ecosystem save in memory, but could not write the v5 copy: " +
                            saveError;
                        return loaded;
                    }

                    status =
                        $"Imported the legacy ecosystem save to {ActiveSavePath}. " +
                        $"The source remains at {sourcePath}.";
                    return loaded;
                }

                status = migrated
                    ? $"Migrated the existing ecosystem save to version {loaded.saveVersion}."
                    : $"Loaded day {loaded.day} from disk.";
                return loaded;
            }
            catch (UnsupportedSaveVersionException exception)
            {
                var unsupportedBackup = CreateBackupPath(sourcePath, "unsupported");
                TryCopyForRecovery(sourcePath, unsupportedBackup);
                ActiveSavePath = currentSavePath +
                                 $".recovered-v{EcosystemWorldFactory.CurrentSaveVersion}.json";
                status =
                    $"Save version {exception.SaveVersion} is newer than this prototype. " +
                    $"The original was preserved at {unsupportedBackup}.";
                return EcosystemWorldFactory.CreateDefaultWorld(gearCatalog);
            }
            catch (Exception exception)
            {
                var corruptBackup = CreateBackupPath(sourcePath, "corrupt");
                TryCopyForRecovery(sourcePath, corruptBackup);

                if (!importingLegacyFile && TryLoadRollingBackup(out var recovered))
                {
                    status =
                        $"The current ecosystem save was unreadable, so its rolling backup was loaded. " +
                        $"The unreadable file was preserved at {corruptBackup}.";
                    Debug.LogWarning($"{status} Reason: {exception.Message}");
                    return recovered;
                }

                status =
                    $"The ecosystem save was unreadable. A backup was written to {corruptBackup}.";
                Debug.LogWarning($"{status} Reason: {exception.Message}");
                return EcosystemWorldFactory.CreateDefaultWorld(gearCatalog);
            }
        }

        public bool Save(EcosystemWorldState state, out string error)
        {
            error = string.Empty;
            if (state == null)
            {
                error = "There is no ecosystem state to save.";
                return false;
            }

            var temporaryPath = ActiveSavePath + ".tmp";
            try
            {
                var directory = Path.GetDirectoryName(ActiveSavePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                WriteAndValidateTemporarySave(state, temporaryPath);
                var rollingBackupPath = ActiveSavePath + ".bak";
                if (File.Exists(ActiveSavePath))
                {
                    File.Replace(temporaryPath, ActiveSavePath, rollingBackupPath, true);
                }
                else
                {
                    File.Move(temporaryPath, ActiveSavePath);
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                Debug.LogError($"Could not save Guild Ecosystem Prototype: {exception.Message}");
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch (Exception cleanupException)
                {
                    Debug.LogWarning($"Could not remove temporary ecosystem save: {cleanupException.Message}");
                }
            }
        }

        public static EcosystemWorldState DeserializeForValidation(
            string json,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog,
            out bool migrated)
        {
            var state = JsonUtility.FromJson<EcosystemWorldState>(json);
            if (state == null)
            {
                throw new InvalidDataException("JSON did not contain an ecosystem world.");
            }
            if (state.saveVersion > EcosystemWorldFactory.CurrentSaveVersion)
            {
                throw new UnsupportedSaveVersionException(state.saveVersion);
            }

            var originalVersion = state.saveVersion;
            EcosystemWorldFactory.UpgradeAndNormalize(
                state,
                gearCatalog ?? Array.Empty<EcosystemGearDefinition>());
            var validationErrors = EcosystemWorldFactory.ValidateInvariants(state, gearCatalog);
            if (validationErrors.Count > 0)
            {
                throw new InvalidDataException(
                    "Ecosystem save failed validation: " + string.Join(" | ", validationErrors));
            }

            migrated = originalVersion != state.saveVersion;
            return state;
        }

        private void WriteAndValidateTemporarySave(EcosystemWorldState state, string temporaryPath)
        {
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(state, true));

            // The file on disk, rather than the in-memory source, is the candidate that must
            // survive migration and invariant validation before it can replace a known-good save.
            var candidate = DeserializeForValidation(
                File.ReadAllText(temporaryPath),
                gearCatalog,
                out _);
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(candidate, true));

            // Verify the normalized serialization as well. This catches a serialization loss
            // introduced between normalization and the atomic replacement boundary.
            DeserializeForValidation(File.ReadAllText(temporaryPath), gearCatalog, out _);
        }

        private bool TryLoadRollingBackup(out EcosystemWorldState state)
        {
            state = null;
            var rollingBackupPath = currentSavePath + ".bak";
            if (!File.Exists(rollingBackupPath))
            {
                return false;
            }

            try
            {
                state = DeserializeForValidation(
                    File.ReadAllText(rollingBackupPath),
                    gearCatalog,
                    out _);
                return true;
            }
            catch (Exception backupException)
            {
                Debug.LogWarning($"Could not load the rolling ecosystem backup: {backupException.Message}");
                return false;
            }
        }

        private void AddLegacyImportPaths(string directory)
        {
            foreach (var fileName in LegacyFileNames)
            {
                var path = Path.Combine(directory, fileName);
                if (!legacyImportPaths.Exists(existing => PathsEqual(existing, path)))
                {
                    legacyImportPaths.Add(path);
                }
            }
        }

        private string FindLegacyImport()
        {
            foreach (var path in legacyImportPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            return null;
        }

        private static bool IsKnownLegacyFileName(string fileName)
        {
            foreach (var legacyFileName in LegacyFileNames)
            {
                if (string.Equals(fileName, legacyFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static void TryCopyForRecovery(string sourcePath, string backupPath)
        {
            try
            {
                File.Copy(sourcePath, backupPath, true);
            }
            catch (Exception backupException)
            {
                Debug.LogError($"Could not preserve the ecosystem save: {backupException.Message}");
            }
        }

        private static string CreateBackupPath(string sourcePath, string reason)
        {
            return sourcePath + $".{reason}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}";
        }

        private sealed class UnsupportedSaveVersionException : Exception
        {
            public UnsupportedSaveVersionException(int saveVersion)
                : base($"Save version {saveVersion} is newer than this build supports.")
            {
                SaveVersion = saveVersion;
            }

            public int SaveVersion { get; }
        }
    }
}
