using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneTransitionManager : MonoBehaviour
{
    // In SettingsManager.cs — called from AudioManager.Awake()
    public static void LoadAll()
    {
        AudioManager.Instance?.SetMasterVolume(PlayerPrefs.GetFloat("MasterVol", 1f));
        AudioManager.Instance?.SetMusicVolume(PlayerPrefs.GetFloat("MusicVol", 0.65f));
        AudioManager.Instance?.SetSFXVolume(PlayerPrefs.GetFloat("SFXVol", 1f));
        GameFlowManager.RoundsToWin = PlayerPrefs.GetInt("RoundsToWin", 2);
        GameFlowManager.RoundTimerSeconds = PlayerPrefs.GetInt("RoundTimerSeconds", 99);
        GameFlowManager.AIDifficulty = PlayerPrefs.GetInt("AIDifficulty", 1);
    }

    public static void SaveAudio(float m, float mu, float sfx)
    {
        PlayerPrefs.SetFloat("MasterVol", m);
        PlayerPrefs.SetFloat("MusicVol", mu);
        PlayerPrefs.SetFloat("SFXVol", sfx);
        PlayerPrefs.Save();
    }

    public static void SaveGameSettings(int rounds, int timer, int diff)
    {
        PlayerPrefs.SetInt("RoundsToWin", rounds);
        PlayerPrefs.SetInt("RoundTimerSeconds", timer);
        PlayerPrefs.SetInt("AIDifficulty", diff);
        PlayerPrefs.Save();
    }
}
