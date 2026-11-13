using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthController : NetworkBehaviour
{
    [SerializeField] private LayerMask deathGroundLayerMask;
    [SerializeField] private Animator bloodScreenHitAnimator;
    [SerializeField] private PlayerCameraController playerCameraController;
    [SerializeField] private Image fillAmountImg;
    [SerializeField] private TextMeshProUGUI healthAmountText;

    [Networked]
    private int currentHealthAmount { get; set; }

    private const int MAX_HEALTH_AMOUNT = 100;
    private PlayerController playerController;
    private Collider2D coll;
    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        coll = GetComponent<Collider2D>();
        playerController = GetComponent<PlayerController>();

        if (Object.HasStateAuthority)
        {
            currentHealthAmount = MAX_HEALTH_AMOUNT;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // The server alone decides when the player dies
        if (!Object.HasStateAuthority) return;
        if (!playerController.PlayerIsAlive) return;

        if (currentHealthAmount <= 0)
        {
            playerController.KillPlayer();
            return;
        }

        var didHitCollider = Runner.GetPhysicsScene2D()
            .OverlapBox(transform.position, coll.bounds.size, 0, deathGroundLayerMask);

        if (didHitCollider != default)
        {
            currentHealthAmount = 0;
        }
    }

    // Kept with the same signature because Bullet.cs calls it
    [Rpc(RpcSources.StateAuthority, RpcTargets.StateAuthority)]
    public void Rpc_ReducePlayerHealth(int damage)
    {
        if (!playerController.PlayerIsAlive) return;
        currentHealthAmount = Mathf.Max(0, currentHealthAmount - damage);
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this, out var prev, out var current))
        {
            switch (change)
            {
                case nameof(currentHealthAmount):
                    var reader = GetPropertyReader<int>(nameof(currentHealthAmount));
                    var (oldHealth, currentHealth) = reader.Read(prev, current);
                    HealthAmountChanged(oldHealth, currentHealth);
                    break;
            }
        }
    }

    private void HealthAmountChanged(int oldHealth, int currentHealth)
    {
        if (currentHealth == oldHealth) return;

        UpdateVisuals(currentHealth);

        if (currentHealth != MAX_HEALTH_AMOUNT)
        {
            PlayerGotHit();
        }
    }

    private void UpdateVisuals(int healthAmount)
    {
        var num = (float)healthAmount / MAX_HEALTH_AMOUNT;
        fillAmountImg.fillAmount = num;
        healthAmountText.text = $"{healthAmount}/{MAX_HEALTH_AMOUNT}";
    }

    private void PlayerGotHit()
    {
        if (!Utils.IsLocalPlayer(Object)) return;

        const string BLOOD_HIT_CLIP_NAME = "BloodScreenHit";
        bloodScreenHitAnimator.Play(BLOOD_HIT_CLIP_NAME);

        var shakeAmount = new Vector3(0.2f, 0.1f);
        playerCameraController.ShakeCamera(shakeAmount);
    }

    // Called from PlayerController on respawn
    public void ResetHealthAmountToMax()
    {
        if (!Object.HasStateAuthority) return;
        currentHealthAmount = MAX_HEALTH_AMOUNT;
    }
}