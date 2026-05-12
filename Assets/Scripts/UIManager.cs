using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    // 4 oyuncunun renkleri: Sarı, Kırmızı, Mavi, Yeşil
    private static readonly Color[] oyuncuRenkleri =
    {
        new Color(1f, 0.9f,  0f,  1f),
        new Color(1f, 0.3f,  0.3f,1f),
        new Color(0.3f,0.6f, 1f,  1f),
        new Color(0.3f,1f,   0.4f,1f),
    };

    [Header("Paneller")]
    public GameObject startPanel;
    public GameObject winPanel;

    [Header("Baslangic Ekrani (Single-Player fallback)")]
    public TMP_InputField inputPlayer1Name;
    public TMP_InputField inputPlayer2Name;
    public Button         btnStart;

    [Header("Oyuncu Bilgileri (index 0-3)")]
    public GameObject[]      playerPanels     = new GameObject[4]; // Her oyuncunun ana paneli
    public TextMeshProUGUI[] txtPlayerNames  = new TextMeshProUGUI[4];
    public TextMeshProUGUI[] txtPlayerStones = new TextMeshProUGUI[4];
    public Transform[]       playerStoneGrids = new Transform[4];

    [Header("Alt Çubuk")]
    public TextMeshProUGUI txtTurnIndicator;
    public TextMeshProUGUI txtHamleSure;

    [Header("Neon Tasarım")]
    public RectTransform   topBar;
    public RectTransform   bottomBar;
    public TextMeshProUGUI txtGameTitle;

    [Header("Kazanma Ekranı")]
    public TextMeshProUGUI txtWinnerName;
    public TextMeshProUGUI txtWinMessage;
    public Button          btnPlayAgain;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        bool multiplayerMod = NetworkManager.Singleton != null &&
                              (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient);

        if (multiplayerMod)
        {
            // Multiplayer: GameManager.OyunuKur zaten başlatıyor, start panel'e gerek yok
            if (startPanel != null) startPanel.SetActive(false);
        }
        else
        {
            // Single-player fallback
            if (startPanel != null) startPanel.SetActive(true);
            if (btnStart != null) btnStart.onClick.AddListener(OnStartClicked);
        }

        if (winPanel != null) winPanel.SetActive(false);
        BasliklariAyarla();
        if (btnPlayAgain != null) btnPlayAgain.onClick.AddListener(OnPlayAgainClicked);
    }

    private void BasliklariAyarla()
    {
        if (txtGameTitle != null)
        {
            if (txtGameTitle.font != null)
                txtGameTitle.fontSharedMaterial = txtGameTitle.font.material;
            txtGameTitle.color     = new Color(0.6f, 0.1f, 1f, 1f);
            txtGameTitle.fontStyle = FontStyles.Bold;
            txtGameTitle.alpha     = 1f;
        }

        if (txtTurnIndicator != null)
        {
            if (txtTurnIndicator.font != null)
                txtTurnIndicator.fontSharedMaterial = txtTurnIndicator.font.material;
            txtTurnIndicator.color     = oyuncuRenkleri[0];
            txtTurnIndicator.fontStyle = FontStyles.Bold;
            txtTurnIndicator.alpha     = 1f;
        }
    }

    // Single-player start butonu
    void OnStartClicked()
    {
        string ad1 = inputPlayer1Name != null ? inputPlayer1Name.text : "";
        string ad2 = inputPlayer2Name != null ? inputPlayer2Name.text : "";
        if (GameManager.instance != null)
            GameManager.instance.OyunuBaslat(ad1, ad2);
    }

    public void OyunEkraniniGoster()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (winPanel   != null) winPanel.SetActive(false);

        if (GameManager.instance == null) return;

        int sayisi = GameManager.instance.GetOyuncuSayisi();

        // Oyuncu listesi henüz sync olmadıysa erken çık — ArayuzuGuncelle düzeltir
        if (sayisi == 0) return;

        // Sadece oynayan oyuncuların paneli aktif olsun
        for (int i = 0; i < playerPanels.Length; i++)
        {
            if (playerPanels[i] != null)
                playerPanels[i].SetActive(i < sayisi);
        }

        for (int i = 0; i < sayisi; i++)
        {
            if (i < playerStoneGrids.Length && playerStoneGrids[i] != null)
            {
                int prefabIdx = i % GameManager.instance.miknatisPrefablar.Length;
                PlayerData pd = GameManager.instance.GetOyuncu(i);
                TasIkonlariniOlustur(playerStoneGrids[i], pd.KalanTas, GameManager.instance.miknatisPrefablar[prefabIdx]);
            }
        }
    }

    public void ArayuzuGuncelle()
    {
        if (GameManager.instance == null) return;

        int sayisi = GameManager.instance.GetOyuncuSayisi();
        if (sayisi == 0) return;

        // Panel görünürlüğünü her zaman güncelle — geç sync durumunda panelleri açar
        for (int i = 0; i < playerPanels.Length; i++)
        {
            if (playerPanels[i] != null)
                playerPanels[i].SetActive(i < sayisi);
        }

        for (int i = 0; i < sayisi; i++)
        {
            PlayerData pd = GameManager.instance.GetOyuncu(i);

            if (i < txtPlayerNames.Length && txtPlayerNames[i] != null)
                txtPlayerNames[i].text = pd.PlayerName.ToString().ToUpper();

            if (i < txtPlayerStones.Length && txtPlayerStones[i] != null)
                txtPlayerStones[i].text = "Kalan: " + pd.KalanTas;

            if (i < playerStoneGrids.Length && playerStoneGrids[i] != null)
            {
                int prefabIdx = i % GameManager.instance.miknatisPrefablar.Length;
                // İkon yoksa oluştur, varsa güncelle
                if (playerStoneGrids[i].childCount == 0)
                    TasIkonlariniOlustur(playerStoneGrids[i], pd.KalanTas, GameManager.instance.miknatisPrefablar[prefabIdx]);
                else
                    TasIkonlariniGuncelle(playerStoneGrids[i], pd.KalanTas, GameManager.instance.miknatisPrefablar[prefabIdx]);
            }
        }

        // Sıra göstergesi
        var aktif = GameManager.instance.GetAktifOyuncu();
        if (aktif != null && txtTurnIndicator != null)
        {
            int idx = GameManager.instance.GetAktifOyuncuIndex();
            txtTurnIndicator.text  = aktif.Value.PlayerName.ToString().ToUpper();
            txtTurnIndicator.color = oyuncuRenkleri[idx % oyuncuRenkleri.Length];
            txtTurnIndicator.alpha = 1f;
        }
    }

    public void HamleSuresiGuncelle(int saniye)
    {
        if (txtHamleSure != null)
        {
            txtHamleSure.text  = saniye + "s";
            txtHamleSure.color = saniye <= 5 ? new Color(1f, 0.2f, 0.2f, 1f) : Color.white;
        }
    }

    public void KazanmaEkraniniGoster(string kazananAdi)
    {
        if (winPanel      != null) winPanel.SetActive(true);
        if (txtWinnerName != null) txtWinnerName.text = kazananAdi.ToUpper();
        if (txtWinMessage != null) txtWinMessage.text = "KAZANDINIZ!";
    }

    void OnPlayAgainClicked()
    {
        if (GameManager.instance != null)
            GameManager.instance.YenidenOyna();
    }

    // ---------------------------------------------------------------
    // Taş ikonları
    // ---------------------------------------------------------------
    void TasIkonlariniOlustur(Transform grid, int adet, GameObject prefab3D)
    {
        if (grid == null) return;
        foreach (Transform child in grid) Destroy(child.gameObject);

        for (int i = 0; i < adet; i++)
        {
            GameObject cell = new GameObject("TasHucresi_" + i, typeof(RectTransform));
            cell.layer = LayerMask.NameToLayer("UI");
            cell.transform.SetParent(grid, false);

            if (prefab3D != null)
            {
                GameObject tas3D = Instantiate(prefab3D, cell.transform);
                tas3D.transform.localScale    = new Vector3(50f, 50f, 50f);
                tas3D.transform.localPosition = new Vector3(0, 0, -1f);

                Rigidbody rb = tas3D.GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

                Magnet mag = tas3D.GetComponent<Magnet>();
                if (mag != null) mag.enabled = false;

                IrregularStone stone = tas3D.GetComponent<IrregularStone>();
                if (stone != null) stone.enabled = false;

                // UI ikonu networke katılmamalı
                NetworkObject netObj = tas3D.GetComponent<NetworkObject>();
                if (netObj != null) Destroy(netObj);
            }
        }
    }

    void TasIkonlariniGuncelle(Transform grid, int kalanTas, GameObject prefab3D)
    {
        if (grid == null) return;
        while (grid.childCount < kalanTas)
        {
            GameObject cell = new GameObject("TasHucresi_Ekstra", typeof(RectTransform));
            cell.layer = LayerMask.NameToLayer("UI");
            cell.transform.SetParent(grid, false);
            if (prefab3D != null)
            {
                GameObject tas3D = Instantiate(prefab3D, cell.transform);
                tas3D.transform.localScale    = new Vector3(50f, 50f, 50f);
                tas3D.transform.localPosition = new Vector3(0, 0, -1f);
                Rigidbody rb = tas3D.GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
                IrregularStone stone = tas3D.GetComponent<IrregularStone>();
                if (stone != null) stone.enabled = false;
                NetworkObject netObj = tas3D.GetComponent<NetworkObject>();
                if (netObj != null) Destroy(netObj);
            }
        }
        for (int i = 0; i < grid.childCount; i++)
            grid.GetChild(i).gameObject.SetActive(i < kalanTas);
    }
}
