using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class Bullet : NetworkBehaviour
{
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private LayerMask groundLayerMask;
    [SerializeField] private int bulletDmg = 10;
    [SerializeField] private float moveSpeed = 20;
    [SerializeField] private float lifeTimeAmount = 0.8f;

    [Networked] private NetworkBool didHitSomething { get; set; }
    [Networked] private TickTimer lifeTimeTimer { get; set; }

    private Collider2D coll;
    private readonly List<LagCompensatedHit> hits = new List<LagCompensatedHit>();

    public override void Spawned()
    {
        Runner.SetIsSimulated(Object, true);
        coll = GetComponent<Collider2D>();
        lifeTimeTimer = TickTimer.CreateFromSeconds(Runner, lifeTimeAmount);
    }

    public override void FixedUpdateNetwork()
    {
        if (!didHitSomething)
        {
            CheckIfHitGround();
            CheckIfWeHitAPlayer();
        }

        if (lifeTimeTimer.ExpiredOrNotRunning(Runner) == false && !didHitSomething)
        {
            transform.Translate(transform.right * moveSpeed * Runner.DeltaTime, Space.World);
        }

        if (lifeTimeTimer.Expired(Runner) || didHitSomething)
        {
            lifeTimeTimer = TickTimer.None;
            Runner.Despawn(Object);
        }
    }

    private void CheckIfHitGround()
    {
        var groundCollider = Runner.GetPhysicsScene2D()
            .OverlapBox(transform.position, coll.bounds.size, 0, groundLayerMask);

        if (groundCollider != null)
        {
            didHitSomething = true;
        }
    }

    private void CheckIfWeHitPlayerSafe() { }

    private void CheckIfWeHitAPlayer()
    {
        // Safety guard: avoids NullReference spam if Lag Compensation is turned off
        if (Runner.LagCompensation == null)
        {
            Debug.LogError("Lag Compensation is not enabled in NetworkProjectConfig.");
            return;
        }

        Runner.LagCompensation.OverlapBox(transform.position, coll.bounds.size, Quaternion.identity,
            Object.InputAuthority, hits, playerLayerMask);

        if (hits.Count == 0) return;

        foreach (var item in hits)
        {
            if (item.Hitbox == null) continue;

            var player = item.Hitbox.GetComponentInParent<PlayerController>();
            if (player == null) continue;

            var didNotHitOurOwnPlayer =
                player.Object.InputAuthority.PlayerId != Object.InputAuthority.PlayerId;

            if (didNotHitOurOwnPlayer && player.PlayerIsAlive)
            {
                if (Runner.IsServer &&
                    player.TryGetComponent(out PlayerHealthController health))
                {
                    health.Rpc_ReducePlayerHealth(bulletDmg);
                }

                didHitSomething = true;
                break;
            }
        }
    }
}