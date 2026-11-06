using UnityEngine;
using UnityEngine.UI;

public class LoadingCavnasController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Button cancelBtn;
    private NetworkRunnerController networkRunnerController;

    private void Start()
    {
        networkRunnerController = GlobalManagers.Instance.NetworkRunnerController;
        networkRunnerController.OnStartedRunnerConnection += OnStartedRunnerConnection;
        networkRunnerController.OnPlayerJoinedSuccessfully += OnPlayerJoinedSuccessfully;
        networkRunnerController.OnStartGameFailed += OnStartGameFailed;

        cancelBtn.onClick.AddListener(networkRunnerController.ShutDownRunner);
        this.gameObject.SetActive(false);
    }

    private void OnStartedRunnerConnection()
    {
        this.gameObject.SetActive(true);
        const string CLIP_NAME = "In";
        StartCoroutine(Utils.PlayAnimAndSetStateWhenFinished(gameObject, animator, CLIP_NAME));
    }

    private void OnPlayerJoinedSuccessfully()
    {
        HideLoading();
    }

    // Join was declined, failed or cancelled: hide the loading screen.
    private void OnStartGameFailed(string message)
    {
        HideLoading();
    }

    private void HideLoading()
    {
        if (!gameObject.activeInHierarchy) return;

        const string CLIP_NAME = "Out";
        StartCoroutine(Utils.PlayAnimAndSetStateWhenFinished(gameObject, animator, CLIP_NAME, false));
    }

    private void OnDestroy()
    {
        if (networkRunnerController == null) return;

        networkRunnerController.OnStartedRunnerConnection -= OnStartedRunnerConnection;
        networkRunnerController.OnPlayerJoinedSuccessfully -= OnPlayerJoinedSuccessfully;
        networkRunnerController.OnStartGameFailed -= OnStartGameFailed;
    }
}