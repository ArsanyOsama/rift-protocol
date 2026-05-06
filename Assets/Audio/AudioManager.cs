// AudioManager.cs
// Assets/_Scripts/Audio/AudioManager.cs

using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    private AudioSource _sfxSource;
    private AudioSource _musicSource;

    [Header("SFX")]
    public AudioClip hitLight;
    public AudioClip hitHeavy;
    public AudioClip whoosh;
    public AudioClip KO;
    public AudioClip roundStart;
    public AudioClip blockSound;

    [Header("Music")]
    public AudioClip menuMusic;
    public AudioClip fightMusic;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Create two AudioSource components — one for SFX (no loop), one for music (loop)
        _sfxSource = gameObject.AddComponent<AudioSource>();
        _musicSource = gameObject.AddComponent<AudioSource>();

        _sfxSource.playOnAwake = false;
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
    }

    /// <summary>Play a one-shot SFX clip.</summary>
    public void Play(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        _sfxSource.PlayOneShot(clip, volume);
    }

    /// <summary>Start looping background music.</summary>
    public void PlayMusic(AudioClip clip, float volume = 0.7f)
    {
        if (clip == null || _musicSource.clip == clip) return;
        _musicSource.clip = clip;
        _musicSource.volume = volume;
        _musicSource.Play();
    }

    public void StopMusic() => _musicSource.Stop();

    public void SetMusicVolume(float v) => _musicSource.volume = v;
}