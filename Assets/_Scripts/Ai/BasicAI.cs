// Assets/_Scripts/AI/BasicAI.cs
using UnityEngine;
using System.Collections;

public class BasicAI : MonoBehaviour
{
    private FighterControllerSimple _fighter;
    private PhysicsBody _physics;
    private CharacterState _target;
    private bool _active = true;

    // Difficulty-tuned values (set from GameFlowManager.AIDifficulty in Start)
    private float _reactionTime = 0.7f;
    private float _attackRange = 2.5f;
    [Range(0f, 1f)]
    private float _blockChance = 0.25f;
    private float _jumpChance = 0.1f;

    void Start()
    {
        _fighter = GetComponent<FighterControllerSimple>();
        _physics = GetComponent<PhysicsBody>();

        foreach (var cs in FindObjectsOfType<CharacterState>())
            if (cs != GetComponent<CharacterState>()) { _target = cs; break; }

        int diff = GameFlowManager.AIDifficulty;
        _reactionTime = diff switch { 0 => 1.2f, 1 => 0.7f, 2 => 0.35f, _ => 0.7f };
        _blockChance = diff switch { 0 => 0.1f, 1 => 0.25f, 2 => 0.5f, _ => 0.25f };

        StartCoroutine(AILoop());
    }

    IEnumerator AILoop()
    {
        while (_active)
        {
            yield return new WaitForSeconds(_reactionTime + Random.Range(-0.15f, 0.25f));
            if (_target == null || _fighter == null) continue;

            float dist = Mathf.Abs(transform.position.x - _target.transform.position.x);

            if (dist > _attackRange + 0.8f)
                MoveToward();
            else if (dist <= _attackRange)
                AttackOrBlock();
            else if (Random.value < _jumpChance && _physics.IsGrounded)
                _fighter.SimulateJump();
        }
    }

    void AttackOrBlock()
    {
        if (Random.value < _blockChance)
        {
            _fighter.SimulateBlock(true);
            return;
        }
        _fighter.SimulateBlock(false);

        float r = Random.value;
        if (r < 0.45f) _fighter.SimulateAttack(AttackWeight.Light);
        else if (r < 0.75f) _fighter.SimulateAttack(AttackWeight.Medium);
        else if (r < 0.90f) _fighter.SimulateAttack(AttackWeight.Heavy);
        else _fighter.SimulateSpecial();
    }

    void MoveToward()
    {
        _fighter.SimulateBlock(false);
        float dir = _target.transform.position.x > transform.position.x ? 1f : -1f;
        _physics.SetMoveInput(dir);
    }

    public void Deactivate() => _active = false;
}