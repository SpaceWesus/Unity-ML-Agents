using System.Collections.Generic;
using UnityEngine;

namespace Turtle.Homies
{
    [DisallowMultipleComponent]
    public sealed class HomiesArenaDirector : MonoBehaviour
    {
        private readonly List<HomiesCombatant> enemies = new();
        private readonly List<HomiesCombatant> corpses = new();
        private readonly List<HomiesCombatant> shadows = new();

        private HomiesPlayerController player;
        private HomiesCombatant playerCombatant;
        private GameObject enemyTemplate;
        private int wave = 1;
        private int remainingToSpawn;
        private float nextSpawnAt;
        private float nextWaveAt;
        private string message = string.Empty;
        private float messageUntil;
        private GUIStyle titleStyle;
        private GUIStyle hudStyle;
        private GUIStyle smallStyle;

        public void Initialize(
            HomiesPlayerController playerController,
            HomiesCombatant hero,
            GameObject template)
        {
            player = playerController;
            playerCombatant = hero;
            enemyTemplate = template;
            remainingToSpawn = 3;
            nextSpawnAt = Time.time + 0.3f;
            ShowMessage("SYSTEM  //  DUNGEON INSTANCE OPEN", 3f);
        }

        public void RegisterInitialEnemy(HomiesCombatant enemy)
        {
            RegisterEnemy(enemy);
        }

        public HomiesCombatant FindNearestEnemy(Vector3 position, float maxDistance)
        {
            HomiesCombatant nearest = null;
            var bestDistance = maxDistance * maxDistance;
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                var distance = (enemy.transform.position - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = enemy;
                }
            }

            return nearest;
        }

