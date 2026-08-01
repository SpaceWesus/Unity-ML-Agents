using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.DungeonRaid
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class RaidChest2D : MonoBehaviour
    {
        [SerializeField] private string chestId = "chest";
        [SerializeField, Min(1)] private int lootTier = 1;
        [SerializeField] private RaidEnemyPodBrain2D lockingPod;
        [SerializeField] private SpriteRenderer chestRenderer;
        [SerializeField] private bool opened;

        private Vector3 closedScale;
        private Color closedColor;

        public string ChestId => chestId;
        public int LootTier => lootTier;
        public bool IsOpened => opened;
        public bool CanOpen => !opened && (lockingPod == null || lockingPod.IsDefeated);
        public Vector2 Position => transform.position;

        private void Awake()
        {
            if (chestRenderer == null)
            {
                chestRenderer = GetComponent<SpriteRenderer>();
            }
            closedScale = transform.localScale;
            closedColor = chestRenderer != null ? chestRenderer.color : Color.white;
            ApplyPresentation();
        }

        private void Update()
        {
            if (!CanOpen || chestRenderer == null) return;
            var pulse = 1f + Mathf.Sin(Time.time * 5f) * 0.06f;
            transform.localScale = new Vector3(
                closedScale.x * pulse,
                closedScale.y / pulse,
                closedScale.z);
        }

        public void ResetChest()
        {
            opened = false;
            transform.localScale = closedScale;
            ApplyPresentation();
        }

        public bool TryOpen(RaidAgent2D opener, DungeonRaidDirector2D raid)
        {
            if (!CanOpen || opener == null || !opener.CanAct) return false;
            opened = true;
            transform.localScale = new Vector3(closedScale.x * 1.15f, closedScale.y * 0.42f, closedScale.z);
            if (chestRenderer != null)
            {
                chestRenderer.color = new Color(0.42f, 0.34f, 0.16f, 0.72f);
            }
            raid?.Effects?.EmitBurst(Position, new Color(1f, 0.72f, 0.12f), 3f, 0.65f);
            raid?.Effects?.EmitText(Position + Vector2.up * 1.1f, $"TIER {lootTier} LOOT", new Color(1f, 0.82f, 0.25f));
            raid?.PublishEvent($"The party opened {name.Trim()}.");
            return true;
        }

        private void ApplyPresentation()
        {
            if (chestRenderer == null) return;
            chestRenderer.color = opened
                ? new Color(0.42f, 0.34f, 0.16f, 0.72f)
                : closedColor;
        }

#if UNITY_EDITOR
        public void ConfigureEditor(string id, int tier, RaidEnemyPodBrain2D pod)
        {
            chestId = id;
            lootTier = Mathf.Max(1, tier);
            lockingPod = pod;
            opened = false;
            chestRenderer = GetComponent<SpriteRenderer>();
            if (chestRenderer != null)
            {
                chestRenderer.sortingOrder = 10;
            }
        }
#endif
    }
}
