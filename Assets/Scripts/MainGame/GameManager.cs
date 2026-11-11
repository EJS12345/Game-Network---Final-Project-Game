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

    // Local only: makes sure the game-over event fires once per machine.
    private bool gameOverRaised;

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

        if (cam != null) cam.gameObject.SetActive(false);
        if (timerText != null) timerText.text = "--:--";

        if (Runner.IsServer)
        {
            matchOver = false;
            matchTimer = TickTimer.CreateFromSeconds(Runner, matchTimerAmount);
        }
    }

    // Only the server decides when the match ends.
    public override void FixedUpdateNetwork()
    {
        if (!Runner.IsServer || matchOver) return;

        if (matchTimer.Expired(Runner))
        {
            matchOver = true;
            matchTimer = TickTimer.None;
            Debug.Log("Match timer had ended");
        }
    }

    // UI and the game-over event run once per frame on every player.
    public override void Render()
    {
        if (matchOver)
        {
            if (timerText != null) timerText.text = "00:00";

            if (!gameOverRaised)
            {
                gameOverRaised = true;
                MatchIsOver = true;
                OnGameIsOver?.Invoke();
            }
            return;
        }

        var remaining = matchTimer.RemainingTime(Runner);

        // Timer hasn't synced yet (late join): keep the placeholder.
        if (!remaining.HasValue) return;

        int totalSeconds = Mathf.CeilToInt(remaining.Value);
        timerText.text = $"{totalSeconds / 60:D2}:{totalSeconds % 60:D2}";
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        MatchIsOver = false;
    }
}