using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    public static GameManager instance;

    [Header("Miknatıs Prefabları (0=P1, 1=P2, 2=P3, 3=P4)")]
    public GameObject[] miknatisPrefablar = new GameObject[4];

    [Header("Süre Ayarları")]
    public float toplamHamleSuresi = 15f;
    public LayerMask zeminKatmani;

    // Server-side only: son tas koyan kimin oldugunu hatirla (ceza icin)
    private int sonOynayanOyuncuIndex = 0;

    // Network Variables — sadece Server yazar, herkes okur
    private NetworkVariable<int> aktifOyuncuIndex = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> oyunBasladi = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> oyunBitti = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> kalanHamleSuresi = new NetworkVariable<float>(
        15f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkList<PlayerData> oyuncular;

    void Awake()
    {
        instance = this;
        oyuncular = new NetworkList<PlayerData>();
    }

    public override void OnNetworkSpawn()
    {
        // Tüm client'lar degisikliklere abone olur
        aktifOyuncuIndex.OnValueChanged += (_, __) => UIManager.instance?.ArayuzuGuncelle();
        kalanHamleSuresi.OnValueChanged += (_, yeni) =>
            UIManager.instance?.HamleSuresiGuncelle(Mathf.CeilToInt(yeni));
        oyunBasladi.OnValueChanged += (_, yeni) =>
        { if (yeni) UIManager.instance?.OyunEkraniniGoster(); };
        oyuncular.OnListChanged += (_) =>
        {
            UIManager.instance?.ArayuzuGuncelle();
            // Client'ta liste güncellenince panelleri ve ikonları da yenile
            if (!IsServer && oyuncular.Count > 0)
                UIManager.instance?.OyunEkraniniGoster();
        };

        if (IsServer)
            StartCoroutine(OyunuKur());
        else
            StartCoroutine(ClientUIOlustur());
    }

    IEnumerator ClientUIOlustur()
    {
        // Oyuncu listesi sync olana kadar bekle (max 5 saniye, 0.1s aralıklarla)
        float beklemeSuresi = 0f;
        while (oyuncular.Count == 0 && beklemeSuresi < 5f)
        {
            yield return new WaitForSeconds(0.1f);
            beklemeSuresi += 0.1f;
        }
        UIManager.instance?.OyunEkraniniGoster();
        UIManager.instance?.ArayuzuGuncelle();
        UIManager.instance?.HamleSuresiGuncelle(Mathf.CeilToInt(kalanHamleSuresi.Value));
    }

    IEnumerator OyunuKur()
    {
        // Tüm client'ların sahneyi yüklemesini bekle
        yield return new WaitForSeconds(0.8f);

        oyuncular.Clear();
        int oyuncuSayisi = NetworkManager.ConnectedClientsList.Count;

        // Oyuncu sayısına göre başlangıç taş miktarı
        int baslangicTas = oyuncuSayisi <= 2 ? 10 : (oyuncuSayisi == 3 ? 11 : 12);

        // Oyuncu sayısına göre saha boyutunu ayarla (server + tüm clientlar)
        if (KlusterSahaIpi.instance != null)
            KlusterSahaIpi.instance.BoyutuAyarla(oyuncuSayisi);
        float sahaX = KlusterSahaIpi.instance != null ? KlusterSahaIpi.instance.yaricapX : 11f;
        float sahaZ = KlusterSahaIpi.instance != null ? KlusterSahaIpi.instance.yaricapZ : 7.5f;
        SahaBoyutuGuncelleClientRpc(sahaX, sahaZ);

        int index = 0;
        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            oyuncular.Add(new PlayerData
            {
                ClientId    = client.ClientId,
                PlayerName  = LobbyManager.GetPlayerName(client.ClientId),
                PlayerIndex = index,
                KalanTas    = baslangicTas
            });
            index++;
        }

        aktifOyuncuIndex.Value  = 0;
        kalanHamleSuresi.Value  = toplamHamleSuresi;
        oyunBasladi.Value       = true;
    }

    void Update()
    {
        if (!IsServer || !oyunBasladi.Value || oyunBitti.Value) return;

        kalanHamleSuresi.Value -= Time.deltaTime;
        if (kalanHamleSuresi.Value <= 0f)
            SirayiDegistirServer();
    }

    void FixedUpdate()
    {
        if (!IsServer || !oyunBasladi.Value || oyunBitti.Value) return;
        KarpismalaraKontrolEt();
    }

    // ---------------------------------------------------------------
    // SERVER RPC — herhangi bir client tarafindan cagrilabilir
    // ---------------------------------------------------------------
    [ServerRpc(RequireOwnership = false)]
    public void TasKoyServerRpc(Vector3 pozisyon, ServerRpcParams rpcParams = default)
    {
        ulong gondericId = rpcParams.Receive.SenderClientId;

        if (oyunBitti.Value || !oyunBasladi.Value || oyuncular.Count == 0) return;

        PlayerData aktif = oyuncular[aktifOyuncuIndex.Value];

        // Sadece sirasi gelen oyuncu tas koyabilir
        if (aktif.ClientId != gondericId) return;
        if (aktif.KalanTas <= 0) return;
        if (!ArenaIcindeMi(pozisyon)) return;

        // Prefab'ı spawn et — server authority
        int prefabIdx = aktif.PlayerIndex % miknatisPrefablar.Length;
        if (miknatisPrefablar[prefabIdx] == null)
        {
            Debug.LogError($"[Server] miknatisPrefablar[{prefabIdx}] bos! Inspector'dan atama yap.");
            return;
        }

        GameObject tasObj = Instantiate(miknatisPrefablar[prefabIdx], pozisyon, Quaternion.identity);
        NetworkObject netObj = tasObj.GetComponent<NetworkObject>();
        if (netObj != null) netObj.Spawn(true);

        PlaySesClientRpc("tas_koy");

        // State güncelle
        sonOynayanOyuncuIndex = aktifOyuncuIndex.Value;
        PlayerData guncellendi = aktif;
        guncellendi.KalanTas--;
        oyuncular[aktifOyuncuIndex.Value] = guncellendi;

        if (guncellendi.KalanTas <= 0)
        {
            oyunBitti.Value = true;
            OyunuBitirClientRpc(guncellendi.PlayerName.ToString());
            return;
        }

        SirayiDegistirServer();
    }

    void SirayiDegistirServer()
    {
        aktifOyuncuIndex.Value = (aktifOyuncuIndex.Value + 1) % oyuncular.Count;
        kalanHamleSuresi.Value = toplamHamleSuresi;
        PlaySesClientRpc("ui_gecis");
    }

    // ---------------------------------------------------------------
    // BFS Çarpışma Tespiti — sadece Server çalıştırır
    // ---------------------------------------------------------------
    void KarpismalaraKontrolEt()
    {
        List<Magnet> aktifList = new List<Magnet>();
        foreach (Magnet m in Magnet.sahnedekiMiknatislar)
            if (m != null && !m.carpistiMi) aktifList.Add(m);

        HashSet<Magnet> islendi = new HashSet<Magnet>();

        foreach (Magnet baslangic in aktifList)
        {
            if (islendi.Contains(baslangic)) continue;

            List<Magnet> kume   = new List<Magnet> { baslangic };
            Queue<Magnet> kuyruk = new Queue<Magnet>();
            kuyruk.Enqueue(baslangic);

            while (kuyruk.Count > 0)
            {
                Magnet current = kuyruk.Dequeue();
                foreach (Magnet diger in Magnet.sahnedekiMiknatislar)
                {
                    if (diger == null || diger.carpistiMi || kume.Contains(diger)) continue;
                    if (Vector3.Distance(current.transform.position, diger.transform.position) <= current.temasMesafesi)
                    {
                        kume.Add(diger);
                        kuyruk.Enqueue(diger);
                    }
                }
            }

            foreach (Magnet km in kume) islendi.Add(km);

            if (kume.Count >= 2)
            {
                foreach (Magnet km in kume) km.CarpismaBaslat();
                StartCoroutine(CarpismaCoroutine(new List<Magnet>(kume)));
            }
        }
    }

    IEnumerator CarpismaCoroutine(List<Magnet> ilkKume)
    {
        // Elektrik efektini tüm client'lara gönder
        Vector3[] pozlar = new Vector3[ilkKume.Count];
        for (int i = 0; i < ilkKume.Count; i++)
            pozlar[i] = ilkKume[i] != null ? ilkKume[i].transform.position : Vector3.zero;
        ElektrikEfektiClientRpc(pozlar);

        yield return new WaitForSeconds(0.5f);

        // FAZ 2: yeni gelen mıknatısları da kümeye ekle
        List<Magnet> genisKume = new List<Magnet>(ilkKume);
        bool yeniEklendi = true;
        while (yeniEklendi)
        {
            yeniEklendi = false;
            foreach (Magnet frozen in new List<Magnet>(genisKume))
            {
                if (frozen == null) continue;
                foreach (Magnet diger in Magnet.sahnedekiMiknatislar)
                {
                    if (diger == null || diger.carpistiMi || genisKume.Contains(diger)) continue;
                    if (Vector3.Distance(frozen.transform.position, diger.transform.position) <= frozen.temasMesafesi)
                    {
                        genisKume.Add(diger);
                        diger.CarpismaBaslat();
                        yeniEklendi = true;
                    }
                }
            }
        }

        // Ceza: son tas koyan oyuncuya taslar geri döner
        if (oyuncular.Count > sonOynayanOyuncuIndex)
        {
            PlayerData cezali = oyuncular[sonOynayanOyuncuIndex];
            cezali.KalanTas += genisKume.Count;
            oyuncular[sonOynayanOyuncuIndex] = cezali;
        }

        // Network objeleri despawn et (true = yok et)
        foreach (Magnet m in genisKume)
        {
            if (m != null && m.NetworkObject != null && m.NetworkObject.IsSpawned)
                m.NetworkObject.Despawn(true);
        }
    }

    // ---------------------------------------------------------------
    // CLIENT RPCs
    // ---------------------------------------------------------------
    [ClientRpc]
    void SahaBoyutuGuncelleClientRpc(float x, float z)
    {
        if (IsServer) return; // Server zaten ayarladı
        if (KlusterSahaIpi.instance != null)
            KlusterSahaIpi.instance.BoyutuDogrudan(x, z);
    }

    [ClientRpc]
    void ElektrikEfektiClientRpc(Vector3[] pozlar)
    {
        if (AudioManager.instance != null) AudioManager.instance.PlayCarpismaSesi();
        foreach (Vector3 poz in pozlar)
            SpawnElektrikEfektiLocal(poz);
    }

    [ClientRpc]
    void OyunuBitirClientRpc(string kazananAdi)
    {
        UIManager.instance?.KazanmaEkraniniGoster(kazananAdi);
    }

    [ClientRpc]
    void PlaySesClientRpc(string sesAdi)
    {
        if (AudioManager.instance == null) return;
        if (sesAdi == "tas_koy")  AudioManager.instance.PlayTasKoymaSesi();
        else if (sesAdi == "ui_gecis") AudioManager.instance.PlayUIGecisSesi();
    }

    void SpawnElektrikEfektiLocal(Vector3 nokta)
    {
        GameObject spark = new GameObject("KlusterSimsek");
        spark.transform.position = nokta;
        Light pLight = spark.AddComponent<Light>();
        pLight.type      = LightType.Point;
        pLight.color     = new Color(0.1f, 0.8f, 1f);
        pLight.intensity = 15f;
        pLight.range     = 5f;
        LightningAura aura = spark.AddComponent<LightningAura>();
        aura.yariCap   = 2.0f;
        aura.neonKolor = new Color(0.1f, 0.9f, 1f);
        Destroy(spark, 1.0f);
    }

    bool ArenaIcindeMi(Vector3 pos)
    {
        float sahaA = KlusterSahaIpi.instance != null ? KlusterSahaIpi.instance.yaricapX : 11f;
        float sahaZ = KlusterSahaIpi.instance != null ? KlusterSahaIpi.instance.yaricapZ : 7.5f;
        return (pos.x * pos.x) / (sahaA * sahaA) + (pos.z * pos.z) / (sahaZ * sahaZ) <= 1f;
    }

    // ---------------------------------------------------------------
    // Public Getters — UIManager ve DragDropManager kullanır
    // ---------------------------------------------------------------
    public int  GetAktifOyuncuIndex()  => aktifOyuncuIndex.Value;
    public bool OyunBasladiMi()        => oyunBasladi.Value;
    public bool OyunBittiMi()          => oyunBitti.Value;
    public int  GetOyuncuSayisi()      => oyuncular.Count;

    public PlayerData? GetAktifOyuncu()
    {
        if (oyuncular.Count == 0) return null;
        return oyuncular[aktifOyuncuIndex.Value];
    }

    public PlayerData GetOyuncu(int index) => oyuncular[index];

    public bool TasKoyabilirMi()
    {
        if (oyunBitti.Value || !oyunBasladi.Value) return false;
        var aktif = GetAktifOyuncu();
        if (aktif == null) return false;
        ulong localId = NetworkManager.Singleton.LocalClientId;
        return aktif.Value.ClientId == localId && aktif.Value.KalanTas > 0;
    }

    public GameObject GetAktifPrefab()
    {
        var aktif = GetAktifOyuncu();
        if (aktif == null) return null;
        int idx = aktif.Value.PlayerIndex % miknatisPrefablar.Length;
        return miknatisPrefablar[idx];
    }

    public int GetLocalPlayerIndex()
    {
        if (NetworkManager.Singleton == null) return 0;
        ulong localId = NetworkManager.Singleton.LocalClientId;
        for (int i = 0; i < oyuncular.Count; i++)
            if (oyuncular[i].ClientId == localId) return i;
        return 0;
    }

    public void YenidenOyna()
    {
        if (!IsServer) return;
        Magnet.sahnedekiMiknatislar.Clear();
        NetworkManager.SceneManager.LoadScene("LobbyScene", LoadSceneMode.Single);
    }

    // Single-player geriye uyumluluk — artık kullanılmıyor ama derleme hatasını engeller
    public void OyunuBaslat(string ad1, string ad2) { }
}
