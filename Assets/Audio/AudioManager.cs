// Assets/_Scripts/Audio/AudioManager.cs
using UnityEngine;
using System.Collections;

public enum AttackWeight
{
    Light,
    Medium,
    Heavy
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Sources")]
    private AudioSource _sfxSource, _musicSource, _uiSource;

    [Header("Base Combat SFX")]
    public AudioClip hitLight, hitMedium, hitHeavy, hitCounter;
    public AudioClip blockLight, blockHeavy;
    public AudioClip knockdownImpact, wakeupSFX;

    [Header("Round SFX")]
    public AudioClip roundStartBell, koSound, timerTickFast, roundWin;

    [Header("UI SFX")]
    public AudioClip uiSelect, uiConfirm, uiBack;

    [Header("Music")]
    public AudioClip menuMusic, victoryJingle;

    [Header("Character SFX (index 0–3)")]
    public CharacterSFXProfile[] characterSFX;

    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.65f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _musicSource = gameObject.AddComponent<AudioSource>();
        _uiSource = gameObject.AddComponent<AudioSource>();
        _musicSource.loop = true;
        _uiSource.ignoreListenerPause = true;

        SettingsManager.LoadAll();
    }

    public void Play(AudioClip c, float vol = 1f)
    { if (c) _sfxSource.PlayOneShot(c, vol * sfxVolume * masterVolume); }

    public void PlayUI(AudioClip c)
    { if (c) _uiSource.PlayOneShot(c, 0.8f * masterVolume); }

    public void PlayMusic(AudioClip c, float vol = 0.65f)
    {
        if (c == null || (_musicSource.clip == c && _musicSource.isPlaying)) return;
        _musicSource.clip = c; _musicSource.volume = vol * musicVolume * masterVolume;
        _musicSource.Play();
    }

    public void StopMusic() => _musicSource.Stop();

    public void SetMasterVolume(float v) { masterVolume = v; }
    public void SetMusicVolume(float v) { musicVolume = v; }
    public void SetSFXVolume(float v) { sfxVolume = v; }

    public void OnHit(HitEvent e)
    {
        bool blocked = e.result == HitResult.Blocked;
        int atkChar = e.attackerIndex == 0
            ? GameFlowManager.P1CharacterIndex
            : GameFlowManager.P2CharacterIndex;
        int defChar = e.attackerIndex == 0
            ? GameFlowManager.P2CharacterIndex
            : GameFlowManager.P1CharacterIndex;

        if (blocked) { Play(e.damageDealt >= 15 ? blockHeavy : blockLight); return; }

        // Base impact
        if (e.damageDealt >= 18) Play(hitHeavy);
        else if (e.damageDealt >= 12) Play(hitMedium);
        else Play(hitLight);

        // [FIXED] Removed the Counter-hit check here to clear the error!

        // Character-specific attack SFX already played on startup; play defender react
        var defSFX = GetProfile(defChar);
        if (defSFX?.hitReactionGrunts?.Length > 0 && Random.value < 0.6f)
        {
            var src = e.attackerIndex == 0 ? _sfxSource : _sfxSource; // use separate src in real build
            src.PlayOneShot(defSFX.hitReactionGrunts[Random.Range(0, defSFX.hitReactionGrunts.Length)]);
        }
    }

    public void PlaySwing(int playerIndex, AttackWeight weight)
    {
        int idx = playerIndex == 0 ? GameFlowManager.P1CharacterIndex : GameFlowManager.P2CharacterIndex;
        var p = GetProfile(idx);
        if (p == null) return;
        var clips = weight switch
        {
            AttackWeight.Light => p.lightSwingClips,
            AttackWeight.Medium => p.mediumSwingClips,
            AttackWeight.Heavy => p.heavySwingClips,
            _ => null
        };
        PlayRandom(clips);
        if (weight == AttackWeight.Heavy) PlayRandom(p.attackGrunts, 0.7f);
    }

    public void PlaySpecialActivation(int playerIndex)
    {
        int idx = playerIndex == 0 ? GameFlowManager.P1CharacterIndex : GameFlowManager.P2CharacterIndex;
        var c = GetProfile(idx)?.specialActivationClip;
        if (c) Play(c);
    }

    public void PlayIntroVoice(int playerIndex)
    {
        int idx = playerIndex == 0 ? GameFlowManager.P1CharacterIndex : GameFlowManager.P2CharacterIndex;
        var c = GetProfile(idx)?.introLine;
        if (c) Play(c, 0.9f);
    }

    public void PlayRoundStart() => Play(roundStartBell);

    public void PlayKOSequence(int winnerIndex)
        => StartCoroutine(KOSoundRoutine(winnerIndex));

    IEnumerator KOSoundRoutine(int winnerIndex)
    {
        _musicSource.volume = 0f;
        Play(koSound);
        yield return new WaitForSeconds(0.3f);
        int charIdx = winnerIndex == 0
            ? GameFlowManager.P1CharacterIndex
            : GameFlowManager.P2CharacterIndex;
        var c = GetProfile(charIdx)?.koVoiceLine;
        if (c) Play(c);
        yield return new WaitForSeconds(1.5f);
        float t = 0f;
        while (t < 1.5f)
        {
            t += Time.deltaTime;
            _musicSource.volume = Mathf.Lerp(0f, musicVolume * masterVolume * 0.65f, t / 1.5f);
            yield return null;
        }
    }

    public void PlayUISelect() => PlayUI(uiSelect);
    public void PlayUIConfirm() => PlayUI(uiConfirm);
    public void PlayUIBack() => PlayUI(uiBack);

    CharacterSFXProfile GetProfile(int idx)
        => (characterSFX != null && idx >= 0 && idx < characterSFX.Length) ? characterSFX[idx] : null;

    void PlayRandom(AudioClip[] clips, float chance = 1f)
    {
        if (clips == null || clips.Length == 0 || Random.value > chance) return;
        Play(clips[Random.Range(0, clips.Length)]);
    }
}