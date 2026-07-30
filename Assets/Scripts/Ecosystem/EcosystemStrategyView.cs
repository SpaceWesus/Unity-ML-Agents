using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Turtle.Ecosystem
{
    /// <summary>
    /// Full-screen, mechanics-first presentation for the guild ecosystem.
    /// This view owns only transient UI selection and scroll state. All world
    /// mutations are routed through EcosystemWorldController player actions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EcosystemStrategyView : MonoBehaviour
    {
        private const float MinimumWidth = 1280f;
        private const float MinimumHeight = 720f;
        private const float Padding = 8f;
        private const float TopBarHeight = 50f;

        private readonly struct MapMarkerLayout
        {
            public readonly LocationState Location;
            public readonly Rect Rect;

            public MapMarkerLayout(LocationState location, Rect rect)
            {
                Location = location;
                Rect = rect;
            }
        }

        private readonly struct HunterTokenLayout
        {
            public readonly HunterProfile Hunter;
            public readonly string LocationId;
            public readonly Rect CircleRect;
            public readonly Rect Bounds;

            public HunterTokenLayout(
                HunterProfile hunter,
                string locationId,
                Rect circleRect,
                Rect bounds)
            {
                Hunter = hunter;
                LocationId = locationId;
                CircleRect = circleRect;
                Bounds = bounds;
            }
        }

        [SerializeField] private EcosystemWorldController host;

        private readonly List<Texture2D> ownedTextures = new();
        private readonly Queue<Action> pendingUiActions = new();
        private readonly List<MapMarkerLayout> mapMarkerLayouts = new();
        private readonly List<HunterTokenLayout> hunterTokenLayouts = new();
        private readonly List<HunterProfile> mapHunterBuffer = new();
        private readonly List<Rect> occupiedMapRects = new();
        private Vector2 rosterScroll;
        private Vector2 hunterDetailScroll;
        private Vector2 contractScroll;
        private Vector2 actionScroll;
        private Vector2 eventScroll;
        private Vector2 decisionScroll;
        private float uiWidth;
        private float uiHeight;
        private string selectedHunterId;
        private string selectedGuildId;
        private string selectedLocationId;
        private string selectedContractId;
        private string hoveredHunterId;

        private GUIStyle screenStyle;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle labelStyle;
        private GUIStyle dimStyle;
        private GUIStyle valueStyle;
        private GUIStyle buttonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle dangerButtonStyle;
        private GUIStyle compactButtonStyle;
        private GUIStyle mapStyle;
        private GUIStyle townMarkerStyle;
        private GUIStyle resourceMarkerStyle;
        private GUIStyle dungeonMarkerStyle;
        private GUIStyle marketMarkerStyle;
        private GUIStyle hospitalMarkerStyle;
        private GUIStyle selectedMarkerStyle;
        private GUIStyle tokenGlyphStyle;
        private GUIStyle resourceLabelStyle;
        private Texture2D circleTexture;

        public void Initialize(EcosystemWorldController worldHost)
        {
            host = worldHost;
            EnsureSelections();
        }

        private void Awake()
        {
            host ??= GetComponent<EcosystemWorldController>();
        }

        private void Update()
        {
            while (pendingUiActions.Count > 0)
            {
                pendingUiActions.Dequeue()?.Invoke();
            }
        }

        private void OnGUI()
        {
            BuildStyles();
            var previousMatrix = GUI.matrix;
            var uiScale = Mathf.Min(
                1f,
                Mathf.Min(Screen.width / MinimumWidth, Screen.height / MinimumHeight));
            uiScale = Mathf.Max(0.25f, uiScale);
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            uiWidth = Screen.width / uiScale;
            uiHeight = Screen.height / uiScale;
            GUI.Box(new Rect(0f, 0f, uiWidth, uiHeight), GUIContent.none, screenStyle);

            if (host == null || host.State == null)
            {
                GUI.Label(
                    new Rect(24f, 24f, Mathf.Max(300f, uiWidth - 48f), 40f),
                    "Guild Ecosystem view is waiting for its world controller.",
                    titleStyle);
                GUI.matrix = previousMatrix;
                return;
            }

            EnsureSelections();
            var state = host.State;
            DrawTopBar(state);

            var width = Mathf.Max(MinimumWidth, uiWidth);
            var height = Mathf.Max(MinimumHeight, uiHeight);
            var contentY = TopBarHeight + Padding;
            var availableHeight = height - contentY - Padding;
            var lowerHeight = Mathf.Clamp(availableHeight * 0.39f, 250f, 330f);
            var upperHeight = availableHeight - lowerHeight - Padding;
            var leftWidth = Mathf.Clamp(width * 0.22f, 252f, 320f);
            var rightWidth = Mathf.Clamp(width * 0.31f, 370f, 470f);
            var centerWidth = width - leftWidth - rightWidth - Padding * 4f;
            var leftX = Padding;
            var centerX = leftX + leftWidth + Padding;
            var rightX = centerX + centerWidth + Padding;
            var lowerY = contentY + upperHeight + Padding;

            DrawRoster(new Rect(leftX, contentY, leftWidth, upperHeight), state);
            DrawMap(new Rect(centerX, contentY, centerWidth, upperHeight), state);
            DrawHunterDetails(new Rect(rightX, contentY, rightWidth, upperHeight), state);

            var guildHeight = Mathf.Clamp(lowerHeight * 0.47f, 116f, 152f);
            DrawGuilds(new Rect(leftX, lowerY, leftWidth, guildHeight), state);
            DrawContracts(
                new Rect(leftX, lowerY + guildHeight + Padding, leftWidth, lowerHeight - guildHeight - Padding),
                state);
            DrawActions(new Rect(centerX, lowerY, centerWidth, lowerHeight), state);

            var eventsHeight = Mathf.Clamp(lowerHeight * 0.54f, 126f, 180f);
            DrawWorldEvents(new Rect(rightX, lowerY, rightWidth, eventsHeight), state);
            DrawLatestDecision(
                new Rect(rightX, lowerY + eventsHeight + Padding, rightWidth, lowerHeight - eventsHeight - Padding),
                state);

            if (uiScale < 1f)
            {
                GUI.Label(
                    new Rect(uiWidth - 340f, uiHeight - 22f, 330f, 18f),
                    $"Scaled to {uiScale:P0}; 1280 x 720 recommended",
                    dimStyle);
            }
            GUI.matrix = previousMatrix;
        }

        private void DrawTopBar(EcosystemWorldState state)
        {
            GUI.Box(new Rect(Padding, Padding, uiWidth - Padding * 2f, TopBarHeight - Padding), GUIContent.none, panelStyle);
            var week = Mathf.Max(1, (state.day - 1) / 7 + 1);
            var activeHunters = state.hunters?.Count(hunter => hunter != null && hunter.IsActive) ?? 0;
            GUI.Label(new Rect(20f, 14f, 365f, 28f),
                $"2D ECOSYSTEM  //  D{state.day} W{week}  //  {activeHunters} ACTIVE", titleStyle);

            var autoLabel = host.AutomaticAdvanceEnabled ? "AUTO RUNNING" : "AUTO PAUSED";
            var cadence = $"{host.AutomaticAdvanceMultiplier:0.#}x  //  1 day / {host.AutomaticDayIntervalSeconds:0.#}s";
            GUI.Label(
                new Rect(390f, 16f, 300f, 24f),
                $"{autoLabel}  //  {cadence}",
                host.AutomaticAdvanceEnabled ? valueStyle : dimStyle);

            var x = uiWidth - 566f;
            if (GUI.Button(
                    new Rect(x, 13f, 76f, 28f),
                    host.AutomaticAdvanceEnabled ? "PAUSE" : "RUN",
                    compactButtonStyle))
            {
                Enqueue(host.ToggleAutomaticAdvance);
            }
            if (GUI.Button(new Rect(x + 82f, 13f, 38f, 28f), "-", compactButtonStyle))
            {
                Enqueue(() => host.AdjustAutomaticSpeed(-1));
            }
            if (GUI.Button(new Rect(x + 126f, 13f, 38f, 28f), "+", compactButtonStyle))
            {
                Enqueue(() => host.AdjustAutomaticSpeed(1));
            }
            if (GUI.Button(new Rect(x + 170f, 13f, 82f, 28f), "+1 DAY", compactButtonStyle))
            {
                Enqueue(() => host.AdvanceDays(1));
            }
            if (GUI.Button(new Rect(x + 258f, 13f, 82f, 28f), "+3 DAYS", compactButtonStyle))
            {
                Enqueue(() => host.AdvanceDays(3));
            }
            if (GUI.Button(new Rect(x + 346f, 13f, 72f, 28f), "WAIT", compactButtonStyle))
            {
                InvokeAction(HunterActionType.Wait);
            }
            if (GUI.Button(new Rect(x + 424f, 13f, 72f, 28f), "SAVE", compactButtonStyle))
            {
                Enqueue(host.SaveNow);
            }
            if (GUI.Button(new Rect(x + 502f, 13f, 56f, 28f), "LOAD", compactButtonStyle))
            {
                Enqueue(host.ReloadNow);
            }
        }

        private void DrawRoster(Rect rect, EcosystemWorldState state)
        {
            var activeCount = state.hunters?.Count(hunter => hunter != null && hunter.IsActive) ?? 0;
            DrawPanel(rect, $"HUNTERS  {activeCount} ACTIVE / {state.hunters?.Count ?? 0} HISTORICAL");
            GUILayout.BeginArea(InnerRect(rect));
            rosterScroll = GUILayout.BeginScrollView(rosterScroll);
            if (state.hunters != null)
            {
                foreach (var hunter in state.hunters)
                {
                    if (hunter == null)
                    {
                        continue;
                    }

                    var selected = hunter.id == selectedHunterId;
                    var life = !hunter.isAlive
                        ? $"DEAD D{hunter.deathDay}"
                        : hunter.isRetired
                            ? $"RETIRED D{hunter.retirementDay}"
                            : InjuryBadge(hunter, state.day);
                    var guild = FindGuild(state, hunter.guildId);
                    var location = FindLocation(state, hunter.locationId);
                    var careerLevel = hunter.career?.CareerLevel ?? Mathf.Max(1, hunter.level);
                    var rank = EcosystemCareerRules.RankFor(hunter);
                    var caption =
                        $"{hunter.displayName}   LV {careerLevel}   RANK {rank}   {life}\n" +
                        $"{ShortName(guild?.displayName, "Independent")}  •  {ShortName(location?.displayName, "Unknown")}";
                    if (GUILayout.Button(caption, selected ? selectedButtonStyle : buttonStyle, GUILayout.Height(47f)))
                    {
                        selectedHunterId = hunter.id;
                    }
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawMap(Rect rect, EcosystemWorldState state)
        {
            DrawPanel(rect, "WORLD MAP  //  CLICK A HUNTER OR LOCATION");
            var mapRect = InnerRect(rect, 9f, 30f, 9f, 9f);
            GUI.Box(mapRect, GUIContent.none, mapStyle);
            var locations = state.map?.locations;
            if (locations == null || locations.Count == 0)
            {
                GUI.Label(mapRect, "No authored locations", dimStyle);
                return;
            }

            hoveredHunterId = string.Empty;
            mapMarkerLayouts.Clear();
            var plotRect = new Rect(
                mapRect.x + 4f,
                mapRect.y + 4f,
                mapRect.width - 8f,
                Mathf.Max(80f, mapRect.height - 48f));
            GetMapBounds(locations, out var minimum, out var maximum);
            foreach (var location in locations)
            {
                if (location == null)
                {
                    continue;
                }

                var normalizedX = Mathf.InverseLerp(minimum.x, maximum.x, location.mapPosition.x);
                var normalizedY = Mathf.InverseLerp(minimum.y, maximum.y, location.mapPosition.y);
                var denseMap = locations.Count > 12;
                var markerWidth = Mathf.Clamp(plotRect.width * (denseMap ? 0.14f : 0.21f), 78f, 146f);
                var markerHeight = denseMap ? 42f : 48f;
                var marker = new Rect(
                    Mathf.Lerp(plotRect.x + 12f, plotRect.xMax - markerWidth - 12f, normalizedX),
                    Mathf.Lerp(plotRect.yMax - markerHeight - 12f, plotRect.y + 12f, normalizedY),
                    markerWidth,
                    markerHeight);
                mapMarkerLayouts.Add(new MapMarkerLayout(location, marker));
                var hunterCount = CountHuntersAt(state, location.id);
                var owner = FindGuild(state, location.controllingGuildId);
                var caption =
                    $"{LocationGlyph(location.locationType)} {location.displayName}\n" +
                    $"{hunterCount} hunters  •  {ShortName(owner?.displayName, "Unclaimed")}";
                var style = location.id == selectedLocationId
                    ? selectedMarkerStyle
                    : MarkerStyle(location.locationType);
                if (GUI.Button(marker, caption, style))
                {
                    selectedLocationId = location.id;
                }
            }

            BuildMapHunterTokens(state, plotRect);
            for (var index = 0; index < hunterTokenLayouts.Count; index++)
            {
                var token = hunterTokenLayouts[index];
                if (token.Hunter.id != selectedHunterId)
                {
                    DrawHunterToken(token, state);
                }
            }
            for (var index = 0; index < hunterTokenLayouts.Count; index++)
            {
                var token = hunterTokenLayouts[index];
                if (token.Hunter.id == selectedHunterId)
                {
                    DrawHunterToken(token, state);
                    break;
                }
            }

            var focusedHunter = FindHunter(
                state,
                string.IsNullOrEmpty(hoveredHunterId) ? selectedHunterId : hoveredHunterId);
            var selected = FindLocation(state, selectedLocationId);
            if (focusedHunter != null)
            {
                var gear = FindGear(focusedHunter.equippedGearId);
                var guild = FindGuild(state, focusedHunter.guildId);
                var build = EcosystemCareerRules.InferBuild(focusedHunter, host.GearCatalog);
                GUI.Label(
                    new Rect(mapRect.x + 8f, mapRect.yMax - 42f, mapRect.width - 16f, 20f),
                    $"HUNTER: {focusedHunter.displayName}  //  {build.Label}  //  " +
                    $"{gear?.TacticalRole.ToString() ?? "Unarmed"} gear  //  " +
                    $"{guild?.displayName ?? "Independent"}",
                    valueStyle);
            }
            else if (selected != null)
            {
                GUI.Label(
                    new Rect(mapRect.x + 8f, mapRect.yMax - 42f, mapRect.width - 16f, 20f),
                    $"SELECTED: {selected.displayName}  //  {selected.locationType}  //  " +
                    $"Danger {selected.danger}  //  Yield {selected.resourceYield}",
                    valueStyle);
            }
            GUI.Label(
                new Rect(mapRect.x + 8f, mapRect.yMax - 22f, mapRect.width - 16f, 18f),
                "ARCHETYPE GLYPH / GEAR FILL  //  GUILD RING  //  RED HEALTH  //  BLUE MANA  //  WHITE SHIELD FRAME",
                dimStyle);
        }

        private void DrawHunterDetails(Rect rect, EcosystemWorldState state)
        {
            DrawPanel(rect, "SELECTED HUNTER");
            var hunter = FindHunter(state, selectedHunterId);
            GUILayout.BeginArea(InnerRect(rect));
            hunterDetailScroll = GUILayout.BeginScrollView(hunterDetailScroll);
            if (hunter == null)
            {
                GUILayout.Label("Select a hunter from the roster.", dimStyle);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            var gear = FindGear(hunter.equippedGearId);
            var guild = FindGuild(state, hunter.guildId);
            var location = FindLocation(state, hunter.locationId);
            var careerLevel = hunter.career?.CareerLevel ?? Mathf.Max(1, hunter.level);
            GUILayout.Label($"{hunter.displayName}   CAREER LV {careerLevel}", titleStyle);
            GUILayout.Label(
                !hunter.isAlive
                    ? $"DEAD ON DAY {hunter.deathDay}  •  {hunter.deathCause}"
                    : hunter.isRetired
                        ? $"RETIRED ON DAY {hunter.retirementDay}  •  {hunter.retirementReason}"
                        : $"ACTIVE  •  AWAKENED D{hunter.awakeningDay}  •  {InjuryBadge(hunter, state.day)}",
                hunter.IsActive ? valueStyle : sectionStyle);
            DrawHunterVitals(hunter);
            DrawHunterCareer(state, hunter);
            DrawFact("Gear / role", $"{gear?.DisplayName ?? "Unarmed"}  /  {gear?.TacticalRole.ToString() ?? "None"}");
            DrawFact("Gold / reward", $"{hunter.gold}g  /  {hunter.pendingRewardGold}g pending");
            DrawFact("Guild", guild?.displayName ?? "Independent");
            DrawFact("Location", location?.displayName ?? "Unknown");
            DrawFact("Party / contract", $"{ShortName(hunter.partyId, "None")}  /  {ShortName(hunter.activeContractId, "None")}");
            DrawFact("Fame / wounds", $"{hunter.fame:0.0}  /  {hunter.wounds}");
            GUILayout.Label("GOAL", sectionStyle);
            GUILayout.Label(ShortName(hunter.goal, "No declared goal"), labelStyle);
            GUILayout.Label("CURRENT ACTIVITY", sectionStyle);
            GUILayout.Label(ShortName(hunter.currentActivity, "Idle"), dimStyle);

            if (gear != null && gear.GrantedMoves.Count > 0)
            {
                GUILayout.Label("GRANTED MOVES", sectionStyle);
                GUILayout.Label(string.Join("  •  ", gear.GrantedMoves), dimStyle);
            }

            GUILayout.Label("ACTIVE INJURIES", sectionStyle);
            var hasInjury = false;
            if (hunter.injuries != null)
            {
                foreach (var injury in hunter.injuries)
                {
                    if (injury == null || injury.healed)
                    {
                        continue;
                    }
                    hasInjury = true;
                    GUILayout.Label($"{injury.displayName} [{injury.severity}] recovers D{injury.recoveryDay}", dimStyle);
                }
            }
            if (!hasInjury)
            {
                GUILayout.Label("None", dimStyle);
            }

            GUILayout.Label("RELATIONSHIPS", sectionStyle);
            if (hunter.relationships != null && hunter.relationships.Count > 0)
            {
                foreach (var relationship in hunter.relationships)
                {
                    if (relationship == null)
                    {
                        continue;
                    }
                    var other = FindHunter(state, relationship.hunterId);
                    GUILayout.Label(
                        $"{other?.displayName ?? relationship.hunterId}: trust {relationship.trust:0.00}, affinity {relationship.affinity:0.00}, rivalry {relationship.rivalry:0.00}, grudge {relationship.grudge:0.00}",
                        dimStyle);
                }
            }
            else
            {
                GUILayout.Label("No recorded relationships", dimStyle);
            }

            GUILayout.Label("RECENT MEMORIES", sectionStyle);
            if (hunter.memories != null && hunter.memories.Count > 0)
            {
                var first = Mathf.Max(0, hunter.memories.Count - 5);
                for (var index = hunter.memories.Count - 1; index >= first; index--)
                {
                    var memory = hunter.memories[index];
                    if (memory != null)
                    {
                        GUILayout.Label($"D{memory.day}: {memory.summary} ({memory.emotionalWeight:+0.00;-0.00;0.00})", dimStyle);
                    }
                }
            }
            else
            {
                GUILayout.Label("No memories yet", dimStyle);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawGuilds(Rect rect, EcosystemWorldState state)
        {
            DrawPanel(rect, "THREE GUILDS");
            GUILayout.BeginArea(InnerRect(rect, 7f, 29f, 7f, 6f));
            for (var index = 0; index < 3; index++)
            {
                var guild = state.guilds != null && index < state.guilds.Count
                    ? state.guilds[index]
                    : null;
                if (guild == null)
                {
                    GUILayout.Box($"GUILD SLOT {index + 1}  •  VACANT", buttonStyle, GUILayout.Height(31f));
                    continue;
                }

                var caption =
                    $"{guild.displayName}  •  {guild.memberIds?.Count ?? 0} hunters  •  " +
                    $"{guild.resources} res  •  {guild.territory} territory  •  {guild.prestige:0} prestige";
                if (GUILayout.Button(
                        caption,
                        guild.id == selectedGuildId ? selectedButtonStyle : buttonStyle,
                        GUILayout.Height(31f)))
                {
                    selectedGuildId = guild.id;
                }
            }
            GUILayout.EndArea();
        }

        private void DrawContracts(Rect rect, EcosystemWorldState state)
        {
            DrawPanel(rect, "EXPIRING CONTRACTS");
            GUILayout.BeginArea(InnerRect(rect));
            contractScroll = GUILayout.BeginScrollView(contractScroll);
            var any = false;
            if (state.contracts != null)
            {
                foreach (var contract in state.contracts)
                {
                    if (contract == null ||
                        contract.status == ContractStatus.Expired ||
                        contract.status == ContractStatus.Cancelled)
                    {
                        continue;
                    }
                    any = true;
                    var days = contract.expiresDay - state.day;
                    var caption =
                        $"{contract.displayName}  [{contract.status}]\n" +
                        $"Rank {contract.difficulty}  •  {contract.rewardGold}g  •  expires in {days}d";
                    if (GUILayout.Button(
                            caption,
                            contract.id == selectedContractId ? selectedButtonStyle : buttonStyle,
                            GUILayout.Height(43f)))
                    {
                        selectedContractId = contract.id;
                        if (!string.IsNullOrEmpty(contract.targetLocationId))
                        {
                            selectedLocationId = contract.targetLocationId;
                        }
                        else if (!string.IsNullOrEmpty(contract.locationId))
                        {
                            selectedLocationId = contract.locationId;
                        }
                    }
                }
            }
            if (!any)
            {
                GUILayout.Label("No active contract offers.", dimStyle);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawCareerActions(HunterProfile player)
        {
            GUILayout.Label("CAREER / TRAINING", sectionStyle);
            var career = player?.career;
            if (career == null || !career.initialized)
            {
                GUILayout.Label("The player's career is not initialized.", dimStyle);
                return;
            }

            var rank = EcosystemCareerRules.RankFor(player);
            var build = EcosystemCareerRules.InferBuild(player, host.GearCatalog);
            GUILayout.Label(
                $"RANK {rank}  //  {build.Label}  //  CAREER LV {career.CareerLevel}",
                valueStyle);
            GUILayout.Label(
                $"AP: {career.UnspentAbilityPoints} unspent  /  {career.InvestedAbilityPoints} invested  /  " +
                $"{career.earnedAbilityPoints} earned",
                dimStyle);

            var planned = EcosystemCareerCatalog.FindAbility(career.plannedAbilityId);
            if (planned != null)
            {
                GUILayout.Label(
                    $"HOLDING {career.UnspentAbilityPoints} AP  //  PLAN: {planned.displayName} " +
                    $"({planned.abilityPointCost} AP)",
                    sectionStyle);
            }
            else if (career.UnspentAbilityPoints > 0)
            {
                GUILayout.Label($"HOLDING {career.UnspentAbilityPoints} UNSPENT AP", sectionStyle);
            }
            else
            {
                GUILayout.Label("NO UNSPENT AP", dimStyle);
            }

            GUILayout.BeginHorizontal();
            DrawCareerCommandButton("TRAIN", HunterActionType.Train, width: 96f);
            GUILayout.Label("Shared daily XP action; available only when the career action rules allow it.", dimStyle);
            GUILayout.EndHorizontal();

            GUILayout.Label("ATTRIBUTES  //  SPEND ONE AP", sectionStyle);
            foreach (var definition in EcosystemCareerCatalog.Attributes)
            {
                if (definition == null)
                {
                    continue;
                }

                var attribute = EcosystemCareerRules.FindAttribute(player, definition.id);
                GUILayout.BeginHorizontal();
                GUILayout.Label(
                    attribute == null
                        ? $"{definition.displayName}: unavailable"
                        : $"{definition.displayName}: {attribute.Value}  (base {attribute.baseValue}, +{attribute.investedAbilityPoints} AP)",
                    labelStyle,
                    GUILayout.MinWidth(250f));
                DrawCareerCommandButton(
                    "+1 AP",
                    HunterActionType.InvestAttribute,
                    definition.id,
                    pointAmount: 1,
                    width: 70f);
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("LEARN / PLAN ABILITIES", sectionStyle);
            var hasUnlearnedAbility = false;
            foreach (var definition in EcosystemCareerCatalog.Abilities)
            {
                if (definition == null || EcosystemCareerRules.IsLearned(career, definition.id))
                {
                    continue;
                }

                hasUnlearnedAbility = true;
                GUILayout.BeginHorizontal();
                GUILayout.Label(
                    $"{definition.displayName} [{definition.kind}]  " +
                    $"cost {definition.abilityPointCost} AP  /  requires {definition.requiredInvestedAbilityPoints} invested",
                    labelStyle,
                    GUILayout.MinWidth(300f));
                DrawCareerCommandButton(
                    "LEARN",
                    HunterActionType.LearnAbility,
                    definition.id,
                    width: 66f);
                DrawCareerCommandButton(
                    career.plannedAbilityId == definition.id ? "PLANNED" : "SAVE FOR",
                    HunterActionType.SaveAbilityPoints,
                    definition.id,
                    width: 78f,
                    additionalEnabled: career.plannedAbilityId != definition.id,
                    additionalReason: "This ability is already the hunter's savings plan.");
                GUILayout.EndHorizontal();
            }
            if (!hasUnlearnedAbility)
            {
                GUILayout.Label("Every catalog ability has been learned.", dimStyle);
            }

            GUILayout.Label("ACTIVE LOADOUT  //  3 COOLDOWNS, 1 ULTIMATE, 2 PASSIVES", sectionStyle);
            for (var slotIndex = 0; slotIndex < EcosystemCareerCatalog.CooldownSlotCount; slotIndex++)
            {
                DrawLoadoutSlot(
                    career,
                    $"Cooldown {slotIndex + 1}",
                    HunterAbilityKind.Cooldown,
                    HunterActionType.EquipAbility,
                    slotIndex,
                    SlotAt(career.loadout?.cooldownAbilityIds, slotIndex));
            }
            DrawLoadoutSlot(
                career,
                "Ultimate",
                HunterAbilityKind.Ultimate,
                HunterActionType.EquipAbility,
                EcosystemCareerCatalog.UltimateSlotIndex,
                career.loadout?.ultimateAbilityId);
            for (var slotIndex = 0; slotIndex < EcosystemCareerCatalog.PassiveSlotCount; slotIndex++)
            {
                DrawLoadoutSlot(
                    career,
                    $"Passive {slotIndex + 1}",
                    HunterAbilityKind.Passive,
                    HunterActionType.EquipPassive,
                    slotIndex,
                    SlotAt(career.loadout?.passiveAbilityIds, slotIndex));
            }
        }

        private void DrawLoadoutSlot(
            HunterCareerState career,
            string slotLabel,
            HunterAbilityKind kind,
            HunterActionType action,
            int slotIndex,
            string currentAbilityId)
        {
            var next = FindNextLearnedAbility(career, kind, currentAbilityId);
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                $"{slotLabel}: {AbilityName(currentAbilityId)}",
                labelStyle,
                GUILayout.MinWidth(260f));
            if (next == null)
            {
                var previous = GUI.enabled;
                GUI.enabled = false;
                GUILayout.Button(
                    new GUIContent("NO ALTERNATE", "Learn another compatible ability to change this slot."),
                    compactButtonStyle,
                    GUILayout.Width(104f),
                    GUILayout.Height(24f));
                GUI.enabled = previous;
            }
            else
            {
                DrawCareerCommandButton(
                    $"NEXT: {next.displayName}",
                    action,
                    next.id,
                    slotIndex,
                    width: 154f);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawCareerCommandButton(
            string label,
            HunterActionType action,
            string progressionId = "",
            int slotIndex = -1,
            int pointAmount = 1,
            float width = 88f,
            bool additionalEnabled = true,
            string additionalReason = "")
        {
            var reason = "The world simulation is not ready.";
            var canExecute = host != null && host.CanPlayerAction(
                action,
                out reason,
                progressionId: progressionId,
                slotIndex: slotIndex,
                pointAmount: pointAmount);
            if (!additionalEnabled)
            {
                canExecute = false;
                reason = additionalReason;
            }

            var previous = GUI.enabled;
            GUI.enabled = canExecute;
            if (GUILayout.Button(
                    new GUIContent(label, canExecute ? string.Empty : ShortName(reason, "Action unavailable.")),
                    compactButtonStyle,
                    GUILayout.Width(width),
                    GUILayout.Height(24f)))
            {
                InvokeAction(
                    action,
                    progressionId: progressionId,
                    slotIndex: slotIndex,
                    pointAmount: pointAmount);
            }
            GUI.enabled = previous;
        }

        private static HunterAbilityDefinition FindNextLearnedAbility(
            HunterCareerState career,
            HunterAbilityKind kind,
            string currentAbilityId)
        {
            HunterAbilityDefinition first = null;
            var returnNext = false;
            foreach (var definition in EcosystemCareerCatalog.Abilities)
            {
                if (definition == null || definition.kind != kind ||
                    !EcosystemCareerRules.IsLearned(career, definition.id))
                {
                    continue;
                }

                first ??= definition;
                if (returnNext)
                {
                    return definition;
                }
                if (definition.id == currentAbilityId)
                {
                    returnNext = true;
                }
            }

            if (string.IsNullOrEmpty(currentAbilityId) || !returnNext)
            {
                return first;
            }
            return first != null && first.id != currentAbilityId ? first : null;
        }

        private void DrawActions(Rect rect, EcosystemWorldState state)
        {
            DrawPanel(rect, "PLAYER ACTIONS  //  ALL CHANGES USE THE WORLD API");
            GUILayout.BeginArea(InnerRect(rect));
            actionScroll = GUILayout.BeginScrollView(actionScroll);
            var player = FindHunter(state, state.playerHunterId);
            var target = FindHunter(state, selectedHunterId);
            var location = FindLocation(state, selectedLocationId);
            var contract = FindContract(state, selectedContractId);
            var targetIsOther = target != null && target.id != state.playerHunterId;
            var playerParty = FindParty(state, player?.partyId);
            var targetParty = FindParty(state, target?.partyId);
            var canInviteToParty = playerParty != null &&
                playerParty.leaderHunterId == state.playerHunterId &&
                playerParty.memberIds.Count < 4 && targetIsOther &&
                string.IsNullOrEmpty(target.partyId) && target.locationId == player.locationId;
            var canJoinParty = player != null && targetIsOther && playerParty == null &&
                targetParty != null && targetParty.status != PartyStatus.Travelling &&
                targetParty.memberIds.Count < 4 && targetParty.locationId == player.locationId &&
                string.IsNullOrEmpty(player.activeContractId) &&
                string.IsNullOrEmpty(targetParty.activeContractId);

            DrawCareerActions(player);
            GUILayout.Space(6f);
            GUILayout.Label("WORLD / SOCIAL ACTIONS", sectionStyle);
            ActionRow(
                "JOIN SELECTED GUILD", HunterActionType.JoinGuild, true,
                "LEAVE GUILD", HunterActionType.LeaveGuild, player != null && !string.IsNullOrEmpty(player.guildId),
                guildId: selectedGuildId);
            ActionRow(
                "RECRUIT HUNTER", HunterActionType.RecruitToGuild, targetIsOther,
                "INVITE TO PARTY", HunterActionType.InviteToParty, canInviteToParty,
                targetHunterId: selectedHunterId,
                guildId: selectedGuildId);
            ActionRow(
                "FORM PARTY", HunterActionType.FormParty,
                player != null && playerParty == null && string.IsNullOrEmpty(player.activeContractId),
                "JOIN PARTY", HunterActionType.JoinParty, canJoinParty,
                targetHunterId: selectedHunterId,
                partyId: targetParty?.id ?? string.Empty);
            ActionRow(
                "LEAVE PARTY", HunterActionType.LeaveParty,
                playerParty != null && playerParty.leaderHunterId != state.playerHunterId &&
                string.IsNullOrEmpty(playerParty.activeContractId),
                "DISBAND PARTY", HunterActionType.DisbandParty,
                playerParty != null && playerParty.leaderHunterId == state.playerHunterId &&
                string.IsNullOrEmpty(playerParty.activeContractId),
                partyId: playerParty?.id ?? string.Empty);
            ActionRow(
                "ACCEPT CONTRACT", HunterActionType.AcceptContract, contract != null,
                "CLAIM REWARD", HunterActionType.ClaimReward, contract != null || (player?.pendingRewardGold ?? 0) > 0,
                contractId: selectedContractId);
            ActionRow(
                "TRAVEL", HunterActionType.Travel, location != null,
                "ENTER DUNGEON", HunterActionType.EnterDungeon, location?.locationType == LocationType.Dungeon,
                locationId: selectedLocationId,
                contractId: selectedContractId);
            ActionRow(
                "RETREAT", HunterActionType.Retreat, player != null,
                "RECOVER", HunterActionType.Recover, player != null,
                targetHunterId: state.playerHunterId,
                contractId: selectedContractId,
                locationId: selectedLocationId);
            ActionRow(
                "CLAIM SITE", HunterActionType.ClaimSite, location?.locationType == LocationType.ResourceSite,
                "HELP", HunterActionType.Help, targetIsOther,
                targetHunterId: selectedHunterId,
                guildId: selectedGuildId,
                locationId: selectedLocationId);
            ActionRow(
                "BETRAY", HunterActionType.Betray, targetIsOther,
                "CHALLENGE", HunterActionType.Challenge, targetIsOther,
                targetHunterId: selectedHunterId);
            ActionRow(
                "RECONCILE", HunterActionType.Reconcile, targetIsOther,
                "WAIT", HunterActionType.Wait, true,
                targetHunterId: selectedHunterId);

            GUILayout.Space(4f);
            GUILayout.Label("PENDING INVITATIONS", sectionStyle);
            var invitationFound = false;
            if (state.invitations != null)
            {
                foreach (var invitation in state.invitations)
                {
                    if (invitation == null ||
                        invitation.status != InvitationStatus.Pending ||
                        invitation.recipientHunterId != state.playerHunterId)
                    {
                        continue;
                    }
                    invitationFound = true;
                    var sender = FindHunter(state, invitation.senderHunterId);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(
                        $"{invitation.invitationType} from {sender?.displayName ?? invitation.senderHunterId} (expires D{invitation.expiresDay})",
                        dimStyle,
                        GUILayout.MinWidth(190f));
                    if (GUILayout.Button("ACCEPT", compactButtonStyle, GUILayout.Width(78f)))
                    {
                        InvokeAction(HunterActionType.AcceptInvitation, invitationId: invitation.id);
                    }
                    if (GUILayout.Button("REJECT", dangerButtonStyle, GUILayout.Width(78f)))
                    {
                        InvokeAction(HunterActionType.RejectInvitation, invitationId: invitation.id);
                    }
                    GUILayout.EndHorizontal();
                }
            }
            if (!invitationFound)
            {
                GUILayout.Label("No pending invitations", dimStyle);
            }

            GUILayout.Space(4f);
            GUILayout.Label("GEAR MOVESETS", sectionStyle);
            var catalog = host.GearCatalog;
            if (catalog != null)
            {
                foreach (var gear in catalog)
                {
                    if (gear == null || player?.inventoryGearIds == null ||
                        !player.inventoryGearIds.Contains(gear.GearId))
                    {
                        continue;
                    }
                    var canTrade = targetIsOther && target.locationId == player.locationId;
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(
                            $"EQUIP {gear.DisplayName}  [{gear.TacticalRole}]",
                            buttonStyle,
                            GUILayout.Height(32f)))
                    {
                        InvokeAction(HunterActionType.EquipGear, targetHunterId: state.playerHunterId, gearId: gear.GearId);
                    }
                    var previous = GUI.enabled;
                    GUI.enabled = canTrade;
                    if (GUILayout.Button(
                            $"TRADE {gear.Price}g",
                            compactButtonStyle,
                            GUILayout.Width(104f),
                            GUILayout.Height(32f)))
                    {
                        InvokeAction(HunterActionType.TradeGear, targetHunterId: selectedHunterId, gearId: gear.GearId);
                    }
                    GUI.enabled = previous;
                    GUILayout.EndHorizontal();
                }
            }

            if (!string.IsNullOrEmpty(host.LastActionMessage))
            {
                GUILayout.Space(5f);
                GUILayout.Label($"RESULT: {host.LastActionMessage}", valueStyle);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawWorldEvents(Rect rect, EcosystemWorldState state)
        {
            DrawPanel(rect, "WORLD EVENTS");
            GUILayout.BeginArea(InnerRect(rect));
            eventScroll = GUILayout.BeginScrollView(eventScroll);
            if (state.structuredEvents != null && state.structuredEvents.Count > 0)
            {
                for (var index = state.structuredEvents.Count - 1; index >= 0; index--)
                {
                    var worldEvent = state.structuredEvents[index];
                    if (worldEvent != null)
                    {
                        GUILayout.Label($"D{worldEvent.day}  [{worldEvent.eventType}]  {worldEvent.summary}", dimStyle);
                    }
                }
            }
            else if (state.eventLog != null)
            {
                for (var index = state.eventLog.Count - 1; index >= 0; index--)
                {
                    GUILayout.Label(state.eventLog[index], dimStyle);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawLatestDecision(Rect rect, EcosystemWorldState state)
        {
            DrawPanel(rect, "LATEST INSPECTABLE DECISION");
            GUILayout.BeginArea(InnerRect(rect));
            decisionScroll = GUILayout.BeginScrollView(decisionScroll);
            var decision = LatestDecision(state);
            if (decision == null)
            {
                GUILayout.Label("No utility decision has been recorded yet.", dimStyle);
            }
            else
            {
                var actor = FindHunter(state, decision.hunterId);
                GUILayout.Label(
                    $"D{decision.day}  {actor?.displayName ?? decision.hunterId}  →  {decision.actionType}  TOTAL {decision.totalScore:+0.00;-0.00;0.00}",
                    valueStyle);
                GUILayout.Label($"Target: {ShortName(decision.targetId, "none")}  •  selected: {decision.selected}", dimStyle);
                GUILayout.Label(
                    $"{ShortName(decision.category, "Uncategorized")}  //  " +
                    $"{(decision.executable ? "EXECUTABLE" : "REJECTED")}",
                    decision.executable ? dimStyle : sectionStyle);
                GUILayout.Label($"ID: {ShortName(decision.decisionId, "legacy record")}", dimStyle);
                if (!string.IsNullOrEmpty(decision.rejectionReason))
                {
                    GUILayout.Label($"REJECTION: {decision.rejectionReason}", sectionStyle);
                }
                if (!string.IsNullOrEmpty(decision.finalExplanation))
                {
                    GUILayout.Label(decision.finalExplanation, labelStyle);
                }
                if (!string.IsNullOrEmpty(decision.tieBreakExplanation))
                {
                    GUILayout.Label($"Tie-break: {decision.tieBreakExplanation}", dimStyle);
                }
                if (decision.factors != null)
                {
                    foreach (var factor in decision.factors)
                    {
                        if (factor == null)
                        {
                            continue;
                        }
                        GUILayout.Label(
                            $"{factor.key}: {factor.contribution:+0.00;-0.00;0.00}  " +
                            $"({factor.rawValue:0.00} × {factor.weight:0.00})  {factor.explanation}",
                            dimStyle);
                    }
                }
            }
            DrawRecentRejectedDecision(state, decision);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawRecentRejectedDecision(
            EcosystemWorldState state,
            HunterDecisionRecord displayed)
        {
            var rejected = LatestRejectedDecision(state, displayed);
            if (rejected == null)
            {
                return;
            }

            GUILayout.Space(5f);
            GUILayout.Label("RECENT REJECTED PROPOSAL", sectionStyle);
            GUILayout.Label(
                $"D{rejected.day}  {rejected.category} / {rejected.actionType}  " +
                $"Target: {ShortName(rejected.targetId, "none")}",
                dimStyle);
            GUILayout.Label(
                ShortName(rejected.rejectionReason, "Rejected by authoritative validation."),
                labelStyle);
        }

        private void ActionRow(
            string leftLabel,
            HunterActionType leftAction,
            bool leftEnabled,
            string rightLabel,
            HunterActionType rightAction,
            bool rightEnabled,
            string targetHunterId = "",
            string guildId = "",
            string contractId = "",
            string locationId = "",
            string gearId = "",
            string partyId = "")
        {
            GUILayout.BeginHorizontal();
            DrawActionButton(
                leftLabel,
                leftAction,
                leftEnabled,
                targetHunterId,
                guildId,
                contractId,
                locationId,
                gearId,
                partyId);
            DrawActionButton(
                rightLabel,
                rightAction,
                rightEnabled,
                targetHunterId,
                guildId,
                contractId,
                locationId,
                gearId,
                partyId);
            GUILayout.EndHorizontal();
        }

        private void DrawActionButton(
            string label,
            HunterActionType action,
            bool enabled,
            string targetHunterId,
            string guildId,
            string contractId,
            string locationId,
            string gearId,
            string partyId)
        {
            var previous = GUI.enabled;
            GUI.enabled = enabled;
            if (GUILayout.Button(label, buttonStyle, GUILayout.Height(30f)))
            {
                InvokeAction(action, targetHunterId, guildId, contractId, locationId, gearId, partyId: partyId);
            }
            GUI.enabled = previous;
        }

        private void InvokeAction(
            HunterActionType action,
            string targetHunterId = "",
            string guildId = "",
            string contractId = "",
            string locationId = "",
            string gearId = "",
            string invitationId = "",
            string partyId = "",
            string progressionId = "",
            int slotIndex = -1,
            int pointAmount = 1)
        {
            Enqueue(() => host.TryPlayerAction(
                action,
                targetHunterId,
                guildId,
                contractId,
                locationId,
                gearId,
                invitationId,
                partyId,
                progressionId,
                slotIndex,
                pointAmount));
        }

        private void Enqueue(Action action)
        {
            if (action != null)
            {
                pendingUiActions.Enqueue(action);
            }
        }

        private void EnsureSelections()
        {
            var state = host?.State;
            if (state == null)
            {
                return;
            }

            if (FindHunter(state, selectedHunterId) == null && state.hunters != null && state.hunters.Count > 0)
            {
                selectedHunterId = state.hunters[0]?.id;
            }
            if (FindGuild(state, selectedGuildId) == null && state.guilds != null && state.guilds.Count > 0)
            {
                selectedGuildId = state.guilds[0]?.id;
            }
            if (FindLocation(state, selectedLocationId) == null && state.map?.locations != null && state.map.locations.Count > 0)
            {
                selectedLocationId = state.map.locations[0]?.id;
            }
            if (FindContract(state, selectedContractId) == null && state.contracts != null && state.contracts.Count > 0)
            {
                selectedContractId = state.contracts[0]?.id;
            }
        }

        private EcosystemGearDefinition FindGear(string gearId)
        {
            var catalog = host.GearCatalog;
            if (catalog == null)
            {
                return null;
            }
            foreach (var gear in catalog)
            {
                if (gear != null && gear.GearId == gearId)
                {
                    return gear;
                }
            }
            return null;
        }

        private static HunterProfile FindHunter(EcosystemWorldState state, string hunterId)
        {
            if (state?.hunters == null || string.IsNullOrEmpty(hunterId))
            {
                return null;
            }
            foreach (var hunter in state.hunters)
            {
                if (hunter != null && hunter.id == hunterId)
                {
                    return hunter;
                }
            }
            return null;
        }

        private static GuildState FindGuild(EcosystemWorldState state, string guildId)
        {
            if (state?.guilds == null || string.IsNullOrEmpty(guildId))
            {
                return null;
            }
            foreach (var guild in state.guilds)
            {
                if (guild != null && guild.id == guildId)
                {
                    return guild;
                }
            }
            return null;
        }

        private static LocationState FindLocation(EcosystemWorldState state, string locationId)
        {
            if (state?.map?.locations == null || string.IsNullOrEmpty(locationId))
            {
                return null;
            }
            foreach (var location in state.map.locations)
            {
                if (location != null && location.id == locationId)
                {
                    return location;
                }
            }
            return null;
        }

        private static ContractState FindContract(EcosystemWorldState state, string contractId)
        {
            if (state?.contracts == null || string.IsNullOrEmpty(contractId))
            {
                return null;
            }
            foreach (var contract in state.contracts)
            {
                if (contract != null && contract.id == contractId)
                {
                    return contract;
                }
            }
            return null;
        }

        private static PartyState FindParty(EcosystemWorldState state, string partyId)
        {
            if (state?.parties == null || string.IsNullOrEmpty(partyId))
            {
                return null;
            }
            foreach (var party in state.parties)
            {
                if (party != null && party.id == partyId && party.status != PartyStatus.Disbanded)
                {
                    return party;
                }
            }
            return null;
        }

        private static HunterDecisionRecord LatestDecision(EcosystemWorldState state)
        {
            if (state?.decisionRecords == null)
            {
                return null;
            }
            for (var index = state.decisionRecords.Count - 1; index >= 0; index--)
            {
                if (state.decisionRecords[index] != null)
                {
                    return state.decisionRecords[index];
                }
            }
            return null;
        }

        private static HunterDecisionRecord LatestCareerDecision(
            EcosystemWorldState state,
            string hunterId)
        {
            if (state?.decisionRecords == null || string.IsNullOrEmpty(hunterId))
            {
                return null;
            }
            for (var index = state.decisionRecords.Count - 1; index >= 0; index--)
            {
                var candidate = state.decisionRecords[index];
                if (candidate == null || candidate.hunterId != hunterId)
                {
                    continue;
                }
                if (string.Equals(candidate.category, "Career", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.category, "Progression", StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static HunterDecisionRecord LatestRejectedDecision(
            EcosystemWorldState state,
            HunterDecisionRecord displayed)
        {
            if (state?.decisionRecords == null)
            {
                return null;
            }
            for (var index = state.decisionRecords.Count - 1; index >= 0; index--)
            {
                var candidate = state.decisionRecords[index];
                if (candidate != null && candidate != displayed && !candidate.executable)
                {
                    return candidate;
                }
            }
            return null;
        }

        private static int CountHuntersAt(EcosystemWorldState state, string locationId)
        {
            var count = 0;
            if (state?.hunters == null)
            {
                return count;
            }
            foreach (var hunter in state.hunters)
            {
                if (hunter != null && hunter.IsActive && hunter.locationId == locationId)
                {
                    count++;
                }
            }
            return count;
        }

        private void BuildMapHunterTokens(EcosystemWorldState state, Rect plotRect)
        {
            hunterTokenLayouts.Clear();
            occupiedMapRects.Clear();
            foreach (var marker in mapMarkerLayouts)
            {
                occupiedMapRects.Add(Expand(marker.Rect, 3f));
            }

            if (state?.hunters == null)
            {
                return;
            }

            var diameter = Mathf.Clamp(
                Mathf.Min(plotRect.width / 22f, plotRect.height / 12f),
                20f,
                28f);
            foreach (var marker in mapMarkerLayouts)
            {
                mapHunterBuffer.Clear();
                foreach (var hunter in state.hunters)
                {
                    if (hunter != null && hunter.IsActive && hunter.locationId == marker.Location.id)
                    {
                        mapHunterBuffer.Add(hunter);
                    }
                }
                mapHunterBuffer.Sort(CompareHunterIds);

                for (var index = 0; index < mapHunterBuffer.Count; index++)
                {
                    TryPlaceHunterToken(
                        marker.Rect,
                        plotRect,
                        diameter,
                        index,
                        out var circleRect,
                        out var bounds);
                    occupiedMapRects.Add(Expand(bounds, 1.5f));
                    hunterTokenLayouts.Add(new HunterTokenLayout(
                        mapHunterBuffer[index],
                        marker.Location.id,
                        circleRect,
                        bounds));
                }
            }
        }

        private bool TryPlaceHunterToken(
            Rect anchor,
            Rect plotRect,
            float diameter,
            int ordinal,
            out Rect circleRect,
            out Rect bounds)
        {
            var boundsWidth = diameter + 8f;
            var boundsHeight = diameter + 13f;
            var fallback = new Rect(
                anchor.center.x - boundsWidth * 0.5f,
                anchor.center.y - boundsHeight * 0.5f,
                boundsWidth,
                boundsHeight);

            for (var attempt = 0; attempt < 48; attempt++)
            {
                var slot = ordinal + attempt;
                var ring = slot / 8 + 1;
                var direction = TokenSlotDirection(slot % 8);
                var radiusX = anchor.width * 0.5f + boundsWidth * 0.5f + 4f +
                              (ring - 1) * (boundsWidth + 3f);
                var radiusY = anchor.height * 0.5f + boundsHeight * 0.5f + 4f +
                              (ring - 1) * (boundsHeight + 3f);
                var center = anchor.center + new Vector2(direction.x * radiusX, direction.y * radiusY);
                var candidate = ClampInside(
                    new Rect(
                        center.x - boundsWidth * 0.5f,
                        center.y - boundsHeight * 0.5f,
                        boundsWidth,
                        boundsHeight),
                    plotRect);
                fallback = candidate;
                if (IsMapSpaceAvailable(candidate))
                {
                    bounds = candidate;
                    circleRect = new Rect(
                        bounds.center.x - diameter * 0.5f,
                        bounds.y,
                        diameter,
                        diameter);
                    return true;
                }
            }

            bounds = ClampInside(fallback, plotRect);
            circleRect = new Rect(
                bounds.center.x - diameter * 0.5f,
                bounds.y,
                diameter,
                diameter);
            return false;
        }

        private void DrawHunterToken(HunterTokenLayout token, EcosystemWorldState state)
        {
            var hunter = token.Hunter;
            var selected = hunter.id == selectedHunterId;
            var gear = FindGear(hunter.equippedGearId);
            var build = EcosystemCareerRules.InferBuild(hunter, host.GearCatalog);
            if (selected)
            {
                DrawTintedCircle(Expand(token.CircleRect, 6f), new Color(1f, 0.82f, 0.22f, 1f));
            }
            DrawTintedCircle(Expand(token.CircleRect, 3f), ResolveGuildColor(hunter.guildId));
            DrawTintedCircle(token.CircleRect, new Color(0.015f, 0.022f, 0.035f, 1f));
            DrawTintedCircle(Contract(token.CircleRect, 3f), ResolveHunterFill(gear));
            GUI.Label(
                token.CircleRect,
                ArchetypeGlyph(build.Primary),
                tokenGlyphStyle);

            var barWidth = token.Bounds.width;
            var barY = token.CircleRect.yMax + 1f;
            var vitals = hunter.vitals;
            var resourceFrame = new Rect(token.Bounds.x, barY, barWidth, 9f);
            DrawCompactResourceBar(
                new Rect(resourceFrame.x + 1f, resourceFrame.y + 1f, resourceFrame.width - 2f, 3f),
                hunter.isAlive && vitals != null ? vitals.HealthRatio : 0f,
                new Color(0.82f, 0.055f, 0.045f, 1f));
            DrawCompactResourceBar(
                new Rect(resourceFrame.x + 1f, resourceFrame.y + 5f, resourceFrame.width - 2f, 3f),
                hunter.isAlive && vitals != null ? vitals.ManaRatio : 0f,
                new Color(0.04f, 0.37f, 1f, 1f));
            DrawShieldFrame(
                resourceFrame,
                hunter.isAlive && vitals != null ? vitals.ShieldRatio : 0f,
                1.4f);

            if (token.Bounds.Contains(Event.current.mousePosition))
            {
                hoveredHunterId = hunter.id;
            }
            var tooltip =
                $"{hunter.displayName} // {build.Label} // {gear?.TacticalRole.ToString() ?? "Unarmed"} gear";
            if (GUI.Button(token.Bounds, new GUIContent(string.Empty, tooltip), GUIStyle.none))
            {
                selectedHunterId = hunter.id;
                selectedLocationId = token.LocationId;
            }
            GUI.color = Color.white;
        }

        private void DrawHunterVitals(HunterProfile hunter)
        {
            var vitals = hunter?.vitals;
            if (vitals == null)
            {
                GUILayout.Label("Campaign vitals unavailable", dimStyle);
                return;
            }

            var area = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.Height(48f),
                GUILayout.ExpandWidth(true));
            var shieldRatio = hunter.isAlive ? vitals.ShieldRatio : 0f;
            GUI.Label(
                new Rect(area.x, area.y, area.width, 11f),
                $"SHIELD  {vitals.currentShield} / {vitals.maximumShield}",
                resourceLabelStyle);
            var resourceFrame = new Rect(area.x, area.y + 12f, area.width, 36f);
            DrawLabeledResourceBar(
                new Rect(resourceFrame.x + 2f, resourceFrame.y + 2f, resourceFrame.width - 4f, 14f),
                hunter.isAlive ? vitals.HealthRatio : 0f,
                new Color(0.82f, 0.055f, 0.045f, 1f),
                $"HEALTH  {vitals.currentHealth} / {vitals.maximumHealth}");
            DrawLabeledResourceBar(
                new Rect(resourceFrame.x + 2f, resourceFrame.y + 19f, resourceFrame.width - 4f, 14f),
                hunter.isAlive ? vitals.ManaRatio : 0f,
                new Color(0.04f, 0.37f, 1f, 1f),
                $"MANA  {vitals.currentMana} / {vitals.maximumMana}");
            DrawShieldFrame(resourceFrame, shieldRatio, 2.2f);
        }

        private void DrawHunterCareer(EcosystemWorldState state, HunterProfile hunter)
        {
            GUILayout.Space(4f);
            GUILayout.Label("CAREER", sectionStyle);
            var career = hunter?.career;
            if (career == null || !career.initialized)
            {
                GUILayout.Label("Career data unavailable", dimStyle);
                return;
            }

            var rank = EcosystemCareerRules.RankFor(hunter);
            var build = EcosystemCareerRules.InferBuild(hunter, host.GearCatalog);
            DrawFact("Rank / build", $"{rank}  /  {build.Label}");
            DrawFact("Career level", career.CareerLevel.ToString());

            var threshold = Math.Max(1L, EcosystemCareerRules.ExperienceThreshold(hunter));
            var currentExperience = Math.Max(0L, career.currentExperience);
            var experienceRatio = (float)Math.Min(1d, (double)currentExperience / threshold);
            var experienceRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.Height(17f),
                GUILayout.ExpandWidth(true));
            DrawLabeledResourceBar(
                experienceRect,
                experienceRatio,
                new Color(0.68f, 0.38f, 0.96f, 1f),
                $"XP  {currentExperience} / {threshold}");
            DrawFact(
                "Ability Points",
                $"{career.InvestedAbilityPoints} invested  /  {career.UnspentAbilityPoints} unspent  /  " +
                $"{career.earnedAbilityPoints} earned");

            GUILayout.Label("ATTRIBUTES", sectionStyle);
            foreach (var definition in EcosystemCareerCatalog.Attributes)
            {
                if (definition == null)
                {
                    continue;
                }
                var attribute = EcosystemCareerRules.FindAttribute(hunter, definition.id);
                GUILayout.Label(
                    attribute == null
                        ? $"{definition.displayName}: unavailable"
                        : $"{definition.displayName}: {attribute.Value}  " +
                          $"(base {attribute.baseValue}, +{attribute.investedAbilityPoints} AP)",
                    dimStyle);
            }

            GUILayout.Label("ARCHETYPE AFFINITIES", sectionStyle);
            foreach (HunterArchetype archetype in Enum.GetValues(typeof(HunterArchetype)))
            {
                GUILayout.Label(
                    $"{archetype}: {EcosystemCareerRules.AffinityFor(hunter, archetype):P0}",
                    dimStyle);
            }

            GUILayout.Label("LEARNED LIBRARY", sectionStyle);
            var learnedAny = false;
            if (career.learnedAbilities != null)
            {
                foreach (var learned in career.learnedAbilities)
                {
                    if (learned == null)
                    {
                        continue;
                    }
                    learnedAny = true;
                    var definition = EcosystemCareerCatalog.FindAbility(learned.abilityId);
                    GUILayout.Label(
                        definition == null
                            ? $"{ShortName(learned.abilityId, "Unknown ability")}  /  {learned.investedAbilityPoints} AP"
                            : $"{definition.displayName} [{definition.kind}]  /  {learned.investedAbilityPoints} AP paid",
                        dimStyle);
                }
            }
            if (!learnedAny)
            {
                GUILayout.Label("None", dimStyle);
            }

            GUILayout.Label("ACTIVE LOADOUT", sectionStyle);
            for (var slotIndex = 0; slotIndex < EcosystemCareerCatalog.CooldownSlotCount; slotIndex++)
            {
                DrawFact(
                    $"Cooldown {slotIndex + 1}",
                    AbilityName(SlotAt(career.loadout?.cooldownAbilityIds, slotIndex)));
            }
            DrawFact("Ultimate", AbilityName(career.loadout?.ultimateAbilityId));
            for (var slotIndex = 0; slotIndex < EcosystemCareerCatalog.PassiveSlotCount; slotIndex++)
            {
                DrawFact(
                    $"Passive {slotIndex + 1}",
                    AbilityName(SlotAt(career.loadout?.passiveAbilityIds, slotIndex)));
            }

            if (hunter.id != state.playerHunterId)
            {
                GUILayout.Label("AUTONOMOUS CAREER PLAN", sectionStyle);
                var planned = EcosystemCareerCatalog.FindAbility(career.plannedAbilityId);
                GUILayout.Label(
                    planned == null
                        ? "No ability is currently planned."
                        : $"Saving {career.UnspentAbilityPoints} AP for {planned.displayName} " +
                          $"({planned.abilityPointCost} AP; requires {planned.requiredInvestedAbilityPoints} invested)",
                    dimStyle);
            }

            GUILayout.Label("LATEST CAREER DECISION", sectionStyle);
            var decision = LatestCareerDecision(state, hunter.id);
            if (decision == null)
            {
                GUILayout.Label("No career decision has been recorded for this hunter.", dimStyle);
                return;
            }

            GUILayout.Label(
                $"D{decision.day}  {decision.actionType}  /  score {decision.totalScore:+0.00;-0.00;0.00}  /  " +
                $"{(decision.selected ? "selected" : "considered")}",
                valueStyle);
            GUILayout.Label(
                $"Target: {ShortName(decision.targetId, "none")}  /  " +
                $"{(decision.executable ? "executable" : ShortName(decision.rejectionReason, "rejected"))}",
                dimStyle);
            if (!string.IsNullOrEmpty(decision.finalExplanation))
            {
                GUILayout.Label(decision.finalExplanation, labelStyle);
            }
            if (decision.factors == null || decision.factors.Count == 0)
            {
                GUILayout.Label("No scored factors were recorded.", dimStyle);
                return;
            }
            foreach (var factor in decision.factors)
            {
                if (factor == null)
                {
                    continue;
                }
                GUILayout.Label(
                    $"{factor.key}: {factor.contribution:+0.00;-0.00;0.00}  " +
                    $"({factor.rawValue:0.00} x {factor.weight:0.00})  {factor.explanation}",
                    dimStyle);
            }
        }

        private void DrawLabeledResourceBar(Rect rect, float ratio, Color fill, string label)
        {
            DrawSolidRect(rect, new Color(0.025f, 0.032f, 0.046f, 1f));
            DrawSolidRect(
                new Rect(rect.x + 1f, rect.y + 1f, (rect.width - 2f) * Mathf.Clamp01(ratio), rect.height - 2f),
                fill);
            DrawRectFrame(rect, new Color(0.7f, 0.76f, 0.84f, 0.45f), 1f);
            GUI.Label(rect, label, resourceLabelStyle);
        }

        private static void DrawCompactResourceBar(Rect rect, float ratio, Color fill)
        {
            DrawSolidRect(rect, new Color(0.02f, 0.025f, 0.035f, 0.98f));
            DrawSolidRect(
                new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height),
                fill);
        }

        private static void DrawShieldFrame(Rect rect, float ratio, float maximumThickness)
        {
            ratio = Mathf.Clamp01(ratio);
            if (ratio <= 0f)
            {
                return;
            }

            var thickness = Mathf.Lerp(0.8f, Mathf.Max(0.8f, maximumThickness), ratio);
            var alpha = Mathf.Lerp(0.35f, 1f, ratio);
            DrawRectFrame(rect, new Color(0.96f, 0.98f, 1f, alpha), thickness);
        }

        private void DrawTintedCircle(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, circleTexture, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static void DrawRectFrame(Rect rect, Color color, float thickness)
        {
            DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawSolidRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static int CompareHunterIds(HunterProfile left, HunterProfile right)
        {
            return string.CompareOrdinal(left?.id, right?.id);
        }

        private bool IsMapSpaceAvailable(Rect candidate)
        {
            foreach (var occupied in occupiedMapRects)
            {
                if (occupied.Overlaps(candidate))
                {
                    return false;
                }
            }
            return true;
        }

        private static Vector2 TokenSlotDirection(int slot)
        {
            return slot switch
            {
                0 => new Vector2(0f, -1f),
                1 => new Vector2(1f, -1f),
                2 => new Vector2(1f, 0f),
                3 => new Vector2(1f, 1f),
                4 => new Vector2(0f, 1f),
                5 => new Vector2(-1f, 1f),
                6 => new Vector2(-1f, 0f),
                _ => new Vector2(-1f, -1f)
            };
        }

        private static Rect ClampInside(Rect rect, Rect container)
        {
            return new Rect(
                Mathf.Clamp(rect.x, container.x, Mathf.Max(container.x, container.xMax - rect.width)),
                Mathf.Clamp(rect.y, container.y, Mathf.Max(container.y, container.yMax - rect.height)),
                rect.width,
                rect.height);
        }

        private static Rect Expand(Rect rect, float amount)
        {
            return new Rect(
                rect.x - amount,
                rect.y - amount,
                rect.width + amount * 2f,
                rect.height + amount * 2f);
        }

        private static Rect Contract(Rect rect, float amount)
        {
            return new Rect(
                rect.x + amount,
                rect.y + amount,
                Mathf.Max(1f, rect.width - amount * 2f),
                Mathf.Max(1f, rect.height - amount * 2f));
        }

        private Color ResolveHunterFill(EcosystemGearDefinition gear)
        {
            var role = gear?.TacticalRole ?? TacticalRole.Flexible;
            var roleColor = role switch
            {
                TacticalRole.Vanguard => new Color(0.1f, 0.62f, 1f, 1f),
                TacticalRole.Bruiser => new Color(1f, 0.27f, 0.08f, 1f),
                TacticalRole.Skirmisher => new Color(0.72f, 0.14f, 1f, 1f),
                TacticalRole.Controller => new Color(0.34f, 0.36f, 1f, 1f),
                TacticalRole.Support => new Color(0.12f, 0.88f, 0.47f, 1f),
                _ => new Color(0.56f, 0.63f, 0.72f, 1f)
            };
            if (gear == null || gear.Accent.a <= 0.01f)
            {
                return roleColor;
            }
            var accent = gear.Accent;
            accent.a = 1f;
            return Color.Lerp(roleColor, accent, 0.68f);
        }

        private static Color ResolveGuildColor(string guildId)
        {
            if (string.IsNullOrEmpty(guildId))
            {
                return new Color(0.52f, 0.58f, 0.66f, 1f);
            }
            if (guildId == "guild-azure") return new Color(0.1f, 0.63f, 1f, 1f);
            if (guildId == "guild-crimson") return new Color(0.95f, 0.12f, 0.16f, 1f);
            if (guildId == "guild-verdant") return new Color(0.1f, 0.82f, 0.38f, 1f);
            if (guildId == "guild-ivory") return new Color(0.9f, 0.92f, 1f, 1f);
            if (guildId == "guild-umbra") return new Color(0.66f, 0.22f, 0.92f, 1f);

            var hue = (EcosystemDeterministicRandom.StableHash(guildId) % 1000u) / 1000f;
            var color = Color.HSVToRGB(hue, 0.72f, 0.96f);
            color.a = 1f;
            return color;
        }

        private static string ArchetypeGlyph(HunterArchetype archetype)
        {
            return archetype switch
            {
                HunterArchetype.Fighter => "F",
                HunterArchetype.Healer => "H",
                HunterArchetype.Assassin => "A",
                HunterArchetype.Ranger => "R",
                HunterArchetype.Tank => "T",
                HunterArchetype.Mage => "M",
                _ => "?"
            };
        }

        private static void GetMapBounds(
            IReadOnlyList<LocationState> locations,
            out Vector2 minimum,
            out Vector2 maximum)
        {
            minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var location in locations)
            {
                if (location == null)
                {
                    continue;
                }
                minimum = Vector2.Min(minimum, location.mapPosition);
                maximum = Vector2.Max(maximum, location.mapPosition);
            }

            if (float.IsNaN(minimum.x) || float.IsInfinity(minimum.x))
            {
                minimum = Vector2.zero;
                maximum = Vector2.one;
                return;
            }
            if (Mathf.Abs(maximum.x - minimum.x) < 0.01f)
            {
                maximum.x = minimum.x + 1f;
            }
            if (Mathf.Abs(maximum.y - minimum.y) < 0.01f)
            {
                maximum.y = minimum.y + 1f;
            }
        }

        private static string InjuryBadge(HunterProfile hunter, int day)
        {
            if (hunter?.injuries == null)
            {
                return hunter != null && hunter.wounds > 0 ? $"WOUNDED x{hunter.wounds}" : "READY";
            }
            var active = 0;
            var latestRecovery = day;
            foreach (var injury in hunter.injuries)
            {
                if (injury == null || injury.healed)
                {
                    continue;
                }
                active++;
                latestRecovery = Mathf.Max(latestRecovery, injury.recoveryDay);
            }
            return active > 0 ? $"INJURED x{active} → D{latestRecovery}" : "READY";
        }

        private static string LocationGlyph(LocationType type)
        {
            return type switch
            {
                LocationType.Town => "[TOWN]",
                LocationType.ResourceSite => "[SITE]",
                LocationType.Dungeon => "[DUNGEON]",
                LocationType.Marketplace => "[MARKET]",
                LocationType.Hospital => "[HOSPITAL]",
                _ => "[?]"
            };
        }

        private GUIStyle MarkerStyle(LocationType type)
        {
            return type switch
            {
                LocationType.Town => townMarkerStyle,
                LocationType.ResourceSite => resourceMarkerStyle,
                LocationType.Dungeon => dungeonMarkerStyle,
                LocationType.Marketplace => marketMarkerStyle,
                LocationType.Hospital => hospitalMarkerStyle,
                _ => buttonStyle
            };
        }

        private static string ShortName(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string AbilityName(string abilityId)
        {
            var definition = EcosystemCareerCatalog.FindAbility(abilityId);
            return definition?.displayName ?? "Empty";
        }

        private static string SlotAt(IReadOnlyList<string> slots, int index)
        {
            return slots != null && index >= 0 && index < slots.Count
                ? slots[index]
                : string.Empty;
        }

        private void DrawFact(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, dimStyle, GUILayout.Width(112f));
            GUILayout.Label(value, labelStyle);
            GUILayout.EndHorizontal();
        }

        private void DrawPanel(Rect rect, string title)
        {
            GUI.Box(rect, GUIContent.none, panelStyle);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, 23f), title, sectionStyle);
        }

        private static Rect InnerRect(
            Rect rect,
            float left = 8f,
            float top = 30f,
            float right = 8f,
            float bottom = 8f)
        {
            return new Rect(
                rect.x + left,
                rect.y + top,
                Mathf.Max(1f, rect.width - left - right),
                Mathf.Max(1f, rect.height - top - bottom));
        }

        private void BuildStyles()
        {
            if (screenStyle != null)
            {
                return;
            }

            var screen = CreateTexture(new Color(0.018f, 0.025f, 0.04f, 1f));
            var panel = CreateTexture(new Color(0.045f, 0.061f, 0.088f, 0.98f));
            var button = CreateTexture(new Color(0.075f, 0.1f, 0.14f, 1f));
            var selected = CreateTexture(new Color(0.08f, 0.28f, 0.48f, 1f));
            var danger = CreateTexture(new Color(0.38f, 0.09f, 0.1f, 1f));
            var map = CreateTexture(new Color(0.025f, 0.052f, 0.068f, 1f));
            var town = CreateTexture(new Color(0.07f, 0.24f, 0.42f, 1f));
            var resource = CreateTexture(new Color(0.08f, 0.31f, 0.19f, 1f));
            var dungeon = CreateTexture(new Color(0.36f, 0.09f, 0.28f, 1f));
            var market = CreateTexture(new Color(0.36f, 0.25f, 0.06f, 1f));
            var hospital = CreateTexture(new Color(0.14f, 0.3f, 0.34f, 1f));
            circleTexture = CreateCircleTexture(64);

            screenStyle = new GUIStyle(GUI.skin.box) { normal = { background = screen } };
            panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 8, 8),
                normal = { background = panel }
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.48f, 0.84f, 1f) }
            };
            sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.42f, 0.77f, 0.96f) }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = new Color(0.9f, 0.94f, 0.98f) }
            };
            dimStyle = new GUIStyle(labelStyle)
            {
                normal = { textColor = new Color(0.65f, 0.72f, 0.8f) }
            };
            valueStyle = new GUIStyle(labelStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.45f, 0.95f, 0.72f) }
            };
            buttonStyle = CreateButtonStyle(button, new Color(0.88f, 0.92f, 0.97f), 11);
            selectedButtonStyle = CreateButtonStyle(selected, Color.white, 11);
            dangerButtonStyle = CreateButtonStyle(danger, new Color(1f, 0.82f, 0.82f), 11);
            compactButtonStyle = CreateButtonStyle(button, new Color(0.74f, 0.88f, 1f), 10);
            mapStyle = new GUIStyle(panelStyle) { normal = { background = map } };
            townMarkerStyle = CreateButtonStyle(town, Color.white, 10);
            resourceMarkerStyle = CreateButtonStyle(resource, Color.white, 10);
            dungeonMarkerStyle = CreateButtonStyle(dungeon, Color.white, 10);
            marketMarkerStyle = CreateButtonStyle(market, Color.white, 9);
            hospitalMarkerStyle = CreateButtonStyle(hospital, Color.white, 9);
            selectedMarkerStyle = CreateButtonStyle(selected, new Color(1f, 0.94f, 0.55f), 10);
            tokenGlyphStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            resourceLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            };
        }

        private static GUIStyle CreateButtonStyle(Texture2D background, Color textColor, int fontSize)
        {
            return new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(5, 5, 3, 3),
                normal = { background = background, textColor = textColor },
                hover = { background = background, textColor = Color.white },
                active = { background = background, textColor = Color.white },
                focused = { background = background, textColor = textColor }
            };
        }

        private Texture2D CreateTexture(Color color)
        {
            var texture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            ownedTextures.Add(texture);
            return texture;
        }

        private Texture2D CreateCircleTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            var radius = center - 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    var alpha = Mathf.Clamp01(radius - distance + 1f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            ownedTextures.Add(texture);
            return texture;
        }

        private void OnDestroy()
        {
            foreach (var texture in ownedTextures)
            {
                if (texture != null)
                {
                    Destroy(texture);
                }
            }
            ownedTextures.Clear();
        }
    }
}
