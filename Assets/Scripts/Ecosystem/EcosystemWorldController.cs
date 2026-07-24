using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Turtle.Ecosystem
{
    [DisallowMultipleComponent]
    public sealed class EcosystemWorldController : MonoBehaviour
    {
        private const string PlayerGuildId = "guild-azure";
        private const float AutomaticDayDuration = 18f;
        private const float WorldEventViewportHeight = 134f;
        private const float WorldEventLineHeight = 21f;
        private const float WorldEventWheelScale = 0.35f;

        [SerializeField] private EcosystemGearDefinition[] gearCatalog = Array.Empty<EcosystemGearDefinition>();
        [SerializeField] private EcosystemPlayerController playerController;

        private readonly List<EcosystemHunterView> hunterViews = new();
        private EcosystemWorldState state;
        private EcosystemSimulation simulation;
        private int selectedHunterIndex = 1;
        private int selectedMissionIndex;
        private float nextAutomaticDay;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle dimStyle;
        private Vector2 logScroll;

        public EcosystemWorldState State => state;
        public string SavePath => Path.Combine(
            Application.persistentDataPath,
            "ecosystem-slice-v1.json");

#if UNITY_EDITOR
        public void ConfigureEditor(
            EcosystemGearDefinition[] availableGear,
            EcosystemPlayerController scenePlayer)
        {
            gearCatalog = availableGear;
            playerController = scenePlayer;
        }
#endif

        private void Awake()
        {
            state = LoadOrCreateState();
            simulation = new EcosystemSimulation(state, gearCatalog);
            selectedHunterIndex = Mathf.Clamp(selectedHunterIndex, 1, state.hunters.Count - 1);
            ApplyPlayerGear();
            SpawnHunterViews();
            RefreshHunterViews();
            nextAutomaticDay = Time.unscaledTime + AutomaticDayDuration;
        }

        private void Update()
        {
            ScrollWorldEvents();

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.leftArrowKey.wasPressedThisFrame)
            {
                SelectHunter(-1);
            }
            if (keyboard.rightArrowKey.wasPressedThisFrame)
            {
                SelectHunter(1);
            }
            if (keyboard.upArrowKey.wasPressedThisFrame)
            {
                SelectMission(-1);
            }
            if (keyboard.downArrowKey.wasPressedThisFrame)
            {
                SelectMission(1);
            }
            if (keyboard.iKey.wasPressedThisFrame)
            {
                InviteSelectedHunter();
            }
            if (keyboard.pKey.wasPressedThisFrame)
            {
                ProposeRaid();
            }
            if (keyboard.tKey.wasPressedThisFrame)
            {
                AdvanceDay("The player waited and watched the world move.");
            }
            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                EquipPlayer(0);
            }
            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                EquipPlayer(1);
            }
            if (keyboard.digit3Key.wasPressedThisFrame)
            {
                EquipPlayer(2);
            }
            if (keyboard.f5Key.wasPressedThisFrame)
            {
                Save();
                AddEvent($"DAY {state.day}: World state saved.");
            }

            if (Time.unscaledTime >= nextAutomaticDay)
            {
                AdvanceDay("The wider world advanced on its own.");
            }
        }

        private void ScrollWorldEvents()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) < 0.01f)
            {
                return;
            }

            var contentHeight = Mathf.Max(
                WorldEventViewportHeight,
                state.eventLog.Count * WorldEventLineHeight);
            var maximum = Mathf.Max(0f, contentHeight - WorldEventViewportHeight);
            logScroll.y = Mathf.Clamp(
                logScroll.y - wheel * WorldEventWheelScale,
                0f,
                maximum);
        }

        private void SelectHunter(int direction)
        {
            selectedHunterIndex += direction;
            if (selectedHunterIndex <= 0)
            {
                selectedHunterIndex = state.hunters.Count - 1;
            }
            else if (selectedHunterIndex >= state.hunters.Count)
            {
                selectedHunterIndex = 1;
            }
        }

        private void SelectMission(int direction)
        {
            selectedMissionIndex =
                (selectedMissionIndex + direction + state.missions.Count) %
                state.missions.Count;
        }

        private void EquipPlayer(int gearIndex)
        {
            if (gearIndex < 0 || gearIndex >= gearCatalog.Length)
            {
                return;
            }

            Player.equippedGearId = gearCatalog[gearIndex].GearId;
            Player.currentActivity = $"Training with {gearCatalog[gearIndex].DisplayName}";
            ApplyPlayerGear();
            AddEvent($"DAY {state.day}: {Player.displayName} equipped {gearCatalog[gearIndex].DisplayName}.");
            Save();
        }

        private void InviteSelectedHunter()
        {
            var candidate = SelectedHunter;
            var playerRelationship = candidate.RelationshipWith(Player.id);
            if (candidate.guildId == PlayerGuildId)
            {
                AddEvent($"{candidate.displayName} is already a member of Azure Wake.");
                return;
            }

            if (!string.IsNullOrEmpty(candidate.guildId))
            {
                playerRelationship.rivalry = Mathf.Clamp(
                    playerRelationship.rivalry + 0.08f,
                    0f,
                    1f);
                Remember(candidate, Player.id, "poaching_attempt",
                    $"{Player.displayName} tried to recruit them away from their guild.", -0.25f);
                AddEvent($"{candidate.displayName} refuses to abandon their current guild.");
                Save();
                return;
            }

            var acceptance =
                playerRelationship.affinity * 0.5f +
                playerRelationship.trust * 0.7f +
                candidate.loyalty * 0.35f +
                candidate.ambition * 0.25f -
                candidate.rivalryToward(Player.id) * 0.5f;
            if (acceptance >= 0.15f)
            {
                candidate.guildId = PlayerGuildId;
                candidate.destinationId = PlayerGuildId;
                candidate.currentActivity = "Joined Azure Wake";
                state.guilds.Find(guild => guild.id == PlayerGuildId)?.memberIds.Add(candidate.id);
                playerRelationship.trust = Mathf.Clamp(playerRelationship.trust + 0.18f, -1f, 1f);
                Remember(candidate, Player.id, "guild_invitation",
                    $"{Player.displayName} welcomed them into Azure Wake.", 0.45f);
                AddEvent($"DAY {state.day}: {candidate.displayName} joined Azure Wake.");
            }
            else
            {
                playerRelationship.affinity = Mathf.Clamp(playerRelationship.affinity - 0.04f, -1f, 1f);
                Remember(candidate, Player.id, "declined_invitation",
                    $"Declined {Player.displayName}'s guild invitation.", -0.08f);
                AddEvent($"{candidate.displayName} declined. Acceptance utility: {acceptance:0.00}.");
            }

            RefreshHunterViews();
            Save();
        }

        private void ProposeRaid()
        {
            var candidate = SelectedHunter;
            var mission = SelectedMission;
            var score = simulation.ScoreRaidInvitation(candidate, Player, mission);
            if (score < 0.05f)
            {
                candidate.RelationshipWith(Player.id).affinity -= 0.02f;
                Remember(candidate, Player.id, "declined_raid",
                    $"Refused a dangerous invitation to {mission.displayName}.", -0.1f);
                AddEvent($"{candidate.displayName} refused {mission.displayName}. Raid utility: {score:0.00}.");
                Save();
                return;
            }

            Player.destinationId = mission.id;
            candidate.destinationId = mission.id;
            var success = simulation.ResolvePartyRaid(
                Player,
                candidate,
                mission,
                GearPower(Player.equippedGearId),
                GearPower(candidate.equippedGearId));
            AddEvent(success
                ? $"{candidate.displayName}'s trust increased after the shared victory."
                : $"{candidate.displayName} remembers being wounded on the failed expedition.");
            RefreshHunterViews();
            Save();
        }

        private void AdvanceDay(string reason)
        {
            AddEvent(reason);
            simulation.AdvanceDay();
            nextAutomaticDay = Time.unscaledTime + AutomaticDayDuration;
            RefreshHunterViews();
            Save();
        }

        private void ApplyPlayerGear()
        {
            var gear = FindGear(Player.equippedGearId);
            if (gear == null && gearCatalog.Length > 0)
            {
                gear = gearCatalog[0];
                Player.equippedGearId = gear.GearId;
            }

            if (gear != null && playerController != null)
            {
                playerController.Equip(gear);
            }
        }

        private void SpawnHunterViews()
        {
            for (var index = 1; index < state.hunters.Count; index++)
            {
                var viewObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                viewObject.name = $"Hunter - {state.hunters[index].displayName}";
                viewObject.transform.SetParent(transform);
                var view = viewObject.AddComponent<EcosystemHunterView>();
                view.Initialize(state.hunters[index]);
                hunterViews.Add(view);
            }
        }

        private void RefreshHunterViews()
        {
            foreach (var view in hunterViews)
            {
                view.Refresh(ResolveDestination(view.Profile), ResolveHunterColor(view.Profile));
            }
        }

        private Vector3 ResolveDestination(HunterProfile hunter)
        {
            var anchor = string.IsNullOrEmpty(hunter.destinationId)
                ? null
                : GameObject.Find(hunter.destinationId);
            if (anchor == null)
            {
                anchor = GameObject.Find("Hub_Center");
            }

            var hash = Mathf.Abs(hunter.id.GetHashCode());
            var angle = hash % 360 * Mathf.Deg2Rad;
            var radius = 1.5f + hash % 4 * 0.45f;
            return anchor.transform.position +
                   new Vector3(Mathf.Cos(angle), 1f, Mathf.Sin(angle)) * radius;
        }

        private Color ResolveHunterColor(HunterProfile hunter)
        {
            return hunter.guildId switch
            {
                PlayerGuildId => new Color(0.08f, 0.5f, 1f),
                "guild-crimson" => new Color(0.9f, 0.08f, 0.12f),
                _ => new Color(0.68f, 0.68f, 0.72f)
            };
        }

        private EcosystemWorldState LoadOrCreateState()
        {
            if (File.Exists(SavePath))
            {
                try
                {
                    var loaded = JsonUtility.FromJson<EcosystemWorldState>(File.ReadAllText(SavePath));
                    if (loaded != null && loaded.saveVersion == 1 && loaded.hunters.Count > 1)
                    {
                        return loaded;
                    }
                }
                catch (Exception exception)
                {
                    var backup = SavePath + $".corrupt-{DateTime.UtcNow.Ticks}";
                    File.Copy(SavePath, backup, true);
                    Debug.LogWarning($"Ecosystem save was unreadable and was backed up to {backup}: {exception.Message}");
                }
            }

            var created = CreateInitialState();
            return created;
        }

        private void Save()
        {
            if (state == null)
            {
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var temporaryPath = SavePath + ".tmp";
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(state, true));
                File.Copy(temporaryPath, SavePath, true);
                File.Delete(temporaryPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not save Ecosystem Slice: {exception.Message}");
            }
        }

        private EcosystemWorldState CreateInitialState()
        {
            var created = new EcosystemWorldState
            {
                playerHunterId = "hunter-player"
            };
            created.guilds.Add(new GuildState
            {
                id = PlayerGuildId,
                displayName = "Azure Wake",
                resources = 80,
                territory = 1,
                prestige = 12f,
                memberIds = new List<string> { "hunter-player" }
            });
            created.guilds.Add(new GuildState
            {
                id = "guild-crimson",
                displayName = "Crimson Compact",
                resources = 105,
                territory = 2,
                prestige = 17f,
                memberIds = new List<string> { "hunter-mara", "hunter-voss" }
            });
            created.missions.Add(new MissionState
            {
                id = "mission-goblin",
                displayName = "Ash-Tunnel Gate",
                difficulty = 1,
                reward = 18,
                favoredTrait = "courage"
            });
            created.missions.Add(new MissionState
            {
                id = "mission-crypt",
                displayName = "Drowned Crypt",
                difficulty = 3,
                reward = 42,
                favoredTrait = "ambition"
            });
            created.missions.Add(new MissionState
            {
                id = "mission-spire",
                displayName = "Voidglass Spire",
                difficulty = 5,
                reward = 78,
                favoredTrait = "greed"
            });

            AddHunter(created, "hunter-player", "Rowan Vale", 2, 0.72f, 0.68f, 0.61f, 0.35f,
                "Found a guild that outlives them", PlayerGuildId, GearId(0));
            AddHunter(created, "hunter-mara", "Mara Quill", 3, 0.83f, 0.77f, 0.42f, 0.51f,
                "Become the most feared gatebreaker", "guild-crimson", GearId(1));
            AddHunter(created, "hunter-voss", "Voss Calder", 4, 0.66f, 0.88f, 0.73f, 0.29f,
                "Expand Crimson territory", "guild-crimson", GearId(0));
            AddHunter(created, "hunter-iona", "Iona Reed", 2, 0.48f, 0.55f, 0.91f, 0.22f,
                "Find a guild worthy of loyalty", string.Empty, GearId(0));
            AddHunter(created, "hunter-kest", "Kest Ardyn", 3, 0.58f, 0.92f, 0.34f, 0.82f,
                "Acquire a legendary relic", string.Empty, GearId(2));
            AddHunter(created, "hunter-brann", "Brann Oath", 2, 0.91f, 0.44f, 0.79f, 0.18f,
                "Protect weaker hunters", string.Empty, GearId(1));
            AddHunter(created, "hunter-sable", "Sable Nyx", 4, 0.69f, 0.81f, 0.28f, 0.75f,
                "Never be controlled again", string.Empty, GearId(2));
            AddHunter(created, "hunter-tarin", "Tarin Moss", 1, 0.39f, 0.63f, 0.84f, 0.41f,
                "Survive long enough to become renowned", string.Empty, GearId(0));

            foreach (var hunter in created.hunters)
            {
                hunter.destinationId = string.IsNullOrEmpty(hunter.guildId)
                    ? "Hub_Center"
                    : hunter.guildId;
                hunter.currentActivity = "Observing the guild district";
            }

            created.eventLog.Add("DAY 1: Azure Wake opened its doors in the frontier district.");
            created.eventLog.Add("DAY 1: Eight persistent hunters entered the simulation.");
            return created;
        }

        private static void AddHunter(
            EcosystemWorldState created,
            string id,
            string hunterName,
            int level,
            float courage,
            float ambition,
            float loyalty,
            float greed,
            string goal,
            string guildId,
            string gearId)
        {
            created.hunters.Add(new HunterProfile
            {
                id = id,
                displayName = hunterName,
                level = level,
                courage = courage,
                ambition = ambition,
                loyalty = loyalty,
                greed = greed,
                goal = goal,
                guildId = guildId,
                equippedGearId = gearId
            });
        }

        private void Remember(
            HunterProfile owner,
            string subjectId,
            string eventType,
            string summary,
            float weight)
        {
            owner.memories.Add(new HunterMemory
            {
                day = state.day,
                subjectId = subjectId,
                eventType = eventType,
                summary = summary,
                emotionalWeight = weight
            });
            while (owner.memories.Count > 12)
            {
                owner.memories.RemoveAt(0);
            }
        }

        private int GearPower(string gearId)
        {
            return FindGear(gearId)?.Power ?? 0;
        }

        private EcosystemGearDefinition FindGear(string gearId)
        {
            foreach (var gear in gearCatalog)
            {
                if (gear != null && gear.GearId == gearId)
                {
                    return gear;
                }
            }

            return null;
        }

        private string GearName(string gearId)
        {
            return FindGear(gearId)?.DisplayName ?? "Unarmed";
        }

        private string GearId(int index)
        {
            return index >= 0 && index < gearCatalog.Length
                ? gearCatalog[index].GearId
                : string.Empty;
        }

        private void AddEvent(string entry)
        {
            state.eventLog.Add(entry);
            while (state.eventLog.Count > 18)
            {
                state.eventLog.RemoveAt(0);
            }
        }

        private HunterProfile Player => state.hunters.Find(hunter => hunter.id == state.playerHunterId);
        private HunterProfile SelectedHunter => state.hunters[selectedHunterIndex];
        private MissionState SelectedMission => state.missions[selectedMissionIndex];

        private void OnDisable()
        {
            Save();
        }

        private void OnGUI()
        {
            BuildStyles();
            GUI.Box(new Rect(14f, 14f, 410f, 282f), GUIContent.none);
            GUI.Label(new Rect(28f, 22f, 382f, 30f), $"ECOSYSTEM SLICE  //  DAY {state.day}", headingStyle);
            GUI.Label(new Rect(28f, 56f, 382f, 25f),
                $"YOU: {Player.displayName}  LV {Player.level}  |  {GearName(Player.equippedGearId)}", bodyStyle);
            GUI.Label(new Rect(28f, 82f, 382f, 22f),
                $"Azure Wake: {state.guilds[0].resources} resources, {state.guilds[0].memberIds.Count} hunters", dimStyle);

            var hunter = SelectedHunter;
            var relation = hunter.RelationshipWith(Player.id);
            GUI.Label(new Rect(28f, 114f, 382f, 25f),
                $"SELECTED HUNTER: {hunter.displayName}  LV {hunter.level}", headingStyle);
            GUI.Label(new Rect(28f, 142f, 382f, 22f),
                $"{GearName(hunter.equippedGearId)}  |  {GuildName(hunter.guildId)}  |  Wounds {hunter.wounds}", bodyStyle);
            GUI.Label(new Rect(28f, 166f, 382f, 42f),
                $"Goal: {hunter.goal}\nNow: {hunter.currentActivity}", dimStyle);
            GUI.Label(new Rect(28f, 210f, 382f, 22f),
                $"Trust {relation.trust:0.00}  Affinity {relation.affinity:0.00}  Rivalry {relation.rivalry:0.00}", bodyStyle);
            GUI.Label(new Rect(28f, 236f, 382f, 42f),
                hunter.memories.Count == 0
                    ? "Latest memory: none yet"
                    : $"Latest memory: {hunter.memories[^1].summary}", dimStyle);

            GUI.Box(new Rect(Screen.width - 420f, 14f, 406f, 230f), GUIContent.none);
            GUI.Label(new Rect(Screen.width - 404f, 22f, 374f, 28f), "MISSION BOARD", headingStyle);
            GUI.Label(new Rect(Screen.width - 404f, 55f, 374f, 25f),
                $"{SelectedMission.displayName}  |  Rank {SelectedMission.difficulty}  |  Reward {SelectedMission.reward}", bodyStyle);
            GUI.Label(new Rect(Screen.width - 404f, 88f, 374f, 126f),
                "LEFT/RIGHT  Select hunter\nUP/DOWN  Select mission\nI  Invite hunter to Azure Wake\nP  Propose selected raid\n1/2/3  Equip moveset gear\nT  Advance one day   |   F5  Save\nWASD + Mouse  Move/aim   |   LMB  Use gear attack",
                dimStyle);

            GUI.Box(new Rect(14f, Screen.height - 196f, 610f, 182f), GUIContent.none);
            GUI.Label(new Rect(28f, Screen.height - 188f, 580f, 25f), "WORLD EVENTS", headingStyle);
            logScroll = GUI.BeginScrollView(
                new Rect(24f, Screen.height - 158f, 590f, 134f),
                logScroll,
                new Rect(0f, 0f, 560f, Mathf.Max(
                    WorldEventViewportHeight,
                    state.eventLog.Count * WorldEventLineHeight)));
            for (var index = 0; index < state.eventLog.Count; index++)
            {
                GUI.Label(
                    new Rect(4f, index * WorldEventLineHeight, 548f, WorldEventLineHeight),
                    state.eventLog[index],
                    dimStyle);
            }
            GUI.EndScrollView();
        }

        private string GuildName(string guildId)
        {
            if (string.IsNullOrEmpty(guildId))
            {
                return "Independent";
            }

            return state.guilds.Find(guild => guild.id == guildId)?.displayName ?? "Unknown guild";
        }

        private void BuildStyles()
        {
            if (headingStyle != null)
            {
                return;
            }

            headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.4f, 0.78f, 1f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.9f, 0.94f, 1f) }
            };
            dimStyle = new GUIStyle(bodyStyle)
            {
                wordWrap = true,
                normal = { textColor = new Color(0.7f, 0.76f, 0.84f) }
            };
        }
    }

    internal static class EcosystemHunterExtensions
    {
        public static float rivalryToward(this HunterProfile hunter, string otherHunterId)
        {
            return hunter.RelationshipWith(otherHunterId).rivalry;
        }
    }
}
