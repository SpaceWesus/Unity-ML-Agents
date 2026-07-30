using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Turtle.Ecosystem
{
    /// <summary>
    /// Read-only top-down materializer for a persisted gate encounter. The canonical
    /// <see cref="DungeonEncounterState"/> owns all positions, vitals, discoveries and
    /// outcomes; this component only builds presentation objects and interpolates them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EcosystemDungeonWorldView : MonoBehaviour,
        IEcosystemEncounterPresentationSource,
        IEcosystemSpatialPoseSource
    {
        private const float ParticipantBodyDiameter = 0.92f;
        private const float ParticipantBarWidth = 1.28f;
        private const int CorridorSortingOrder = -110;
        private const int RoomBorderSortingOrder = -100;
        private const int RoomFloorSortingOrder = -99;
        private const int DressingSortingOrder = -70;
        private const int NodeSortingOrder = 20;

        private sealed class ChildActivationSnapshot
        {
            public GameObject GameObject;
            public bool WasActive;
        }

        private sealed class RoomVisual
        {
            public GameObject Root;
            public SpriteRenderer Border;
            public SpriteRenderer Floor;
            public TextMesh Label;
        }

        private sealed class NodeVisual
        {
            public GameObject Root;
            public SpriteRenderer Renderer;
            public TextMesh Label;
        }

        private sealed class ParticipantVisual
        {
            public EncounterParticipantState State;
            public GameObject Root;
            public Transform VisualRoot;
            public SpriteRenderer Body;
            public SpriteRenderer FacingMarker;
            public SpriteRenderer HealthBackground;
            public SpriteRenderer HealthFill;
            public SpriteRenderer ShieldBackground;
            public SpriteRenderer ShieldFill;
            public TextMesh Label;
            public Vector2 CurrentPlanar;
            public Vector2 DesiredPlanar;
            public Vector2 PreviousCanonicalPlanar;
            public Vector2 Facing = Vector2.up;
            public bool IsMoving;
            public bool Initialized;
        }

        [Header("Authored scene references")]
        [Tooltip("The serialized authored-world root. The dungeon stage is one direct child of this root.")]
        [SerializeField] private Transform authoredOverworldRoot;
        [SerializeField] private Transform authoredDungeonStageRoot;
        [SerializeField] private EcosystemSpatialAuthoring authoredSpatialMap;
        [SerializeField] private EcosystemSpatialWorldView overworldView;
        [SerializeField] private EcosystemMapCameraController mapCamera;
        [SerializeField] private Camera presentationCamera;
        [SerializeField] private Sprite authoredCircleSprite;
        [SerializeField] private Sprite authoredSquareSprite;

        [Header("Dungeon presentation")]
        [SerializeField, Min(0.1f)] private float corridorWidth = 2.7f;
        [SerializeField, Min(0.1f)] private float roomBorderWidth = 0.48f;
        [SerializeField, Min(0.1f)] private float participantInterpolationSharpness = 18f;
        [SerializeField, Min(0.1f)] private float cameraFollowSharpness = 11f;
        [SerializeField, Min(2f)] private float dungeonOrthographicSize = 7.5f;
        [SerializeField, Min(0f)] private float boundsPadding = 2f;
        [SerializeField] private bool showUndiscoveredRoomsForPrototype = true;

        private readonly List<ChildActivationSnapshot> overworldChildSnapshots = new();
        private readonly Dictionary<string, RoomVisual> roomVisuals =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, NodeVisual> hazardVisuals =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, NodeVisual> lootVisuals =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, NodeVisual> resourceVisuals =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, ParticipantVisual> participantVisuals =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, EncounterParticipantState> huntersBySourceId =
            new(StringComparer.Ordinal);
        private readonly List<string> participantReleaseBuffer = new();

        private GateInstanceState activeGate;
        private DungeonEncounterState activeEncounter;
        private EncounterParticipantState controlledParticipant;
        private string controlledHunterId = string.Empty;
        private Transform runtimeContentRoot;
        private Rect dungeonBounds = new(-10f, -10f, 20f, 20f);
        private long presentationRevision;
        private long observedFixedTick = long.MinValue;
        private long observedEventSequence = long.MinValue;
        private DungeonEncounterStatus observedStatus;
        private int observedParticipantCount = -1;
        private bool isShowingEncounter;
        private bool presentationStateCaptured;
        private bool previousOverworldViewEnabled;
        private bool previousMapCameraEnabled;
        private bool previousStageActive;
        private Vector3 previousCameraPosition;
        private float previousOrthographicSize;
        private bool isTransitioning;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle mutedStyle;

        public GateInstanceState ActiveGate => activeGate;
        public DungeonEncounterState ActiveEncounter => activeEncounter;
        public bool IsShowingEncounter => isShowingEncounter;
        public EncounterParticipantState ControlledParticipant => controlledParticipant;
        public long EncounterRevision => presentationRevision;
        public long SpatialRevision => presentationRevision;

        public event Action LeaveViewRequested;
        public event Action RetreatRequested;

        private void Awake()
        {
            if (presentationCamera == null)
            {
                presentationCamera = mapCamera != null ? mapCamera.ControlledCamera : Camera.main;
            }
        }

        private void Update()
        {
            if (!isShowingEncounter || activeEncounter == null)
            {
                return;
            }

            if (CanonicalSnapshotChanged())
            {
                RefreshPresentation();
            }

            AdvanceParticipantPresentation(Time.unscaledDeltaTime);
            RaiseLeaveRequestFromKeyboard();
        }

        private void LateUpdate()
        {
            if (!isShowingEncounter || presentationCamera == null)
            {
                return;
            }

            var target = ResolveCameraTarget();
            var plane = ResolvePlane();
            var current = EcosystemSpatialCoordinates.ToPlanar(
                presentationCamera.transform.position,
                plane);
            var interpolation = cameraFollowSharpness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-cameraFollowSharpness * Mathf.Max(0f, Time.unscaledDeltaTime));
            var next = Vector2.Lerp(current, target, interpolation);
            next = ClampCameraToDungeon(next);
            presentationCamera.transform.position = EcosystemSpatialCoordinates.WithPlanar(
                presentationCamera.transform.position,
                next,
                plane);
        }

        private void OnDisable()
        {
            if (!isTransitioning && isShowingEncounter && Application.isPlaying)
            {
                HideEncounter();
            }
        }

        private void OnDestroy()
        {
            if (presentationStateCaptured)
            {
                RestoreOverworldPresentation();
            }
        }

        /// <summary>
        /// Materializes an already-persisted gate/encounter pair. This method never invokes
        /// generation, advances simulation, or changes either supplied state object.
        /// </summary>
        public void ShowEncounter(
            GateInstanceState gate,
            DungeonEncounterState encounter,
            string playerControlledHunterId)
        {
            if (gate == null || encounter == null)
            {
                Debug.LogError("Cannot show a dungeon without both a persisted gate and encounter.", this);
                return;
            }
            if (!string.IsNullOrEmpty(encounter.gateId) &&
                !string.Equals(encounter.gateId, gate.id, StringComparison.Ordinal))
            {
                Debug.LogError(
                    $"Encounter '{encounter.id}' belongs to gate '{encounter.gateId}', not '{gate.id}'.",
                    this);
                return;
            }
            if (authoredDungeonStageRoot == null)
            {
                Debug.LogError("The authored dungeon stage reference is missing.", this);
                return;
            }
            if (authoredCircleSprite == null || authoredSquareSprite == null)
            {
                Debug.LogError(
                    "The dungeon view needs serialized circle and square sprite assets.",
                    this);
                return;
            }

            isTransitioning = true;
            try
            {
                CaptureAndEnterDungeonPresentation();
                activeGate = gate;
                activeEncounter = encounter;
                controlledHunterId = playerControlledHunterId ?? string.Empty;
                isShowingEncounter = true;

                EnsureRuntimeContentRoot();
                ClearRuntimeContent();
                BuildStaticGeometry();
                BuildMutableNodeVisuals();
                BuildOrRefreshParticipants(true);
                RefreshRoomVisuals();
                RefreshMutableNodes();
                RefreshControlledParticipant();
                CalculateDungeonBounds();
                PositionCameraForEntry();
                RecordObservedSnapshot();
                IncrementRevision();
            }
            catch (Exception exception)
            {
                // A presentation failure must not strand the authored overworld in a
                // disabled state. Canonical gate/encounter data is deliberately untouched.
                Debug.LogException(exception, this);
                isShowingEncounter = false;
                ClearRuntimeContent();
                RestoreOverworldPresentation();
                activeGate = null;
                activeEncounter = null;
                controlledParticipant = null;
                controlledHunterId = string.Empty;
                IncrementRevision();
            }
            finally
            {
                isTransitioning = false;
            }
        }

        /// <summary>
        /// Leaves only the rendered dungeon view. No retreat or campaign action is applied.
        /// </summary>
        public void HideEncounter()
        {
            if (!isShowingEncounter && !presentationStateCaptured)
            {
                return;
            }

            isTransitioning = true;
            try
            {
                isShowingEncounter = false;
                ClearRuntimeContent();
                RestoreOverworldPresentation();
                activeGate = null;
                activeEncounter = null;
                controlledParticipant = null;
                controlledHunterId = string.Empty;
                huntersBySourceId.Clear();
                participantVisuals.Clear();
                roomVisuals.Clear();
                hazardVisuals.Clear();
                lootVisuals.Clear();
                resourceVisuals.Clear();
                observedFixedTick = long.MinValue;
                observedEventSequence = long.MinValue;
                observedParticipantCount = -1;
                IncrementRevision();
            }
            finally
            {
                isTransitioning = false;
            }
        }

        /// <summary>
        /// Refreshes visual objects from the current canonical snapshot without changing it.
        /// </summary>
        public void RefreshPresentation()
        {
            if (!isShowingEncounter || activeEncounter == null)
            {
                return;
            }

            BuildOrRefreshParticipants(false);
            RefreshRoomVisuals();
            RefreshMutableNodes();
            RefreshControlledParticipant();
            RecordObservedSnapshot();
            IncrementRevision();
        }

        public bool TryGetHunterPresentation(
            string hunterId,
            out EcosystemEncounterPresentation presentation)
        {
            presentation = default;
            if (!isShowingEncounter || string.IsNullOrEmpty(hunterId) ||
                !huntersBySourceId.TryGetValue(hunterId, out var participant) ||
                participant == null)
            {
                return false;
            }

            var vitals = participant.vitals;
            presentation = new EcosystemEncounterPresentation(
                vitals?.HealthRatio ?? 0f,
                vitals?.ManaRatio ?? 0f,
                vitals?.ShieldRatio ?? 0f,
                participant.lifeState != EncounterParticipantLifeState.Extracted);
            return true;
        }

        public bool TryGetHunterPose(string hunterId, out EcosystemSpatialPose pose)
        {
            pose = default;
            if (!isShowingEncounter || string.IsNullOrEmpty(hunterId) ||
                !huntersBySourceId.TryGetValue(hunterId, out var participant) ||
                participant == null)
            {
                return false;
            }

            var moving = participantVisuals.TryGetValue(participant.entityId, out var visual) &&
                         visual.IsMoving;
            pose = new EcosystemSpatialPose(
                participant.position,
                participant.facing,
                moving,
                string.IsNullOrEmpty(activeEncounter?.id)
                    ? "dungeon"
                    : $"dungeon:{activeEncounter.id}");
            return true;
        }

        private void CaptureAndEnterDungeonPresentation()
        {
            if (!presentationStateCaptured)
            {
                presentationStateCaptured = true;
                previousOverworldViewEnabled = overworldView != null && overworldView.enabled;
                previousMapCameraEnabled = mapCamera != null && mapCamera.enabled;
                previousStageActive = authoredDungeonStageRoot != null &&
                                      authoredDungeonStageRoot.gameObject.activeSelf;
                if (presentationCamera != null)
                {
                    previousCameraPosition = presentationCamera.transform.position;
                    previousOrthographicSize = presentationCamera.orthographicSize;
                }

                overworldChildSnapshots.Clear();
                if (authoredOverworldRoot != null)
                {
                    for (var index = 0; index < authoredOverworldRoot.childCount; index++)
                    {
                        var child = authoredOverworldRoot.GetChild(index);
                        if (child == null || child == authoredDungeonStageRoot)
                        {
                            continue;
                        }
                        overworldChildSnapshots.Add(new ChildActivationSnapshot
                        {
                            GameObject = child.gameObject,
                            WasActive = child.gameObject.activeSelf
                        });
                    }
                }
            }

            foreach (var snapshot in overworldChildSnapshots)
            {
                if (snapshot.GameObject != null)
                {
                    snapshot.GameObject.SetActive(false);
                }
            }
            if (authoredDungeonStageRoot != null)
            {
                authoredDungeonStageRoot.gameObject.SetActive(true);
            }
            if (overworldView != null)
            {
                overworldView.enabled = false;
            }
            if (mapCamera != null)
            {
                mapCamera.enabled = false;
            }
        }

        private void RestoreOverworldPresentation()
        {
            if (!presentationStateCaptured)
            {
                return;
            }

            foreach (var snapshot in overworldChildSnapshots)
            {
                if (snapshot.GameObject != null)
                {
                    snapshot.GameObject.SetActive(snapshot.WasActive);
                }
            }
            overworldChildSnapshots.Clear();

            if (authoredDungeonStageRoot != null)
            {
                authoredDungeonStageRoot.gameObject.SetActive(previousStageActive);
            }
            if (overworldView != null)
            {
                overworldView.enabled = previousOverworldViewEnabled;
            }
            if (mapCamera != null)
            {
                mapCamera.enabled = previousMapCameraEnabled;
            }
            if (presentationCamera != null)
            {
                presentationCamera.transform.position = previousCameraPosition;
                if (presentationCamera.orthographic)
                {
                    presentationCamera.orthographicSize = previousOrthographicSize;
                }
            }
            presentationStateCaptured = false;
        }

        private void EnsureRuntimeContentRoot()
        {
            if (runtimeContentRoot != null)
            {
                return;
            }

            var existing = authoredDungeonStageRoot.Find("Runtime Dungeon Projection");
            if (existing != null)
            {
                runtimeContentRoot = existing;
                return;
            }

            var root = new GameObject("Runtime Dungeon Projection");
            runtimeContentRoot = root.transform;
            runtimeContentRoot.SetParent(authoredDungeonStageRoot, false);
        }

        private void ClearRuntimeContent()
        {
            if (runtimeContentRoot != null)
            {
                for (var index = runtimeContentRoot.childCount - 1; index >= 0; index--)
                {
                    var child = runtimeContentRoot.GetChild(index);
                    if (child == null)
                    {
                        continue;
                    }
                    child.gameObject.SetActive(false);
                    DestroyPresentationObject(child.gameObject);
                }
            }

            roomVisuals.Clear();
            hazardVisuals.Clear();
            lootVisuals.Clear();
            resourceVisuals.Clear();
            participantVisuals.Clear();
            huntersBySourceId.Clear();
        }

        private void BuildStaticGeometry()
        {
            var areas = ResolveAreas();
            var connections = ResolveConnections();
            var roomsRoot = CreateContainer("Rooms");
            var corridorsRoot = CreateContainer("Connections");

            if (connections != null)
            {
                foreach (var connection in connections)
                {
                    if (connection == null)
                    {
                        continue;
                    }
                    var points = ResolveConnectionPoints(connection, areas);
                    for (var index = 1; index < points.Count; index++)
                    {
                        CreateSegment(
                            $"{connection.id} [{index:D2}]",
                            corridorsRoot,
                            points[index - 1],
                            points[index],
                            corridorWidth + roomBorderWidth,
                            new Color(0.025f, 0.03f, 0.04f, 1f),
                            CorridorSortingOrder - 1);
                        CreateSegment(
                            $"{connection.id} Floor [{index:D2}]",
                            corridorsRoot,
                            points[index - 1],
                            points[index],
                            corridorWidth,
                            connection.locked
                                ? new Color(0.42f, 0.08f, 0.08f, 1f)
                                : ResolveCorridorColor(activeGate.biome),
                            CorridorSortingOrder);
                    }
                }
            }

            if (areas == null)
            {
                return;
            }
            foreach (var area in areas)
            {
                if (area == null || string.IsNullOrEmpty(area.id))
                {
                    continue;
                }

                var root = CreateObject($"Room - {area.displayName}", roomsRoot);
                SetPlanarLocalPosition(root, area.center, 0f);
                var border = CreateSprite(
                    "Border",
                    root,
                    authoredSquareSprite,
                    new Color(0.02f, 0.025f, 0.03f, 1f),
                    Vector2.zero,
                    area.size + Vector2.one * roomBorderWidth,
                    RoomBorderSortingOrder);
                var floor = CreateSprite(
                    "Floor",
                    root,
                    authoredSquareSprite,
                    ResolveRoomColor(activeGate.biome, area.areaType, area.discovered, area.cleared),
                    Vector2.zero,
                    area.size,
                    RoomFloorSortingOrder);
                var label = CreateText(
                    "Room Label",
                    root,
                    AreaDisplayName(area),
                    new Vector2(0f, area.size.y * 0.5f - 0.65f),
                    0.095f,
                    new Color(0.88f, 0.91f, 0.95f, 0.9f),
                    RoomFloorSortingOrder + 2);
                roomVisuals[area.id] = new RoomVisual
                {
                    Root = root.gameObject,
                    Border = border,
                    Floor = floor,
                    Label = label
                };
                AddRoomStyleMarkers(root, area);
            }
        }

        private void BuildMutableNodeVisuals()
        {
            var nodesRoot = CreateContainer("Nodes and Hazards");
            if (activeEncounter.hazards != null)
            {
                foreach (var hazard in activeEncounter.hazards)
                {
                    if (hazard == null || string.IsNullOrEmpty(hazard.id))
                    {
                        continue;
                    }
                    var root = CreateObject($"Hazard - {hazard.hazardType}", nodesRoot);
                    SetPlanarLocalPosition(root, hazard.position, 0f);
                    var renderer = CreateSprite(
                        "Hazard Area",
                        root,
                        authoredCircleSprite,
                        ResolveHazardColor(hazard.hazardType),
                        Vector2.zero,
                        Vector2.one * Mathf.Max(0.2f, hazard.radius * 2f),
                        DressingSortingOrder);
                    var label = CreateText(
                        "Hazard Label",
                        root,
                        Nicify(hazard.hazardType.ToString()),
                        new Vector2(0f, -Mathf.Max(0.8f, hazard.radius + 0.25f)),
                        0.055f,
                        new Color(1f, 0.84f, 0.62f, 0.82f),
                        DressingSortingOrder + 1);
                    hazardVisuals[hazard.id] = new NodeVisual
                    {
                        Root = root.gameObject,
                        Renderer = renderer,
                        Label = label
                    };
                }
            }

            if (activeEncounter.lootNodes != null)
            {
                foreach (var loot in activeEncounter.lootNodes)
                {
                    if (loot == null || string.IsNullOrEmpty(loot.id))
                    {
                        continue;
                    }
                    var root = CreateObject("Loot Cache", nodesRoot);
                    SetPlanarLocalPosition(root, loot.position, 0f);
                    var renderer = CreateSprite(
                        "Chest",
                        root,
                        authoredSquareSprite,
                        new Color(0.96f, 0.67f, 0.08f, 1f),
                        Vector2.zero,
                        new Vector2(0.9f, 0.72f),
                        NodeSortingOrder);
                    renderer.transform.localRotation = LocalVisualRotation(45f);
                    var label = CreateText(
                        "Loot Label",
                        root,
                        "LOOT",
                        new Vector2(0f, -0.82f),
                        0.06f,
                        new Color(1f, 0.83f, 0.22f, 1f),
                        NodeSortingOrder + 1);
                    lootVisuals[loot.id] = new NodeVisual
                    {
                        Root = root.gameObject,
                        Renderer = renderer,
                        Label = label
                    };
                }
            }

            if (activeEncounter.resourceNodes == null)
            {
                return;
            }
            foreach (var resource in activeEncounter.resourceNodes)
            {
                if (resource == null || string.IsNullOrEmpty(resource.id))
                {
                    continue;
                }
                var root = CreateObject("Resource Node", nodesRoot);
                SetPlanarLocalPosition(root, resource.position, 0f);
                var renderer = CreateSprite(
                    "Mana Crystal",
                    root,
                    authoredCircleSprite,
                    new Color(0.16f, 0.76f, 1f, 1f),
                    Vector2.zero,
                    new Vector2(0.78f, 1.08f),
                    NodeSortingOrder);
                var label = CreateText(
                    "Resource Label",
                    root,
                    "MANA",
                    new Vector2(0f, -0.9f),
                    0.055f,
                    new Color(0.38f, 0.86f, 1f, 1f),
                    NodeSortingOrder + 1);
                resourceVisuals[resource.id] = new NodeVisual
                {
                    Root = root.gameObject,
                    Renderer = renderer,
                    Label = label
                };
            }
        }

        private void BuildOrRefreshParticipants(bool snapToCanonical)
        {
            if (activeEncounter?.participants == null)
            {
                return;
            }

            var participantRoot = runtimeContentRoot.Find("Participants") ??
                                  CreateContainer("Participants");
            huntersBySourceId.Clear();
            participantReleaseBuffer.Clear();
            foreach (var key in participantVisuals.Keys)
            {
                participantReleaseBuffer.Add(key);
            }

            foreach (var participant in activeEncounter.participants)
            {
                if (participant == null || string.IsNullOrEmpty(participant.entityId))
                {
                    continue;
                }

                if (participant.participantKind == EncounterParticipantKind.Hunter &&
                    !string.IsNullOrEmpty(participant.sourceHunterId))
                {
                    huntersBySourceId[participant.sourceHunterId] = participant;
                }

                participantReleaseBuffer.Remove(participant.entityId);
                var isNewVisual = !participantVisuals.TryGetValue(
                    participant.entityId,
                    out var visual);
                if (isNewVisual)
                {
                    visual = CreateParticipantVisual(participantRoot, participant);
                    participantVisuals[participant.entityId] = visual;
                }

                visual.State = participant;
                visual.PreviousCanonicalPlanar = visual.Initialized
                    ? visual.DesiredPlanar
                    : participant.position;
                visual.DesiredPlanar = participant.position;
                visual.IsMoving = visual.Initialized &&
                                  (visual.DesiredPlanar - visual.PreviousCanonicalPlanar)
                                  .sqrMagnitude > 0.0004f;
                if (participant.facing.sqrMagnitude > 0.0001f)
                {
                    visual.Facing = participant.facing.normalized;
                }
                if (snapToCanonical || isNewVisual || !visual.Initialized)
                {
                    visual.CurrentPlanar = participant.position;
                    SetPlanarLocalPosition(visual.Root.transform, visual.CurrentPlanar, 0f);
                }
                visual.Initialized = true;
                ApplyParticipantVisual(visual);
            }

            foreach (var entityId in participantReleaseBuffer)
            {
                if (!participantVisuals.TryGetValue(entityId, out var visual))
                {
                    continue;
                }
                participantVisuals.Remove(entityId);
                if (visual.Root != null)
                {
                    visual.Root.SetActive(false);
                    DestroyPresentationObject(visual.Root);
                }
            }
        }

        private ParticipantVisual CreateParticipantVisual(
            Transform parent,
            EncounterParticipantState participant)
        {
            var root = CreateObject($"Participant - {participant.displayName}", parent);
            SetPlanarLocalPosition(root, participant.position, 0f);
            var visualRoot = CreateObject("Visual", root);
            visualRoot.localRotation = PlaneRotation();
            var boss = IsBoss(participant);
            var size = ParticipantBodyDiameter * (boss ? 1.42f : 1f);
            var body = CreateSpriteLocal(
                "Body",
                visualRoot,
                authoredCircleSprite,
                ParticipantColor(participant, boss),
                Vector2.zero,
                Vector2.one * size,
                100);
            var facing = CreateSpriteLocal(
                "Facing",
                visualRoot,
                authoredCircleSprite,
                new Color(1f, 1f, 1f, 0.92f),
                new Vector2(0f, size * 0.39f),
                Vector2.one * (boss ? 0.2f : 0.15f),
                102);
            var healthBackground = CreateSpriteLocal(
                "Health Background",
                visualRoot,
                authoredSquareSprite,
                new Color(0.045f, 0.025f, 0.03f, 0.98f),
                new Vector2(0f, size * 0.72f),
                new Vector2(ParticipantBarWidth, 0.11f),
                103);
            var healthFill = CreateSpriteLocal(
                "Health Fill",
                visualRoot,
                authoredSquareSprite,
                new Color(0.91f, 0.055f, 0.045f, 1f),
                new Vector2(0f, size * 0.72f),
                new Vector2(ParticipantBarWidth, 0.075f),
                104);
            var shieldBackground = CreateSpriteLocal(
                "Shield Background",
                visualRoot,
                authoredSquareSprite,
                new Color(0.04f, 0.055f, 0.07f, 0.96f),
                new Vector2(0f, size * 0.72f - 0.16f),
                new Vector2(ParticipantBarWidth, 0.085f),
                103);
            var shieldFill = CreateSpriteLocal(
                "Shield Fill",
                visualRoot,
                authoredSquareSprite,
                new Color(0.91f, 0.96f, 1f, 1f),
                new Vector2(0f, size * 0.72f - 0.16f),
                new Vector2(ParticipantBarWidth, 0.055f),
                104);
            var label = CreateTextLocal(
                "Name",
                visualRoot,
                participant.displayName,
                new Vector2(0f, size * 0.72f + 0.34f),
                boss ? 0.075f : 0.06f,
                Color.white,
                106);
            return new ParticipantVisual
            {
                State = participant,
                Root = root.gameObject,
                VisualRoot = visualRoot,
                Body = body,
                FacingMarker = facing,
                HealthBackground = healthBackground,
                HealthFill = healthFill,
                ShieldBackground = shieldBackground,
                ShieldFill = shieldFill,
                Label = label,
                CurrentPlanar = participant.position,
                DesiredPlanar = participant.position,
                PreviousCanonicalPlanar = participant.position,
                Facing = participant.facing.sqrMagnitude > 0.0001f
                    ? participant.facing.normalized
                    : Vector2.up
            };
        }

        private void ApplyParticipantVisual(ParticipantVisual visual)
        {
            var participant = visual.State;
            var visible = participant.lifeState != EncounterParticipantLifeState.Extracted;
            visual.Root.SetActive(visible);
            if (!visible)
            {
                return;
            }

            var boss = IsBoss(participant);
            var bodyColor = ParticipantColor(participant, boss);
            if (participant.lifeState != EncounterParticipantLifeState.Active)
            {
                bodyColor = Color.Lerp(bodyColor, new Color(0.12f, 0.12f, 0.13f, 0.72f), 0.68f);
            }
            visual.Body.color = bodyColor;
            visual.FacingMarker.color = participant.lifeState == EncounterParticipantLifeState.Active
                ? new Color(1f, 1f, 1f, 0.92f)
                : new Color(0.5f, 0.5f, 0.5f, 0.35f);
            visual.FacingMarker.transform.localPosition = new Vector3(
                visual.Facing.x * (boss ? 0.5f : 0.36f),
                visual.Facing.y * (boss ? 0.5f : 0.36f),
                visual.FacingMarker.transform.localPosition.z);

            var vitals = participant.vitals;
            SetBarFill(visual.HealthFill, vitals?.HealthRatio ?? 0f, ParticipantBarWidth);
            SetBarFill(visual.ShieldFill, vitals?.ShieldRatio ?? 0f, ParticipantBarWidth);
            visual.ShieldBackground.enabled = vitals != null && vitals.maximumShield > 0;
            visual.ShieldFill.enabled = visual.ShieldBackground.enabled;
            visual.Label.text = ParticipantDisplayName(participant);
            ApplyParticipantSorting(visual);
        }

        private void AdvanceParticipantPresentation(float deltaTime)
        {
            var interpolation = participantInterpolationSharpness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-participantInterpolationSharpness * Mathf.Max(0f, deltaTime));
            foreach (var visual in participantVisuals.Values)
            {
                if (visual?.Root == null || !visual.Root.activeSelf)
                {
                    continue;
                }
                var previous = visual.CurrentPlanar;
                visual.CurrentPlanar = Vector2.Lerp(previous, visual.DesiredPlanar, interpolation);
                if ((visual.DesiredPlanar - visual.CurrentPlanar).sqrMagnitude < 0.000001f)
                {
                    visual.CurrentPlanar = visual.DesiredPlanar;
                }
                SetPlanarLocalPosition(visual.Root.transform, visual.CurrentPlanar, 0f);
                ApplyParticipantSorting(visual);
            }
        }

        private void ApplyParticipantSorting(ParticipantVisual visual)
        {
            var baseOrder = 1000 - Mathf.RoundToInt(visual.CurrentPlanar.y * 8f);
            visual.Body.sortingOrder = baseOrder;
            visual.FacingMarker.sortingOrder = baseOrder + 1;
            visual.HealthBackground.sortingOrder = baseOrder + 2;
            visual.ShieldBackground.sortingOrder = baseOrder + 2;
            visual.HealthFill.sortingOrder = baseOrder + 3;
            visual.ShieldFill.sortingOrder = baseOrder + 3;
            var renderer = visual.Label != null ? visual.Label.GetComponent<MeshRenderer>() : null;
            if (renderer != null)
            {
                renderer.sortingOrder = baseOrder + 4;
            }
        }

        private void RefreshRoomVisuals()
        {
            var areas = ResolveAreas();
            if (areas == null)
            {
                return;
            }
            foreach (var area in areas)
            {
                if (area == null || !roomVisuals.TryGetValue(area.id, out var visual))
                {
                    continue;
                }
                visual.Root.SetActive(showUndiscoveredRoomsForPrototype || area.discovered);
                visual.Floor.color = ResolveRoomColor(
                    activeGate.biome,
                    area.areaType,
                    area.discovered,
                    area.cleared);
                visual.Border.color = area.cleared
                    ? new Color(0.1f, 0.34f, 0.21f, 1f)
                    : new Color(0.02f, 0.025f, 0.03f, 1f);
                visual.Label.text = AreaDisplayName(area);
                visual.Label.color = area.discovered
                    ? new Color(0.88f, 0.91f, 0.95f, 0.9f)
                    : new Color(0.56f, 0.59f, 0.63f, 0.55f);
            }
        }

        private void RefreshMutableNodes()
        {
            if (activeEncounter.hazards != null)
            {
                foreach (var hazard in activeEncounter.hazards)
                {
                    if (hazard == null || !hazardVisuals.TryGetValue(hazard.id, out var visual))
                    {
                        continue;
                    }
                    visual.Root.SetActive(hazard.active);
                    visual.Renderer.color = ResolveHazardColor(hazard.hazardType);
                }
            }
            if (activeEncounter.lootNodes != null)
            {
                foreach (var loot in activeEncounter.lootNodes)
                {
                    if (loot == null || !lootVisuals.TryGetValue(loot.id, out var visual))
                    {
                        continue;
                    }
                    var visible = loot.status == DungeonLootStatus.Available;
                    visual.Root.SetActive(visible);
                    visual.Label.text = loot.status == DungeonLootStatus.Claimed ? "CLAIMED" : "LOOT";
                }
            }
            if (activeEncounter.resourceNodes == null)
            {
                return;
            }
            foreach (var resource in activeEncounter.resourceNodes)
            {
                if (resource == null || !resourceVisuals.TryGetValue(resource.id, out var visual))
                {
                    continue;
                }
                var remainingRatio = resource.initialAmount > 0
                    ? Mathf.Clamp01((float)resource.remainingAmount / resource.initialAmount)
                    : 0f;
                visual.Root.SetActive(resource.remainingAmount > 0);
                visual.Renderer.transform.localScale = new Vector3(
                    Mathf.Lerp(0.36f, 0.78f, remainingRatio),
                    Mathf.Lerp(0.5f, 1.08f, remainingRatio),
                    1f);
                visual.Label.text = $"MANA {Mathf.Max(0, resource.remainingAmount)}";
            }
        }

        private void RefreshControlledParticipant()
        {
            controlledParticipant = null;
            if (activeEncounter?.participants == null)
            {
                return;
            }
            foreach (var participant in activeEncounter.participants)
            {
                if (participant == null)
                {
                    continue;
                }
                if (string.Equals(participant.sourceHunterId, controlledHunterId,
                        StringComparison.Ordinal) ||
                    string.Equals(participant.entityId, controlledHunterId,
                        StringComparison.Ordinal))
                {
                    controlledParticipant = participant;
                    return;
                }
            }
        }

        private void AddRoomStyleMarkers(Transform roomRoot, DungeonAreaState area)
        {
            var markerColor = ResolveStyleMarkerColor(activeGate.biome);
            var half = area.size * 0.5f;
            switch (activeGate.biome)
            {
                case DungeonBiomeType.FungalNest:
                    CreateStyleMarker(roomRoot, new Vector2(-half.x + 1f, -half.y + 1f), 0.62f, markerColor);
                    CreateStyleMarker(roomRoot, new Vector2(half.x - 1.2f, half.y - 1.1f), 0.8f, markerColor);
                    break;
                case DungeonBiomeType.FrostWarrens:
                    CreateStyleMarker(roomRoot, new Vector2(-half.x + 0.9f, half.y - 0.9f), 0.52f, markerColor);
                    CreateStyleMarker(roomRoot, new Vector2(half.x - 0.9f, -half.y + 0.9f), 0.52f, markerColor);
                    break;
                case DungeonBiomeType.RuinedTemple:
                    CreatePillar(roomRoot, new Vector2(-half.x + 0.7f, 0f), markerColor);
                    CreatePillar(roomRoot, new Vector2(half.x - 0.7f, 0f), markerColor);
                    break;
                default:
                    CreateStyleMarker(roomRoot, new Vector2(-half.x + 0.8f, half.y - 0.8f), 0.45f, markerColor);
                    CreateStyleMarker(roomRoot, new Vector2(half.x - 0.8f, -half.y + 0.8f), 0.45f, markerColor);
                    break;
            }
        }

        private void CreateStyleMarker(Transform parent, Vector2 position, float size, Color color)
        {
            CreateSprite(
                "Biome Dressing",
                parent,
                authoredCircleSprite,
                color,
                position,
                Vector2.one * size,
                DressingSortingOrder);
        }

        private void CreatePillar(Transform parent, Vector2 position, Color color)
        {
            CreateSprite(
                "Ruined Pillar",
                parent,
                authoredSquareSprite,
                color,
                position,
                new Vector2(0.48f, 1.6f),
                DressingSortingOrder);
        }

        private void CalculateDungeonBounds()
        {
            var areas = ResolveAreas();
            if (areas == null || areas.Count == 0)
            {
                dungeonBounds = new Rect(-10f, -10f, 20f, 20f);
                return;
            }

            var initialized = false;
            var minimum = Vector2.zero;
            var maximum = Vector2.zero;
            foreach (var area in areas)
            {
                if (area == null)
                {
                    continue;
                }
                var half = area.size * 0.5f;
                var areaMinimum = area.center - half;
                var areaMaximum = area.center + half;
                if (!initialized)
                {
                    initialized = true;
                    minimum = areaMinimum;
                    maximum = areaMaximum;
                }
                else
                {
                    minimum = Vector2.Min(minimum, areaMinimum);
                    maximum = Vector2.Max(maximum, areaMaximum);
                }
            }

            if (!initialized)
            {
                dungeonBounds = new Rect(-10f, -10f, 20f, 20f);
                return;
            }
            minimum -= Vector2.one * boundsPadding;
            maximum += Vector2.one * boundsPadding;
            dungeonBounds = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private void PositionCameraForEntry()
        {
            if (presentationCamera == null)
            {
                return;
            }
            if (presentationCamera.orthographic)
            {
                presentationCamera.orthographicSize = dungeonOrthographicSize;
            }
            var plane = ResolvePlane();
            var target = ClampCameraToDungeon(ResolveCameraTarget());
            presentationCamera.transform.position = EcosystemSpatialCoordinates.WithPlanar(
                presentationCamera.transform.position,
                target,
                plane);
        }

        private Vector2 ResolveCameraTarget()
        {
            if (controlledParticipant != null &&
                participantVisuals.TryGetValue(controlledParticipant.entityId, out var visual) &&
                visual.Root != null && visual.Root.activeSelf)
            {
                return visual.CurrentPlanar;
            }
            if (!string.IsNullOrEmpty(activeEncounter?.entranceAreaId))
            {
                var entrance = ResolveAreas()?.Find(area =>
                    area != null && area.id == activeEncounter.entranceAreaId);
                if (entrance != null)
                {
                    return entrance.center;
                }
            }
            return dungeonBounds.center;
        }

        private Vector2 ClampCameraToDungeon(Vector2 planarPosition)
        {
            if (presentationCamera == null || !presentationCamera.orthographic)
            {
                return planarPosition;
            }
            var halfHeight = presentationCamera.orthographicSize;
            var halfWidth = halfHeight * presentationCamera.aspect;
            var minimumX = dungeonBounds.xMin + halfWidth;
            var maximumX = dungeonBounds.xMax - halfWidth;
            var minimumY = dungeonBounds.yMin + halfHeight;
            var maximumY = dungeonBounds.yMax - halfHeight;
            planarPosition.x = minimumX > maximumX
                ? dungeonBounds.center.x
                : Mathf.Clamp(planarPosition.x, minimumX, maximumX);
            planarPosition.y = minimumY > maximumY
                ? dungeonBounds.center.y
                : Mathf.Clamp(planarPosition.y, minimumY, maximumY);
            return planarPosition;
        }

        private bool CanonicalSnapshotChanged()
        {
            return activeEncounter.fixedTick != observedFixedTick ||
                   activeEncounter.eventSequence != observedEventSequence ||
                   activeEncounter.status != observedStatus ||
                   (activeEncounter.participants?.Count ?? 0) != observedParticipantCount;
        }

        private void RecordObservedSnapshot()
        {
            observedFixedTick = activeEncounter?.fixedTick ?? long.MinValue;
            observedEventSequence = activeEncounter?.eventSequence ?? long.MinValue;
            observedStatus = activeEncounter?.status ?? DungeonEncounterStatus.Paused;
            observedParticipantCount = activeEncounter?.participants?.Count ?? -1;
        }

        private void IncrementRevision()
        {
            presentationRevision = presentationRevision == long.MaxValue
                ? 1
                : presentationRevision + 1;
        }

        private List<DungeonAreaState> ResolveAreas()
        {
            return activeEncounter?.areas != null && activeEncounter.areas.Count > 0
                ? activeEncounter.areas
                : activeGate?.areas;
        }

        private List<DungeonConnectionState> ResolveConnections()
        {
            return activeEncounter?.connections != null && activeEncounter.connections.Count > 0
                ? activeEncounter.connections
                : activeGate?.connections;
        }

        private static List<Vector2> ResolveConnectionPoints(
            DungeonConnectionState connection,
            List<DungeonAreaState> areas)
        {
            if (connection.waypoints != null && connection.waypoints.Count >= 2)
            {
                return connection.waypoints;
            }
            var result = new List<Vector2>(2);
            if (areas == null)
            {
                return result;
            }
            var from = areas.Find(area => area != null && area.id == connection.fromAreaId);
            var to = areas.Find(area => area != null && area.id == connection.toAreaId);
            if (from != null && to != null)
            {
                result.Add(from.center);
                result.Add(to.center);
            }
            return result;
        }

        private Transform CreateContainer(string name)
        {
            return CreateObject(name, runtimeContentRoot);
        }

        private static Transform CreateObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name);
            var result = gameObject.transform;
            result.SetParent(parent, false);
            return result;
        }

        private SpriteRenderer CreateSprite(
            string name,
            Transform parent,
            Sprite sprite,
            Color color,
            Vector2 planarLocalPosition,
            Vector2 localSize,
            int sortingOrder)
        {
            var root = CreateObject(name, parent);
            SetPlanarLocalPosition(root, planarLocalPosition, 0f);
            root.localRotation = PlaneRotation();
            root.localScale = new Vector3(localSize.x, localSize.y, 1f);
            var renderer = root.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static SpriteRenderer CreateSpriteLocal(
            string name,
            Transform parent,
            Sprite sprite,
            Color color,
            Vector2 localPosition,
            Vector2 localSize,
            int sortingOrder)
        {
            var root = CreateObject(name, parent);
            root.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            root.localScale = new Vector3(localSize.x, localSize.y, 1f);
            var renderer = root.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void CreateSegment(
            string name,
            Transform parent,
            Vector2 from,
            Vector2 to,
            float width,
            Color color,
            int sortingOrder)
        {
            var delta = to - from;
            var length = delta.magnitude;
            if (length <= 0.001f)
            {
                return;
            }
            var renderer = CreateSprite(
                name,
                parent,
                authoredSquareSprite,
                color,
                (from + to) * 0.5f,
                new Vector2(length, width),
                sortingOrder);
            renderer.transform.localRotation = LocalVisualRotation(
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private TextMesh CreateText(
            string name,
            Transform parent,
            string value,
            Vector2 planarLocalPosition,
            float characterSize,
            Color color,
            int sortingOrder)
        {
            var root = CreateObject(name, parent);
            SetPlanarLocalPosition(root, planarLocalPosition, 0f);
            root.localRotation = PlaneRotation();
            return ConfigureText(root, value, characterSize, color, sortingOrder);
        }

        private static TextMesh CreateTextLocal(
            string name,
            Transform parent,
            string value,
            Vector2 localPosition,
            float characterSize,
            Color color,
            int sortingOrder)
        {
            var root = CreateObject(name, parent);
            root.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            return ConfigureText(root, value, characterSize, color, sortingOrder);
        }

        private static TextMesh ConfigureText(
            Transform root,
            string value,
            float characterSize,
            Color color,
            int sortingOrder)
        {
            var text = root.gameObject.AddComponent<TextMesh>();
            text.text = value ?? string.Empty;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 32;
            text.characterSize = characterSize;
            text.color = color;
            var renderer = text.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = sortingOrder;
            }
            return text;
        }

        private void SetPlanarLocalPosition(
            Transform target,
            Vector2 planarPosition,
            float perpendicularPosition)
        {
            target.localPosition = EcosystemSpatialCoordinates.ToWorld(
                planarPosition,
                ResolvePlane(),
                perpendicularPosition);
        }

        private Quaternion PlaneRotation()
        {
            return ResolvePlane() == EcosystemSpatialPlane.XY
                ? Quaternion.identity
                : Quaternion.Euler(90f, 0f, 0f);
        }

        private Quaternion LocalVisualRotation(float angleDegrees)
        {
            return ResolvePlane() == EcosystemSpatialPlane.XY
                ? Quaternion.Euler(0f, 0f, angleDegrees)
                : Quaternion.Euler(90f, -angleDegrees, 0f);
        }

        private EcosystemSpatialPlane ResolvePlane()
        {
            return authoredSpatialMap != null
                ? authoredSpatialMap.SpatialPlane
                : EcosystemSpatialPlane.XY;
        }

        private static void SetBarFill(SpriteRenderer renderer, float ratio, float width)
        {
            ratio = Mathf.Clamp01(ratio);
            var scale = renderer.transform.localScale;
            scale.x = width * ratio;
            renderer.transform.localScale = scale;
            var position = renderer.transform.localPosition;
            position.x = -width * (1f - ratio) * 0.5f;
            renderer.transform.localPosition = position;
        }

        private static bool IsBoss(EncounterParticipantState participant)
        {
            return participant.participantKind == EncounterParticipantKind.Monster &&
                   ((!string.IsNullOrEmpty(participant.definitionId) &&
                     participant.definitionId.IndexOf(
                         "boss",
                         StringComparison.OrdinalIgnoreCase) >= 0) ||
                    participant.combatPower >= 60f);
        }

        private static string ParticipantDisplayName(EncounterParticipantState participant)
        {
            var name = string.IsNullOrWhiteSpace(participant.displayName)
                ? participant.entityId
                : participant.displayName;
            return participant.lifeState switch
            {
                EncounterParticipantLifeState.Incapacitated => $"{name}  [DOWN]",
                EncounterParticipantLifeState.Defeated => $"{name}  [DEFEATED]",
                EncounterParticipantLifeState.Extracted => $"{name}  [EXTRACTED]",
                _ => name
            };
        }

        private static Color ParticipantColor(EncounterParticipantState participant, bool boss)
        {
            if (participant.participantKind == EncounterParticipantKind.Hunter)
            {
                return new Color(0.12f, 0.58f, 0.92f, 1f);
            }
            return boss
                ? new Color(0.92f, 0.18f, 0.12f, 1f)
                : new Color(0.78f, 0.24f, 0.14f, 1f);
        }

        private static Color ResolveRoomColor(
            DungeonBiomeType biome,
            DungeonAreaType areaType,
            bool discovered,
            bool cleared)
        {
            var color = biome switch
            {
                DungeonBiomeType.AshCavern => new Color(0.29f, 0.16f, 0.11f, 1f),
                DungeonBiomeType.DrownedCrypt => new Color(0.08f, 0.23f, 0.29f, 1f),
                DungeonBiomeType.VoidSpire => new Color(0.2f, 0.09f, 0.31f, 1f),
                DungeonBiomeType.FrostWarrens => new Color(0.22f, 0.42f, 0.52f, 1f),
                DungeonBiomeType.RuinedTemple => new Color(0.34f, 0.28f, 0.16f, 1f),
                DungeonBiomeType.FungalNest => new Color(0.2f, 0.3f, 0.14f, 1f),
                _ => new Color(0.22f, 0.23f, 0.25f, 1f)
            };
            color = areaType switch
            {
                DungeonAreaType.Entrance => Color.Lerp(color, new Color(0.18f, 0.5f, 0.58f), 0.35f),
                DungeonAreaType.Treasure => Color.Lerp(color, new Color(0.7f, 0.46f, 0.08f), 0.32f),
                DungeonAreaType.Resource => Color.Lerp(color, new Color(0.08f, 0.5f, 0.72f), 0.28f),
                DungeonAreaType.Boss => Color.Lerp(color, new Color(0.55f, 0.055f, 0.045f), 0.48f),
                DungeonAreaType.Exit => Color.Lerp(color, new Color(0.18f, 0.52f, 0.25f), 0.35f),
                _ => color
            };
            if (!discovered)
            {
                color = Color.Lerp(color, new Color(0.035f, 0.04f, 0.055f, 1f), 0.72f);
            }
            if (cleared)
            {
                color = Color.Lerp(color, new Color(0.12f, 0.34f, 0.2f, 1f), 0.18f);
            }
            return color;
        }

        private static Color ResolveCorridorColor(DungeonBiomeType biome)
        {
            var room = ResolveRoomColor(biome, DungeonAreaType.Combat, true, false);
            return Color.Lerp(room, new Color(0.035f, 0.04f, 0.05f, 1f), 0.42f);
        }

        private static Color ResolveStyleMarkerColor(DungeonBiomeType biome)
        {
            return biome switch
            {
                DungeonBiomeType.AshCavern => new Color(0.8f, 0.23f, 0.06f, 0.62f),
                DungeonBiomeType.DrownedCrypt => new Color(0.08f, 0.68f, 0.76f, 0.5f),
                DungeonBiomeType.VoidSpire => new Color(0.72f, 0.2f, 1f, 0.6f),
                DungeonBiomeType.FrostWarrens => new Color(0.67f, 0.92f, 1f, 0.68f),
                DungeonBiomeType.RuinedTemple => new Color(0.7f, 0.58f, 0.32f, 0.58f),
                DungeonBiomeType.FungalNest => new Color(0.55f, 0.88f, 0.18f, 0.58f),
                _ => new Color(0.58f, 0.62f, 0.7f, 0.5f)
            };
        }

        private static Color ResolveHazardColor(DungeonHazardType hazard)
        {
            return hazard switch
            {
                DungeonHazardType.LavaVent => new Color(1f, 0.2f, 0.015f, 0.62f),
                DungeonHazardType.FloodedGround => new Color(0.04f, 0.52f, 0.86f, 0.46f),
                DungeonHazardType.VoidRift => new Color(0.62f, 0.08f, 0.9f, 0.62f),
                DungeonHazardType.FrostPatch => new Color(0.66f, 0.92f, 1f, 0.56f),
                DungeonHazardType.FallingDebris => new Color(0.58f, 0.38f, 0.16f, 0.52f),
                DungeonHazardType.PoisonPool => new Color(0.38f, 0.8f, 0.08f, 0.52f),
                _ => new Color(0.62f, 0.62f, 0.66f, 0.35f)
            };
        }

        private static string AreaDisplayName(DungeonAreaState area)
        {
            if (!area.discovered)
            {
                return "?  Unknown Area";
            }
            var name = string.IsNullOrWhiteSpace(area.displayName)
                ? Nicify(area.areaType.ToString())
                : area.displayName;
            return area.cleared ? $"{name}  [CLEARED]" : name;
        }

        private static string Nicify(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            var result = value[0].ToString();
            for (var index = 1; index < value.Length; index++)
            {
                if (char.IsUpper(value[index]) && !char.IsWhiteSpace(value[index - 1]))
                {
                    result += " ";
                }
                result += value[index];
            }
            return result;
        }

        private static void DestroyPresentationObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void RaiseLeaveRequestFromKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                LeaveViewRequested?.Invoke();
            }
        }

        private void OnGUI()
        {
            if (!isShowingEncounter || activeGate == null || activeEncounter == null)
            {
                return;
            }
            EnsureGuiStyles();
            var canRetreat = activeEncounter.status is DungeonEncounterStatus.Active or
                DungeonEncounterStatus.Preparing or DungeonEncounterStatus.Paused;
            var leaveRequested = false;
            var retreatRequested = false;
            var width = Mathf.Min(440f, Mathf.Max(280f, Screen.width - 24f));
            var panelRect = new Rect(12f, 12f, width, 196f);
            GUILayout.BeginArea(panelRect, GUIContent.none, panelStyle);
            GUILayout.Label(activeGate.displayName ?? "Gate Encounter", titleStyle);
            GUILayout.Label(
                $"{Nicify(activeGate.biome.ToString())}  //  {Nicify(activeGate.layoutStyle.ToString())}  //  " +
                $"Rank {DifficultyLabel(activeGate.appraisedDifficulty)}",
                mutedStyle);
            GUILayout.Label(
                $"Status: {Nicify(activeEncounter.status.ToString())}     " +
                $"Enemies: {CountAliveEnemies()}     Tick: {activeEncounter.fixedTick}");
            GUILayout.Space(3f);
            GUILayout.Label("WASD move  //  Mouse aim  //  LMB attack  //  E interact  //  Esc leave", mutedStyle);
            GUILayout.Space(3f);
            GUILayout.Label($"Latest: {LatestEventSummary()}", mutedStyle);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Leave View", GUILayout.Height(27f)))
            {
                leaveRequested = true;
            }
            var previousEnabled = GUI.enabled;
            GUI.enabled = canRetreat;
            if (GUILayout.Button("Request Retreat", GUILayout.Height(27f)))
            {
                retreatRequested = true;
            }
            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Invoke after the layout is complete: a listener may synchronously hide the
            // view and clear ActiveEncounter.
            if (leaveRequested)
            {
                LeaveViewRequested?.Invoke();
            }
            else if (retreatRequested)
            {
                RetreatRequested?.Invoke();
            }
        }

        private void EnsureGuiStyles()
        {
            if (panelStyle != null)
            {
                return;
            }
            panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(12, 12, 10, 10),
                alignment = TextAnchor.UpperLeft
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            mutedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = new Color(0.75f, 0.79f, 0.84f, 1f) }
            };
        }

        private int CountAliveEnemies()
        {
            if (activeEncounter.participants == null)
            {
                return 0;
            }
            var count = 0;
            foreach (var participant in activeEncounter.participants)
            {
                if (participant != null &&
                    participant.participantKind == EncounterParticipantKind.Monster &&
                    participant.CanFight)
                {
                    count++;
                }
            }
            return count;
        }

        private string LatestEventSummary()
        {
            if (activeEncounter.recentEvents == null)
            {
                return "The party crosses the gate threshold.";
            }
            for (var index = activeEncounter.recentEvents.Count - 1; index >= 0; index--)
            {
                var encounterEvent = activeEncounter.recentEvents[index];
                if (encounterEvent != null && !string.IsNullOrWhiteSpace(encounterEvent.summary))
                {
                    return encounterEvent.summary.Length > 96
                        ? encounterEvent.summary.Substring(0, 93) + "..."
                        : encounterEvent.summary;
                }
            }
            return "The party crosses the gate threshold.";
        }

        private static string DifficultyLabel(int difficulty)
        {
            return Mathf.Clamp(difficulty, 1, 6) switch
            {
                1 => "E",
                2 => "D",
                3 => "C",
                4 => "B",
                5 => "A",
                _ => "S"
            };
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            Transform worldRoot,
            Transform dungeonStage,
            EcosystemSpatialAuthoring spatialMap,
            EcosystemSpatialWorldView spatialWorldView,
            EcosystemMapCameraController cameraController,
            Camera cameraReference,
            Sprite circleSprite,
            Sprite squareSprite)
        {
            authoredOverworldRoot = worldRoot;
            authoredDungeonStageRoot = dungeonStage;
            authoredSpatialMap = spatialMap;
            overworldView = spatialWorldView;
            mapCamera = cameraController;
            presentationCamera = cameraReference;
            authoredCircleSprite = circleSprite;
            authoredSquareSprite = squareSprite;
        }
#endif
    }
}
