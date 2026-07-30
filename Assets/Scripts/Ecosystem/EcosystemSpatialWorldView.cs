using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Turtle.Ecosystem
{
    /// <summary>
    /// Central projection and locomotion owner for the visible ecosystem. It diffs persistent
    /// hunter IDs into reusable pawn slots, applies read-only visuals, performs crowd spacing,
    /// and handles world selection. Domain state and deterministic outcomes remain elsewhere.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EcosystemSpatialWorldView : MonoBehaviour
    {
        private sealed class PawnMotion
        {
            public string HunterId;
            public EcosystemHunterPawn2D Pawn;
            public Vector2 CurrentPlanar;
            public Vector2 DesiredPlanar;
            public Vector2 Facing = Vector2.up;
            public bool Initialized;
            public bool Visible = true;
            public bool HasCanonicalPose;
            public bool FallbackTravelling;
            public string JourneyOriginId = string.Empty;
            public string JourneyDestinationId = string.Empty;
            public Vector2 JourneyStart;
            public Vector2 JourneyEnd;
            public float JourneyElapsed;
            public float JourneyDuration = 1f;
            public EcosystemRoutePathAuthoring JourneyRoute;
        }

        [Header("World references")]
        [SerializeField] private EcosystemWorldController host;
        [SerializeField] private EcosystemSpatialAuthoring spatialAuthoring;
        [SerializeField] private EcosystemMapCameraController mapCamera;
        [SerializeField] private EcosystemSpatialHud hud;
        [SerializeField] private EcosystemPlayerInput2D playerInput;
        [SerializeField] private Camera selectionCamera;
        [SerializeField] private Transform runtimePawnRoot;
        [SerializeField] private EcosystemHunterPawn2D pawnTemplate;
        [SerializeField] private EcosystemHunterPawn2D[] serializedPawnSlots =
            Array.Empty<EcosystemHunterPawn2D>();

        [Header("Spatial presentation")]
        [SerializeField, Min(0.1f)] private float fallbackMapScale = 3f;
        [SerializeField, Min(0.1f)] private float fallbackMoveSpeed = 3.6f;
        [SerializeField, Min(0.1f)] private float minimumTravelPresentationSeconds = 4f;
        [SerializeField, Min(0.2f)] private float minimumHunterSpacing = 1.05f;
        [SerializeField, Range(0f, 4f)] private float separationStrength = 1.35f;
        [SerializeField, Min(0f)] private float pawnPerpendicularPosition = -0.2f;
        [SerializeField, Min(0.02f)] private float stateRefreshInterval = 0.15f;
        [SerializeField] private string visibleSpatialLayerId = "overworld";

        [Header("Selection")]
        [SerializeField] private bool worldSelectionEnabled = true;
        [SerializeField] private LayerMask selectionLayers = ~0;

        private readonly Dictionary<string, PawnMotion> motionsByHunterId =
            new(StringComparer.Ordinal);
        private readonly List<PawnMotion> activeMotions = new(128);
        private readonly Stack<EcosystemHunterPawn2D> freePawnSlots = new(128);
        private readonly HashSet<EcosystemHunterPawn2D> registeredSlots = new();
        private readonly HashSet<string> refreshedHunterIds = new(StringComparer.Ordinal);
        private readonly List<string> releaseBuffer = new(32);
        private readonly Dictionary<string, int> occupancyOrdinals = new(StringComparer.Ordinal);
        private readonly Dictionary<int, EcosystemHunterPawn2D> pawnByColliderId = new();
        private readonly Dictionary<Vector2Int, int> spatialCellHeads = new();
        private readonly Collider2D[] selection2DResults = new Collider2D[24];
        private readonly RaycastHit[] selection3DResults = new RaycastHit[24];
        private int[] spatialNextIndices = Array.Empty<int>();

        private IEcosystemSpatialPoseSource spatialPoseSource;
        private IEcosystemEncounterPresentationSource encounterPresentationSource;
        private EcosystemWorldState lastStateReference;
        private int lastStateDay = int.MinValue;
        private long lastSimulationSequence = long.MinValue;
        private int lastHunterCount = -1;
        private long lastSpatialRevision = long.MinValue;
        private long lastEncounterRevision = long.MinValue;
        private float nextStateRefreshAt;
        private bool initialized;
        private string selectedHunterId = string.Empty;
        private string selectedLocationId = string.Empty;
        private EcosystemHunterPawn2D controlledPawn;

        public string SelectedHunterId => selectedHunterId;
        public string SelectedLocationId => selectedLocationId;
        public EcosystemHunterPawn2D ControlledPawn => controlledPawn;
        public Transform ControlledPawnTransform => controlledPawn != null ? controlledPawn.transform : null;
        public EcosystemPlayerInput2D PlayerInput => playerInput;

        public event Action<string, string> SelectionChanged;

        private void Awake()
        {
            if (host == null)
            {
                host = GetComponent<EcosystemWorldController>();
            }
            InitializePawnSlots();
        }

        private void Start()
        {
            if (host != null)
            {
                Initialize(host);
            }
        }

        private void Update()
        {
            if (!initialized && host != null && host.State != null)
            {
                Initialize(host);
            }
            if (!initialized)
            {
                return;
            }

            RefreshIfStateChanged();
            HandleWorldSelection();
            AdvanceCentralMotion(Time.unscaledDeltaTime);
            ApplyPlayerAimFacing();
        }

        public void Initialize(EcosystemWorldController worldHost)
        {
            Initialize(
                worldHost,
                spatialAuthoring,
                mapCamera,
                hud,
                playerInput,
                selectionCamera);
        }

        public void Initialize(
            EcosystemWorldController worldHost,
            EcosystemSpatialAuthoring authoredMap,
            EcosystemMapCameraController cameraController,
            EcosystemSpatialHud spatialHud,
            EcosystemPlayerInput2D inputAdapter,
            Camera cameraReference)
        {
            host = worldHost;
            spatialAuthoring = authoredMap;
            mapCamera = cameraController;
            hud = spatialHud;
            playerInput = inputAdapter;
            selectionCamera = cameraReference != null
                ? cameraReference
                : cameraController != null
                    ? cameraController.ControlledCamera
                    : null;
            runtimePawnRoot = runtimePawnRoot != null
                ? runtimePawnRoot
                : spatialAuthoring != null
                    ? spatialAuthoring.DynamicActorRoot
                    : transform;

            InitializePawnSlots();
            spatialAuthoring?.RebuildLookup();
            hud?.Initialize(host, this);
            if (mapCamera != null)
            {
                mapCamera.Initialize(selectionCamera, spatialAuthoring, hud);
            }
            playerInput?.Initialize(selectionCamera, spatialAuthoring, hud);
            initialized = host != null;
            RefreshWorld();
        }

        public void SetSpatialPoseSource(IEcosystemSpatialPoseSource source)
        {
            spatialPoseSource = source;
            lastSpatialRevision = long.MinValue;
            RefreshWorld();
        }

        public void SetEncounterPresentationSource(
            IEcosystemEncounterPresentationSource source)
        {
            encounterPresentationSource = source;
            lastEncounterRevision = long.MinValue;
            RefreshWorld();
        }

        public void RefreshWorld()
        {
            if (host?.State == null)
            {
                return;
            }
            Refresh(host.State, host.GearCatalog);
        }

        public void Refresh(
            EcosystemWorldState state,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog)
        {
            if (state == null)
            {
                return;
            }

            InitializePawnSlots();
            refreshedHunterIds.Clear();
            occupancyOrdinals.Clear();
            controlledPawn = null;

            if (state.hunters != null)
            {
                foreach (var hunter in state.hunters)
                {
                    if (hunter == null || !hunter.IsActive || string.IsNullOrEmpty(hunter.id))
                    {
                        continue;
                    }

                    refreshedHunterIds.Add(hunter.id);
                    var ordinal = NextOccupancyOrdinal(hunter.locationId);
                    var motion = GetOrCreateMotion(hunter, state, ordinal);
                    RefreshPawnVisual(motion, hunter, state, gearCatalog);
                    RefreshMotionTarget(motion, hunter, state, ordinal);
                    if (hunter.id == state.playerHunterId)
                    {
                        controlledPawn = motion.Visible ? motion.Pawn : null;
                    }
                }
            }

            releaseBuffer.Clear();
            foreach (var pair in motionsByHunterId)
            {
                if (!refreshedHunterIds.Contains(pair.Key))
                {
                    releaseBuffer.Add(pair.Key);
                }
            }
            foreach (var hunterId in releaseBuffer)
            {
                ReleaseMotion(hunterId);
            }

            if (!string.IsNullOrEmpty(selectedHunterId) &&
                !motionsByHunterId.ContainsKey(selectedHunterId))
            {
                ClearSelection();
            }

            if (mapCamera != null && controlledPawn != null)
            {
                mapCamera.SetFollowTarget(controlledPawn.transform);
            }

            lastStateReference = state;
            lastStateDay = state.day;
            lastSimulationSequence = state.simulationSequence;
            lastHunterCount = state.hunters?.Count ?? 0;
            lastSpatialRevision = spatialPoseSource?.SpatialRevision ?? long.MinValue;
            lastEncounterRevision = encounterPresentationSource?.EncounterRevision ?? long.MinValue;
            nextStateRefreshAt = Time.unscaledTime + stateRefreshInterval;
            hud?.Refresh();
        }

        public bool TryGetPawn(string hunterId, out EcosystemHunterPawn2D pawn)
        {
            pawn = null;
            if (string.IsNullOrEmpty(hunterId) ||
                !motionsByHunterId.TryGetValue(hunterId, out var motion))
            {
                return false;
            }
            pawn = motion.Pawn;
            return pawn != null;
        }

        public void SelectHunter(string hunterId)
        {
            if (selectedHunterId == hunterId && string.IsNullOrEmpty(selectedLocationId))
            {
                return;
            }

            if (motionsByHunterId.TryGetValue(selectedHunterId, out var previous))
            {
                previous.Pawn.SetSelected(false);
            }
            selectedHunterId = hunterId ?? string.Empty;
            selectedLocationId = string.Empty;
            if (motionsByHunterId.TryGetValue(selectedHunterId, out var selected))
            {
                selected.Pawn.SetSelected(true);
            }
            SelectionChanged?.Invoke(selectedHunterId, selectedLocationId);
        }

        public void SelectLocation(string locationId)
        {
            if (motionsByHunterId.TryGetValue(selectedHunterId, out var previous))
            {
                previous.Pawn.SetSelected(false);
            }
            selectedHunterId = string.Empty;
            selectedLocationId = locationId ?? string.Empty;
            SelectionChanged?.Invoke(selectedHunterId, selectedLocationId);
        }

        public void ClearSelection()
        {
            if (motionsByHunterId.TryGetValue(selectedHunterId, out var previous))
            {
                previous.Pawn.SetSelected(false);
            }
            selectedHunterId = string.Empty;
            selectedLocationId = string.Empty;
            SelectionChanged?.Invoke(selectedHunterId, selectedLocationId);
        }

        public void FocusControlledHunter(bool snap)
        {
            if (controlledPawn == null || mapCamera == null)
            {
                return;
            }
            mapCamera.SetFollowTarget(controlledPawn.transform, snap);
            mapCamera.ResumeFollow(snap);
        }

        public void FocusHunter(string hunterId, bool snap)
        {
            if (!TryGetPawn(hunterId, out var pawn) || mapCamera == null)
            {
                return;
            }
            SelectHunter(hunterId);
            mapCamera.CenterOn(pawn.transform, hunterId != host?.State?.playerHunterId);
            if (hunterId == host?.State?.playerHunterId)
            {
                mapCamera.SetFollowTarget(pawn.transform);
                mapCamera.ResumeFollow(snap);
            }
        }

        public void FocusLocation(string locationId, bool snap)
        {
            if (host?.State == null || mapCamera == null)
            {
                return;
            }
            var location = FindLocation(host.State, locationId);
            if (location == null)
            {
                return;
            }
            SelectLocation(locationId);
            mapCamera.CenterOnPlanar(ResolveLocationPlanar(host.State, locationId), true);
        }

        private void RefreshIfStateChanged()
        {
            var state = host?.State;
            if (state == null)
            {
                return;
            }

            var spatialRevision = spatialPoseSource?.SpatialRevision ?? long.MinValue;
            var encounterRevision = encounterPresentationSource?.EncounterRevision ?? long.MinValue;
            var hunterCount = state.hunters?.Count ?? 0;
            if (!ReferenceEquals(state, lastStateReference) ||
                state.day != lastStateDay ||
                state.simulationSequence != lastSimulationSequence ||
                hunterCount != lastHunterCount ||
                spatialRevision != lastSpatialRevision ||
                encounterRevision != lastEncounterRevision ||
                Time.unscaledTime >= nextStateRefreshAt)
            {
                Refresh(state, host.GearCatalog);
            }
        }

        private void InitializePawnSlots()
        {
            if (serializedPawnSlots == null)
            {
                serializedPawnSlots = Array.Empty<EcosystemHunterPawn2D>();
            }
            foreach (var pawn in serializedPawnSlots)
            {
                if (pawn == null || !registeredSlots.Add(pawn))
                {
                    continue;
                }
                if (runtimePawnRoot != null)
                {
                    pawn.transform.SetParent(runtimePawnRoot, true);
                }
                pawn.Release();
                freePawnSlots.Push(pawn);
            }
        }

        private PawnMotion GetOrCreateMotion(
            HunterProfile hunter,
            EcosystemWorldState state,
            int occupancyOrdinal)
        {
            if (motionsByHunterId.TryGetValue(hunter.id, out var existing))
            {
                return existing;
            }

            var pawn = AcquirePawn();
            var plane = spatialAuthoring != null
                ? spatialAuthoring.SpatialPlane
                : EcosystemSpatialPlane.XY;
            pawn.Bind(hunter.id, plane);
            pawnByColliderId[pawn.SelectionCollider.GetInstanceID()] = pawn;
            var startingPosition = ResolveRestPlanar(state, hunter, occupancyOrdinal);
            var motion = new PawnMotion
            {
                HunterId = hunter.id,
                Pawn = pawn,
                CurrentPlanar = startingPosition,
                DesiredPlanar = startingPosition,
                Initialized = true
            };
            pawn.SetPlanarPosition(startingPosition, pawnPerpendicularPosition);
            pawn.SetSortingOrder(100 + activeMotions.Count * 8);
            motionsByHunterId.Add(hunter.id, motion);
            activeMotions.Add(motion);
            return motion;
        }

        private EcosystemHunterPawn2D AcquirePawn()
        {
            EcosystemHunterPawn2D pawn;
            if (freePawnSlots.Count > 0)
            {
                pawn = freePawnSlots.Pop();
            }
            else if (pawnTemplate != null)
            {
                pawn = Instantiate(pawnTemplate, ResolvePawnRoot());
            }
            else
            {
                var pawnObject = new GameObject("Hunter Pawn Slot");
                pawnObject.transform.SetParent(ResolvePawnRoot(), false);
                pawn = pawnObject.AddComponent<EcosystemHunterPawn2D>();
            }
            registeredSlots.Add(pawn);
            return pawn;
        }

        private Transform ResolvePawnRoot()
        {
            if (runtimePawnRoot != null)
            {
                return runtimePawnRoot;
            }
            if (spatialAuthoring != null)
            {
                runtimePawnRoot = spatialAuthoring.DynamicActorRoot;
            }
            return runtimePawnRoot != null ? runtimePawnRoot : transform;
        }

        private void ReleaseMotion(string hunterId)
        {
            if (!motionsByHunterId.Remove(hunterId, out var motion))
            {
                return;
            }
            activeMotions.Remove(motion);
            if (motion.Pawn != null)
            {
                if (motion.Pawn.SelectionCollider != null)
                {
                    pawnByColliderId.Remove(motion.Pawn.SelectionCollider.GetInstanceID());
                }
                motion.Pawn.Release();
                freePawnSlots.Push(motion.Pawn);
            }
        }

        private void RefreshPawnVisual(
            PawnMotion motion,
            HunterProfile hunter,
            EcosystemWorldState state,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog)
        {
            var gear = FindGear(gearCatalog, hunter.equippedGearId);
            var build = EcosystemCareerRules.InferBuild(hunter, gearCatalog);
            var classColor = ResolveArchetypeColor(build.Primary);
            var bodyColor = gear != null
                ? Color.Lerp(classColor, gear.Accent, 0.38f)
                : classColor;
            bodyColor.a = 1f;

            var healthRatio = hunter.vitals?.HealthRatio ?? 0f;
            var manaRatio = hunter.vitals?.ManaRatio ?? 0f;
            var shieldRatio = hunter.vitals?.ShieldRatio ?? 0f;
            var visible = true;
            if (encounterPresentationSource != null &&
                encounterPresentationSource.TryGetHunterPresentation(
                    hunter.id,
                    out var encounterPresentation))
            {
                healthRatio = encounterPresentation.HealthRatio;
                manaRatio = encounterPresentation.ManaRatio;
                shieldRatio = encounterPresentation.ShieldRatio;
                visible = encounterPresentation.Visible;
            }

            motion.Pawn.ApplyVisual(new EcosystemHunterPawnVisual(
                ResolveGuildColor(hunter.guildId),
                bodyColor,
                ArchetypeGlyph(build.Primary),
                healthRatio,
                manaRatio,
                shieldRatio));
            motion.Visible = visible;
            motion.Pawn.gameObject.SetActive(visible);
            motion.Pawn.SetSelected(visible && hunter.id == selectedHunterId);
        }

        private void RefreshMotionTarget(
            PawnMotion motion,
            HunterProfile hunter,
            EcosystemWorldState state,
            int occupancyOrdinal)
        {
            if (spatialPoseSource != null &&
                spatialPoseSource.TryGetHunterPose(hunter.id, out var canonicalPose))
            {
                motion.HasCanonicalPose = true;
                motion.FallbackTravelling = false;
                motion.DesiredPlanar = canonicalPose.PlanarPosition;
                motion.Facing = canonicalPose.PlanarFacing;
                motion.Visible &= string.Equals(
                    canonicalPose.LayerId,
                    visibleSpatialLayerId,
                    StringComparison.Ordinal);
                motion.Pawn.gameObject.SetActive(motion.Visible);
                return;
            }

            motion.HasCanonicalPose = false;
            if (!string.IsNullOrEmpty(hunter.destinationId) && hunter.travelDaysRemaining > 0)
            {
                if (!motion.FallbackTravelling ||
                    motion.JourneyDestinationId != hunter.destinationId)
                {
                    motion.FallbackTravelling = true;
                    motion.JourneyOriginId = hunter.locationId;
                    motion.JourneyDestinationId = hunter.destinationId;
                    motion.JourneyStart = ResolveLocationPlanar(state, hunter.locationId);
                    motion.JourneyEnd = ResolveLocationPlanar(state, hunter.destinationId);
                    motion.JourneyElapsed = 0f;
                    var daySeconds = host != null ? host.AutomaticDayIntervalSeconds : 10f;
                    motion.JourneyDuration = Mathf.Max(
                        minimumTravelPresentationSeconds,
                        daySeconds * Mathf.Max(1, hunter.travelDaysRemaining));
                    motion.JourneyRoute = null;
                    spatialAuthoring?.TryGetRoute(
                        hunter.locationId,
                        hunter.destinationId,
                        out motion.JourneyRoute);
                    if (!motion.Initialized)
                    {
                        motion.CurrentPlanar = motion.JourneyStart;
                    }
                }
                return;
            }

            motion.FallbackTravelling = false;
            motion.JourneyDestinationId = string.Empty;
            motion.JourneyRoute = null;
            motion.DesiredPlanar = ResolveRestPlanar(state, hunter, occupancyOrdinal);
        }

        private void AdvanceCentralMotion(float deltaTime)
        {
            if (activeMotions.Count == 0)
            {
                return;
            }

            EnsureSpatialIndexCapacity(activeMotions.Count);
            spatialCellHeads.Clear();
            var cellSize = Mathf.Max(0.2f, minimumHunterSpacing);
            for (var index = 0; index < activeMotions.Count; index++)
            {
                spatialNextIndices[index] = -1;
                var motion = activeMotions[index];
                if (!motion.Visible || motion.Pawn == null)
                {
                    continue;
                }
                var cell = CellFor(motion.CurrentPlanar, cellSize);
                if (spatialCellHeads.TryGetValue(cell, out var previousHead))
                {
                    spatialNextIndices[index] = previousHead;
                }
                spatialCellHeads[cell] = index;
            }

            var fallbackClockRunning = host == null || host.AutomaticAdvanceEnabled;
            for (var index = 0; index < activeMotions.Count; index++)
            {
                var motion = activeMotions[index];
                if (!motion.Visible || motion.Pawn == null)
                {
                    continue;
                }

                if (motion.FallbackTravelling)
                {
                    if (fallbackClockRunning)
                    {
                        motion.JourneyElapsed = Mathf.Min(
                            motion.JourneyDuration * 0.985f,
                            motion.JourneyElapsed + Mathf.Max(0f, deltaTime));
                    }
                    var progress = motion.JourneyDuration <= 0f
                        ? 0f
                        : Mathf.Clamp01(motion.JourneyElapsed / motion.JourneyDuration);
                    motion.DesiredPlanar = motion.JourneyRoute != null
                        ? motion.JourneyRoute.EvaluatePlanar(
                            progress,
                            motion.JourneyOriginId,
                            motion.JourneyStart,
                            motion.JourneyEnd,
                            ResolvePlane())
                        : Vector2.Lerp(motion.JourneyStart, motion.JourneyEnd, progress);
                }

                var separation = motion.HasCanonicalPose
                    ? Vector2.zero
                    : CalculateSeparation(index, cellSize);
                var target = motion.DesiredPlanar + separation * separationStrength;
                var previous = motion.CurrentPlanar;
                if (motion.HasCanonicalPose)
                {
                    var interpolation = 1f - Mathf.Exp(-18f * Mathf.Max(0f, deltaTime));
                    motion.CurrentPlanar = Vector2.Lerp(previous, target, interpolation);
                }
                else
                {
                    motion.CurrentPlanar = Vector2.MoveTowards(
                        previous,
                        target,
                        fallbackMoveSpeed * Mathf.Max(0f, deltaTime));
                }

                var movement = motion.CurrentPlanar - previous;
                if (movement.sqrMagnitude > 0.000001f)
                {
                    motion.Facing = movement.normalized;
                }
                motion.Pawn.SetPlanarFacing(motion.Facing);
                motion.Pawn.SetPlanarPosition(
                    motion.CurrentPlanar,
                    pawnPerpendicularPosition);
            }
        }

        private Vector2 CalculateSeparation(int motionIndex, float cellSize)
        {
            var motion = activeMotions[motionIndex];
            var originCell = CellFor(motion.CurrentPlanar, cellSize);
            var separation = Vector2.zero;
            for (var y = -1; y <= 1; y++)
            {
                for (var x = -1; x <= 1; x++)
                {
                    if (!spatialCellHeads.TryGetValue(
                            new Vector2Int(originCell.x + x, originCell.y + y),
                            out var candidateIndex))
                    {
                        continue;
                    }

                    while (candidateIndex >= 0)
                    {
                        if (candidateIndex != motionIndex)
                        {
                            var candidate = activeMotions[candidateIndex];
                            var offset = motion.CurrentPlanar - candidate.CurrentPlanar;
                            var distance = offset.magnitude;
                            if (distance < minimumHunterSpacing)
                            {
                                if (distance <= 0.0001f)
                                {
                                    var hash = EcosystemDeterministicRandom.StableHash(
                                        $"{motion.HunterId}|spacing|{candidate.HunterId}");
                                    var angle = hash % 360u * Mathf.Deg2Rad;
                                    offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                                    distance = 1f;
                                }
                                separation += offset / distance *
                                              ((minimumHunterSpacing - distance) /
                                               minimumHunterSpacing);
                            }
                        }
                        candidateIndex = spatialNextIndices[candidateIndex];
                    }
                }
            }
            return Vector2.ClampMagnitude(separation, minimumHunterSpacing);
        }

        private void ApplyPlayerAimFacing()
        {
            if (controlledPawn == null || playerInput == null ||
                !motionsByHunterId.TryGetValue(controlledPawn.HunterId, out var motion))
            {
                return;
            }
            var aim = playerInput.CurrentIntent.AimPlanarPosition - motion.CurrentPlanar;
            if (aim.sqrMagnitude > 0.0001f)
            {
                motion.Facing = aim.normalized;
                controlledPawn.SetPlanarFacing(motion.Facing);
            }
        }

        private void HandleWorldSelection()
        {
            if (!worldSelectionEnabled || selectionCamera == null)
            {
                return;
            }
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            var screenPosition = mouse.position.ReadValue();
            if (hud != null && hud.IsPointerOverHud(screenPosition))
            {
                return;
            }

            if (ResolvePlane() == EcosystemSpatialPlane.XY)
            {
                Physics2D.SyncTransforms();
                var world = selectionCamera.ScreenToWorldPoint(new Vector3(
                    screenPosition.x,
                    screenPosition.y,
                    Mathf.Abs(selectionCamera.transform.position.z)));
                var contactFilter = new ContactFilter2D
                {
                    useTriggers = true
                };
                contactFilter.SetLayerMask(selectionLayers);
                var count = Physics2D.OverlapPoint(
                    new Vector2(world.x, world.y),
                    contactFilter,
                    selection2DResults);
                if (TrySelectFrom2DResults(count))
                {
                    return;
                }
            }
            else
            {
                var ray = selectionCamera.ScreenPointToRay(screenPosition);
                var count = Physics.RaycastNonAlloc(
                    ray,
                    selection3DResults,
                    float.PositiveInfinity,
                    selectionLayers.value,
                    QueryTriggerInteraction.Collide);
                if (TrySelectFrom3DResults(count))
                {
                    return;
                }
            }
            ClearSelection();
        }

        private bool TrySelectFrom2DResults(int count)
        {
            for (var index = 0; index < count; index++)
            {
                var collider = selection2DResults[index];
                if (collider != null &&
                    pawnByColliderId.TryGetValue(collider.GetInstanceID(), out var pawn))
                {
                    SelectHunter(pawn.HunterId);
                    return true;
                }
            }
            if (spatialAuthoring == null)
            {
                return false;
            }
            for (var index = 0; index < count; index++)
            {
                if (spatialAuthoring.TryGetLocationId(selection2DResults[index], out var locationId))
                {
                    SelectLocation(locationId);
                    return true;
                }
            }
            return false;
        }

        private bool TrySelectFrom3DResults(int count)
        {
            if (spatialAuthoring == null)
            {
                return false;
            }
            for (var index = 0; index < count; index++)
            {
                if (spatialAuthoring.TryGetLocationId(
                        selection3DResults[index].collider,
                        out var locationId))
                {
                    SelectLocation(locationId);
                    return true;
                }
            }
            return false;
        }

        private Vector2 ResolveRestPlanar(
            EcosystemWorldState state,
            HunterProfile hunter,
            int occupancyOrdinal)
        {
            if (spatialAuthoring != null &&
                spatialAuthoring.TryGetLocation(hunter.locationId, out var authoredLocation))
            {
                return authoredLocation.OccupancyPosition(
                    hunter.id,
                    occupancyOrdinal,
                    minimumHunterSpacing,
                    spatialAuthoring.SpatialPlane);
            }

            var center = ResolveLocationPlanar(state, hunter.locationId);
            var hash = EcosystemDeterministicRandom.StableHash(hunter.id);
            var angle = (hash % 360u) * Mathf.Deg2Rad + occupancyOrdinal * 2.39996323f;
            var radius = minimumHunterSpacing * (0.55f + Mathf.Sqrt(occupancyOrdinal + 1f) * 0.7f);
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private Vector2 ResolveLocationPlanar(EcosystemWorldState state, string locationId)
        {
            var location = FindLocation(state, locationId);
            if (spatialAuthoring != null)
            {
                return spatialAuthoring.ResolveLocationPlanarPosition(location, fallbackMapScale);
            }
            return location != null ? location.mapPosition * fallbackMapScale : Vector2.zero;
        }

        private int NextOccupancyOrdinal(string locationId)
        {
            locationId ??= string.Empty;
            occupancyOrdinals.TryGetValue(locationId, out var ordinal);
            occupancyOrdinals[locationId] = ordinal + 1;
            return ordinal;
        }

        private void EnsureSpatialIndexCapacity(int count)
        {
            if (spatialNextIndices.Length >= count)
            {
                return;
            }
            Array.Resize(ref spatialNextIndices, Mathf.NextPowerOfTwo(Mathf.Max(8, count)));
        }

        private static Vector2Int CellFor(Vector2 position, float cellSize)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / cellSize),
                Mathf.FloorToInt(position.y / cellSize));
        }

        private EcosystemSpatialPlane ResolvePlane()
        {
            return spatialAuthoring != null
                ? spatialAuthoring.SpatialPlane
                : EcosystemSpatialPlane.XY;
        }

        private static LocationState FindLocation(EcosystemWorldState state, string locationId)
        {
            if (state?.map?.locations == null || string.IsNullOrEmpty(locationId))
            {
                return null;
            }
            return state.map.locations.Find(location => location != null && location.id == locationId);
        }

        private static EcosystemGearDefinition FindGear(
            IReadOnlyList<EcosystemGearDefinition> gearCatalog,
            string gearId)
        {
            if (gearCatalog == null || string.IsNullOrEmpty(gearId))
            {
                return null;
            }
            for (var index = 0; index < gearCatalog.Count; index++)
            {
                var gear = gearCatalog[index];
                if (gear != null && gear.GearId == gearId)
                {
                    return gear;
                }
            }
            return null;
        }

        private static Color ResolveGuildColor(string guildId)
        {
            return guildId switch
            {
                "guild-azure" => new Color(0.08f, 0.52f, 1f, 1f),
                "guild-crimson" => new Color(0.92f, 0.08f, 0.12f, 1f),
                "guild-verdant" => new Color(0.18f, 0.78f, 0.36f, 1f),
                "guild-ivory" => new Color(0.92f, 0.94f, 1f, 1f),
                "guild-umbra" => new Color(0.62f, 0.2f, 0.84f, 1f),
                _ => new Color(0.62f, 0.64f, 0.68f, 1f)
            };
        }

        private static Color ResolveArchetypeColor(HunterArchetype archetype)
        {
            return archetype switch
            {
                HunterArchetype.Fighter => new Color(0.82f, 0.25f, 0.12f, 1f),
                HunterArchetype.Healer => new Color(0.22f, 0.82f, 0.5f, 1f),
                HunterArchetype.Assassin => new Color(0.52f, 0.18f, 0.72f, 1f),
                HunterArchetype.Ranger => new Color(0.18f, 0.64f, 0.28f, 1f),
                HunterArchetype.Tank => new Color(0.3f, 0.5f, 0.76f, 1f),
                HunterArchetype.Mage => new Color(0.18f, 0.42f, 0.92f, 1f),
                _ => new Color(0.56f, 0.56f, 0.6f, 1f)
            };
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

#if UNITY_EDITOR
        public void ConfigureEditor(
            EcosystemWorldController worldHost,
            EcosystemSpatialAuthoring authoredMap,
            EcosystemMapCameraController cameraController,
            EcosystemSpatialHud spatialHud,
            EcosystemPlayerInput2D inputAdapter,
            Camera cameraReference,
            Transform authoredRuntimePawnRoot,
            EcosystemHunterPawn2D authoredPawnTemplate,
            EcosystemHunterPawn2D[] authoredPawnSlots)
        {
            host = worldHost;
            spatialAuthoring = authoredMap;
            mapCamera = cameraController;
            hud = spatialHud;
            playerInput = inputAdapter;
            selectionCamera = cameraReference;
            runtimePawnRoot = authoredRuntimePawnRoot;
            pawnTemplate = authoredPawnTemplate;
            serializedPawnSlots = authoredPawnSlots ?? Array.Empty<EcosystemHunterPawn2D>();
        }
#endif
    }
}
