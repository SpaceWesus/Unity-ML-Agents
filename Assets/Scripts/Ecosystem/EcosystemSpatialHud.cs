using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.Ecosystem
{
    /// <summary>
    /// Compact overlay for time controls, selection, and contextual world actions. It leaves
    /// the spatial world unobstructed and routes every mutation through EcosystemWorldController.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EcosystemSpatialHud : MonoBehaviour
    {
        [SerializeField] private EcosystemWorldController host;
        [SerializeField] private EcosystemSpatialWorldView worldView;
        [SerializeField, Min(280f)] private float inspectorWidth = 390f;

        private readonly Queue<Action> pendingActions = new();
        private Rect topBarRect;
        private Rect inspectorRect;
        private Rect messageRect;
        private Rect contractBoardRect;
        private string selectedHunterId = string.Empty;
        private string selectedLocationId = string.Empty;
        private bool subscribed;

        public string SelectedHunterId => selectedHunterId;
        public string SelectedLocationId => selectedLocationId;

        private void Awake()
        {
            host ??= GetComponent<EcosystemWorldController>();
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            while (pendingActions.Count > 0)
            {
                pendingActions.Dequeue()?.Invoke();
            }
        }

        private void OnGUI()
        {
            EnsureRects();
            if (host == null || host.State == null)
            {
                GUI.Box(topBarRect, "2D ECOSYSTEM  //  Waiting for persistent world state");
                return;
            }

            DrawTopBar(host.State);
            DrawGateBoard(host.State);
            DrawSelection(host.State);
            if (!string.IsNullOrWhiteSpace(host.LastActionMessage))
            {
                GUI.Box(messageRect, host.LastActionMessage);
            }
        }

        public void Initialize(
            EcosystemWorldController worldHost,
            EcosystemSpatialWorldView spatialWorldView)
        {
            Unsubscribe();
            host = worldHost;
            worldView = spatialWorldView;
            Subscribe();
            Refresh();
        }

        public void Refresh()
        {
            if (worldView != null)
            {
                selectedHunterId = worldView.SelectedHunterId;
                selectedLocationId = worldView.SelectedLocationId;
            }
        }

        /// <summary>
        /// Input System screen positions use a bottom-left origin; IMGUI uses top-left.
        /// </summary>
        public bool IsPointerOverHud(Vector2 inputSystemScreenPosition)
        {
            EnsureRects();
            var guiPoint = new Vector2(
                inputSystemScreenPosition.x,
                Screen.height - inputSystemScreenPosition.y);
            return topBarRect.Contains(guiPoint) ||
                   contractBoardRect.Contains(guiPoint) ||
                   inspectorRect.Contains(guiPoint) ||
                   messageRect.Contains(guiPoint);
        }

        private void DrawTopBar(EcosystemWorldState state)
        {
            GUI.Box(topBarRect, GUIContent.none);
            var activeCount = 0;
            if (state.hunters != null)
            {
                foreach (var hunter in state.hunters)
                {
                    if (hunter != null && hunter.IsActive) activeCount++;
                }
            }

            GUI.Label(
                new Rect(topBarRect.x + 10f, topBarRect.y + 8f, 300f, 22f),
                $"2D ECOSYSTEM   DAY {state.day}   //   {activeCount} ACTIVE HUNTERS");

            var x = topBarRect.xMax - 506f;
            if (GUI.Button(
                    new Rect(x, topBarRect.y + 5f, 80f, 28f),
                    host.AutomaticAdvanceEnabled ? "PAUSE" : "RUN"))
            {
                Enqueue(host.ToggleAutomaticAdvance);
            }
            if (GUI.Button(new Rect(x + 86f, topBarRect.y + 5f, 38f, 28f), "-"))
            {
                Enqueue(() => host.AdjustAutomaticSpeed(-1));
            }
            if (GUI.Button(new Rect(x + 130f, topBarRect.y + 5f, 38f, 28f), "+"))
            {
                Enqueue(() => host.AdjustAutomaticSpeed(1));
            }
            if (GUI.Button(new Rect(x + 174f, topBarRect.y + 5f, 82f, 28f), "+1 DAY"))
            {
                Enqueue(() => host.AdvanceDays(1));
            }
            if (GUI.Button(new Rect(x + 262f, topBarRect.y + 5f, 76f, 28f), "FOLLOW"))
            {
                Enqueue(() => worldView?.FocusControlledHunter(true));
            }
            if (GUI.Button(new Rect(x + 344f, topBarRect.y + 5f, 72f, 28f), "SAVE"))
            {
                Enqueue(host.SaveNow);
            }
            if (GUI.Button(new Rect(x + 422f, topBarRect.y + 5f, 72f, 28f), "LOAD"))
            {
                Enqueue(host.ReloadNow);
            }
        }

        private void DrawSelection(EcosystemWorldState state)
        {
            var hunter = FindHunter(state, selectedHunterId);
            var location = FindLocation(state, selectedLocationId);
            var hasSelection = hunter != null || location != null;
            inspectorRect.height = hasSelection ? 178f : 0f;
            if (!hasSelection)
            {
                return;
            }

            GUI.Box(inspectorRect, GUIContent.none);
            if (GUI.Button(
                    new Rect(inspectorRect.xMax - 28f, inspectorRect.y + 6f, 22f, 22f),
                    "X"))
            {
                Enqueue(() => worldView?.ClearSelection());
            }

            if (hunter != null)
            {
                DrawHunterSelection(state, hunter);
            }
            else
            {
                DrawLocationSelection(state, location);
            }
        }

        private void DrawGateBoard(EcosystemWorldState state)
        {
            if (state.contracts == null || contractBoardRect.width <= 0f)
            {
                return;
            }

            GUI.Box(contractBoardRect, GUIContent.none);
            GUI.Label(
                new Rect(contractBoardRect.x + 10f, contractBoardRect.y + 7f,
                    contractBoardRect.width - 20f, 22f),
                "ASSOCIATION GATE BOARD");

            var player = FindHunter(state, state.playerHunterId);
            var rowY = contractBoardRect.y + 32f;
            var rowCount = 0;
            foreach (var contract in state.contracts)
            {
                if (contract == null ||
                    contract.status is not (ContractStatus.Offered or
                        ContractStatus.Accepted or ContractStatus.Active) ||
                    contract.expiresDay < state.day)
                {
                    continue;
                }
                if (rowCount >= 6)
                {
                    break;
                }

                var gate = state.gates?.Find(item =>
                    item != null && item.id == contract.gateId);
                var owned = contract.acceptedHunterId == state.playerHunterId ||
                            (!string.IsNullOrEmpty(player?.partyId) &&
                             contract.acceptedPartyId == player.partyId);
                var status = owned ? contract.status.ToString().ToUpperInvariant() :
                    contract.status == ContractStatus.Offered ? "OPEN" : "CLAIMED";
                GUI.Label(
                    new Rect(contractBoardRect.x + 10f, rowY,
                        contractBoardRect.width - 112f, 36f),
                    $"{contract.displayName}\n" +
                    $"{gate?.biome.ToString() ?? "Unappraised"}  //  D{contract.difficulty}  //  {status}");

                if (contract.status == ContractStatus.Offered)
                {
                    var canAccept = host.CanPlayerAction(
                        HunterActionType.AcceptContract,
                        out _,
                        contractId: contract.id);
                    GUI.enabled = canAccept;
                    if (GUI.Button(
                            new Rect(contractBoardRect.xMax - 94f, rowY + 3f, 82f, 28f),
                            "ACCEPT"))
                    {
                        var contractId = contract.id;
                        Enqueue(() => host.TryPlayerAction(
                            HunterActionType.AcceptContract,
                            contractId: contractId));
                    }
                    GUI.enabled = true;
                }
                else if (contract.status == ContractStatus.Active &&
                         !string.IsNullOrEmpty(contract.activeEncounterId))
                {
                    if (GUI.Button(
                            new Rect(contractBoardRect.xMax - 94f, rowY + 3f, 82f, 28f),
                            owned ? "ENTER" : "WATCH"))
                    {
                        var encounterId = contract.activeEncounterId;
                        Enqueue(() => host.ViewEncounter(encounterId));
                    }
                }
                else if (owned)
                {
                    if (GUI.Button(
                            new Rect(contractBoardRect.xMax - 94f, rowY + 3f, 82f, 28f),
                            "LOCATE"))
                    {
                        var locationId = string.IsNullOrEmpty(contract.targetLocationId)
                            ? contract.locationId
                            : contract.targetLocationId;
                        Enqueue(() => worldView?.FocusLocation(locationId, true));
                    }
                }

                rowY += 43f;
                rowCount++;
            }

            if (rowCount == 0)
            {
                GUI.Label(
                    new Rect(contractBoardRect.x + 10f, rowY,
                        contractBoardRect.width - 20f, 28f),
                    "No current gates are available.");
            }
        }

        private void DrawHunterSelection(EcosystemWorldState state, HunterProfile hunter)
        {
            var rank = EcosystemCareerRules.RankFor(hunter);
            var build = EcosystemCareerRules.InferBuild(hunter, host.GearCatalog);
            GUI.Label(
                new Rect(inspectorRect.x + 12f, inspectorRect.y + 9f, inspectorRect.width - 48f, 22f),
                $"{hunter.displayName}   //   RANK {rank}   //   {build.Label}");
            GUI.Label(
                new Rect(inspectorRect.x + 12f, inspectorRect.y + 34f, inspectorRect.width - 24f, 38f),
                $"{hunter.currentActivity}\n{FindLocation(state, hunter.locationId)?.displayName ?? "Unknown location"}");

            var vitals = hunter.vitals;
            DrawBar(
                new Rect(inspectorRect.x + 12f, inspectorRect.y + 78f, inspectorRect.width - 24f, 16f),
                vitals?.HealthRatio ?? 0f,
                new Color(0.78f, 0.04f, 0.035f, 1f),
                vitals == null ? "HEALTH" : $"HEALTH  {vitals.currentHealth}/{vitals.maximumHealth}");
            DrawBar(
                new Rect(inspectorRect.x + 12f, inspectorRect.y + 98f, inspectorRect.width - 24f, 16f),
                vitals?.ManaRatio ?? 0f,
                new Color(0.025f, 0.28f, 0.9f, 1f),
                vitals == null ? "MANA" : $"MANA  {vitals.currentMana}/{vitals.maximumMana}");

            if (GUI.Button(
                    new Rect(inspectorRect.x + 12f, inspectorRect.yMax - 36f, 126f, 27f),
                    "CENTER CAMERA"))
            {
                Enqueue(() => worldView?.FocusHunter(hunter.id, true));
            }
        }

        private void DrawLocationSelection(EcosystemWorldState state, LocationState location)
        {
            var hunterCount = 0;
            foreach (var hunter in state.hunters)
            {
                if (hunter != null && hunter.IsActive && hunter.locationId == location.id)
                {
                    hunterCount++;
                }
            }

            GUI.Label(
                new Rect(inspectorRect.x + 12f, inspectorRect.y + 9f, inspectorRect.width - 48f, 22f),
                $"{location.displayName}   //   {location.locationType}");
            GUI.Label(
                new Rect(inspectorRect.x + 12f, inspectorRect.y + 36f, inspectorRect.width - 24f, 42f),
                $"Danger {location.danger}   //   Yield {location.resourceYield}\n" +
                $"{hunterCount} active hunter{(hunterCount == 1 ? string.Empty : "s")} present");

            var travelAllowed = host.CanPlayerAction(
                HunterActionType.Travel,
                out var travelReason,
                locationId: location.id);
            GUI.enabled = travelAllowed;
            if (GUI.Button(
                    new Rect(inspectorRect.x + 12f, inspectorRect.yMax - 42f, 96f, 30f),
                    "TRAVEL"))
            {
                var destinationId = location.id;
                Enqueue(() => host.TryPlayerAction(
                    HunterActionType.Travel,
                    locationId: destinationId));
            }
            GUI.enabled = true;

            var contract = FindAcceptedContractAt(state, location.id);
            var enterReason = contract == null
                ? "No accepted dungeon contract targets this location."
                : string.Empty;
            var enterAllowed = contract != null && host.CanPlayerAction(
                HunterActionType.EnterDungeon,
                out enterReason,
                contractId: contract.id);
            GUI.enabled = enterAllowed;
            if (GUI.Button(
                    new Rect(inspectorRect.x + 116f, inspectorRect.yMax - 42f, 132f, 30f),
                    "ENTER DUNGEON"))
            {
                var contractId = contract.id;
                Enqueue(() => host.TryPlayerAction(
                    HunterActionType.EnterDungeon,
                    contractId: contractId));
            }
            GUI.enabled = true;

            if (GUI.Button(
                    new Rect(inspectorRect.x + 256f, inspectorRect.yMax - 42f, 122f, 30f),
                    "CENTER CAMERA"))
            {
                Enqueue(() => worldView?.FocusLocation(location.id, true));
            }

            var reason = travelAllowed
                ? enterAllowed || contract == null
                    ? string.Empty
                    : enterReason
                : travelReason;
            if (!string.IsNullOrEmpty(reason))
            {
                GUI.Label(
                    new Rect(inspectorRect.x + 12f, inspectorRect.y + 82f, inspectorRect.width - 24f, 36f),
                    reason);
            }
        }

        private static void DrawBar(Rect rect, float ratio, Color fill, string label)
        {
            ratio = Mathf.Clamp01(ratio);
            GUI.Box(rect, GUIContent.none);
            var previous = GUI.color;
            GUI.color = fill;
            GUI.DrawTexture(
                new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * ratio, rect.height - 4f),
                Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(rect, label);
        }

        private void Subscribe()
        {
            if (subscribed || worldView == null)
            {
                return;
            }
            worldView.SelectionChanged += OnSelectionChanged;
            subscribed = true;
            Refresh();
        }

        private void Unsubscribe()
        {
            if (!subscribed || worldView == null)
            {
                subscribed = false;
                return;
            }
            worldView.SelectionChanged -= OnSelectionChanged;
            subscribed = false;
        }

        private void OnSelectionChanged(string hunterId, string locationId)
        {
            selectedHunterId = hunterId ?? string.Empty;
            selectedLocationId = locationId ?? string.Empty;
        }

        private void Enqueue(Action action)
        {
            if (action != null)
            {
                pendingActions.Enqueue(action);
            }
        }

        private void EnsureRects()
        {
            topBarRect = new Rect(8f, 8f, Mathf.Max(640f, Screen.width - 16f), 38f);
            inspectorRect = new Rect(
                8f,
                Mathf.Max(54f, Screen.height - 186f),
                Mathf.Min(inspectorWidth, Mathf.Max(280f, Screen.width - 16f)),
                inspectorRect.height);
            messageRect = new Rect(
                Mathf.Max(8f, Screen.width * 0.5f - 250f),
                52f,
                Mathf.Min(500f, Screen.width - 16f),
                string.IsNullOrWhiteSpace(host?.LastActionMessage) ? 0f : 34f);
            contractBoardRect = Screen.width >= 980
                ? new Rect(Screen.width - 426f, 54f, 418f, 300f)
                : new Rect(0f, 0f, 0f, 0f);
        }

        private static HunterProfile FindHunter(EcosystemWorldState state, string hunterId)
        {
            if (state?.hunters == null || string.IsNullOrEmpty(hunterId)) return null;
            return state.hunters.Find(hunter => hunter != null && hunter.id == hunterId);
        }

        private static LocationState FindLocation(EcosystemWorldState state, string locationId)
        {
            if (state?.map?.locations == null || string.IsNullOrEmpty(locationId)) return null;
            return state.map.locations.Find(location => location != null && location.id == locationId);
        }

        private static ContractState FindAcceptedContractAt(
            EcosystemWorldState state,
            string locationId)
        {
            if (state?.contracts == null) return null;
            foreach (var contract in state.contracts)
            {
                if (contract != null && contract.status == ContractStatus.Accepted &&
                    (contract.targetLocationId == locationId || contract.locationId == locationId))
                {
                    return contract;
                }
            }
            return null;
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            EcosystemWorldController worldHost,
            EcosystemSpatialWorldView spatialWorldView)
        {
            host = worldHost;
            worldView = spatialWorldView;
        }
#endif
    }
}
