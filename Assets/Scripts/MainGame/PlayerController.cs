using System;
using System.Collections.Generic;
using Fusion;
// Removed 'using Fusion.Addons.Physics;' because it conflicts with the 2D setup and was uninstalled.
using TMPro;
using UnityEngine;

public class PlayerController : NetworkBehaviour, IBeforeUpdate
{
    public bool AcceptAnyInput => PlayerIsAlive && !GameManager.MatchIsOver && !playerChatController.IsTyping;

    [SerializeField] private PlayerChatController playerChatController;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private GameObject cam;
    [SerializeField] private float moveSpeed = 6;
    [SerializeField] private float jumpForce = 1000;

    [Header("Grounded Vars")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundDetectionObj;

    [Networked] public TickTimer RespawnTimer { get; private set; }
    [Networked] public NetworkBool PlayerIsAlive { get; private set; }

    [Networked]
    private NetworkString<_8> playerName { get; set; }

    [Networked] private NetworkButtons buttonsPrev { get; set; }
    [Networked] private Vector2 serverNextSpawnPoint { get; set; }
    [Networked] private NetworkBool isGrounded { get; set; }
    [Networked] private TickTimer respawnToNewPointTimer { get; set; }

    private float horizontal;
    private Rigidbody2D rigid;
    private PlayerWeaponController playerWeaponController;
    private PlayerVisualController playerVisualController;
    private PlayerHealthController playerHealthController;
    private ChangeDetector _changeDetector;

    public enum PlayerInputButtons
    {
        None,
        Jump,
        Shoot
    }

    public override void Spawned()
    {
        Runner.SetIsSimulated(Object, true);

        rigid = GetComponent<Rigidbody2D>();
        playerWeaponController = GetComponent<PlayerWeaponController>();
        playerVisualController = GetComponent<PlayerVisualController>();
        playerHealthController = GetComponent<PlayerHealthController>();
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState, false);

        GlobalManagers.Instance.PlayerSpawnerController.AddToEntry(base.Object.InputAuthority, base.Object);

        SetLocalObjects();
        PlayerIsAlive = true;
    }

    private void SetLocalObjects()
    {
        if (Utils.IsLocalPlayer(Object))
        {
            cam.transform.SetParent(null);
            cam.SetActive(true);

            var nickName = GlobalManagers.Instance.NetworkRunnerController.LocalPlayerNickname;
            RpcSetNickName(nickName);
        }
        else
        {
            base.Object.RenderSource = RenderSource.Interpolated;
            base.Object.ForceRemoteRenderTimeframe = true;
        }
    }

    [Rpc(sources: RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcSetNickName(NetworkString<_8> nickName)
    {
        playerName = nickName;
    }

    private void OnNicknameChanged(NetworkString<_8> nickName)
    {
        SetPlayerNickname(nickName);
    }

    private void SetPlayerNickname(NetworkString<_8> nickName)
    {
        playerNameText.text = nickName + " " + Object.InputAuthority.PlayerId;
    }

    public void KillPlayer()
    {
        const int RESPAWN_AMOUNT = 5;
        if (Runner.IsServer)
        {
            serverNextSpawnPoint = GlobalManagers.Instance.PlayerSpawnerController.GetRandomSpawnPoint();
            respawnToNewPointTimer = TickTimer.CreateFromSeconds(Runner, RESPAWN_AMOUNT - 1);
        }

        PlayerIsAlive = false;
        rigid.simulated = false;
        playerVisualController.TriggerDieAnimation();
        RespawnTimer = TickTimer.CreateFromSeconds(Runner, RESPAWN_AMOUNT);
    }

    public void BeforeUpdate()
    {
        if (Utils.IsLocalPlayer(Object) && AcceptAnyInput)
        {
            const string HORIZONTAL = "Horizontal";
            horizontal = Input.GetAxisRaw(HORIZONTAL);
        }
    }

    public override void FixedUpdateNetwork()
    {
        CheckRespawnTimer();

        if (Runner.TryGetInputForPlayer<PlayerData>(Object.InputAuthority, out var input))
        {
            if (AcceptAnyInput)
            {
                rigid.linearVelocity = new Vector2(input.HorizontalInput * moveSpeed, rigid.linearVelocity.y);

                CheckJumpInput(input);

                buttonsPrev = input.NetworkButtons;
            }
            else
            {
                rigid.linearVelocity = Vector2.zero;
            }
        }

        playerVisualController.UpdateScaleTransforms(rigid.linearVelocity);
    }

    private void CheckRespawnTimer()
    {
        if (PlayerIsAlive) return;

        if (respawnToNewPointTimer.Expired(Runner))
        {
            // FIX: Removed the dependency on 'NetworkRigidbody' and directly teleported the native 2D Rigidbody instead.
            rigid.position = serverNextSpawnPoint;

            respawnToNewPointTimer = TickTimer.None;
        }

        if (RespawnTimer.Expired(Runner))
        {
            RespawnTimer = TickTimer.None;
            RespawnPlayer();
        }
    }

    private void RespawnPlayer()
    {
        PlayerIsAlive = true;
        rigid.simulated = true;
        playerVisualController.TriggerRespawnAnimation();
        playerHealthController.ResetHealthAmountToMax();
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this, out var prev, out var current))
        {
            switch (change)
            {
                case nameof(playerName):
                    var reader = GetPropertyReader<NetworkString<_8>>(nameof(playerName));
                    var (oldName, currentName) = reader.Read(prev, current);
                    OnNicknameChanged(currentName);
                    break;
            }
        }

        playerVisualController.RendererVisuals(rigid.linearVelocity, playerWeaponController.IsHoldingShootingKey);
    }

    private void CheckJumpInput(PlayerData input)
    {
        var transform1 = groundDetectionObj.transform;
        isGrounded = (bool)Runner.GetPhysicsScene2D().OverlapBox(transform1.position,
            transform1.localScale, 0, groundLayer);

        if (isGrounded)
        {
            var pressed = input.NetworkButtons.GetPressed(buttonsPrev);
            if (pressed.WasPressed(buttonsPrev, PlayerInputButtons.Jump))
            {
                rigid.AddForce(Vector2.up * jumpForce, ForceMode2D.Force);
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        GlobalManagers.Instance.ObjectPoolingManager.RemoveNetworkObjectFromDic(Object);
        Destroy(gameObject);
    }

    public PlayerData GetPlayerNetworkInput()
    {
        PlayerData data = new PlayerData();
        data.HorizontalInput = horizontal;
        data.GunPivotRotation = playerWeaponController.LocalQuaternionPivotRot;
        data.NetworkButtons.Set(PlayerInputButtons.Jump, Input.GetKey(KeyCode.Space));
        data.NetworkButtons.Set(PlayerInputButtons.Shoot, Input.GetButton("Fire1"));
        return data;
    }
}