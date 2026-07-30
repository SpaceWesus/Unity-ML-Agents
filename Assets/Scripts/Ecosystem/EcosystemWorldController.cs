using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Turtle.Ecosystem
{
    [DisallowMultipleComponent]
    public sealed class EcosystemWorldController : MonoBehaviour
    {
        private const float AutomaticDayDuration = 20f;
        private static readonly float[] AutomaticSpeedMultipliers = { 0.5f, 1f, 2f, 4f };

        [SerializeField] private EcosystemGearDefinition[] gearCatalog =
            Array.Empty<EcosystemGearDefinition>();
        [SerializeField] private EcosystemPlayerController playerController;
        [SerializeField] private bool spawnThreeDimensionalHunterViews;
        [SerializeField] private bool automaticAdvanceEnabled = true;
        [SerializeField, Range(0, 3)] private int automaticSpeedIndex = 1;
        [Header("2D spatial ecosystem")]
        [SerializeField] private EcosystemSpatialWorldView spatialWorldView;
        [SerializeField] private EcosystemSpatialHud spatialHud;
        [SerializeField] private EcosystemPlayerInput2D playerInput2D;
        [SerializeField] private EcosystemMapCameraController mapCameraController;
        [SerializeField] private EcosystemDungeonWorldView dungeonWorldView;

        private readonly List<EcosystemHunterView> hunterViews = new();
        private EcosystemWorldState state;
        private EcosystemSimulation simulation;
        private EcosystemSaveRepository saveRepository;
        private EcosystemStrategyView strategyView;
        private float nextAutomaticDay;
        private float encounterClockAccumulator;
        private EcosystemPlayerIntent2D latestPlayerIntent;
        private bool bufferedPrimaryAttack;
        private bool bufferedInteraction;

        public EcosystemWorldState State => state;
        public IReadOnlyList<EcosystemGearDefinition> GearCatalog => gearCatalog;
        public string LastActionMessage { get; private set; } = string.Empty;
        public string SavePath => saveRepository?.ActiveSavePath ?? string.Empty;
        public EcosystemSpatialWorldView SpatialWorldView => spatialWorldView;
        public EcosystemDungeonWorldView DungeonWorldView => dungeonWorldView;
        public float AutomaticAdvanceMultiplier =>
            AutomaticSpeedMultipliers[Mathf.Clamp(automaticSpeedIndex, 0, AutomaticSpeedMultipliers.Length - 1)];
        public float AutomaticDayIntervalSeconds => AutomaticDayDuration / AutomaticAdvanceMultiplier;
        public bool AutomaticAdvanceEnabled
        {
            get => automaticAdvanceEnabled;
            set
            {
                automaticAdvanceEnabled = value;
                ScheduleNextAutomaticDay();
            }
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            EcosystemGearDefinition[] availableGear,
            EcosystemPlayerController scenePlayer,
            EcosystemSpatialWorldView authoredSpatialWorldView = null,
            EcosystemSpatialHud authoredSpatialHud = null,
            EcosystemPlayerInput2D authoredPlayerInput = null,
            EcosystemMapCameraController authoredMapCamera = null,
            EcosystemDungeonWorldView authoredDungeonWorldView = null)
        {
            gearCatalog = availableGear;
            playerController = scenePlayer;
            spatialWorldView = authoredSpatialWorldView;
            spatialHud = authoredSpatialHud;
            playerInput2D = authoredPlayerInput;
            mapCameraController = authoredMapCamera;
            dungeonWorldView = authoredDungeonWorldView;
        }
#endif

        private void Awake()
        {
            gearCatalog ??= Array.Empty<EcosystemGearDefinition>();
            saveRepository = new EcosystemSaveRepository(gearCatalog);
            state = saveRepository.LoadOrCreate(out var loadStatus);
            EcosystemWorldFactory.UpgradeAndNormalize(state, gearCatalog);
            simulation = new EcosystemSimulation(state, gearCatalog);
            LastActionMessage = loadStatus;

            // This scene is now a mechanics-first 2D campaign prototype. The legacy 3D
            // character remains serialized for later reuse, but does not capture mouse/input.
            if (playerController != null)
            {
                playerController.enabled = false;
            }
            var cameraRig = FindFirstObjectByType<EcosystemCameraRig>();
            if (cameraRig != null)
            {
                cameraRig.enabled = false;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            ApplyPlayerGear();
            if (spawnThreeDimensionalHunterViews)
            {
                SpawnHunterViews();
                RefreshHunterViews();
            }

            strategyView = GetComponent<EcosystemStrategyView>();
            if (strategyView != null)
            {
                // Retained for data/debug archaeology, but the playable spatial world is now
                // the scene's primary presentation instead of a full-screen text dashboard.
                strategyView.enabled = false;
            }

            spatialWorldView ??= GetComponent<EcosystemSpatialWorldView>();
            spatialHud ??= GetComponent<EcosystemSpatialHud>();
            playerInput2D ??= GetComponent<EcosystemPlayerInput2D>();
            dungeonWorldView ??= GetComponent<EcosystemDungeonWorldView>();
            mapCameraController ??= FindFirstObjectByType<EcosystemMapCameraController>();
            spatialWorldView?.Initialize(this);
            if (spatialWorldView != null && dungeonWorldView != null)
            {
                spatialWorldView.SetSpatialPoseSource(dungeonWorldView);
                spatialWorldView.SetEncounterPresentationSource(dungeonWorldView);
            }
            if (dungeonWorldView != null)
            {
                dungeonWorldView.LeaveViewRequested += LeaveDungeonView;
                dungeonWorldView.RetreatRequested += RetreatFromViewedDungeon;
            }
            if (playerInput2D != null)
            {
                playerInput2D.GameplayInputEnabled = false;
            }
            ShowPlayerEncounterIfPresent();
            ScheduleNextAutomaticDay();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.tKey.wasPressedThisFrame)
                {
                    AdvanceDays(1);
                }
                if (keyboard.f5Key.wasPressedThisFrame)
                {
                    SaveNow();
                }
                if (keyboard.f9Key.wasPressedThisFrame)
                {
                    ReloadNow();
                }
                if (keyboard.pKey.wasPressedThisFrame)
                {
                    ToggleAutomaticAdvance();
                }
            }

            CapturePlayerEncounterInput();
            AdvanceEncounterClock();

            if (automaticAdvanceEnabled && Time.unscaledTime >= nextAutomaticDay)
            {
                AdvanceDaysInternal(1, false);
            }
        }

        private void CapturePlayerEncounterInput()
        {
            if (playerInput2D == null || dungeonWorldView == null ||
                !dungeonWorldView.IsShowingEncounter)
            {
                return;
            }

            latestPlayerIntent = playerInput2D.CurrentIntent;
            bufferedPrimaryAttack |= latestPlayerIntent.LightAttackPressed;
            bufferedInteraction |= latestPlayerIntent.InteractPressed;
        }

        private void AdvanceEncounterClock()
        {
            if (simulation == null || state?.encounters == null || state.encounters.Count == 0)
            {
                return;
            }

            var directlyPlaying = dungeonWorldView != null &&
                                  dungeonWorldView.IsShowingEncounter;
            if (!automaticAdvanceEnabled && !directlyPlaying)
            {
                return;
            }

            var clockMultiplier = automaticAdvanceEnabled
                ? AutomaticAdvanceMultiplier
                : 1f;
            encounterClockAccumulator += Time.unscaledDeltaTime * clockMultiplier;
            var steps = 0;
            while (encounterClockAccumulator >= EcosystemEncounterSimulation.FixedStepSeconds &&
                   steps < 8)
            {
                encounterClockAccumulator -= EcosystemEncounterSimulation.FixedStepSeconds;
                simulation.AdvanceEncounterSteps(1, OverrideControlledEncounterIntent);
                bufferedPrimaryAttack = false;
                bufferedInteraction = false;
                steps++;
            }

            if (steps == 0)
            {
                return;
            }
            if (steps == 8)
            {
                // Avoid a runaway catch-up burst after a breakpoint or long Editor stall.
                encounterClockAccumulator = Mathf.Min(
                    encounterClockAccumulator,
                    EcosystemEncounterSimulation.FixedStepSeconds);
            }
            dungeonWorldView?.RefreshPresentation();
            spatialWorldView?.RefreshWorld();
        }

        private bool OverrideControlledEncounterIntent(
            DungeonEncounterState encounter,
            EncounterParticipantState participant,
            out EncounterInputIntent intent)
        {
            intent = null;
            if (encounter == null || participant == null || dungeonWorldView == null ||
                !dungeonWorldView.IsShowingEncounter ||
                !ReferenceEquals(encounter, dungeonWorldView.ActiveEncounter) ||
                participant.participantKind != EncounterParticipantKind.Hunter ||
                participant.sourceHunterId != state?.playerHunterId)
            {
                return false;
            }

            var aim = latestPlayerIntent.AimPlanarPosition - participant.position;
            if (aim.sqrMagnitude <= 0.0001f)
            {
                aim = participant.facing;
            }
            intent = new EncounterInputIntent
            {
                entityId = participant.entityId,
                movement = latestPlayerIntent.Movement,
                aim = aim.normalized,
                primaryAttack = bufferedPrimaryAttack,
                interact = bufferedInteraction
            };
            return true;
        }

        private void ShowPlayerEncounterIfPresent()
        {
            if (state == null || dungeonWorldView == null)
            {
                return;
            }
            var player = FindHunter(state.playerHunterId);
            var encounterId = player?.currentEncounterId;
            if (string.IsNullOrEmpty(encounterId))
            {
                var contract = state.contracts?.Find(item =>
                    item != null && item.status == ContractStatus.Active &&
                    item.acceptedPartyId == player?.partyId);
                encounterId = contract?.activeEncounterId;
            }
            var encounter = string.IsNullOrEmpty(encounterId)
                ? null
                : state.encounters?.Find(item => item != null && item.id == encounterId);
            var gate = encounter == null
                ? null
                : state.gates?.Find(item => item != null && item.id == encounter.gateId);
            if (encounter == null || gate == null)
            {
                return;
            }

            latestPlayerIntent = default;
            bufferedPrimaryAttack = false;
            bufferedInteraction = false;
            encounterClockAccumulator = 0f;
            dungeonWorldView.ShowEncounter(gate, encounter, state.playerHunterId);
            if (playerInput2D != null)
            {
                playerInput2D.GameplayInputEnabled = true;
            }
        }

        public void LeaveDungeonView()
        {
            dungeonWorldView?.HideEncounter();
            if (playerInput2D != null)
            {
                playerInput2D.GameplayInputEnabled = false;
            }
            bufferedPrimaryAttack = false;
            bufferedInteraction = false;
            spatialWorldView?.RefreshWorld();
            spatialWorldView?.FocusControlledHunter(false);
        }

        public void RetreatFromViewedDungeon()
        {
            var encounter = dungeonWorldView?.ActiveEncounter;
            var contract = encounter == null
                ? null
                : state?.contracts?.Find(item => item != null && item.id == encounter.contractId);
            if (contract == null)
            {
                LastActionMessage = "There is no active gate expedition to retreat from.";
                return;
            }
            TryPlayerAction(HunterActionType.Retreat, contractId: contract.id);
        }

        public bool ViewEncounter(string encounterId)
        {
            var encounter = string.IsNullOrEmpty(encounterId)
                ? null
                : state?.encounters?.Find(item => item != null && item.id == encounterId);
            var gate = encounter == null
                ? null
                : state?.gates?.Find(item => item != null && item.id == encounter.gateId);
            if (encounter == null || gate == null || dungeonWorldView == null)
            {
                LastActionMessage = "That gate does not currently have a materializable encounter.";
                return false;
            }

            var controlledHunterId = encounter.participants?.Exists(participant =>
                participant != null && participant.sourceHunterId == state.playerHunterId) == true
                ? state.playerHunterId
                : string.Empty;
            dungeonWorldView.ShowEncounter(gate, encounter, controlledHunterId);
            if (playerInput2D != null)
            {
                playerInput2D.GameplayInputEnabled = !string.IsNullOrEmpty(controlledHunterId);
            }
            LastActionMessage = string.IsNullOrEmpty(controlledHunterId)
                ? $"Spectating {gate.displayName}; all hunters remain AI-controlled."
                : $"Controlling {FindHunter(controlledHunterId)?.displayName} in {gate.displayName}.";
            return true;
        }

        public EcosystemActionResult TryPlayerAction(
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
            if (simulation == null || state == null)
            {
                var unavailable = EcosystemActionResult.Failed("The world simulation is not ready.");
                LastActionMessage = unavailable.summary;
                return unavailable;
            }

            var request = CreatePlayerActionRequest(
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
                pointAmount);
            var result = simulation.ExecutePlayerAction(request);
            LastActionMessage = result.summary;
            if (result.success)
            {
                ApplyPlayerGear();
                RefreshHunterViews();
                spatialWorldView?.RefreshWorld();
                if (action == HunterActionType.EnterDungeon)
                {
                    ShowPlayerEncounterIfPresent();
                }
                else if (action == HunterActionType.Retreat)
                {
                    LeaveDungeonView();
                }
                SaveWorld(false);
            }
            return result;
        }

        public bool CanPlayerAction(
            HunterActionType action,
            out string reason,
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
            if (simulation == null || state == null)
            {
                reason = "The world simulation is not ready.";
                return false;
            }

            var request = CreatePlayerActionRequest(
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
                pointAmount);
            return simulation.Actions.CanExecute(request, out reason);
        }

        public void AdvanceDays(int days)
        {
            AdvanceDaysInternal(days, true);
        }

        private void AdvanceDaysInternal(int days, bool advanceEncounterClock)
        {
            if (simulation == null || days <= 0)
            {
                return;
            }

            simulation.AdvanceDays(days, advanceEncounterClock);
            LastActionMessage = days == 1
                ? $"Advanced to day {state.day}."
                : $"Advanced {days} days to day {state.day} (week {(state.day - 1) / 7 + 1}).";
            ApplyPlayerGear();
            RefreshHunterViews();
            spatialWorldView?.RefreshWorld();
            dungeonWorldView?.RefreshPresentation();
            ScheduleNextAutomaticDay();
            SaveWorld(false);
        }

        public void AdjustAutomaticSpeed(int direction)
        {
            var previousIndex = automaticSpeedIndex;
            automaticSpeedIndex = Mathf.Clamp(
                automaticSpeedIndex + Math.Sign(direction),
                0,
                AutomaticSpeedMultipliers.Length - 1);
            if (automaticSpeedIndex == previousIndex)
            {
                return;
            }

            ScheduleNextAutomaticDay();
            LastActionMessage =
                $"Automatic world speed set to {AutomaticAdvanceMultiplier:0.#}x " +
                $"(one day every {AutomaticDayIntervalSeconds:0.#} seconds).";
        }

        public void ToggleAutomaticAdvance()
        {
            AutomaticAdvanceEnabled = !AutomaticAdvanceEnabled;
            LastActionMessage = AutomaticAdvanceEnabled
                ? "Automatic world advancement resumed."
                : "Automatic world advancement paused.";
        }

        public void SaveNow()
        {
            SaveWorld(true);
        }

        public void ReloadNow()
        {
            if (saveRepository == null)
            {
                LastActionMessage = "Load failed: the save repository is not ready.";
                return;
            }

            try
            {
                state = saveRepository.LoadOrCreate(out var loadStatus);
                EcosystemWorldFactory.UpgradeAndNormalize(state, gearCatalog);
                simulation = new EcosystemSimulation(state, gearCatalog);
                LastActionMessage = loadStatus;
                ApplyPlayerGear();
                RebuildHunterViews();
                dungeonWorldView?.HideEncounter();
                spatialWorldView?.Initialize(this);
                ShowPlayerEncounterIfPresent();
                ScheduleNextAutomaticDay();
            }
            catch (Exception exception)
            {
                LastActionMessage = $"Load failed: {exception.Message}";
            }
        }

        private void ApplyPlayerGear()
        {
            var player = FindHunter(state?.playerHunterId);
            var gear = FindGear(player?.equippedGearId);
            if (gear != null && playerController != null)
            {
                playerController.Equip(gear);
            }
        }

        private EcosystemActionRequest CreatePlayerActionRequest(
            HunterActionType action,
            string targetHunterId,
            string guildId,
            string contractId,
            string locationId,
            string gearId,
            string invitationId,
            string partyId,
            string progressionId,
            int slotIndex,
            int pointAmount)
        {
            return new EcosystemActionRequest(action, state.playerHunterId)
            {
                targetHunterId = targetHunterId,
                guildId = guildId,
                contractId = contractId,
                locationId = locationId,
                gearId = gearId,
                invitationId = invitationId,
                partyId = partyId,
                progressionId = progressionId,
                slotIndex = slotIndex,
                pointAmount = pointAmount
            };
        }

        private void ScheduleNextAutomaticDay()
        {
            nextAutomaticDay = Time.unscaledTime + AutomaticDayIntervalSeconds;
        }

        private void SpawnHunterViews()
        {
            foreach (var hunter in state.hunters)
            {
                if (hunter == null || hunter.id == state.playerHunterId || !hunter.IsActive)
                {
                    continue;
                }

                var viewObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                viewObject.name = $"Hunter - {hunter.displayName}";
                viewObject.transform.SetParent(transform);
                var view = viewObject.AddComponent<EcosystemHunterView>();
                view.Initialize(hunter);
                hunterViews.Add(view);
            }
        }

        private void RebuildHunterViews()
        {
            foreach (var view in hunterViews)
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }
            hunterViews.Clear();
            if (spawnThreeDimensionalHunterViews)
            {
                SpawnHunterViews();
                RefreshHunterViews();
            }
        }

        private void RefreshHunterViews()
        {
            foreach (var view in hunterViews)
            {
                if (view == null || view.Profile == null)
                {
                    continue;
                }
                view.gameObject.SetActive(view.Profile.IsActive);
                if (view.Profile.IsActive)
                {
                    view.Refresh(ResolveDestination(view.Profile), ResolveHunterColor(view.Profile));
                }
            }
        }

        private Vector3 ResolveDestination(HunterProfile hunter)
        {
            var location = state.map.locations.Find(item => item.id == hunter.locationId);
            if (location == null)
            {
                return Vector3.up;
            }

            var hash = EcosystemDeterministicRandom.StableHash(hunter.id);
            var angle = hash % 360u * Mathf.Deg2Rad;
            var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) *
                         (0.6f + hash % 4u * 0.2f);
            return new Vector3(
                Mathf.Lerp(-18f, 18f, location.mapPosition.x),
                1f,
                Mathf.Lerp(-18f, 18f, location.mapPosition.y)) + offset;
        }

        private Color ResolveHunterColor(HunterProfile hunter)
        {
            var guildIndex = state.guilds.FindIndex(guild => guild.id == hunter.guildId);
            return guildIndex switch
            {
                0 => new Color(0.08f, 0.5f, 1f),
                1 => new Color(0.9f, 0.08f, 0.12f),
                2 => new Color(0.92f, 0.62f, 0.08f),
                3 => new Color(0.88f, 0.9f, 0.96f),
                4 => new Color(0.56f, 0.16f, 0.76f),
                _ => new Color(0.68f, 0.68f, 0.72f)
            };
        }

        private void SaveWorld(bool userInitiated)
        {
            if (saveRepository == null || state == null)
            {
                return;
            }

            if (saveRepository.Save(state, out var error))
            {
                if (userInitiated)
                {
                    LastActionMessage = $"Saved day {state.day} to {saveRepository.ActiveSavePath}.";
                }
            }
            else
            {
                LastActionMessage = $"Save failed: {error}";
            }
        }

        private HunterProfile FindHunter(string id) =>
            string.IsNullOrEmpty(id) || state == null
                ? null
                : state.hunters.Find(hunter => hunter.id == id);

        private EcosystemGearDefinition FindGear(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            foreach (var gear in gearCatalog)
            {
                if (gear != null && gear.GearId == id)
                {
                    return gear;
                }
            }
            return null;
        }

        private void OnDisable()
        {
            if (dungeonWorldView != null)
            {
                dungeonWorldView.LeaveViewRequested -= LeaveDungeonView;
                dungeonWorldView.RetreatRequested -= RetreatFromViewedDungeon;
            }
            SaveWorld(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
