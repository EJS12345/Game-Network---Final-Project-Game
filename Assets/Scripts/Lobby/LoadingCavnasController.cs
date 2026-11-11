using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LoadingCavnasController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Button cancelBtn;

    private const float FORCE_HIDE_DELAY = 1f;

    private NetworkRunnerController networkRunnerController;
    private bool isShown;

    private void Start()
    {
        networkRunnerController = GlobalManagers.Instance.NetworkRunnerController;
        networkRunnerController.OnStartedRunnerConnection += OnStartedRunnerConnection;
        networkRunnerController.OnPlayerJoinedSuccessfully += OnPlayerJoinedSuccessfully;
        networkRunnerController.OnStartGameFailed += OnStartGameFailed;

        cancelBtn.onClick.AddListener(networkRunnerController.ShutDownRunner);
        gameObject.SetActive(false);
    }

    // Safety net: if an event was missed, hide as soon as matchmaking is no longer running.
    private void Update()
    {
        if (isShown && networkRunnerController != null && !networkRunnerController.IsStarting)
        {
            HideLoading();
        }
    }

    private void OnStartedRunnerConnection()
    {
        gameObject.SetActive(true);
        StopAllCoroutines();
        isShown = true;

        const string CLIP_NAME = "In";
        StartCoroutine(Utils.PlayAnimAndSetStateWhenFinished(gameObject, animator, CLIP_NAME));
    }

    private void OnPlayerJoinedSuccessfully()
    {
        HideLoading();
    }

    private void OnStartGameFailed(string message)
    {
        HideLoading();
    }

    private void HideLoading()
    {
        isShown = false;

        if (!gameObject.activeInHierarchy) return;

        StopAllCoroutines();

        const string CLIP_NAME = "Out";
        StartCoroutine(Utils.PlayAnimAndSetStateWhenFinished(gameObject, animator, CLIP_NAME, false));
        StartCoroutine(ForceHide());
    }

    private IEnumerator ForceHide()
    {
        yield return new WaitForSecondsRealtime(FORCE_HIDE_DELAY);
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (networkRunnerController == null) return;

        networkRunnerController.OnStartedRunnerConnection -= OnStartedRunnerConnection;
        networkRunnerController.OnPlayerJoinedSuccessfully -= OnPlayerJoinedSuccessfully;
        networkRunnerController.OnStartGameFailed -= OnStartGameFailed;
    }
}