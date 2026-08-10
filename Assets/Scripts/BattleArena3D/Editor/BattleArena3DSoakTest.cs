using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Turtle.BattleArena3D.Editor
{
    public static class BattleArena3DSoakTest
    {
        private const string Prefix = "Turtle.BattleArena3D.Soak.";
        private const string ActiveKey = Prefix + "Active";
        private const string StageKey = Prefix + "Stage";
        private const string StartedAtKey = Prefix + "StartedAt";
        private const string StageCombatTimeKey = Prefix + "StageCombatTime";
        private const string StageEditorTimeKey = Prefix + "StageEditorTime";
        private const string RuntimeErrorKey = Prefix + "RuntimeError";
        private const string MonsterSignatureKey = Prefix + "MonsterSignature";
        private const string ProjectileSignatureKey = Prefix + "ProjectileSignature";
        private const string TelegraphSignatureKey = Prefix + "TelegraphSignature";
        private const string SummaryKey = Prefix + "Summary";
        private const string AttackBaselineKey = Prefix + "AttackBaseline";
        private const string HitBaselineKey = Prefix + "HitBaseline";
        private const string AbilityBaselineKey = Prefix + "AbilityBaseline";
        private const string AreaBaselineKey = Prefix + "AreaBaseline";
        private const string StatusBaselineKey = Prefix + "StatusBaseline";
        private const string CommanderBaselineKey = Prefix + "CommanderBaseline";
        private const string TelegraphBaselineKey = Prefix + "TelegraphBaseline";
        private const string DefaultsAppliedKey = Prefix + "DefaultsApplied";
        private const string AccessibilityPresetAppliedKey = Prefix + "AccessibilityPresetApplied";

        private const string RequestRelativePath = "Temp/CodexValidation/run-3d-test-arena-soak.request";
        private const string ResultRelativePath = "Temp/CodexValidation/3d-test-arena-soak.result";
        private const string ScreenshotRelativePath = "Temp/CodexValidation/3d-test-arena-soak.png";
        private const float SustainedCombatSeconds = 8f;
        private const float WatchdogSeconds = 120f;
        private static double nextPollAt;

        private static string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName ??
                                             Directory.GetCurrentDirectory();
        private static string RequestPath => ResolvePath(RequestRelativePath);
        private static string ResultPath => ResolvePath(ResultRelativePath);
        private static string ScreenshotPath => ResolvePath(ScreenshotRelativePath);

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Application.logMessageReceived -= OnRuntimeLog;
            Application.logMessageReceived += OnRuntimeLog;
        }

        [MenuItem("Turtle/Battle Arena 3D/Run 3D Battle Soak Test")]
        public static void RunFromMenu()
        {
            Begin();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false))
            {
                PollRequest();
                return;
            }
            if (!EditorApplication.isPlaying) return;

            var director = UnityEngine.Object.FindFirstObjectByType<BattleArena3DDirector>();
            if (director == null)
            {
                Complete(false, "3D Test Arena entered Play Mode without BattleArena3DDirector.");
                return;
            }
            var presentation = director.PresentationController;
            if (presentation == null || !presentation.IsConfigured)
            {
                Complete(false, "3D Test Arena has no configured accessibility/presentation controller.");
                return;
            }

            var stage = SessionState.GetInt(StageKey, 0);
            if (stage == 0 && !SessionState.GetBool(DefaultsAppliedKey, false))
            {
                presentation.ApplyValidationDefaults();
                if (!HasExpectedDefaultPresentation(presentation.Options))
                {
                    Complete(false, "The first soak generation could not restore presentation defaults.");
                    return;
                }
                SessionState.SetBool(DefaultsAppliedKey, true);
            }
            if (stage >= 2)
            {
                if (!SessionState.GetBool(AccessibilityPresetAppliedKey, false) ||
                    !HasExpectedAccessibilityPreset(presentation.Options))
                {
                    Complete(false, "The accessibility preset did not remain active for the second generation.");
                    return;
                }
            }

            var elapsed = (float)EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedAtKey, 0f);
            var runtimeError = SessionState.GetString(RuntimeErrorKey, string.Empty);
            if (!string.IsNullOrEmpty(runtimeError))
            {
                Complete(false, "Runtime error during the 3D battle soak: " + runtimeError);
                return;
            }
            if (elapsed > WatchdogSeconds)
            {
                Complete(false, DescribeTimeout(director, elapsed));
                return;
            }

            Time.timeScale = 2f;
            if (director.HunterCount != 30)
            {
                Complete(false, $"Expected 30 persistent hunters; observed {director.HunterCount}.");
                return;
            }

            switch (stage)
            {
                case 0:
                    WaitForFirstFullScaleLatch(director);
                    break;
                case 1:
                    SustainFirstGeneration(director);
                    break;
                case 2:
                    WaitForSecondFullScaleLatch(director);
                    break;
                case 3:
                    SustainSecondGeneration(director);
                    break;
                case 4:
                    if (EditorApplication.timeSinceStartup >= SessionState.GetFloat(StageEditorTimeKey, 0f))
                    {
                        Complete(true, SessionState.GetString(SummaryKey, "3D Test Arena soak passed."));
                    }
                    break;
            }
        }

        private static void PollRequest()
        {
            if (EditorApplication.timeSinceStartup < nextPollAt) return;
            nextPollAt = EditorApplication.timeSinceStartup + 0.5d;
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(RequestPath);
            Begin();
        }

        private static void WaitForFirstFullScaleLatch(BattleArena3DDirector director)
        {
            if (!HasFullScaleLatch(director)) return;
            if (director.LivingHunterCount != 30)
            {
                Complete(false, "A hunter was incapacitated before the first 250-combatant latch.");
                return;
            }
            SessionState.SetString(MonsterSignatureKey, CaptureMonsterPoolSignature());
            SessionState.SetString(ProjectileSignatureKey, CapturePoolSignature<BattleArena3DProjectile>());
            SessionState.SetString(TelegraphSignatureKey, CapturePoolSignature<BattleArena3DTelegraph>());
            StoreActivityBaseline(director);
            SessionState.SetFloat(StageCombatTimeKey, director.CombatTime);
            SessionState.SetInt(StageKey, 1);
        }

        private static void SustainFirstGeneration(BattleArena3DDirector director)
        {
            var duration = director.CombatTime - SessionState.GetFloat(StageCombatTimeKey, 0f);
            if (duration < SustainedCombatSeconds || !HasSustainedActivity(director)) return;

            var firstSummary = DescribeGeneration("first", director, duration);
            if (!PoolSignaturesMatch())
            {
                Complete(false, "A runtime pool changed membership during the first sustained window.");
                return;
            }

            director.RestartBattle();
            if (director.ActiveMonsterCount != 0 || director.ActiveProjectileCount != 0 ||
                director.ActiveTelegraphCount != 0)
            {
                Complete(false,
                    $"Restart did not clear runtime state: monsters={director.ActiveMonsterCount}, " +
                    $"projectiles={director.ActiveProjectileCount}, telegraphs={director.ActiveTelegraphCount}.");
                return;
            }
            if (!PoolSignaturesMatch())
            {
                Complete(false, "Restart replaced objects instead of retaining the prewarmed pools.");
                return;
            }

            var presentation = director.PresentationController;
            presentation.ApplyValidationAccessibilityPreset();
            if (!HasExpectedAccessibilityPreset(presentation.Options))
            {
                Complete(false, "The second generation could not apply the validation accessibility preset.");
                return;
            }
            director.SelectNextHunter(1);
            if (director.SelectedUnit == null)
            {
                Complete(false, "The selected-only world-bar preset had no selected hunter to present.");
                return;
            }
            SessionState.SetBool(AccessibilityPresetAppliedKey, true);

            SessionState.SetString(SummaryKey, firstSummary);
            SessionState.SetInt(StageKey, 2);
        }

        private static void WaitForSecondFullScaleLatch(BattleArena3DDirector director)
        {
            if (!HasFullScaleLatch(director)) return;
            if (director.LivingHunterCount != 30)
            {
                Complete(false, "A hunter was incapacitated before the second 250-combatant latch.");
                return;
            }
            StoreActivityBaseline(director);
            SessionState.SetFloat(StageCombatTimeKey, director.CombatTime);
            SessionState.SetInt(StageKey, 3);
        }

        private static void SustainSecondGeneration(BattleArena3DDirector director)
        {
            var duration = director.CombatTime - SessionState.GetFloat(StageCombatTimeKey, 0f);
            if (duration < SustainedCombatSeconds || !HasSustainedActivity(director)) return;
            if (!PoolSignaturesMatch())
            {
                Complete(false, "The second generation did not reuse the original runtime object pools.");
                return;
            }
            if (director.DroppedTelegraphs > 0)
            {
                Complete(false,
                    $"The normal 250-combatant workload exhausted the telegraph pool " +
                    $"{director.DroppedTelegraphs} times.");
                return;
            }
            if (director.FeedbackEventCount <= 0 || director.DamageLabelEmissionCount <= 0)
            {
                Complete(false,
                    $"Minimal-effects combat did not retain functional feedback: " +
                    $"events={director.FeedbackEventCount}, labels={director.DamageLabelEmissionCount}.");
                return;
            }
            if (director.CameraImpulseCount != 0)
            {
                Complete(false,
                    $"Camera motion was disabled for the second generation, but " +
                    $"{director.CameraImpulseCount} impulses were emitted.");
                return;
            }

            var summary = SessionState.GetString(SummaryKey, string.Empty) + Environment.NewLine +
                          DescribeGeneration("second", director, duration) + Environment.NewLine +
                          $"The second deployment retained combat and telegraphs with the 125% HUD, " +
                          $"minimal effects, disabled camera motion, selected-only world bars, " +
                          $"high-contrast factions, and reduced motion. It emitted " +
                          $"{director.FeedbackEventCount} feedback events and " +
                          $"{director.DamageLabelEmissionCount} contextual damage labels with zero " +
                          $"camera impulses. " +
                          $"Both deployments reused identical monster, projectile, and telegraph instance sets. " +
                          $"Screenshot: {ScreenshotRelativePath}";
            SessionState.SetString(SummaryKey, summary);
            Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath) ?? ProjectRoot);
            ScreenCapture.CaptureScreenshot(ScreenshotPath);
            SessionState.SetFloat(StageEditorTimeKey, (float)EditorApplication.timeSinceStartup + 1f);
            SessionState.SetInt(StageKey, 4);
        }

        private static bool HasFullScaleLatch(BattleArena3DDirector director)
        {
            return director.Phase == BattleArenaPhase3D.Wave &&
                   director.ActiveMonsterCount == 220 &&
                   director.PeakConcurrentMonsterCount == 220 &&
                   director.PeakCombatantCount == 250;
        }

        private static void StoreActivityBaseline(BattleArena3DDirector director)
        {
            SessionState.SetInt(AttackBaselineKey, director.AttackAttempts);
            SessionState.SetInt(HitBaselineKey, director.ConfirmedHits);
            SessionState.SetInt(AbilityBaselineKey, director.AbilityCasts);
            SessionState.SetInt(AreaBaselineKey, director.AreaAbilityCasts);
            SessionState.SetInt(StatusBaselineKey, director.StatusApplications);
            SessionState.SetInt(CommanderBaselineKey, director.CommanderDecisions);
            SessionState.SetInt(TelegraphBaselineKey, director.TelegraphEmissions);
        }

        private static bool HasSustainedActivity(BattleArena3DDirector director)
        {
            return director.AttackAttempts - SessionState.GetInt(AttackBaselineKey, 0) >= 25 &&
                   director.ConfirmedHits - SessionState.GetInt(HitBaselineKey, 0) >= 40 &&
                   director.AbilityCasts - SessionState.GetInt(AbilityBaselineKey, 0) >= 35 &&
                   director.AreaAbilityCasts - SessionState.GetInt(AreaBaselineKey, 0) >= 8 &&
                   director.StatusApplications - SessionState.GetInt(StatusBaselineKey, 0) >= 8 &&
                   director.CommanderDecisions - SessionState.GetInt(CommanderBaselineKey, 0) >= 4 &&
                   director.TelegraphEmissions - SessionState.GetInt(TelegraphBaselineKey, 0) >= 12;
        }

        private static bool PoolSignaturesMatch()
        {
            return SessionState.GetString(MonsterSignatureKey, string.Empty) == CaptureMonsterPoolSignature() &&
                   SessionState.GetString(ProjectileSignatureKey, string.Empty) ==
                   CapturePoolSignature<BattleArena3DProjectile>() &&
                   SessionState.GetString(TelegraphSignatureKey, string.Empty) ==
                   CapturePoolSignature<BattleArena3DTelegraph>();
        }

        private static bool HasExpectedDefaultPresentation(BattleArenaPresentationOptions3D options)
        {
            return Mathf.Approximately(options.UiScale, 1f) &&
                   options.EffectsLevel == BattleArenaEffectsLevel3D.Full &&
                   options.CameraMotion == BattleArenaCameraMotion3D.Reduced &&
                   options.WorldBars == BattleArenaWorldBars3D.Contextual &&
                   options.DamageNumbers == BattleArenaDamageNumbers3D.Contextual &&
                   !options.HighContrastFactions &&
                   !options.ReducedMotion;
        }

        private static bool HasExpectedAccessibilityPreset(BattleArenaPresentationOptions3D options)
        {
            return Mathf.Approximately(options.UiScale, 1.25f) &&
                   options.EffectsLevel == BattleArenaEffectsLevel3D.Minimal &&
                   options.CameraMotion == BattleArenaCameraMotion3D.Off &&
                   options.WorldBars == BattleArenaWorldBars3D.SelectedOnly &&
                   options.DamageNumbers == BattleArenaDamageNumbers3D.Contextual &&
                   options.HighContrastFactions &&
                   options.ReducedMotion;
        }

        private static string CaptureMonsterPoolSignature()
        {
            return string.Join(",", UnityEngine.Object.FindObjectsByType<BattleArena3DUnit>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(unit => unit.Faction == BattleArenaFaction3D.Monsters)
                .Select(unit => unit.GetInstanceID())
                .OrderBy(id => id));
        }

        private static string CapturePoolSignature<T>() where T : UnityEngine.Object
        {
            return string.Join(",", UnityEngine.Object.FindObjectsByType<T>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(instance => instance.GetInstanceID())
                .OrderBy(id => id));
        }

        private static string DescribeGeneration(string label, BattleArena3DDirector director, float duration)
        {
            return $"The {label} generation sustained 30 hunters + peak 220 monsters for " +
                   $"{duration:0.0} simulated seconds: {director.AttackAttempts} attacks, " +
                   $"{director.ConfirmedHits} contact hits, {director.AbilityCasts} abilities, " +
                   $"{director.AreaAbilityCasts} AOE casts, {director.StatusApplications} statuses, " +
                   $"{director.CommanderDecisions} commander decisions, and " +
                   $"{director.TelegraphEmissions} pooled combat rings.";
        }

        private static string DescribeTimeout(BattleArena3DDirector director, float elapsed)
        {
            return $"Timed out after {elapsed:0.0}s in stage {SessionState.GetInt(StageKey, 0)}: " +
                   $"phase={director.Phase}, hunters={director.LivingHunterCount}, " +
                   $"monsters={director.ActiveMonsterCount}, peak={director.PeakConcurrentMonsterCount}, " +
                   $"attacks={director.AttackAttempts}, hits={director.ConfirmedHits}, " +
                   $"abilities={director.AbilityCasts}, AOE={director.AreaAbilityCasts}, " +
                   $"statuses={director.StatusApplications}, commander={director.CommanderDecisions}, " +
                   $"telegraphs={director.TelegraphEmissions}, dropped={director.DroppedTelegraphs}, " +
                   $"feedback={director.FeedbackEventCount}, labels={director.DamageLabelEmissionCount}, " +
                   $"impulses={director.CameraImpulseCount}.";
        }

        private static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BattleArena3DSceneBuilder.ScenePath) == null)
            {
                WriteResult("FAIL", $"Missing scene: {BattleArena3DSceneBuilder.ScenePath}");
                return;
            }

            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(StageKey, 0);
            SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetString(RuntimeErrorKey, string.Empty);
            SessionState.SetString(SummaryKey, string.Empty);
            SessionState.SetBool(DefaultsAppliedKey, false);
            SessionState.SetBool(AccessibilityPresetAppliedKey, false);
            if (File.Exists(ResultPath)) File.Delete(ResultPath);
            if (File.Exists(ScreenshotPath)) File.Delete(ScreenshotPath);
            EditorSceneManager.OpenScene(BattleArena3DSceneBuilder.ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ActiveKey, false))
            {
                SessionState.SetBool(ActiveKey, false);
            }
        }

        private static void OnRuntimeLog(string condition, string stackTrace, LogType type)
        {
            if (!SessionState.GetBool(ActiveKey, false) ||
                type is not (LogType.Error or LogType.Exception or LogType.Assert)) return;
            var message = string.IsNullOrWhiteSpace(condition) ? stackTrace : condition;
            if (message.Length > 800) message = message[..800];
            SessionState.SetString(RuntimeErrorKey, message);
        }

        private static void Complete(bool passed, string message)
        {
            WriteResult(passed ? "PASS" : "FAIL", message);
            Debug.Log(passed ? message : $"3D Test Arena soak failed: {message}");
            SessionState.SetBool(ActiveKey, false);
            Time.timeScale = 1f;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        private static void WriteResult(string status, string message)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath) ?? ProjectRoot);
            File.WriteAllText(ResultPath, status + Environment.NewLine + message + Environment.NewLine);
        }

        private static string ResolvePath(string relativePath)
        {
            return Path.Combine(ProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
