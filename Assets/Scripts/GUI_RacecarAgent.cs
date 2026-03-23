using System;
using UnityEngine;

public class GUI_RacecarAgent : MonoBehaviour
{
    [SerializeField] private RacecarAgent _racecarAgent;
    
    private GUIStyle _defaultStyle = new GUIStyle();
    private GUIStyle _positiveStyle = new GUIStyle();
    private GUIStyle _negativeStyle = new GUIStyle();

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Define GUI styles
        _defaultStyle.fontSize = 20;
        _defaultStyle.normal.textColor = Color.yellow;

        _positiveStyle.fontSize = 20;
        _positiveStyle.normal.textColor = Color.green;

        _negativeStyle.fontSize = 20;
        _negativeStyle.normal.textColor = Color.red;
    }

    private void OnGUI()
    {
        string debugEpisode = "Episode: " + _racecarAgent.CurrentEpisode + " - Step: " + _racecarAgent.StepCount;
        string debugReward = "Reward: " + _racecarAgent.CumulativeReward.ToString(); 

        // Select style based on reward value
        GUIStyle rewardStyle = _racecarAgent.CumulativeReward < 0 ? _negativeStyle : _positiveStyle;

        // Display the debug text
        GUI.Label(new Rect(20, 20, 500, 30), debugEpisode, _defaultStyle);
        GUI.Label(new Rect(20, 60, 500, 30), debugReward, rewardStyle);
    }


}
