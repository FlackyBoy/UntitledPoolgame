using UnityEngine;
using UntitledPoolGame.Player;

namespace UntitledPoolGame.Pool
{
    // The rifle's animations, on two layers of the player's controller made by
    // Tools > Pool > Préparer les animations du Shooter (ShooterAnimationTools):
    // the whole body walks and runs with the weapon, and the top half stays in
    // the clip's hold (idle_strafing), or plays the ready and firing clips, over
    // it. The hold always: the walk clips carry the arms another way (50 degrees
    // further left, read from the FBX), and the cue swung with each direction
    // (04/11). Driven from code, as the R&D combat layers are: weights and
    // CrossFade, no transitions.
    public partial class LocalShooterWeapon
    {
        // Also read by ShooterAnimationTools.
        public const string BodyLayer = "Shooter Corps";
        public const string FireLayer = "Shooter Tir";
        public const string MoveXParameter = "ShooterX";
        public const string MoveYParameter = "ShooterY";
        public const string MoveState = "Move";
        public const string HoldState = "Hold";
        public const string ReadyState = "Ready";
        public const string ShootState = "Shoot";

        private static readonly int MoveXHash = Animator.StringToHash(MoveXParameter);
        private static readonly int MoveYHash = Animator.StringToHash(MoveYParameter);
        private static readonly int MoveHash = Animator.StringToHash(MoveState);
        private static readonly int HoldHash = Animator.StringToHash(HoldState);
        private static readonly int ReadyHash = Animator.StringToHash(ReadyState);
        private static readonly int ShootHash = Animator.StringToHash(ShootState);

        private const float MoveDamp = 0.1f;
        // The ready clip starts with the arms hanging apart, which would swing
        // the cue across the body: joined here, with the hands coming up.
        private const float ReadyStartShare = 0.35f;

        private enum Upper { Hold, Ready, Shoot }
        private Upper upper;

        private Animator animator;
        private CharacterController controller;
        private LocalPlayerShrink body;
        private LocalFpsPlayerController bodyFps;
        private int bodyLayer = -1, fireLayer = -1;
        private bool moveStarted;
        private bool wasDown = true;
        private float readyUntil, shootUntil;

        private bool IsDown => body != null && body.IsDown;

        private void InitAnimation()
        {
            body = GetComponent<LocalPlayerShrink>();
            bodyFps = GetComponent<LocalFpsPlayerController>();
            controller = GetComponent<CharacterController>();
            LocalCharacterAnimationController animControl = GetComponent<LocalCharacterAnimationController>();
            animator = animControl != null ? animControl.Animator : null;
            if (TryGetComponent(out LocalPlayerLocomotion locomotion)) locomotion.ArmedAnimation = true;
            if (animator == null) return;
            bodyLayer = animator.GetLayerIndex(BodyLayer);
            fireLayer = animator.GetLayerIndex(FireLayer);
            if (bodyLayer < 0 || fireLayer < 0)
                Debug.LogWarning($"[ShooterMini] Calques « {BodyLayer} » / « {FireLayer} » absents du contrôleur d'animation : lance Tools > Pool > Préparer les animations du Shooter.", this);
        }

        private void UpdateAnimation(float dt)
        {
            if (animator == null || bodyLayer < 0 || fireLayer < 0 || dt <= 0f) return;
            bool down = IsDown;

            // Ready at the start of the match and each time they get back up.
            if (wasDown && !down)
            {
                readyUntil = Time.time + cfg.readyTime * (1f - ReadyStartShare);
                upper = Upper.Ready;
                animator.CrossFadeInFixedTime(ReadyHash, cfg.animationFade, fireLayer, cfg.readyTime * ReadyStartShare);
            }
            wasDown = down;
            if (!moveStarted)
            {
                moveStarted = true;
                animator.Play(MoveHash, bodyLayer, 0f);
            }

            Vector2 move = NormalizedMove();
            animator.SetFloat(MoveXHash, move.x, MoveDamp, dt);
            animator.SetFloat(MoveYHash, move.y, MoveDamp, dt);

            // Down, the fall's own animations play.
            float step = dt / Mathf.Max(cfg.animationFade, 0.01f);
            animator.SetLayerWeight(bodyLayer, Mathf.MoveTowards(animator.GetLayerWeight(bodyLayer), down ? 0f : 1f, step));
            animator.SetLayerWeight(fireLayer, Mathf.MoveTowards(animator.GetLayerWeight(fireLayer), down ? 0f : 1f, step));
            bool finished = (upper == Upper.Ready && Time.time >= readyUntil) || (upper == Upper.Shoot && Time.time >= shootUntil);
            if (finished)
            {
                upper = Upper.Hold;
                animator.CrossFadeInFixedTime(HoldHash, cfg.animationFade, fireLayer, 0f);
            }
        }

        // The body's local velocity at normal size, on the blend tree's scale:
        // 1 at walking speed, 2 at sprinting speed.
        private Vector2 NormalizedMove()
        {
            MovementSettings speeds = MovementSettings.Instance;
            float size = bodyFps != null ? bodyFps.SizeScale : 1f;
            Vector3 local = transform.InverseTransformDirection(controller.velocity) / Mathf.Max(size, 0.001f);
            Vector2 flat = new Vector2(local.x, local.z);
            float speed = flat.magnitude;
            if (speed < 0.01f) return Vector2.zero;
            float walk = Mathf.Max(speeds.walkSpeed, 0.01f);
            float scale = speed <= walk ? speed / walk : 1f + (speed - walk) / Mathf.Max(speeds.sprintSpeed - walk, 0.01f);
            return flat / speed * Mathf.Min(scale, 2f);
        }

        // The firing loop, restarted only if it had stopped: held down, it
        // keeps looping rather than snapping back at each shot.
        private void PlayShot()
        {
            if (animator == null || fireLayer < 0) return;
            if (upper != Upper.Shoot) animator.CrossFadeInFixedTime(ShootHash, cfg.animationFade, fireLayer, 0f);
            upper = Upper.Shoot;
            shootUntil = Time.time + cfg.fireInterval + cfg.shootHold;
        }
    }
}
