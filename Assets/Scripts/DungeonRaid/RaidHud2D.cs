using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.DungeonRaid
{
    [DisallowMultipleComponent]
    public sealed class RaidHud2D : MonoBehaviour
    {
        [SerializeField] private DungeonRaidDirector2D raid;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;

        private void OnGUI()
        {
            if (raid == null) return;
            EnsureStyles();
            var panel = new Rect(12f, 12f, 390f, 188f);
            var previousColor = GUI.color;
            GUI.color = new Color(0.025f, 0.04f, 0.055f, 0.94f);
            GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.color = previousColor;
            GUI.Label(new Rect(26f, 22f, 360f, 28f), "AUTONOMOUS DAMP CAVE RAID", titleStyle);
            var partyPhase = raid.Party != null ? raid.Party.Phase.ToString() : "Unwired";
            var living = 0;
            var downed = 0;
            for (var index = 0; index < raid.Hunters.Count; index++)
            {
                if (raid.Hunters[index].CanAct) living++;
                else if (raid.Hunters[index].CanBeRescued) downed++;
            }
            var monsters = 0;
            for (var index = 0; index < raid.Monsters.Count; index++)
            {
                if (raid.Monsters[index].CanAct) monsters++;
            }
            GUI.Label(new Rect(26f, 55f, 350f, 22f),
                $"Party: {partyPhase}   Hunters: {living} active / {downed} downed", bodyStyle);
            GUI.Label(new Rect(26f, 78f, 350f, 22f),
                $"Hostiles remaining: {monsters}   Raid time: {raid.RaidTime:0.0}s", bodyStyle);
            GUI.Label(new Rect(26f, 106f, 350f, 42f), raid.LatestEvent, bodyStyle);
            if (!string.IsNullOrEmpty(raid.ResultMessage))
            {
                GUI.Label(new Rect(26f, 151f, 350f, 26f), raid.ResultMessage, titleStyle);
            }
        }

        private void EnsureStyles()
        {
            if (panelStyle != null) return;
            panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = Texture2D.whiteTexture },
                padding = new RectOffset(12, 12, 10, 10)
            };
            panelStyle.normal.textColor = Color.white;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.75f, 1f, 0.9f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
        }

#if UNITY_EDITOR
        public void ConfigureEditor(DungeonRaidDirector2D director)
        {
            raid = director;
        }
#endif
    }
}
