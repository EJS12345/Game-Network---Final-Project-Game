using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkRunnerController : MonoBehaviour, INetworkRunnerCallbacks
{
    public event Action OnStartedRunnerConnection;
    public event Action OnPlayerJoinedSuccessfully;

    // Raised when a start/join is declined or fails. The string is a player-friendly
    // message (empty when the player simply cancelled).
    public event Action<string> OnStartGameFailed;

    public string LocalPlayerNickname { get; private set; }

    [SerializeField] private NetworkRunner networkRunnerPrefab;

    [Header("Scene Settings")]
    [Tooltip("The Build Index of your MainGame scene (Check File > Build Settings)")]
    [SerializeField] private int mainGameSceneIndex = 1;

    private const string LOBBY_SCENE = "Lobby";
    private const int MAX_ROOM_NAME_LENGTH = 32;

    private NetworkRunner networkRunnerInstance;
    private bool isStarting;
    private bool cancelRequested;

    public void ShutDownRunner()
    {
        cancelRequested = isStarting;

        if (networkRunnerInstance != null)
        {
            networkRunnerInstance.Shutdown();
        }
    }

    public void SetPlayerNickname(string str)
    {
        LocalPlayerNickname = str;
    }

    public async void StartGame(GameMode mode, string roomName)
    {
        // Matchmaking already in progress: ignore extra clicks.
        if (isStarting)
        {
            Debug.Log("A connection is already in progress, please wait.");
            return;
        }

        roomName = roomName?.Trim();

        // Join Room needs a real code. Random / Create may pass an empty one.
        if (mode == GameMode.Client)
        {
            if (string.IsNullOrEmpty(roomName))
            {
                Fail("Enter a room code first.");
                return;
            }

            if (roomName.Length > MAX_ROOM_NAME_LENGTH)
            {
                Fail("That room code is too long.");
                return;
            }
        }

        isStarting = true;
        cancelRequested = false;
        OnStartedRunnerConnection?.Invoke();

        try
        {
            if (networkRunnerInstance == null)
            {
                networkRunnerInstance = Instantiate(networkRunnerPrefab);
                networkRunnerInstance.AddCallbacks(this);
            }

            networkRunnerInstance.ProvideInput = true;

            Debug.Log($"StartGame -> mode: {mode}, room: '{roomName}'");

            var startGameArgs = new StartGameArgs()
            {
                GameMode = mode,
                SessionName = roomName,
                PlayerCount = 4,
                SceneManager = networkRunnerInstance.GetComponent<INetworkSceneManager>(),
                ObjectProvider = networkRunnerInstance.GetComponent<INetworkObjectProvider>()
            };

            var result = await networkRunnerInstance.StartGame(startGameArgs);

            if (result.Ok)
            {
                if (networkRunnerInstance != null && networkRunnerInstance.IsServer)
                {
                    var unitySceneParams = new LoadSceneParameters(LoadSceneMode.Single);
                    await networkRunnerInstance.LoadScene(SceneRef.FromIndex(mainGameSceneIndex), unitySceneParams);
                }
                return;
            }

            // Failed or cancelled: clean up and tell the UI.
            CleanupRunner();

            if (cancelRequested)
            {
                OnStartGameFailed?.Invoke(string.Empty);
            }
            else
            {
                Debug.LogWarning($"Failed to start: {result.ShutdownReason}");
                Fail(GetFriendlyMessage(result.ShutdownReason, roomName));
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            CleanupRunner();
            Fail("Something went wrong while connecting. Please try again.");
        }
        finally
        {
            isStarting = false;
        }
    }

    private static string GetFriendlyMessage(ShutdownReason reason, string roomName)
    {
        switch (reason)
        {
            case ShutdownReason.GameNotFound:
                return $"Room '{roomName}' doesn't exist (yet). Check the code, or wait for the host to create it.";
            case ShutdownReason.GameClosed:
                return "That room is closed.";
            case ShutdownReason.GameIsFull:
                return "That room is full.";
            default:
                return "Couldn't connect. Please try again.";
        }
    }

    private void Fail(string message)
    {
        Debug.LogWarning(message);
        OnStartGameFailed?.Invoke(message);
    }

    private void CleanupRunner()
    {
        if (networkRunnerInstance != null)
        {
            Destroy(networkRunnerInstance.gameObject);
        }
        networkRunnerInstance = null;
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log("OnPlayerJoined");
        OnPlayerJoinedSuccessfully?.Invoke();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log("OnPlayerLeft");
    }

    public void OnInput(NetworkRunner runner, NetworkInput input) { }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
        Debug.Log("OnInputMissing");
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"OnShutdown ({shutdownReason})");

        if (runner == networkRunnerInstance)
        {
            networkRunnerInstance = null;
        }

        // Only go back to the lobby if we're not already there.
        // Reloading it after a failed join would wipe the loading/error UI.
        if (SceneManager.GetActiveScene().name != LOBBY_SCENE)
        {
            SceneManager.LoadScene(LOBBY_SCENE);
        }
    }

    public void OnConnectedToServer(NetworkRunner runner) { Debug.Log("OnConnectedToServer"); }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { Debug.Log("OnDisconnectedFromServer"); }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { Debug.Log("OnConnectRequest"); }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { Debug.Log("OnConnectFailed"); }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    public void OnSceneLoadDone(NetworkRunner runner) { Debug.Log("OnSceneLoadDone"); }

    public void OnSceneLoadStart(NetworkRunner runner) { Debug.Log("OnSceneLoadStart"); }
}