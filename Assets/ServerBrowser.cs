using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;

public class ServerBrowser : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button createServerButton;
    [SerializeField] private Button joinServerButton;
    [SerializeField] private InputField addressInputField;

    private void Start()
    {
        if (createServerButton != null)
            createServerButton.onClick.AddListener(CreateServer);

        if (joinServerButton != null)
            joinServerButton.onClick.AddListener(JoinServer);
    }

    public void CreateServer()
    {
        if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
        {
            NetworkManager.Singleton.StartHost();
            Debug.Log("Server created and hosting started.");
        }
        else
        {
            Debug.LogWarning("Already running as server or client.");
        }
    }

    public void JoinServer()
    {
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            string address = "127.0.0.1";
            if (addressInputField != null && !string.IsNullOrEmpty(addressInputField.text))
                address = addressInputField.text;

            // Use UnityTransport instead of the deprecated UNetTransport
            var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            transport.SetConnectionData(address, 7777); // port number, change as needed

            NetworkManager.Singleton.StartClient();
            Debug.Log($"Attempting to join server at {address}");
        }
        else
        {
            Debug.LogWarning("Already running as server or client.");
        }
    }
}