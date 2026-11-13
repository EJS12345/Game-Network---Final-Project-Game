using System;
using Fusion;
using TMPro;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public event Action OnGameIsOver;
    public static bool MatchIsOver { get; private set; }

    [field: SerializeField] public Collider2D CameraBounds { get; private set; }
    [SerializeField] private Camera cam;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private float matchTimerAmount = 60;

    [Networked] private TickTimer matchTimer { get; set; }
    [Networked] private NetworkBool matchOver { get; set; }

    private bool gameOverRaised;
    private bool loggedFirstSync;

    private void Awake()
    {
        if (GlobalManagers.Instance != null)
        {
            GlobalManagers.Instance.GameManager = this;
        }
    }

    public override void Spawned()
    {
        Runner.SetIsSimulated(Object, true);

        MatchIsOver = false;
        gameOverRaised = false;
        loggedFirstSync = false;

        if (cam != null) cam.gameObject.SetActive(false);
        if (timerText != null) timerText.text = "--:--";

        Debug.Log($"[GameManager] Spawned. IsServer={Runner.IsServer}, timerAmount={matchTimerAmount}");

        if (Runner.IsServer)
        {
            matchOver = false;
            matchTimer = TickTimer.CreateFromSeconds(Runner, matchTimerAmount);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Runner.IsServer || matchOver) return;

        if (matchTimer.Expired(Runner))
        {
            matchOver = true;
            matchTimer = TickTimer.None;
            Debug.Log("[GameManager] Match timer ended");
        }
    }

    public override void Render()
    {
        if (timerText == null) return;

        if (matchOver)
        {
            timerText.text = "00:00";

            if (!gameOverRaised)
            {
                gameOverRaised = true;
                MatchIsOver = true;
                OnGameIsOver?.Invoke();
            }
            return;
        }

        var remaining = matchTimer.RemainingTime(Runner);

        if (!remaining.HasValue)
        {
            timerText.text = "--:--";   // not synced yet
            return;
        }

        if (!loggedFirstSync)
        {
            loggedFirstSync = true;
            Debug.Log($"[GameManager] Timer synced. IsServer={Runner.IsServer}, remaining={remaining.Value:F1}s");
        }

        int totalSeconds = Mathf.CeilToInt(remaining.Value);
        timerText.text = $"{totalSeconds / 60:D2}:{totalSeconds % 60:D2}";
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        MatchIsOver = false;
    }
}