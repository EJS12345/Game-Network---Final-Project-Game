using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkRunnerController : MonoBehaviour, INetworkRunnerCallbacks
{
    public event Action OnStartedRunnerConnection;
    public event Action OnPlayerJoinedSuccessfully;

    // Raised when a start/join is declined or fails. Empty string = player cancelled
    public event Action<string> OnStartGameFailed;

    public string LocalPlayerNickname { get; private set; }

    [SerializeField] private NetworkRunner networkRunnerPrefab;

    [Header("Scene Settings")]
    [Tooltip("The Build Index of your MainGame scene (Check File > Build Settings)")]
    [SerializeField] private int mainGameSceneIndex = 1;

    private const string LOBBY_SCENE = "Lobby";
    private const int MAX_ROOM_NAME_LENGTH = 32;
    private const float START_TIMEOUT_SECONDS = 20f;

    private NetworkRunner networkRunnerInstance;
    private bool isStarting;
    private bool cancelRequested;

    public bool IsStarting => isStarting;

    public void ShutDownRunner()
    {
        if (!isStarting) return;

        cancelRequested = true;

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
        if (isStarting)
        {
            Debug.Log("A connection is already in progress, please wait.");
            return;
        }

        roomName = roomName?.Trim();

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

        CleanupRunner(networkRunnerInstance);
        networkRunnerInstance = null;

        Raise(OnStartedRunnerConnection);

        NetworkRunner runner = null;

        try
        {
            runner = Instantiate(networkRunnerPrefab);
            networkRunnerInstance = runner;
            runner.AddCallbacks(this);
            runner.ProvideInput = true;

            Debug.Log($"StartGame -> mode: {mode}, room: '{roomName}'");

            var startGameArgs = new StartGameArgs()
            {
                GameMode = mode,
                SessionName = roomName,
                PlayerCount = 4,
                SceneManager = runner.GetComponent<INetworkSceneManager>(),
                ObjectProvider = runner.GetComponent<INetworkObjectProvider>()
            };

            var startTask = runner.StartGame(startGameArgs);
            var finished = await Task.WhenAny(startTask, Task.Delay(TimeSpan.FromSeconds(START_TIMEOUT_SECONDS)));

            if (finished != startTask)
            {
                startTask.ContinueWith(t => { var _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                runner.Shutdown();
                CleanupRunner(runner);
                if (networkRunnerInstance == runner) networkRunnerInstance = null;
                Fail("The connection timed out. Please try again.");
                return;
            }

            var result = await startTask;

            if (result.Ok)
            {
                if (runner != null && runner.IsServer)
                {
                    var unitySceneParams = new LoadSceneParameters(LoadSceneMode.Single);
                    await runner.LoadScene(SceneRef.FromIndex(mainGameSceneIndex), unitySceneParams);
                }
                return;
            }

            CleanupRunner(runner);
            if (networkRunnerInstance == runner) networkRunnerInstance = null;

            if (cancelRequested)
            {
                Raise(OnStartGameFailed, string.Empty);
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
            CleanupRunner(runner);
            if (networkRunnerInstance == runner) networkRunnerInstance = null;
            Fail("Something went wrong while connecting. Please try again.");
        }
        finally
        {
            isStarting = false;
            cancelRequested = false;
        }
    }

    private static string GetFriendlyMessage(ShutdownReason reason, string roomName)
    {
        switch (reason)
        {
            case ShutdownReason.GameNotFound:
                return $"Room '{roomName}' doesn't exist (yet). Check the code, or wait for the host to create it.";
            case ShutdownReason.GameIdAlreadyExists:
                return $"A room named '{roomName}' already exists. Pick another name or join it instead.";
            case ShutdownReason.GameClosed:
                return "That room is closed.";
            case ShutdownReason.GameIsFull:
                return "That room is full.";
            default:
                return "Couldn't connect. Please try again.";
        }
    }

    // Calls every subscriber separately so one broken handler can't block the rest
    private static void Raise(Action action)
    {
        if (action == null) return;
        foreach (Action handler in action.GetInvocationList())
        {
            try { handler(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    private static void Raise(Action<string> action, string message)
    {
        if (action == null) return;
        foreach (Action<string> handler in action.GetInvocationList())
        {
            try { handler(message); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    private void Fail(string message)
    {
        Debug.LogWarning(message);
        Raise(OnStartGameFailed, message);
    }

    private static void CleanupRunner(NetworkRunner runner)
    {
        if (runner == null) return;
        Destroy(runner.gameObject);
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log("OnPlayerJoined");
        Raise(OnPlayerJoinedSuccessfully);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { Debug.Log("OnPlayerLeft"); }

    public void OnInput(NetworkRunner runner, NetworkInput input) { }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"OnShutdown ({shutdownReason})");

        if (!isStarting && runner == networkRunnerInstance)
        {
            networkRunnerInstance = null;
        }

        if (SceneManager.GetActiveScene().name != LOBBY_SCENE)
        {
            SceneManager.LoadScene(LOBBY_SCENE);
        }
    }

    public void OnConnectedToServer(NetworkRunner runner) { Debug.Log("OnConnectedToServer"); }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { Debug.Log("OnDisconnectedFromServer"); }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

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