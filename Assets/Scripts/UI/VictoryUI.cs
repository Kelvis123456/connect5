using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class VictoryUI : MonoBehaviour
{
    [SerializeField] private GameObject          _panel;
    [SerializeField] private TextMeshProUGUI     _titleText;
    [SerializeField] private TextMeshProUGUI     _subtitleText;
    [SerializeField] private Button              _btnPlayAgain;
    [SerializeField] private Button              _btnLobby;

    private void Awake()
    {
        _panel.SetActive(false);
        _btnPlayAgain.onClick.AddListener(OnPlayAgain);
        _btnLobby.onClick.AddListener(OnLobby);
    }

    public void ShowWin(string winnerName)
    {
        _titleText.text    = "¡Victoria!";
        _subtitleText.text = $"{winnerName} ganó la partida";
        _btnPlayAgain.gameObject.SetActive(GameSettings.Mode != GameMode.Online);
        _panel.SetActive(true);
    }

    public void ShowDraw()
    {
        _titleText.text    = "¡Empate!";
        _subtitleText.text = "Nadie ganó esta vez";
        _btnPlayAgain.gameObject.SetActive(GameSettings.Mode != GameMode.Online);
        _panel.SetActive(true);
    }

    private void OnPlayAgain()
    {
        // Reload the scene — resets board visuals and game state.
        // GameSettings.Mode / LaunchedFromMenu are static and persist.
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnLobby()
    {
        if (GameSettings.Mode == GameMode.Online && RelayManager.Instance != null)
            RelayManager.Instance.Disconnect();

        GameSettings.LaunchedFromMenu = false;
        GameSettings.ResetScores();
        SceneManager.LoadScene("LobbyScene");
    }
}
