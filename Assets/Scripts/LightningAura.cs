using UnityEngine;

public class LightningAura : MonoBehaviour
{
    public Color neonKolor = new Color(0.1f, 0.9f, 1f, 1f);
    public float yariCap = 1.0f;
    
    private float sonrakiSimsekZamani = 0f;

    void Update()
    {
        if (Time.time >= sonrakiSimsekZamani)
        {
            // Saniyede 10-15 civarı şimşek patlat
            SimsekOlustur();
            sonrakiSimsekZamani = Time.time + Random.Range(0.05f, 0.1f);
        }
    }

    void SimsekOlustur()
    {
        GameObject simsek = new GameObject("MinikSimsek");
        simsek.transform.SetParent(this.transform);
        
        LineRenderer lr = simsek.AddComponent<LineRenderer>();
        lr.positionCount = 4; // Kırık çizgi için 4 boğum
        lr.startWidth = 0.08f;
        lr.endWidth = 0.01f;
        lr.useWorldSpace = true;

        Shader s = Shader.Find("Sprites/Default");
        if (s != null) lr.material = new Material(s);
        lr.startColor = neonKolor;
        lr.endColor = new Color(neonKolor.r, neonKolor.g, neonKolor.b, 0f); // Uca doğru kaybolsun

        Vector3 baslangic = transform.position;
        // Şimşek rastgele bir yöne doğru fırlasın
        Vector3 yon = Random.onUnitSphere;
        yon.y = Mathf.Abs(yon.y) * 0.5f; // Çok havaya veya yere uçmasın, yana yayılsın

        float uzunluk = Random.Range(yariCap * 0.7f, yariCap * 1.5f);
        Vector3 bitis = baslangic + (yon * uzunluk);

        // Kırık çizgi (zig-zag) hesaplaması
        for (int i = 0; i < 4; i++)
        {
            float oran = i / 3f;
            Vector3 poz = Vector3.Lerp(baslangic, bitis, oran);
            
            // Ara noktalarda rastgele sapmalar (Zig-zag etkisi)
            if (i == 1 || i == 2)
            {
                poz += Random.onUnitSphere * 0.3f;
            }
            lr.SetPosition(i, poz);
        }

        // Şimşekler anlık yanıp sönmeli (0.05 saniye ile 0.15 saniye arası yaşar)
        Destroy(simsek, Random.Range(0.05f, 0.15f));
    }
}