        public HomiesCombatant FindAimedEnemy(
            Camera aimingCamera,
            Vector3 origin,
            float maxDistance,
            float maxAngle)
        {
            HomiesCombatant bestTarget = null;
            var bestScore = float.MaxValue;
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                var targetPoint = enemy.transform.position + Vector3.up * 0.45f;
                var toTarget = targetPoint - aimingCamera.transform.position;
                var distanceFromHunter = Vector3.Distance(origin, enemy.transform.position);
                if (distanceFromHunter > maxDistance ||
                    Vector3.Angle(aimingCamera.transform.forward, toTarget) > maxAngle)
                {
                    continue;
                }

                var viewport = aimingCamera.WorldToViewportPoint(targetPoint);
                if (viewport.z <= 0f)
                {
                    continue;
                }

                var cursorDistance = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f).sqrMagnitude;
                var score = cursorDistance * 100f + distanceFromHunter * 0.01f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = enemy;
                }
            }

            return bestTarget;
        }

        public HomiesCombatant FindNearestHunter(Vector3 position)
        {
            HomiesCombatant nearest = playerCombatant != null && playerCombatant.IsAlive
                ? playerCombatant
                : null;
            var bestDistance = nearest == null
                ? float.MaxValue
                : (nearest.transform.position - position).sqrMagnitude;

            foreach (var shadow in shadows)
            {
                if (shadow == null || !shadow.IsAlive)
                {
                    continue;
                }

                var distance = (shadow.transform.position - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = shadow;
                }
            }

            return nearest;
        }

        public void TryArise(Camera aimingCamera)
        {
            HomiesCombatant nearest = null;
            var bestCursorDistance = 0.24f * 0.24f;
            foreach (var corpse in corpses)
            {
                if (corpse == null)
                {
                    continue;
                }

                if (Vector3.Distance(corpse.transform.position, player.transform.position) > 14f)
                {
                    continue;
                }

                var viewport = aimingCamera.WorldToViewportPoint(
                    corpse.transform.position + Vector3.up * 0.25f);
                if (viewport.z <= 0f)
                {
                    continue;
                }

                var cursorDistance = new Vector2(
                    viewport.x - 0.5f,
                    viewport.y - 0.5f).sqrMagnitude;
                if (cursorDistance < bestCursorDistance)
                {
                    nearest = corpse;
                    bestCursorDistance = cursorDistance;
                }
            }

            if (nearest == null)
            {
                ShowMessage("NO SHADOW ANSWERS THE CALL", 1.25f);
                return;
            }

            if (shadows.Count >= 8)
            {
                ShowMessage("SHADOW CAPACITY REACHED  //  8", 1.25f);
                return;
            }

            corpses.Remove(nearest);
            nearest.transform.rotation = Quaternion.identity;
            nearest.RestoreAsShadow(55f + player.Level * 8f, 14f + player.Level * 2f);
            var enemyAgent = nearest.GetComponent<HomiesEnemyAgent>();
            if (enemyAgent != null)
            {
                Destroy(enemyAgent);
            }

            var shadowAgent = nearest.gameObject.AddComponent<HomiesShadowAgent>();
            shadowAgent.Initialize(this, player.transform, shadows.Count);
            shadows.Add(nearest);
            ShowMessage($"ARISE  //  SHADOW ARMY {shadows.Count}/8", 1.8f);
        }

        public void CommandShadows(Camera aimingCamera)
        {
            var target = FindAimedEnemy(
                aimingCamera,
                player.transform.position,
                30f,
                18f);
            if (target == null)
            {
                ShowMessage("NO HOSTILE TARGET", 1.1f);
                return;
            }

            foreach (var shadow in shadows)
            {
                if (shadow != null && shadow.IsAlive &&
                    shadow.TryGetComponent<HomiesShadowAgent>(out var agent))
                {
                    agent.CommandAttack(target);
                }
            }

            ShowMessage("SHADOWS  //  FOCUS TARGET", 1.1f);
        }

        public void ShowMessage(string text, float duration)
        {
            message = text;
            messageUntil = Time.time + duration;
        }

        private void Update()
        {
            enemies.RemoveAll(item => item == null || !item.IsAlive);
            shadows.RemoveAll(item => item == null || !item.IsAlive);
            corpses.RemoveAll(item => item == null);

            if (!playerCombatant.IsAlive)
            {
                if (KeyboardRestartPressed())
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                }

                return;
            }

            if (remainingToSpawn > 0 && Time.time >= nextSpawnAt)
            {
                SpawnEnemy();
                remainingToSpawn--;
                nextSpawnAt = Time.time + 0.55f;
            }

            if (remainingToSpawn == 0 && enemies.Count == 0)
            {
                if (nextWaveAt <= 0f)
                {
                    nextWaveAt = Time.time + 3f;
                    ShowMessage($"GATE CLEARED  //  WAVE {wave}", 2.2f);
                    playerCombatant.Heal(playerCombatant.MaxHealth * 0.25f);
                }
                else if (Time.time >= nextWaveAt)
                {
                    wave++;
                    remainingToSpawn = Mathf.Min(3 + wave * 2, 18);
                    nextWaveAt = 0f;
                    ShowMessage(wave % 5 == 0
                        ? $"WARNING  //  BOSS WAVE {wave}"
                        : $"GATE SURGE  //  WAVE {wave}", 2.2f);
                }
            }
        }

        private static bool KeyboardRestartPressed()
        {
            return UnityEngine.InputSystem.Keyboard.current != null &&
                   UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame;
        }

        private void SpawnEnemy()
        {
            var spawnIndex = remainingToSpawn + wave * 3;
            var angle = spawnIndex * 2.39996f;
            var radius = 8f + (spawnIndex % 4) * 1.2f;
            var position = new Vector3(Mathf.Cos(angle) * radius, 1f, Mathf.Sin(angle) * radius);
            var instance = Instantiate(enemyTemplate, position, Quaternion.identity, transform);
            instance.name = wave % 5 == 0 && remainingToSpawn == 1
                ? $"Gate Boss {wave}"
                : $"Dungeon Knight W{wave}-{remainingToSpawn}";
            instance.SetActive(true);

            var isBoss = wave % 5 == 0 && remainingToSpawn == 1;
            var combatant = instance.AddComponent<HomiesCombatant>();
            combatant.Configure(
                HomiesFaction.Enemy,
                (55f + wave * 16f) * (isBoss ? 3f : 1f),
                (8f + wave * 2.2f) * (isBoss ? 1.5f : 1f),
                isBoss ? new Color(0.85f, 0.06f, 0.22f) : new Color(0.42f, 0.03f, 0.08f),
                isBoss);
            var agent = instance.AddComponent<HomiesEnemyAgent>();
            agent.Initialize(this, (3.3f + wave * 0.08f) * (isBoss ? 0.8f : 1f));
            RegisterEnemy(combatant);
        }

        private void RegisterEnemy(HomiesCombatant enemy)
        {
            enemies.Add(enemy);
            enemy.Defeated += OnCombatantDefeated;
        }

        private void OnCombatantDefeated(HomiesCombatant defeated, HomiesCombatant source)
        {
            defeated.Defeated -= OnCombatantDefeated;
            if (defeated.Faction == HomiesFaction.Enemy)
            {
                enemies.Remove(defeated);
                corpses.Add(defeated);
                defeated.SetCorpseAppearance();
                player.AddExperience(defeated.IsBoss ? 100 : 14 + wave * 3);
                ShowMessage(defeated.IsBoss
                    ? "BOSS DEFEATED  //  PRESS E TO ARISE"
                    : "SOUL AVAILABLE  //  PRESS E TO ARISE", 2f);
            }
            else if (defeated.Faction == HomiesFaction.Shadow)
            {
                shadows.Remove(defeated);
                Destroy(defeated.gameObject, 1f);
            }
            else
            {
                ShowMessage("HUNTER ELIMINATED  //  ENTER TO REAWAKEN", 60f);
            }
        }

        private void OnGUI()
        {
            BuildStyles();
            var width = Mathf.Min(440f, Screen.width - 32f);
            GUI.Box(new Rect(16f, 16f, width, 124f), GUIContent.none);
            GUI.Label(new Rect(30f, 24f, width - 28f, 28f), "MONARCH PROTOCOL", titleStyle);
            GUI.Label(
                new Rect(30f, 55f, width - 28f, 25f),
                $"HP  {Mathf.CeilToInt(playerCombatant.CurrentHealth)} / {Mathf.CeilToInt(playerCombatant.MaxHealth)}",
                hudStyle);
            GUI.Label(
                new Rect(30f, 79f, width - 28f, 25f),
                $"LEVEL {player.Level}    XP {player.Experience}/{player.ExperienceToNextLevel}    WAVE {wave}",
                hudStyle);
            GUI.Label(
                new Rect(30f, 103f, width - 28f, 25f),
                $"HOSTILES {enemies.Count + remainingToSpawn}    SHADOWS {shadows.Count}/8",
                hudStyle);

            GUI.Box(new Rect(16f, Screen.height - 78f, 590f, 58f), GUIContent.none);
            GUI.Label(
                new Rect(30f, Screen.height - 70f, 560f, 46f),
                "WASD Move   |   LMB Aim Slash   |   Space/Shift Dash\nE Aim + Arise   |   R Aim + Command Shadows   |   Mouse Orbit",
                smallStyle);

            DrawCrosshair();

            if (Time.time < messageUntil)
            {
                var rect = new Rect(Screen.width * 0.5f - 330f, 50f, 660f, 54f);
                GUI.Box(rect, GUIContent.none);
                GUI.Label(rect, message, titleStyle);
            }
        }

        private static void DrawCrosshair()
        {
            const float size = 12f;
            const float thickness = 2f;
            var centerX = Screen.width * 0.5f;
            var centerY = Screen.height * 0.5f;
            var previousColor = GUI.color;
            GUI.color = new Color(0.55f, 0.78f, 1f, 0.9f);
            GUI.DrawTexture(
                new Rect(centerX - size, centerY - thickness * 0.5f, size * 2f, thickness),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(centerX - thickness * 0.5f, centerY - size, thickness, size * 2f),
                Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void BuildStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.45f, 0.72f, 1f) }
            };
            hudStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = new Color(0.85f, 0.91f, 1f) }
            };
            smallStyle = new GUIStyle(hudStyle)
            {
                fontSize = 14,
                wordWrap = true
            };
        }
    }
}
