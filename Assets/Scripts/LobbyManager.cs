using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;

public class LobbyManager : MonoBehaviour
{
    [Header("UI — Ortak")]
    public Button      hostLanButton;
    public Button      joinLanButton;
    public Button      hostInternetButton;
    public Button      joinInternetButton;
    public Button      startGameButton;
    public TMP_InputField ipInputField;        // LAN: IP adresi
    public TMP_InputField joinCodeInputField;  // İnternet: Join Code
    public TMP_InputField playerNameInput;     // Bu oyuncunun adı
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI playerCountText;
    public TextMeshProUGUI playerListText;
    public TextMeshProUGUI joinCodeDisplayText; // Host'un Join Code'u gösterdiği yer

    [Header("Sahne Adı")]
    public string oyunSahneAdi = "GameScene"; // Build Settings'e eklenen sahne adı
    public string lobbiSahneAdi = "LobbyScene"; // Build Settings'e eklenen lobby sahne adı

    private const int MAX_PLAYERS = 4;
    private const int MIN_PLAYERS = 2;

    // Oyuncu adlarını saklar: ClientId → isim
    // Static olduğu için sahne geçişinde kaybolmaz
    private static Dictionary<ulong, string> playerNames = new Dictionary<ulong, string>();

    public static string GetPlayerName(ulong clientId)
    {
        return playerNames.TryGetValue(clientId, out string name) ? name : $"Oyuncu {clientId}";
    }

