using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Netcode;

public class DragDropManager : MonoBehaviour
{
    [Header("Ayarlar")]
    public float tasYuksekligi = 0.6f;

    private GameObject ghostTas;
    private bool       surukleniyor = false;
    private Camera     cam;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (GameManager.instance == null) return;
        if (!GameManager.instance.OyunBasladiMi()) return;
        if (GameManager.instance.OyunBittiMi()) return;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)                        SuruklemeBaslat();
            else if (touch.phase == TouchPhase.Moved && surukleniyor)   SuruklemeDevam();
            else if ((touch.phase == TouchPhase.Ended ||
                      touch.phase == TouchPhase.Canceled) && surukleniyor) SuruklemeBirak();
        }
        else
        {
            if (Input.GetMouseButtonDown(0))                    SuruklemeBaslat();
            else if (Input.GetMouseButton(0) && surukleniyor)  SuruklemeDevam();
            else if (Input.GetMouseButtonUp(0) && surukleniyor) SuruklemeBirak();
        }
    }

    Vector3 GetInputPosition()
    {
        return Input.touchCount > 0 ? (Vector3)Input.GetTouch(0).position : Input.mousePosition;
    }

    void SuruklemeBaslat()
    {
        if (UIDenGeliyor()) return;

        // Sadece sırası gelen ve kendi turn'ü olan oyuncu hareket edebilir
        if (!GameManager.instance.TasKoyabilirMi()) return;

        int localIndex = GameManager.instance.GetLocalPlayerIndex();
        Vector3 inputPos = GetInputPosition();

        if (!GecerliBaslangicBolgesi(localIndex, inputPos)) return;

        Vector3? poz = ZeminPozisyonuBul();
        if (poz == null) return;

        GameObject prefab = GameManager.instance.GetAktifPrefab();
        if (prefab == null) return;

        Vector3 spawnPoz = new Vector3(poz.Value.x, poz.Value.y + tasYuksekligi, poz.Value.z);
        ghostTas = Instantiate(prefab, spawnPoz, Quaternion.identity);

        // Ghost: fizik ve çekim kapalı
        Rigidbody rb = ghostTas.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

        Magnet mag = ghostTas.GetComponent<Magnet>();
        if (mag != null) mag.enabled = false;

        // NetworkObject varsa devre dışı bırak — ghost network'e katılmamalı
        NetworkObject netObj = ghostTas.GetComponent<NetworkObject>();
        if (netObj != null) Destroy(netObj);

        // Neon aura efekti
        MeshFilter mf = ghostTas.GetComponent<MeshFilter>();
        if (mf != null && mf.mesh != null)
        {
            GameObject neon = new GameObject("SoftNeonAura");
            neon.transform.SetParent(ghostTas.transform);
            neon.transform.localPosition = new Vector3(0, -0.05f, 0);
            neon.transform.localScale    = new Vector3(1.20f, 0.4f, 1.20f);

            MeshFilter mfAura   = neon.AddComponent<MeshFilter>();
            mfAura.sharedMesh   = mf.mesh;
            MeshRenderer mrAura = neon.AddComponent<MeshRenderer>();

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Sprites/Default");

            Material mat = new Material(unlit);
            mat.color = new Color(0.1f, 0.85f, 1f, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mat.color);
            mrAura.material = mat;
        }

        surukleniyor = true;
    }

    void SuruklemeDevam()
    {
        if (ghostTas == null) return;
        Vector3? poz = ZeminPozisyonuBul();
        if (poz != null)
            ghostTas.transform.position = new Vector3(poz.Value.x, poz.Value.y + tasYuksekligi, poz.Value.z);
    }

    void SuruklemeBirak()
    {
        if (ghostTas == null) { surukleniyor = false; return; }

        Vector3 pozisyon = ghostTas.transform.position;
        Destroy(ghostTas);
        ghostTas     = null;
        surukleniyor = false;

        // Sunucuya taş koyma isteği gönder — server doğrular ve spawn eder
        GameManager.instance.TasKoyServerRpc(pozisyon);
    }

    // ---------------------------------------------------------------
    // Oyuncu başlangıç bölgesi kontrolü
    // Oyuncu Sayısı | 0=Sol  | 1=Sağ  | 2=Üst  | 3=Alt
    // 2 oyuncu     | %25    | %25    |  —     |  —
    // 3-4 oyuncu   | %20    | %20    | %20    | %20
    // ---------------------------------------------------------------
    bool GecerliBaslangicBolgesi(int playerIndex, Vector3 inputPos)
    {
        int oyuncuSayisi = GameManager.instance.GetOyuncuSayisi();
        float esik = oyuncuSayisi <= 2 ? 0.25f : 0.20f;

        switch (playerIndex)
        {
            case 0: return inputPos.x < Screen.width  * esik;
            case 1: return inputPos.x > Screen.width  * (1f - esik);
            case 2: return inputPos.y > Screen.height * (1f - esik);
            case 3: return inputPos.y < Screen.height * esik;
            default: return false;
        }
    }

    Vector3? ZeminPozisyonuBul()
    {
        Vector3 inputPos = GetInputPosition();
        Ray ray = cam.ScreenPointToRay(inputPos);

        if (GameManager.instance != null)
        {
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f, GameManager.instance.zeminKatmani))
                return hit.point;
        }

        Plane zemin = new Plane(Vector3.up, Vector3.zero);
        float mesafe;
        if (zemin.Raycast(ray, out mesafe)) return ray.GetPoint(mesafe);

        return null;
    }

    bool UIDenGeliyor()
    {
        if (EventSystem.current == null) return false;
        if (Input.touchCount > 0)
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return EventSystem.current.IsPointerOverGameObject();
    }
}
