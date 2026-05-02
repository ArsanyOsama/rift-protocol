using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// BasicAI.cs — Simple CPU opponent for single-player mode
public class BasicAI : MonoBehaviour
{
    [Header("AI Settings")]
    public float reactionTime = 0.8f; // seconds between decisions
    public float attackRange = 2.5f;  // world units — within this, it attacks
    public float chaseRange = 8f;     // world units — within this, it walks toward player

    [Range(0f, 1f)]
    public float blockChance = 0.25f; // 25% chance to block incoming hits

    private FighterControllerSimple _self;
    private FighterControllerSimple _target;
    private Animator _animator;
    private bool _isActive = true;

    void Start()
    {
        _self = GetComponent<FighterControllerSimple>();
        _animator = GetComponent<Animator>();

        // Find the human player automatically
        foreach (var f in FindObjectsOfType<FighterControllerSimple>())
        {
            if (f != _self)
            {
                _target = f;
                break;
            }
        }

        StartCoroutine(AILoop());
    }

    IEnumerator AILoop()
    {
        while (_isActive)
        {
            // Adds a bit of variety to the reaction time so it's not perfectly robotic
            yield return new WaitForSeconds(reactionTime + Random.Range(-0.2f, 0.3f));

            if (_target == null) continue;

            float dist = Mathf.Abs(transform.position.x - _target.transform.position.x);

            if (dist <= attackRange)
                DoAttack();
            else if (dist <= chaseRange)
                MoveTowardPlayer();
            else
                DoIdle();
        }
    }

    void DoAttack()
    {
        // Pick a random attack weighted toward light attacks
        float roll = Random.value;

        if (roll < 0.5f)
            _animator.SetTrigger("LightAttack");
        else if (roll < 0.8f)
            _animator.SetTrigger("MediumAttack");
        else
            _animator.SetTrigger("HeavyAttack");
    }

    void MoveTowardPlayer()
    {
        float dir = _target.transform.position.x > transform.position.x ? 1f : -1f;

        // Tell PhysicsBody to move in this direction
        GetComponent<PhysicsBody>()?.SetMoveInput(dir);
    }

    void DoIdle()
    {
        GetComponent<PhysicsBody>()?.SetMoveInput(0f);
    }
}
