using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CharacterState))]
public class FighterAnimatorController : MonoBehaviour
{
    private Animator _anim;
    private CharacterState _state;
    private PhysicsBody _body;
    private CharState _prevState;

    void Awake()
    {
        _anim = GetComponent<Animator>();
        _state = GetComponent<CharacterState>();
        _body = GetComponent<PhysicsBody>();
    }

    void Update()
    {
        if (_state.CurrentState == _prevState) return;
        _prevState = _state.CurrentState;

        switch (_state.CurrentState)
        {
            case CharState.Idle:
                _anim.SetFloat("MoveX", 0f);
                break;
            case CharState.WalkForward:
                _anim.SetFloat("MoveX", 1f);
                break;
            case CharState.WalkBack:
                _anim.SetFloat("MoveX", -1f);
                break;
            case CharState.CrouchTransition:
            case CharState.Crouching:
                _anim.SetBool("IsCrouching", true);
                break;
            case CharState.LightAttack:
                _anim.SetTrigger("LightAttack"); break;
            case CharState.MediumAttack:
                _anim.SetTrigger("MediumAttack"); break;
            case CharState.HeavyAttack:
                _anim.SetTrigger("HeavyAttack"); break;
            case CharState.SpecialMove:
                _anim.SetTrigger("SpecialMove"); break;
            case CharState.HitStunStanding:
                _anim.SetTrigger("HitStun"); break;
            case CharState.KnockdownFalling:
                _anim.SetTrigger("Knockdown"); break;
            case CharState.WakeUp:
                _anim.SetTrigger("WakeUp"); break;
            case CharState.Victory:
                _anim.SetTrigger("Victory"); break;
            case CharState.BlockingStanding:
                _anim.SetBool("IsBlocking", true); break;
            case CharState.JumpNeutral:
            case CharState.JumpForward:
            case CharState.JumpBack:
                _anim.SetBool("IsGrounded", false);
                _anim.SetTrigger("Jump");
                break;
        }

        // Reset blocking when leaving block states
        if (_prevState != CharState.BlockingStanding &&
            _prevState != CharState.BlockingCrouching)
            _anim.SetBool("IsBlocking", false);

        // Reset crouching when leaving crouch states
        if (_prevState != CharState.Crouching &&
            _prevState != CharState.CrouchTransition)
            _anim.SetBool("IsCrouching", false);

        // Ground sync
        if (_body != null)
            _anim.SetBool("IsGrounded", _body.IsGrounded);
    }

    // ── ANIMATION EVENTS (called from animation clip keyframes) ─
    // Place these event function calls on the exact frame in the clip
    // where the hitbox should be active.
    public void OnHitboxActive() => GetComponent<FighterControllerSimple>()?.OnAttackImpactFrame();
    public void OnHitboxInactive() => GetComponent<FighterControllerSimple>()?.OnAttackEnd();
    public void OnFootstep() => AudioManager.Instance?.Play(AudioManager.Instance.whoosh, 0.3f);
    public void OnAttackWhoosh() => AudioManager.Instance?.Play(AudioManager.Instance.whoosh, 0.8f);
}
