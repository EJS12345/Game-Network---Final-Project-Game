using UnityEngine;

public class LobbyUIManager : MonoBehaviour
{
    [SerializeField] private LoadingCavnasController loadingCanvasControllerPrefab;
    [SerializeField] private LobbyPanelBase[] lobbyPanels;

    private void Start()
    {
        foreach (var lobby in lobbyPanels)
        {
            if (lobby == null) continue;
            lobby.InitPanel(this);
        }

        if (loadingCanvasControllerPrefab != null)
        {
            Instantiate(loadingCanvasControllerPrefab);
        }
    }

    public void ShowPanel(LobbyPanelBase.LobbyPanelType type)
    {
        foreach (var lobby in lobbyPanels)
        {
            if (lobby != null && lobby.PanelType == type)
            {
                lobby.ShowPanel();
                break;
            }
        }
    }
}