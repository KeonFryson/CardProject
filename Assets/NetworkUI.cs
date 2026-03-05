using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NetworkUI : MonoBehaviour
{
    [SerializeField] public Button hostButton;
    [SerializeField] public Button ClientButton;
    [SerializeField] public Button ServerButton;

    public void Awake()
    {
        hostButton.onClick.AddListener(StartHost);
        var hostbuttonText = hostButton.GetComponentInChildren<TextMeshProUGUI>();
        if (hostbuttonText != null)
        {
            hostbuttonText.text = "Host";
        }
        ClientButton.onClick.AddListener(StartClient);
            var clientbuttonText = ClientButton.GetComponentInChildren<TextMeshProUGUI>();
        if (clientbuttonText != null)
        {
            clientbuttonText.text = "Client";
        }
        ServerButton.onClick.AddListener(StartServer);
        var serverbuttonText = ServerButton.GetComponentInChildren<TextMeshProUGUI>();
        if (serverbuttonText != null)
        {
            serverbuttonText.text = "Server";
        }
    }

    public void StartHost()
    {
        Debug.Log("Starting Host...");
        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.LoadScene("CardPlay", LoadSceneMode.Single);
    }

    public void StartClient()
    {
        Debug.Log("Starting Client...");
        NetworkManager.Singleton.StartClient();
        
    }

    public void StartServer()
    {
        Debug.Log("Starting Server...");
        NetworkManager.Singleton.StartServer();
        NetworkManager.Singleton.SceneManager.LoadScene("CardPlay", LoadSceneMode.Single);
    }
}