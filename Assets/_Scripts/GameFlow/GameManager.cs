// Assets/_Scripts/Core/GameManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Fighter Prefabs (index 0=Kael 1=Sira 2=Hemdan 3=Ramez)")]
    public GameObject[] fighterPrefabs;

    [Header("Sequences")]
    public IntroSequenceController introSequence;
    public KOSequenceController koSequence;

    [Header("Stage Music (index matches stage)")]
    public AudioClip[] stageMusicTracks;

    // ── Runtime ─────────────────────────────────────────────
    private CharacterState _csP1, _csP2;
    private PhysicsBody _pbP1, _pbP2;
    private Animator _animP1, _animP2;
    private int _roundNumber = 1;
    private bool _roundActive = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    IEnumerator Start()
    {
        // ── 1. Instantiate fighters from selection ──────────
        SpawnFighters();

        // ── 2. Load stage additively ─────────────────────────
        StageManager.Instance?.LoadStageAdditive(GameFlowManager.SelectedMapIndex);

        // ── 3. Wait for StageBootstrap to be available ───────
        float timeout = 5f;
        while (StageBootstrap.Instance == null && timeout > 0f)
        { yield return null; timeout -= Time.deltaTime; }

        // ── 4. Apply spawn positions ─────────────────────────
        ApplySpawnPositions();

        // ── 5. Wire HUD events ───────────────────────────────
        WireHUDEvents();

        // ── 6. Stage music ──────────────────────────────────
        PlayStageMusic();

        // ── 7. First round ──────────────────────────────────
        yield return StartRound();
    }

    // ─────────────────────────────────────────────────────────
    //  FIGHTER SPAWNING
    // ─────────────────────────────────────────────────────────
    void SpawnFighters()
    {
        var p1Go = Instantiate(
            fighterPrefabs[GameFlowManager.P1CharacterIndex],
            new Vector3(-3f, 0f, 0f), Quaternion.identity);
        p1Go.name = "P1_Fighter";
        var p1Ctrl = p1Go.GetComponent<FighterControllerSimple>();
        if (p1Ctrl) p1Ctrl.playerIndex = 0;
        _csP1 = p1Go.GetComponent<CharacterState>();
        _pbP1 = p1Go.GetComponent<PhysicsBody>();
        _animP1 = p1Go.GetComponent<Animator>();

        var p2Go = Instantiate(
            fighterPrefabs[GameFlowManager.P2CharacterIndex],
            new Vector3(3f, 0f, 0f), Quaternion.identity);
        p2Go.name = "P2_Fighter";
        var p2Ctrl = p2Go.GetComponent<FighterControllerSimple>();
        if (p2Ctrl) p2Ctrl.playerIndex = 1;
        _csP2 = p2Go.GetComponent<CharacterState>();
        _pbP2 = p2Go.GetComponent<PhysicsBody>();
        _animP2 = p2Go.GetComponent<Animator>();

        // AI on P2 for single player
        if (!GameFlowManager.IsMultiplayer)
            p2Go.AddComponent<BasicAI>();

        // [FIXED] Removed the broken manual Opponent facing assignments.
        // Your FighterControllerSimple now handles opponent tracking automatically!

        // Register fighters
        CollisionManager.Instance?.SetFighters(p1Ctrl, p2Ctrl);
        ArenaManager.Instance?.SetFighters(_pbP1, _pbP2);

        // Give camera target the fighter transforms
        CameraTargetController.Instance?.SetFighters(p1Go.transform, p2Go.transform);

        // Give intro sequence the animators
        if (introSequence)
        {
            introSequence.p1Animator = _animP1;
            introSequence.p2Animator = _animP2;
        }
    }

    void ApplySpawnPositions()
    {
        var b = StageBootstrap.Instance;
        if (b == null) return;
        _pbP1?.Warp(b.spawnP1.position);
        _pbP2?.Warp(b.spawnP2.position);

        // Lock the camera to the new stage's walls!
        CameraTargetController.Instance?.SetStageBounds(b.stageLeftWall, b.stageRightWall);
    }

    public void OnStageLoaded(Vector3 p1Pos, Vector3 p2Pos, int stageIndex)
    {
        _pbP1?.Warp(p1Pos);
        _pbP2?.Warp(p2Pos);
        var b = StageBootstrap.Instance;

        if (b != null)
        {
            // Lock the camera to the new stage's walls!
            CameraTargetController.Instance?.SetStageBounds(b.stageLeftWall, b.stageRightWall);
        }
    }

    // ─────────────────────────────────────────────────────────
    //  HUD WIRING
    // ─────────────────────────────────────────────────────────
    void WireHUDEvents()
    {
        if (_csP1)
        {
            _csP1.OnHPChanged += (c, m) => HUDController.Instance?.UpdateHealth(0, c, m);
            _csP1.OnComboUpdated += (n) => HUDController.Instance?.ShowCombo(0, n);
            _csP1.OnDied += (_) => StartCoroutine(OnFighterKO(1));
        }
        if (_csP2)
        {
            _csP2.OnHPChanged += (c, m) => HUDController.Instance?.UpdateHealth(1, c, m);
            _csP2.OnComboUpdated += (n) => HUDController.Instance?.ShowCombo(1, n);
            _csP2.OnDied += (_) => StartCoroutine(OnFighterKO(0));
        }
        HUDController.Instance?.UpdateHealth(0, _csP1?.maxHP ?? 100, _csP1?.maxHP ?? 100);
        HUDController.Instance?.UpdateHealth(1, _csP2?.maxHP ?? 100, _csP2?.maxHP ?? 100);
    }

    // ─────────────────────────────────────────────────────────
    //  ROUND MANAGEMENT
    // ─────────────────────────────────────────────────────────
    IEnumerator StartRound()
    {
        _csP1?.ResetHP(); _csP2?.ResetHP();
        HUDController.Instance?.UpdatePower(0, 0f);
        HUDController.Instance?.UpdatePower(1, 0f);
        ApplySpawnPositions();

        if (introSequence != null)
        {
            introSequence.roundNumber = _roundNumber;
            yield return introSequence.PlayIntroSequence();
        }
        _roundActive = true;
    }

    IEnumerator OnFighterKO(int winnerIndex)
    {
        if (!_roundActive) yield break;
        _roundActive = false;
        HUDController.Instance?.StopTimer();

        Animator winAnim = winnerIndex == 0 ? _animP1 : _animP2;

        if (koSequence != null)
            yield return koSequence.PlayKOSequence(winnerIndex, winAnim);

        if (winnerIndex == 0) GameFlowManager.P1RoundWins++;
        else GameFlowManager.P2RoundWins++;

        HUDController.Instance?.UpdateRoundDots(0, GameFlowManager.P1RoundWins);
        HUDController.Instance?.UpdateRoundDots(1, GameFlowManager.P2RoundWins);

        bool matchDone = GameFlowManager.P1RoundWins >= GameFlowManager.RoundsToWin
                      || GameFlowManager.P2RoundWins >= GameFlowManager.RoundsToWin;

        if (matchDone)
        {
            GameFlowManager.WinnerName = GameFlowManager.GetCharName(
                winnerIndex == 0 ? GameFlowManager.P1CharacterIndex
                                 : GameFlowManager.P2CharacterIndex);
            yield return new WaitForSeconds(1.5f);
            SceneManager.LoadScene("Victory");
        }
        else
        {
            _roundNumber++;
            yield return new WaitForSeconds(1.5f);
            yield return StartRound();
        }
    }

    void Update()
    {
        if (!_roundActive) return;
        if (HUDController.Instance?.GetTimerValue() <= 0f)
            StartCoroutine(OnTimerExpired());
    }

    IEnumerator OnTimerExpired()
    {
        if (!_roundActive) yield break;
        _roundActive = false;
        HUDController.Instance?.StopTimer();
        int p1HP = _csP1?.currentHP ?? 0, p2HP = _csP2?.currentHP ?? 0;
        int winner = p1HP > p2HP ? 0 : p2HP > p1HP ? 1 : -1;
        if (winner >= 0) yield return OnFighterKO(winner);
        else { _roundNumber++; yield return new WaitForSeconds(1.5f); yield return StartRound(); }
    }

    void PlayStageMusic()
    {
        if (stageMusicTracks != null && GameFlowManager.SelectedMapIndex < stageMusicTracks.Length)
            AudioManager.Instance?.PlayMusic(stageMusicTracks[GameFlowManager.SelectedMapIndex]);
    }
}