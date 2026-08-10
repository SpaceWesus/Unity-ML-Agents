using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.DungeonRaid
{
    [DisallowMultipleComponent]
    public sealed class RaidHud2D : MonoBehaviour
    {
        [SerializeField] private DungeonRaidDirector2D raid;
        [SerializeField] private bool showCombatantIndicators = true;

        private readonly List<RaidStatusEffectSnapshot> statusBuffer = new(8);
        private readonly Dictionary<string, string> abilityCodeCache =
            new(StringComparer.Ordinal);
        private Camera raidViewCamera;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle badgeStyle;
        private GUIStyle tooltipStyle;
        private GUIStyle teamNameStyle;
        private GUIStyle teamRoleStyle;
        private GUIStyle barStyle;
        private GUIStyle barTextStyle;

        private void OnGUI()
        {
            if (raid == null) return;
            EnsureStyles();
            if (showCombatantIndicators) DrawCombatantIndicators();
            DrawRaidSummary();
            DrawTeamStatusPanel();
            DrawTooltip();
        }

        private void DrawRaidSummary()
        {
            var panel = new Rect(12f, 12f, 420f, 220f);
            var previousColor = GUI.color;
            GUI.color = new Color(0.025f, 0.04f, 0.055f, 0.94f);
            GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.color = previousColor;
            GUI.Label(new Rect(26f, 22f, 390f, 28f),
                "AUTONOMOUS DAMP CAVE RAID", titleStyle);
            var partyPhase = raid.Party != null ? raid.Party.Phase.ToString() : "Unwired";
            var living = 0;
            var downed = 0;
            for (var index = 0; index < raid.Hunters.Count; index++)
            {
                if (raid.Hunters[index].CanAct) living++;
                else if (raid.Hunters[index].CanBeRescued) downed++;
            }
            var monsters = 0;
            for (var index = 0; index < raid.Monsters.Count; index++)
            {
                if (raid.Monsters[index].CanReceiveDamage) monsters++;
            }
            GUI.Label(new Rect(26f, 55f, 380f, 22f),
                $"Party: {partyPhase}   Hunters: {living} active / {downed} downed", bodyStyle);
            GUI.Label(new Rect(26f, 78f, 380f, 22f),
                $"Hostiles remaining: {monsters}   Raid time: {raid.RaidTime:0.0}s", bodyStyle);
            GUI.Label(new Rect(26f, 106f, 380f, 42f), raid.LatestEvent, bodyStyle);
            GUI.Label(new Rect(26f, 150f, 380f, 32f),
                "World badges: active status effects. Team HUD: " +
                "R ready, # cooldown, M mana, X unable. Hover for details.",
                bodyStyle);
            if (!string.IsNullOrEmpty(raid.ResultMessage))
            {
                GUI.Label(new Rect(26f, 188f, 380f, 26f), raid.ResultMessage, titleStyle);
            }
        }

        private void DrawCombatantIndicators()
        {
            if (raidViewCamera == null) raidViewCamera = Camera.main;
            if (raidViewCamera == null) return;
            DrawIndicators(raid.Hunters);
            DrawIndicators(raid.Monsters);
        }

        private void DrawIndicators(IReadOnlyList<RaidAgent2D> agents)
        {
            for (var index = 0; index < agents.Count; index++)
            {
                DrawIndicators(agents[index]);
            }
        }

        private void DrawIndicators(RaidAgent2D agent)
        {
            if (agent == null || agent.LifeState == RaidLifeState.Dead) return;
            var screen = raidViewCamera.WorldToScreenPoint(agent.transform.position);
            if (screen.z <= 0f || screen.x < -40f || screen.x > Screen.width + 40f ||
                screen.y < -40f || screen.y > Screen.height + 40f)
            {
                return;
            }

            var centerX = screen.x;
            var centerY = Screen.height - screen.y;
            agent.CollectActiveStatusEffects(raid.RaidTime, statusBuffer);
            DrawStatusBadges(agent, centerX, centerY - 58f);
        }

        private void DrawStatusBadges(RaidAgent2D agent, float centerX, float y)
        {
            const float width = 31f;
            const float height = 24f;
            const float gap = 3f;
            var totalWidth = statusBuffer.Count * width +
                             Mathf.Max(0, statusBuffer.Count - 1) * gap;
            var x = centerX - totalWidth * 0.5f;
            for (var index = 0; index < statusBuffer.Count; index++)
            {
                var status = statusBuffer[index];
                var seconds = Mathf.CeilToInt(status.RemainingSeconds);
                var label = $"{StatusCode(status.Kind)}\n{seconds}";
                var tooltip = $"{agent.DisplayName}\n{StatusName(status.Kind)}: " +
                              $"{status.RemainingSeconds:0.0}s remaining";
                DrawBadge(
                    new Rect(x + index * (width + gap), y, width, height),
                    label,
                    tooltip,
                    StatusColor(status.Kind));
            }
        }

        private void DrawTeamStatusPanel()
        {
            var hunters = raid.Hunters;
            if (hunters.Count == 0) return;

            const float panelMargin = 12f;
            const float headerHeight = 30f;
            const float rowHeight = 48f;
            var panelWidth = Mathf.Min(620f, Screen.width - panelMargin * 2f);
            if (panelWidth < 360f) return;
            var panelHeight = headerHeight + hunters.Count * rowHeight + 10f;
            var panel = new Rect(
                panelMargin,
                Screen.height - panelHeight - panelMargin,
                panelWidth,
                panelHeight);
            DrawTintedBox(panel, new Color(0.025f, 0.04f, 0.055f, 0.94f));
            GUI.Label(
                new Rect(panel.x + 12f, panel.y + 5f, panel.width - 24f, 24f),
                "STRIKE TEAM",
                titleStyle);

            for (var index = 0; index < hunters.Count; index++)
            {
                var hunter = hunters[index];
                if (hunter == null) continue;
                var row = new Rect(
                    panel.x + 8f,
                    panel.y + headerHeight + index * rowHeight,
                    panel.width - 16f,
                    rowHeight - 3f);
                var rowColor = hunter.LifeState switch
                {
                    RaidLifeState.Dead => new Color(0.16f, 0.12f, 0.12f, 0.9f),
                    RaidLifeState.Downed => new Color(0.25f, 0.13f, 0.1f, 0.9f),
                    _ => new Color(0.075f, 0.095f, 0.12f, 0.9f)
                };
                DrawTintedBox(row, rowColor);
                DrawHunterIdentity(hunter, row);
                DrawHunterResources(hunter, row);
                DrawTeamAbilityBadges(
                    hunter,
                    new Rect(row.x + 282f, row.y + 5f, row.width - 290f, row.height - 10f));
            }
        }

        private void DrawHunterIdentity(RaidAgent2D hunter, Rect row)
        {
            GUI.Label(
                new Rect(row.x + 8f, row.y + 4f, 104f, 20f),
                hunter.DisplayName,
                teamNameStyle);
            GUI.Label(
                new Rect(row.x + 8f, row.y + 24f, 104f, 16f),
                hunter.Role.ToString().ToUpperInvariant(),
                teamRoleStyle);
        }

        private void DrawHunterResources(RaidAgent2D hunter, Rect row)
        {
            const float barWidth = 154f;
            var barX = row.x + 116f;
            DrawResourceBar(
                new Rect(barX, row.y + 7f, barWidth, 14f),
                hunter.HealthRatio,
                new Color(0.82f, 0.06f, 0.045f),
                $"HP  {Mathf.CeilToInt(hunter.CurrentHealth)} / {Mathf.CeilToInt(hunter.MaximumHealth)}");
            var manaRatio = hunter.MaximumMana <= 0f
                ? 0f
                : Mathf.Clamp01(hunter.CurrentMana / hunter.MaximumMana);
            DrawResourceBar(
                new Rect(barX, row.y + 26f, barWidth, 12f),
                manaRatio,
                new Color(0.06f, 0.32f, 0.92f),
                $"MP  {Mathf.CeilToInt(hunter.CurrentMana)} / {Mathf.CeilToInt(hunter.MaximumMana)}");
        }

        private void DrawTeamAbilityBadges(RaidAgent2D agent, Rect area)
        {
            var abilities = agent.Abilities;
            if (abilities.Count == 0 || area.width <= 0f) return;
            const float gap = 5f;
            var width = Mathf.Min(
                92f,
                (area.width - Mathf.Max(0, abilities.Count - 1) * gap) / abilities.Count);
            if (width < 38f) return;
            for (var index = 0; index < abilities.Count; index++)
            {
                var ability = abilities[index];
                if (ability == null) continue;
                var availability = agent.GetAbilityAvailability(ability, raid.RaidTime);
                var cooldown = agent.GetAbilityCooldownRemaining(ability, raid.RaidTime);
                var stateCode = availability switch
                {
                    RaidAbilityAvailability.Ready => "R",
                    RaidAbilityAvailability.Cooldown =>
                        Mathf.CeilToInt(cooldown).ToString(),
                    RaidAbilityAvailability.InsufficientMana => "M",
                    _ => "X"
                };
                var stateDescription = availability switch
                {
                    RaidAbilityAvailability.Ready => "ready",
                    RaidAbilityAvailability.Cooldown => $"{cooldown:0.0}s cooldown",
                    RaidAbilityAvailability.InsufficientMana =>
                        $"needs {Mathf.Max(0f, ability.manaCost - agent.CurrentMana):0} more mana",
                    _ => "unable to act"
                };
                var color = availability switch
                {
                    RaidAbilityAvailability.Ready => ability.color,
                    RaidAbilityAvailability.Cooldown => Color.Lerp(ability.color, Color.black, 0.62f),
                    RaidAbilityAvailability.InsufficientMana => new Color(0.12f, 0.28f, 0.52f),
                    _ => new Color(0.18f, 0.2f, 0.23f)
                };
                DrawBadge(
                    new Rect(area.x + index * (width + gap), area.y, width, area.height),
                    $"{AbilityCode(ability)}\n{stateCode}",
                    $"{agent.DisplayName}\n{ability.displayName}: {stateDescription} " +
                    $"({ability.manaCost:0} mana)",
                    color);
            }
        }

        private void DrawResourceBar(Rect rect, float ratio, Color fillColor, string label)
        {
            DrawTintedBox(rect, new Color(0.025f, 0.025f, 0.03f, 0.98f), barStyle);
            ratio = Mathf.Clamp01(ratio);
            if (ratio > 0f)
            {
                var fill = new Rect(rect.x + 1f, rect.y + 1f,
                    Mathf.Max(1f, (rect.width - 2f) * ratio), rect.height - 2f);
                DrawTintedBox(fill, fillColor, barStyle);
            }
            GUI.Label(rect, label, barTextStyle);
        }

        private void DrawTintedBox(Rect rect, Color color, GUIStyle style = null)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.Box(rect, GUIContent.none, style ?? panelStyle);
            GUI.color = previousColor;
        }

        private void DrawBadge(Rect rect, string label, string tooltip, Color color)
        {
            var previousBackground = GUI.backgroundColor;
            var previousColor = GUI.color;
            GUI.backgroundColor = new Color(color.r, color.g, color.b, 0.96f);
            GUI.color = Color.white;
            GUI.Box(rect, new GUIContent(label, tooltip), badgeStyle);
            GUI.backgroundColor = previousBackground;
            GUI.color = previousColor;
        }

        private void DrawTooltip()
        {
            if (string.IsNullOrWhiteSpace(GUI.tooltip) || Event.current == null) return;
            var mouse = Event.current.mousePosition;
            var rect = new Rect(
                Mathf.Min(mouse.x + 16f, Screen.width - 270f),
                Mathf.Min(mouse.y + 18f, Screen.height - 58f),
                258f,
                48f);
            GUI.Box(rect, GUI.tooltip, tooltipStyle);
        }

        private string AbilityCode(RaidAbilitySpec ability)
        {
            var key = ability.id ?? ability.displayName ?? string.Empty;
            if (abilityCodeCache.TryGetValue(key, out var cached)) return cached;
            var name = ability.displayName ?? "Ability";
            var letters = new char[3];
            var count = 0;
            var atWordStart = true;
            for (var index = 0; index < name.Length && count < letters.Length; index++)
            {
                var character = name[index];
                if (char.IsLetterOrDigit(character) && atWordStart)
                {
                    letters[count++] = char.ToUpperInvariant(character);
                    atWordStart = false;
                }
                else if (!char.IsLetterOrDigit(character))
                {
                    atWordStart = true;
                }
            }
            if (count < 2)
            {
                count = 0;
                for (var index = 0; index < name.Length && count < letters.Length; index++)
                {
                    if (char.IsLetterOrDigit(name[index]))
                    {
                        letters[count++] = char.ToUpperInvariant(name[index]);
                    }
                }
            }
            cached = count > 0 ? new string(letters, 0, count) : "ABL";
            abilityCodeCache[key] = cached;
            return cached;
        }

        private static string StatusCode(RaidStatusEffectKind kind) => kind switch
        {
            RaidStatusEffectKind.TemporaryShield => "SHD",
            RaidStatusEffectKind.Taunted => "TNT",
            RaidStatusEffectKind.Stunned => "STN",
            RaidStatusEffectKind.DamageUp => "ATK",
            RaidStatusEffectKind.Empowered => "EMP",
            RaidStatusEffectKind.Vulnerable => "MRK",
            RaidStatusEffectKind.Burning => "BRN",
            RaidStatusEffectKind.Downed => "DWN",
            _ => "FX"
        };

        private static string StatusName(RaidStatusEffectKind kind) => kind switch
        {
            RaidStatusEffectKind.TemporaryShield => "Temporary shield",
            RaidStatusEffectKind.Taunted => "Taunted",
            RaidStatusEffectKind.Stunned => "Stunned",
            RaidStatusEffectKind.DamageUp => "Damage increased",
            RaidStatusEffectKind.Empowered => "Empowered",
            RaidStatusEffectKind.Vulnerable => "Marked vulnerable",
            RaidStatusEffectKind.Burning => "Burning",
            RaidStatusEffectKind.Downed => "Downed",
            _ => "Status effect"
        };

        private static Color StatusColor(RaidStatusEffectKind kind) => kind switch
        {
            RaidStatusEffectKind.TemporaryShield => new Color(1f, 0.96f, 0.72f),
            RaidStatusEffectKind.Taunted => new Color(0.2f, 0.75f, 1f),
            RaidStatusEffectKind.Stunned => new Color(0.7f, 0.88f, 1f),
            RaidStatusEffectKind.DamageUp => new Color(1f, 0.36f, 0.12f),
            RaidStatusEffectKind.Empowered => new Color(0.92f, 0.2f, 1f),
            RaidStatusEffectKind.Vulnerable => new Color(1f, 0.15f, 0.48f),
            RaidStatusEffectKind.Burning => new Color(1f, 0.42f, 0.06f),
            RaidStatusEffectKind.Downed => new Color(0.68f, 0.12f, 0.12f),
            _ => Color.white
        };

        private void EnsureStyles()
        {
            if (panelStyle != null) return;
            panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = Texture2D.whiteTexture },
                padding = new RectOffset(12, 12, 10, 10)
            };
            panelStyle.normal.textColor = Color.white;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.75f, 1f, 0.9f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            badgeStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                padding = new RectOffset(1, 1, 1, 1),
                normal =
                {
                    background = Texture2D.whiteTexture,
                    textColor = Color.white
                },
                hover =
                {
                    background = Texture2D.whiteTexture,
                    textColor = Color.white
                }
            };
            tooltipStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            teamNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            };
            teamRoleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.62f, 0.74f, 0.82f) }
            };
            barStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = Texture2D.whiteTexture },
                border = new RectOffset(),
                padding = new RectOffset()
            };
            barTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }

#if UNITY_EDITOR
        public void ConfigureEditor(DungeonRaidDirector2D director)
        {
            raid = director;
            showCombatantIndicators = true;
        }
#endif
    }
}
