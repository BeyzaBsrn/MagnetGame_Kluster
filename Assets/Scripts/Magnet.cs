using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class Magnet : NetworkBehaviour
{
    [Header("Manyetik Alan Ayarları")]
    public float cekimAlani    = 3f;
    public float cekimGucu     = 150f;
    public float temasMesafesi = 1.1f;

    [HideInInspector] public Rigidbody rb;

    // Sadece Server bu listeyi BFS için kullanır
    public static List<Magnet> sahnedekiMiknatislar = new List<Magnet>();

    [HideInInspector] public bool carpistiMi = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        // Listeye sadece Server ekler — Client fiziği çalıştırmaz
        if (IsServer && !sahnedekiMiknatislar.Contains(this))
            sahnedekiMiknatislar.Add(this);

        // Client tarafında fizik ve çekim kapalı; NetworkTransform pozisyonu stream eder
        if (!IsServer && rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        sahnedekiMiknatislar.Remove(this);
    }

    new void OnDestroy()
    {
        // NetworkObject olmayan (UI ikonu gibi) instance'lar için güvenlik
        sahnedekiMiknatislar.Remove(this);
    }

    // GameManager tarafından çağrılır: dondur ve çarpıştı işaretle
    public void CarpismaBaslat()
    {
        carpistiMi = true;
        if (rb != null) rb.isKinematic = true;
    }

    public void ElektrikEfektiYarat(Vector3 nokta)
    {
        GameObject spark = new GameObject("KlusterSimsekPatlamasi");
        spark.transform.position = nokta;
        Light pLight       = spark.AddComponent<Light>();
        pLight.type        = LightType.Point;
        pLight.color       = new Color(0.1f, 0.8f, 1f, 1f);
        pLight.intensity   = 15f;
        pLight.range       = 5f;
        LightningAura aura = spark.AddComponent<LightningAura>();
        aura.yariCap       = 2.0f;
        aura.neonKolor     = new Color(0.1f, 0.9f, 1f, 1f);
        Destroy(spark, 1.0f);
    }

    void FixedUpdate()
    {
        // Fizik yalnızca Server'da çalışır; Client pozisyonu NetworkTransform ile alır
        if (!IsServer) return;
        if (carpistiMi || rb == null || rb.isKinematic) return;

        // Arena sınırı: dışarı çıkan mıknatısı merkeze itmek
        float sahaA = 11f;
        float sahaZ = 7.5f;
        if (KlusterSahaIpi.instance != null)
        {
            sahaA = KlusterSahaIpi.instance.yaricapX;
            sahaZ = KlusterSahaIpi.instance.yaricapZ;
        }

        float elips = (transform.position.x * transform.position.x) / (sahaA * sahaA)
                    + (transform.position.z * transform.position.z) / (sahaZ * sahaZ);
        if (elips > 1f)
        {
            Vector3 don = (Vector3.zero - transform.position).normalized;
            rb.AddForce(don * 200f, ForceMode.Force);
        }

        // Çekim kuvveti — carpistiMi olanlar dahil (C onlara doğru çekilmeye devam etsin)
        foreach (Magnet diger in sahnedekiMiknatislar)
        {
            if (diger == null || diger == this) continue;
            float mesafe = Vector3.Distance(transform.position, diger.transform.position);
            if (mesafe > temasMesafesi && mesafe < cekimAlani)
            {
                float guc = cekimGucu / (mesafe * mesafe);
                Vector3 yon = (diger.transform.position - transform.position).normalized;
                rb.AddForce(yon * guc, ForceMode.Force);
            }
        }
    }
}
