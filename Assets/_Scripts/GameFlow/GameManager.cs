// GameManager.cs
// Assets/_Scripts/GameFlow/GameManager.cs
// Owns the round and match loop. Listens to StateMachine.OnFighterDied.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static GameManager Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Characters — assigned by CharacterSelectManager at runtime")]
    public GameObject[] characterPrefabs; // 0=Kael, 1=Sira, 2=Hemdan, 3=Ramez

    [Header("Spawn points")]
    public Transform spawnP1;
    public Transform spawnP2;

    [Header("Match settings")]
    public int roundsToWin = 2;
    public float roundTimerSeconds = 60f;
    public bool isSinglePlayer = false;

    // ── Events — UI subscribes to these ──────────────────────────────────────
    public event Action<int> OnRoundStart;   // int = round number
    public event Action<int> OnRoundEnd;     // int = winner player index (-1 = draw)
    public event Action<int> OnMatchEnd;     // int = winner player index
    public event Action<float, float> OnTimerTick; // current, max

    // ── Runtime state ─────────────────────────────────────────────────────────
    private int _p1Wins;
    private int _p2Wins;
    private int _currentRound = 1;
    private float _timerRemaining;
    private bool _roundActive;

    private GameObject _p1Fighter;
    private GameObject _p2Fighter;

    // ── Announcer reference (drag in Inspector) ────────────────────────────────
    [Header("UI")]
    public RoundAnnouncerUI announcer;

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Listen for any fighter dying
        StateMachine.OnFighterDied += HandleFighterDied;

        // Spawn characters from CharacterSelectManager selection
        SpawnFighters();

        // Start first round
        StartCoroutine(BeginRound(_currentRound));
    }

    void OnDestroy()
    {
        StateMachine.OnFighterDied -= HandleFighterDied;
    }

    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (!_roundActive) return;

        _timerRemaining -= Time.deltaTime;
        OnTimerTick?.Invoke(_timerRemaining, roundTimerSeconds);

        if (_timerRemaining <= 0f)
            HandleTimeOut();
    }

    // ── Spawn ─────────────────────────────────────────────────────────────────
    void SpawnFighters()
    {
        int p1Idx = CharacterSelectManager.P1Selection;
        int p2Idx = CharacterSelectManager.P2Selection;

        // Clamp in case selections are out of range
        p1Idx = Mathf.Clamp(p1Idx, 0, characterPrefabs.Length - 1);
        p2Idx = Mathf.Clamp(p2Idx, 0, characterPrefabs.Length - 1);

        _p1Fighter = Instantiate(characterPrefabs[p1Idx],
            spawnP1 != null ? spawnP1.position : new Vector3(-2.5f, 0, 0),
            Quaternion.identity);

        _p2Fighter = Instantiate(characterPrefabs[p2Idx],
            spawnP2 != null ? spawnP2.position : new Vector3(2.5f, 0, 0),
            Quaternion.identity);

        // Assign player indices
        var p1ctrl = _p1Fighter.GetComponent<FighterControllerSimple>();
        var p2ctrl = _p2Fighter.GetComponent<FighterControllerSimple>();
        if (p1ctrl != null) p1ctrl.playerIndex = 0;
        if (p2ctrl != null) p2ctrl.playerIndex = 1;

        // If single player mode: add AI to P2
        if (isSinglePlayer && _p2Fighter != null)
        {
            _p2Fighter.AddComponent<BasicAI>();
            // Remove the PlayerInput if present
            var playerInput = _p2Fighter.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null) Destroy(playerInput);
        }

        // Wire ArenaManager to both fighters
        if (ArenaManager.Instance != null)
            ArenaManager.Instance.ResetFightersToStart();

        // Wire CollisionManager
        // (CollisionManager reads fighters via IBoxProvider — it needs them in Inspector)
        // If you're spawning dynamically, assign them here:
        var cm = CollisionManager.Instance;
        if (cm != null)
        {
            var fighters = FindObjectsOfType<FighterControllerSimple>();
            // CollisionManager will pick them up via its IBoxProvider cast
            // Make sure both are assigned in Inspector OR wired dynamically
        }
    }

    // ── Round lifecycle ────────────────────────────────────────────────────────
    IEnumerator BeginRound(int roundNumber)
    {
        _roundActive = false;
        _timerRemaining = roundTimerSeconds;

        // Reset fighter positions
        ArenaManager.Instance?.ResetFightersToStart();
        ArenaManager.Instance?.SnapCameraToCenter();

        // Reset fighter HP
        ResetFighterHP(_p1Fighter);
        ResetFighterHP(_p2Fighter);

        // Show round announcement
        OnRoundStart?.Invoke(roundNumber);
        if (announcer != null)
            yield return StartCoroutine(announcer.ShowRoundStart(roundNumber));

        // Enable input — round is now live
        _roundActive = true;
    }

    void ResetFighterHP(GameObject fighter)
    {
        if (fighter == null) return;

        // Reset Brain 2 (Stats/HP)
        var cs = fighter.GetComponent<CharacterState>();
        if (cs != null)
        {
            cs.ResetToFull();
        }

        // Reset Brain 1 (Locomotion/Animation)
        var sm = fighter.GetComponent<StateMachine>();
        if (sm != null)
        {
            sm.currentHP = sm.maxHP; // Keep StateMachine's internal HP synced just in case
            sm.ForceTransition(FighterStateType.Idle);
        }
    }

    // ── Fighter died ──────────────────────────────────────────────────────────
    void HandleFighterDied(int playerIndex)
    {
        if (!_roundActive) return;
        _roundActive = false;

        int winnerIndex = playerIndex == 0 ? 1 : 0;
        StartCoroutine(EndRound(winnerIndex));
    }

    // ── Timer ran out ─────────────────────────────────────────────────────────
    void HandleTimeOut()
    {
        if (!_roundActive) return;
        _roundActive = false;

        // Compare HP — lower HP loses
        int p1HP = GetFighterHP(_p1Fighter);
        int p2HP = GetFighterHP(_p2Fighter);

        int winnerIndex = p1HP > p2HP ? 0 : p2HP > p1HP ? 1 : -1; // -1 = draw
        StartCoroutine(EndRound(winnerIndex));
    }

    int GetFighterHP(GameObject fighter)
    {
        if (fighter == null) return 0;
        var sm = fighter.GetComponent<StateMachine>();
        return sm != null ? sm.currentHP : 0;
    }

    // ── End round ─────────────────────────────────────────────────────────────
    IEnumerator EndRound(int winnerIndex)
    {
        // Show KO sequence
        if (announcer != null)
            yield return StartCoroutine(announcer.ShowKO());

        OnRoundEnd?.Invoke(winnerIndex);

        // Increment wins
        if (winnerIndex == 0) _p1Wins++;
        else if (winnerIndex == 1) _p2Wins++;
        // winnerIndex == -1 is a draw, neither gets a win

        _currentRound++;

        // Check for match winner
        if (_p1Wins >= roundsToWin)
        {
            StartCoroutine(EndMatch(0));
        }
        else if (_p2Wins >= roundsToWin)
        {
            StartCoroutine(EndMatch(1));
        }
        else
        {
            // Start next round
            yield return new WaitForSeconds(1f);
            StartCoroutine(BeginRound(_currentRound));
        }
    }

    // ── End match ─────────────────────────────────────────────────────────────
    IEnumerator EndMatch(int winnerIndex)
    {
        OnMatchEnd?.Invoke(winnerIndex);
        yield return new WaitForSeconds(4f);
        // Load character select or main menu
        SceneManager.LoadScene("CharacterSelect");
    }
}