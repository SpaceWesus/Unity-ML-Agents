using UnityEngine;
using UnityEngine.SceneManagement;

namespace Turtle.Homies
{
    public static class HomiesGameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartHomiesGame()
        {
            if (SceneManager.GetActiveScene().name != "Homies" ||
                Object.FindFirstObjectByType<HomiesArenaDirector>() != null)
            {
                return;
            }

            var heroObject = GameObject.Find("Hero");
            var enemyObject = GameObject.Find("Enemy");
            var camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (heroObject == null || enemyObject == null || camera == null)
            {
                Debug.LogError("Homies requires scene objects named Hero, Enemy, and Main Camera.");
                return;
            }

            var gameRoot = new GameObject("Monarch Protocol");
            var director = gameRoot.AddComponent<HomiesArenaDirector>();

            var template = Object.Instantiate(enemyObject, gameRoot.transform);
            template.name = "Enemy Template";
            template.SetActive(false);

            heroObject.transform.SetPositionAndRotation(new Vector3(0f, 1f, -2f), Quaternion.identity);
            var heroCombatant = heroObject.AddComponent<HomiesCombatant>();
            heroCombatant.Configure(
                HomiesFaction.Player,
                130f,
                28f,
                new Color(0.08f, 0.38f, 0.95f));

            var player = heroObject.AddComponent<HomiesPlayerController>();
            player.Initialize(director, camera);

            camera.transform.SetPositionAndRotation(
                new Vector3(7f, 6f, -9f),
                Quaternion.Euler(22f, -35f, 0f));
            var cameraRig = camera.gameObject.AddComponent<HomiesCameraRig>();
            cameraRig.Initialize(heroObject.transform, heroCombatant);

            enemyObject.transform.SetPositionAndRotation(new Vector3(0f, 1f, 5f), Quaternion.Euler(0f, 180f, 0f));
            var firstEnemy = enemyObject.AddComponent<HomiesCombatant>();
            firstEnemy.Configure(
                HomiesFaction.Enemy,
                70f,
                10f,
                new Color(0.45f, 0.03f, 0.08f));
            var firstAgent = enemyObject.AddComponent<HomiesEnemyAgent>();
            firstAgent.Initialize(director, 3.5f);

            director.Initialize(player, heroCombatant, template);
            director.RegisterInitialEnemy(firstEnemy);

            RenderSettings.ambientLight = new Color(0.025f, 0.03f, 0.08f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.018f, 0.015f, 0.04f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.018f;
            RenderSettings.skybox = null;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.01f, 0.008f, 0.03f);
        }

    }
}
