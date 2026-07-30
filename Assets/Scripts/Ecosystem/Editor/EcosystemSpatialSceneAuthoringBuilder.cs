using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Turtle.Ecosystem.Editor
{
    /// <summary>
    /// Authors the persistent, world-space part of the 2D ecosystem. The owned
    /// hierarchy is created only by an explicit editor command and is serialized in
    /// the scene; Play Mode never reconstructs terrain, roads, or landmarks.
    /// </summary>
    public static class EcosystemSpatialSceneAuthoringBuilder
    {
        public const string AuthoredWorldName = "Authored 2D Overworld";
        public const string DungeonStageName = "Materialized Dungeon Stage";
        public const int PawnSlotCount = 100;

        private const float MapScale = 4f;
        private const string GeneratedAssetFolder = "Assets/Data/Ecosystem/Spatial";
        private const string CircleSpritePath = GeneratedAssetFolder + "/Spatial Circle.png";
        private const string SquareSpritePath = GeneratedAssetFolder + "/Spatial Square.png";

        public readonly struct BuildResult
        {
            public BuildResult(
                EcosystemSpatialAuthoring authoring,
                Transform authoredWorldRoot,
                Transform dynamicActorRoot,
                Transform dungeonStageRoot,
                EcosystemHunterPawn2D[] pawnSlots,
                Sprite circleSprite,
                Sprite squareSprite)
            {
                Authoring = authoring;
                AuthoredWorldRoot = authoredWorldRoot;
                DynamicActorRoot = dynamicActorRoot;
                DungeonStageRoot = dungeonStageRoot;
                PawnSlots = pawnSlots ?? Array.Empty<EcosystemHunterPawn2D>();
                CircleSprite = circleSprite;
                SquareSprite = squareSprite;
            }

            public EcosystemSpatialAuthoring Authoring { get; }
            public Transform AuthoredWorldRoot { get; }
            public Transform DynamicActorRoot { get; }
            public Transform DungeonStageRoot { get; }
            public EcosystemHunterPawn2D[] PawnSlots { get; }
            public Sprite CircleSprite { get; }
            public Sprite SquareSprite { get; }
        }

        public static BuildResult Ensure(
            Scene scene,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog,
            bool rebuildOwnedWorld = false)
        {
            EnsureGeneratedAssetFolder();
            var circleSprite = EnsureSpriteAsset(CircleSpritePath, true);
            var squareSprite = EnsureSpriteAsset(SquareSpritePath, false);

            var existing = scene.GetRootGameObjects()
                .FirstOrDefault(item => item.name == AuthoredWorldName);
            if (existing != null && rebuildOwnedWorld)
            {
                Undo.DestroyObjectImmediate(existing);
                existing = null;
            }

            if (existing != null)
            {
                var collected = CollectExisting(existing.transform, circleSprite, squareSprite);
                var complete = collected.Authoring != null &&
                               collected.DynamicActorRoot != null &&
                               collected.DungeonStageRoot != null &&
                               collected.PawnSlots.Length >= PawnSlotCount;
                if (complete)
                {
                    return collected;
                }

                Debug.LogWarning(
                    "Replacing an incomplete ecosystem-owned world hierarchy left by a " +
                    "previous interrupted authoring pass.");
                Undo.DestroyObjectImmediate(existing);
                existing = null;
            }

            var worldState = EcosystemWorldFactory.CreateDefaultWorld(
                gearCatalog,
                EcosystemWorldFactory.DefaultWorldSeed);
            var worldRoot = CreateObject(AuthoredWorldName, null);
            SceneManager.MoveGameObjectToScene(worldRoot.gameObject, scene);
            var authoring = Undo.AddComponent<EcosystemSpatialAuthoring>(worldRoot.gameObject);

            var terrainRoot = CreateObject("Terrain and Biomes", worldRoot);
            var routeRoot = CreateObject("Travel Roads", worldRoot);
            var locationRoot = CreateObject("Towns, Facilities, Resources, and Gates", worldRoot);
            var dynamicRoot = CreateObject("Dynamic Hunter Pawn Pool", worldRoot);
            var dungeonStageRoot = CreateObject(DungeonStageName, worldRoot);
            dungeonStageRoot.gameObject.SetActive(false);

            BuildTerrain(terrainRoot, circleSprite, squareSprite);
            BuildWorldBounds(terrainRoot);

            var locationAuthoring = new List<EcosystemLocationAnchorAuthoring>();
            var locationTransforms = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var location in worldState.map.locations.OrderBy(item => item.id, StringComparer.Ordinal))
            {
                var entry = BuildLocation(
                    locationRoot,
                    location,
                    circleSprite,
                    squareSprite,
                    out var anchorTransform);
                locationAuthoring.Add(entry);
                locationTransforms.Add(location.id, anchorTransform);
            }

            var routeAuthoring = new List<EcosystemRoutePathAuthoring>();
            foreach (var route in worldState.map.routes
                         .OrderBy(item => item.fromLocationId, StringComparer.Ordinal)
                         .ThenBy(item => item.toLocationId, StringComparer.Ordinal))
            {
                if (!locationTransforms.TryGetValue(route.fromLocationId, out var from) ||
                    !locationTransforms.TryGetValue(route.toLocationId, out var to))
                {
                    continue;
                }
                routeAuthoring.Add(BuildRoute(routeRoot, route, from.position, to.position, squareSprite));
            }

            var bounds = new Rect(-48f, -34f, 96f, 82f);
            authoring.ConfigureEditor(
                EcosystemSpatialPlane.XY,
                bounds,
                dynamicRoot,
                locationAuthoring.ToArray(),
                routeAuthoring.ToArray());

            var pawnSlots = BuildPawnPool(
                dynamicRoot,
                authoring,
                worldState,
                gearCatalog,
                circleSprite,
                squareSprite);

            EditorUtility.SetDirty(authoring);
            return new BuildResult(
                authoring,
                worldRoot,
                dynamicRoot,
                dungeonStageRoot,
                pawnSlots,
                circleSprite,
                squareSprite);
        }

        private static BuildResult CollectExisting(
            Transform worldRoot,
            Sprite circleSprite,
            Sprite squareSprite)
        {
            var authoring = worldRoot.GetComponent<EcosystemSpatialAuthoring>();
            var dynamicRoot = worldRoot.Find("Dynamic Hunter Pawn Pool");
            var dungeonStageRoot = worldRoot.Find(DungeonStageName);
            var pawns = dynamicRoot == null
                ? Array.Empty<EcosystemHunterPawn2D>()
                : dynamicRoot.GetComponentsInChildren<EcosystemHunterPawn2D>(true);
            return new BuildResult(
                authoring,
                worldRoot,
                dynamicRoot,
                dungeonStageRoot,
                pawns,
                circleSprite,
                squareSprite);
        }

        private static void BuildTerrain(Transform parent, Sprite circle, Sprite square)
        {
            CreateSprite(
                "Regional Ground",
                parent,
                square,
                new Color(0.16f, 0.24f, 0.18f, 1f),
                Vector3.zero,
                new Vector2(100f, 86f),
                -120);

            CreateSprite(
                "Western Meadow",
                parent,
                circle,
                new Color(0.2f, 0.34f, 0.22f, 1f),
                new Vector3(-27f, 0f, 0f),
                new Vector2(39f, 49f),
                -115);
            CreateSprite(
                "North Frostland",
                parent,
                circle,
                new Color(0.31f, 0.42f, 0.43f, 1f),
                new Vector3(-1f, 29f, 0f),
                new Vector2(54f, 31f),
                -114);
            CreateSprite(
                "Eastern Badlands",
                parent,
                circle,
                new Color(0.38f, 0.25f, 0.17f, 1f),
                new Vector3(31f, 2f, 0f),
                new Vector2(43f, 53f),
                -113);

            var riverPoints = new[]
            {
                new Vector2(-45f, 30f),
                new Vector2(-25f, 20f),
                new Vector2(-8f, 17f),
                new Vector2(6f, 6f),
                new Vector2(20f, -9f),
                new Vector2(45f, -17f)
            };
            for (var index = 1; index < riverPoints.Length; index++)
            {
                CreateSegment(
                    $"River {index:00}",
                    parent,
                    square,
                    riverPoints[index - 1],
                    riverPoints[index],
                    2.4f,
                    new Color(0.08f, 0.32f, 0.51f, 1f),
                    -110);
            }

            var decorationRoot = CreateObject("Persistent Scenery", parent);
            for (var index = 0; index < 72; index++)
            {
                var hash = EcosystemDeterministicRandom.StableHash($"overworld-decoration-{index}");
                var x = -44f + hash % 880u / 10f;
                var y = -30f + (hash / 887u) % 740u / 10f;
                var isRock = hash % 5u == 0u;
                var size = 0.28f + hash % 8u * 0.045f;
                CreateSprite(
                    isRock ? $"Rock {index:00}" : $"Tree Canopy {index:00}",
                    decorationRoot,
                    circle,
                    isRock
                        ? new Color(0.32f, 0.35f, 0.36f, 1f)
                        : new Color(0.06f, 0.27f, 0.12f, 1f),
                    new Vector3(x, y, 0f),
                    new Vector2(size, size),
                    -95);
            }
        }

        private static void BuildWorldBounds(Transform parent)
        {
            var boundsRoot = CreateObject("World Bounds", parent);
            CreateBoundary(boundsRoot, "North Boundary", new Vector2(0f, 48f), new Vector2(102f, 1f));
            CreateBoundary(boundsRoot, "South Boundary", new Vector2(0f, -36f), new Vector2(102f, 1f));
            CreateBoundary(boundsRoot, "West Boundary", new Vector2(-50f, 6f), new Vector2(1f, 84f));
            CreateBoundary(boundsRoot, "East Boundary", new Vector2(50f, 6f), new Vector2(1f, 84f));
        }

        private static void CreateBoundary(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size)
        {
            var boundary = CreateObject(name, parent);
            boundary.position = new Vector3(position.x, position.y, 0f);
            var collider = Undo.AddComponent<BoxCollider2D>(boundary.gameObject);
            collider.size = size;
        }

        private static EcosystemLocationAnchorAuthoring BuildLocation(
            Transform parent,
            LocationState location,
            Sprite circle,
            Sprite square,
            out Transform anchor)
        {
            anchor = CreateObject($"{location.displayName} [{location.id}]", parent);
            anchor.position = new Vector3(
                location.mapPosition.x * MapScale,
                location.mapPosition.y * MapScale,
                0f);

            var radius = LocationRadius(location.locationType);
            var zoneColor = LocationColor(location.locationType);
            CreateSprite(
                "Location Footprint",
                anchor,
                circle,
                zoneColor,
                Vector3.zero,
                new Vector2(radius * 2f, radius * 2f),
                -42);
            DecorateLocation(anchor, location, circle, square, radius);

            var label = CreateObject("World Label", anchor);
            label.localPosition = new Vector3(0f, radius + 0.62f, 0f);
            var text = Undo.AddComponent<TextMesh>(label.gameObject);
            text.text = location.displayName;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.15f;
            text.fontSize = 40;
            text.color = new Color(0.94f, 0.96f, 1f, 1f);
            text.GetComponent<MeshRenderer>().sortingOrder = 30;

            var arrival = CreateObject("Arrival Point", anchor);
            arrival.localPosition = new Vector3(0f, -radius * 0.58f, 0f);
            var occupancy = new Transform[16];
            for (var index = 0; index < occupancy.Length; index++)
            {
                var point = CreateObject($"Occupancy {index + 1:00}", anchor);
                var angle = index * Mathf.PI * 2f / occupancy.Length;
                var ring = index < 8 ? radius * 0.36f : radius * 0.68f;
                point.localPosition = new Vector3(Mathf.Cos(angle) * ring, Mathf.Sin(angle) * ring, 0f);
                occupancy[index] = point;
            }

            var collider = Undo.AddComponent<CircleCollider2D>(anchor.gameObject);
            collider.radius = radius;
            collider.isTrigger = true;

            var authored = new EcosystemLocationAnchorAuthoring();
            authored.ConfigureEditor(location.id, anchor, arrival, occupancy, collider, null);
            return authored;
        }

        private static void DecorateLocation(
            Transform anchor,
            LocationState location,
            Sprite circle,
            Sprite square,
            float radius)
        {
            switch (location.locationType)
            {
                case LocationType.Town:
                    for (var index = 0; index < 7; index++)
                    {
                        var angle = index * Mathf.PI * 2f / 7f + 0.3f;
                        var distance = index == 0 ? 0f : radius * 0.48f;
                        CreateSprite(
                            $"Building {index + 1:00}",
                            anchor,
                            square,
                            index % 2 == 0
                                ? new Color(0.72f, 0.58f, 0.34f, 1f)
                                : new Color(0.56f, 0.42f, 0.26f, 1f),
                            new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance, 0f),
                            new Vector2(0.95f, 0.76f),
                            5);
                    }
                    break;
                case LocationType.Marketplace:
                    for (var index = 0; index < 5; index++)
                    {
                        CreateSprite(
                            $"Market Stall {index + 1:00}",
                            anchor,
                            square,
                            index % 2 == 0
                                ? new Color(0.95f, 0.61f, 0.12f, 1f)
                                : new Color(0.78f, 0.2f, 0.15f, 1f),
                            new Vector3((index - 2) * 0.62f, (index % 2 - 0.5f) * 0.65f, 0f),
                            new Vector2(0.48f, 0.52f),
                            5);
                    }
                    break;
                case LocationType.Hospital:
                    CreateSprite(
                        "Hospital Building",
                        anchor,
                        square,
                        new Color(0.82f, 0.88f, 0.9f, 1f),
                        Vector3.zero,
                        new Vector2(2.2f, 1.7f),
                        5);
                    CreateSprite(
                        "Medical Cross Vertical",
                        anchor,
                        square,
                        new Color(0.9f, 0.12f, 0.14f, 1f),
                        new Vector3(0f, 0f, 0f),
                        new Vector2(0.32f, 1.15f),
                        8);
                    CreateSprite(
                        "Medical Cross Horizontal",
                        anchor,
                        square,
                        new Color(0.9f, 0.12f, 0.14f, 1f),
                        new Vector3(0f, 0f, 0f),
                        new Vector2(1.15f, 0.32f),
                        8);
                    break;
                case LocationType.ResourceSite:
                    for (var index = 0; index < 8; index++)
                    {
                        var angle = index * Mathf.PI * 2f / 8f;
                        CreateSprite(
                            $"Resource Cluster {index + 1:00}",
                            anchor,
                            index % 3 == 0 ? square : circle,
                            new Color(0.12f, 0.84f, 0.68f, 1f),
                            new Vector3(Mathf.Cos(angle) * radius * 0.55f, Mathf.Sin(angle) * radius * 0.55f, 0f),
                            new Vector2(0.42f, 0.68f),
                            5);
                    }
                    break;
                case LocationType.Dungeon:
                    CreateSprite(
                        "Gate Outer Aura",
                        anchor,
                        circle,
                        new Color(0.48f, 0.12f, 0.86f, 0.88f),
                        Vector3.zero,
                        new Vector2(2.6f, 2.6f),
                        5);
                    CreateSprite(
                        "Gate Interior",
                        anchor,
                        circle,
                        new Color(0.025f, 0.018f, 0.08f, 1f),
                        Vector3.zero,
                        new Vector2(1.9f, 1.9f),
                        7);
                    for (var side = -1; side <= 1; side += 2)
                    {
                        CreateSprite(
                            side < 0 ? "Gate Pillar Left" : "Gate Pillar Right",
                            anchor,
                            square,
                            new Color(0.24f, 0.2f, 0.3f, 1f),
                            new Vector3(side * 1.34f, 0f, 0f),
                            new Vector2(0.42f, 2.9f),
                            4);
                    }
                    break;
            }
        }

        private static EcosystemRoutePathAuthoring BuildRoute(
            Transform parent,
            WorldRouteState route,
            Vector3 from,
            Vector3 to,
            Sprite square)
        {
            var routeRoot = CreateObject($"{route.fromLocationId} to {route.toLocationId}", parent);
            var start = new Vector2(from.x, from.y);
            var end = new Vector2(to.x, to.y);
            var direction = end - start;
            var perpendicular = direction.sqrMagnitude < 0.01f
                ? Vector2.zero
                : new Vector2(-direction.y, direction.x).normalized;
            var bendSign = EcosystemDeterministicRandom.StableHash(
                route.fromLocationId + "|" + route.toLocationId) % 2u == 0u ? 1f : -1f;
            var midpoint = Vector2.Lerp(start, end, 0.5f) +
                           perpendicular * bendSign * Mathf.Min(2.2f, direction.magnitude * 0.08f);
            var positions = new[] { start, midpoint, end };
            var waypoints = new Transform[positions.Length];
            for (var index = 0; index < positions.Length; index++)
            {
                var waypoint = CreateObject($"Waypoint {index + 1:00}", routeRoot);
                waypoint.position = new Vector3(positions[index].x, positions[index].y, 0f);
                waypoints[index] = waypoint;
                if (index > 0)
                {
                    CreateSegment(
                        $"Road Segment {index:00}",
                        routeRoot,
                        square,
                        positions[index - 1],
                        positions[index],
                        0.46f + Mathf.Clamp(route.travelDays, 1, 3) * 0.08f,
                        new Color(0.46f, 0.38f, 0.28f, 1f),
                        -55);
                }
            }

            var authored = new EcosystemRoutePathAuthoring();
            authored.ConfigureEditor(route.fromLocationId, route.toLocationId, waypoints, null);
            return authored;
        }

        private static EcosystemHunterPawn2D[] BuildPawnPool(
            Transform parent,
            EcosystemSpatialAuthoring authoring,
            EcosystemWorldState world,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog,
            Sprite circle,
            Sprite square)
        {
            var pawns = new EcosystemHunterPawn2D[PawnSlotCount];
            var occupancyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < PawnSlotCount; index++)
            {
                var hunter = index < world.hunters.Count ? world.hunters[index] : null;
                var slot = CreateObject($"Hunter Pawn Slot {index + 1:000}", parent);
                var pawn = Undo.AddComponent<EcosystemHunterPawn2D>(slot.gameObject);
                pawn.ConfigureEditor(circle, square, 100 + index);
                pawns[index] = pawn;

                if (hunter == null || !hunter.IsActive)
                {
                    slot.gameObject.SetActive(false);
                    continue;
                }

                occupancyCounts.TryGetValue(hunter.locationId, out var ordinal);
                occupancyCounts[hunter.locationId] = ordinal + 1;
                var location = world.map.locations.Find(item => item.id == hunter.locationId);
                var position = authoring.TryGetLocation(hunter.locationId, out var anchor)
                    ? anchor.OccupancyPosition(hunter.id, ordinal, 0.78f, EcosystemSpatialPlane.XY)
                    : location?.mapPosition * MapScale ?? Vector2.zero;
                pawn.SetPlanarPosition(position);
                pawn.ApplyVisual(CreatePreviewVisual(world, hunter, gearCatalog));
                pawn.SetSelected(hunter.id == world.playerHunterId);
                slot.gameObject.name = $"Hunter Pawn Slot {index + 1:000} (Preview {hunter.displayName})";
                slot.gameObject.SetActive(true);
            }
            return pawns;
        }

        private static EcosystemHunterPawnVisual CreatePreviewVisual(
            EcosystemWorldState world,
            HunterProfile hunter,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog)
        {
            var guildIndex = world.guilds.FindIndex(guild => guild.id == hunter.guildId);
            var guildColor = guildIndex switch
            {
                0 => new Color(0.08f, 0.58f, 1f, 1f),
                1 => new Color(0.94f, 0.11f, 0.16f, 1f),
                2 => new Color(0.1f, 0.8f, 0.38f, 1f),
                3 => new Color(0.9f, 0.92f, 1f, 1f),
                4 => new Color(0.66f, 0.22f, 0.92f, 1f),
                _ => new Color(0.52f, 0.58f, 0.66f, 1f)
            };
            var gear = gearCatalog?.FirstOrDefault(item => item != null && item.GearId == hunter.equippedGearId);
            var bodyColor = gear == null
                ? new Color(0.44f, 0.5f, 0.58f, 1f)
                : gear.Accent.a > 0.05f
                    ? new Color(gear.Accent.r, gear.Accent.g, gear.Accent.b, 1f)
                    : new Color(0.32f, 0.52f, 0.78f, 1f);
            var build = EcosystemCareerRules.InferBuild(hunter, gearCatalog);
            var glyph = build.Primary switch
            {
                HunterArchetype.Fighter => "F",
                HunterArchetype.Healer => "H",
                HunterArchetype.Assassin => "A",
                HunterArchetype.Ranger => "R",
                HunterArchetype.Tank => "T",
                HunterArchetype.Mage => "M",
                _ => "?"
            };
            return new EcosystemHunterPawnVisual(
                guildColor,
                bodyColor,
                glyph,
                hunter.vitals?.HealthRatio ?? 1f,
                hunter.vitals?.ManaRatio ?? 1f,
                hunter.vitals?.ShieldRatio ?? 1f);
        }

        private static Transform CreateObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");
            var transform = gameObject.transform;
            if (parent != null)
            {
                transform.SetParent(parent, false);
            }
            return transform;
        }

        private static SpriteRenderer CreateSprite(
            string name,
            Transform parent,
            Sprite sprite,
            Color color,
            Vector3 localPosition,
            Vector2 localSize,
            int sortingOrder)
        {
            var transform = CreateObject(name, parent);
            transform.localPosition = localPosition;
            transform.localScale = new Vector3(localSize.x, localSize.y, 1f);
            var renderer = Undo.AddComponent<SpriteRenderer>(transform.gameObject);
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static void CreateSegment(
            string name,
            Transform parent,
            Sprite square,
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
                square,
                color,
                Vector3.zero,
                new Vector2(length, width),
                sortingOrder);
            renderer.transform.position = new Vector3((from.x + to.x) * 0.5f, (from.y + to.y) * 0.5f, 0f);
            renderer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private static float LocationRadius(LocationType type)
        {
            return type switch
            {
                LocationType.Town => 3.6f,
                LocationType.Marketplace => 2.2f,
                LocationType.Hospital => 2.35f,
                LocationType.ResourceSite => 2.55f,
                LocationType.Dungeon => 2.15f,
                _ => 2f
            };
        }

        private static Color LocationColor(LocationType type)
        {
            return type switch
            {
                LocationType.Town => new Color(0.22f, 0.34f, 0.48f, 0.96f),
                LocationType.Marketplace => new Color(0.52f, 0.34f, 0.08f, 0.96f),
                LocationType.Hospital => new Color(0.24f, 0.48f, 0.52f, 0.96f),
                LocationType.ResourceSite => new Color(0.08f, 0.42f, 0.22f, 0.96f),
                LocationType.Dungeon => new Color(0.34f, 0.06f, 0.42f, 0.96f),
                _ => Color.gray
            };
        }

        private static void EnsureGeneratedAssetFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Data/Ecosystem"))
            {
                AssetDatabase.CreateFolder("Assets/Data", "Ecosystem");
            }
            if (!AssetDatabase.IsValidFolder(GeneratedAssetFolder))
            {
                AssetDatabase.CreateFolder("Assets/Data/Ecosystem", "Spatial");
            }
        }

        private static Sprite EnsureSpriteAsset(string assetPath, bool circle)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
            {
                return sprite;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            var radius = size * 0.47f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var alpha = circle
                        ? (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + 0.8f -
                            Vector2.Distance(new Vector2(x, y), new Vector2(center, center))) * 255f)
                        : (byte)255;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = size;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }
    }
}