    void Start()
    {
        if (hostLanButton      != null) hostLanButton.onClick.AddListener(StartLANHost);
        if (joinLanButton      != null) joinLanButton.onClick.AddListener(StartLANClient);
        if (hostInternetButton != null) hostInternetButton.onClick.AddListener(StartInternetHost);
        if (joinInternetButton != null) joinInternetButton.onClick.AddListener(StartInternetClient);
        if (startGameButton    != null)
        {
            startGameButton.onClick.AddListener(StartGame);
            startGameButton.gameObject.SetActive(false);
        }
        if (joinCodeDisplayText != null) joinCodeDisplayText.gameObject.SetActive(false);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback  += _ => OnConnectionChanged();
            NetworkManager.Singleton.OnClientDisconnectCallback += _ => OnConnectionChanged();
            NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
        }
    }

    // ---------------------------------------------------------------
    // Bağlantı Onay Sistemi
    // ---------------------------------------------------------------
    private void ApprovalCheck(
        NetworkManager.ConnectionApprovalRequest  request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        bool dolu = NetworkManager.Singleton.ConnectedClients.Count >= MAX_PLAYERS;
        response.Approved         = !dolu;
        response.CreatePlayerObject = false;
        response.Pending          = false;

        if (dolu)
        {
            UpdateStatus("Bağlantı Reddedildi: Lobi Dolu!");
            return;
        }

        // Client'ın adını payload'dan oku ve kaydet
        if (request.Payload != null && request.Payload.Length > 0)
        {
            string gelen = System.Text.Encoding.UTF8.GetString(request.Payload);
            playerNames[request.ClientNetworkId] = gelen;
        }
    }

    // ---------------------------------------------------------------
    // LAN — HOST
    // ---------------------------------------------------------------
    public void StartLANHost()
    {
        if (NetworkManager.Singleton == null) return;

        string adim = GetLocalPlayerName();
        playerNames[NetworkManager.Singleton.LocalClientId] = adim;
        // Host'un kendi adı payload ile gelmez, direkt kaydedilir

        NetworkManager.Singleton.StartHost();

        if (startGameButton != null) startGameButton.gameObject.SetActive(true);
        UpdateStatus("LAN Host Başlatıldı. Diğerleri IP ile bağlanabilir.");
        OnConnectionChanged();
    }

    // ---------------------------------------------------------------
    // LAN — CLIENT
    // ---------------------------------------------------------------
    public void StartLANClient()
    {
        if (NetworkManager.Singleton == null) return;

        string ip = "127.0.0.1";
        if (ipInputField != null && !string.IsNullOrEmpty(ipInputField.text))
            ip = ipInputField.text.Trim();

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null) transport.ConnectionData.Address = ip;

        // Adı payload olarak gönder
        NetworkManager.Singleton.NetworkConfig.ConnectionData =
            System.Text.Encoding.UTF8.GetBytes(GetLocalPlayerName());

        NetworkManager.Singleton.StartClient();
        UpdateStatus($"LAN: {ip} adresine bağlanılıyor...");
    }

    // ---------------------------------------------------------------
    // İNTERNET — HOST (Unity Relay)
    // Relay paketlerini kurduktan sonra aşağıdaki bloğun yorumlarını kaldır
    // ---------------------------------------------------------------
    public async void StartInternetHost()
    {
        UpdateStatus("Relay sunucusuna bağlanılıyor...");

        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        var allocation = await RelayService.Instance.CreateAllocationAsync(MAX_PLAYERS - 1);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        var relayData = new RelayServerData(allocation, "dtls");
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayData);

        string adim = GetLocalPlayerName();
        playerNames[NetworkManager.Singleton.LocalClientId] = adim;
        NetworkManager.Singleton.StartHost();

        if (startGameButton != null) startGameButton.gameObject.SetActive(true);
        if (joinCodeDisplayText != null)
        {
            joinCodeDisplayText.gameObject.SetActive(true);
            joinCodeDisplayText.text = "Join Code: " + joinCode;
        }
        UpdateStatus($"İnternet Host hazır. Join Code: {joinCode}");
        OnConnectionChanged();
    }

    // ---------------------------------------------------------------
    // İNTERNET — CLIENT (Unity Relay)
    // ---------------------------------------------------------------
    public async void StartInternetClient()
    {
        if (joinCodeInputField == null || string.IsNullOrEmpty(joinCodeInputField.text))
        {
            UpdateStatus("Lütfen Join Code girin.");
            return;
        }
        string code = joinCodeInputField.text.Trim().ToUpper();
        UpdateStatus($"Join Code ile bağlanılıyor: {code}");

        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        var joinAllocation = await RelayService.Instance.JoinAllocationAsync(code);
        var relayData = new RelayServerData(joinAllocation, "dtls");
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayData);

        NetworkManager.Singleton.NetworkConfig.ConnectionData =
            System.Text.Encoding.UTF8.GetBytes(GetLocalPlayerName());

        NetworkManager.Singleton.StartClient();
        UpdateStatus($"Relay üzerinden bağlanıldı: {code}");
    }

    // ---------------------------------------------------------------
    // Bağlantı değişikliklerinde UI güncelle
    // ---------------------------------------------------------------
    private void OnConnectionChanged()
    {
        UpdatePlayerCount();
        UpdatePlayerList();
        CheckStartGameCondition();
    }

    private void UpdatePlayerCount()
    {
        if (NetworkManager.Singleton == null) return;
        int count = NetworkManager.Singleton.ConnectedClients.Count;
        if (playerCountText != null)
            playerCountText.text = $"Oyuncular: {count} / {MAX_PLAYERS}";
    }

    private void UpdatePlayerList()
    {
        if (playerListText == null || NetworkManager.Singleton == null) return;
        string list = "Bağlı Oyuncular:\n";
        int idx = 1;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            string isim = GetPlayerName(client.ClientId);
            bool benimMi = client.ClientId == NetworkManager.Singleton.LocalClientId;
            list += $"{idx}. {isim}{(benimMi ? " ◀ SİZ" : "")}\n";
            idx++;
        }
        playerListText.text = list;
    }

    private void CheckStartGameCondition()
    {
        if (!NetworkManager.Singleton.IsServer) return;
        int count = NetworkManager.Singleton.ConnectedClients.Count;
        if (startGameButton != null)
            startGameButton.interactable = (count >= MIN_PLAYERS);
    }

    // ---------------------------------------------------------------
    // Oyunu Başlat — sadece Host çalıştırır
    // ---------------------------------------------------------------
    public void StartGame()
    {
        if (!NetworkManager.Singleton.IsServer) return;
        UpdateStatus("Oyun Sahnesi Yükleniyor...");
        // Build Settings'te oyunSahneAdi kayıtlı olmalı!
        NetworkManager.Singleton.SceneManager.LoadScene(oyunSahneAdi, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private string GetLocalPlayerName()
    {
        if (playerNameInput != null && !string.IsNullOrEmpty(playerNameInput.text))
            return playerNameInput.text.Trim();
        return "Oyuncu";
    }

    private void UpdateStatus(string mesaj)
    {
        if (statusText != null) statusText.text = mesaj;
        Debug.Log($"[Lobby] {mesaj}");
    }
}
