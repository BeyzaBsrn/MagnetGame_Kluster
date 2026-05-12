using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class KlusterSahaIpi : MonoBehaviour
{
    public static KlusterSahaIpi instance;

    [Header("Matematiksel Elips (Magnetler Buradan Uçamaz)")]
    [Range(2f, 40f)] public float yaricapX = 11f;  
    [Range(2f, 40f)] public float yaricapZ = 7.5f;   
    public float ipKalinligi = 0.3f;               
    private LineRenderer neonGlow;
    private LineRenderer neonCore;
    private LineRenderer icDuvar;
    private GameObject sariZeminObj;

    void Awake()
    {
        instance = this;
        transform.localScale = Vector3.one;
    }

    void Start()
    {
        Shader spriteShader = Shader.Find("Sprites/Default"); 
        if (spriteShader == null) spriteShader = Shader.Find("Unlit/Color");

        // 1. NEON GLOW (Dış Işıma - Kalın ve Saydam)
        neonGlow = GetComponent<LineRenderer>();
        if(neonGlow == null) neonGlow = gameObject.AddComponent<LineRenderer>();
        neonGlow.useWorldSpace = false;
        neonGlow.numCapVertices = 5;
        neonGlow.numCornerVertices = 5;
        neonGlow.material = new Material(spriteShader);

        // 2. NEON CORE (Parlak Çizgi - İnce ve Tam Opak)
        GameObject objCore = new GameObject("SahaIpi_NeonCore");
        objCore.transform.SetParent(this.transform);
        objCore.transform.localPosition = new Vector3(0, 0.01f, 0); // Üstte Dursun
        neonCore = objCore.AddComponent<LineRenderer>();
        neonCore.useWorldSpace = false;
        neonCore.numCapVertices = 5;
        neonCore.numCornerVertices = 5;
        neonCore.material = new Material(spriteShader);

        // 3. İÇ DUVAR (Koyu Gri Sınır)
        GameObject objDuvar = new GameObject("SahaIpi_IcDuvar");
        objDuvar.transform.SetParent(this.transform);
        objDuvar.transform.localPosition = new Vector3(0, 0.02f, 0); // En Üstte
        icDuvar = objDuvar.AddComponent<LineRenderer>();
        icDuvar.useWorldSpace = false;
        icDuvar.numCapVertices = 5;
        icDuvar.numCornerVertices = 5;
        Material duvarMat = new Material(spriteShader);
        duvarMat.color = new Color(0.15f, 0.15f, 0.15f, 1f); // Koyu Gri
        icDuvar.material = duvarMat;

        // Gradient Ayarları (Sol Mavi, Sağ Kırmızı)
        GradientAlphaKey[] alphaGlow = new GradientAlphaKey[] { new GradientAlphaKey(0.2f, 0.0f), new GradientAlphaKey(0.2f, 1.0f) };
        GradientAlphaKey[] alphaCore = new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) };
        
        GradientColorKey[] renkler = new GradientColorKey[] { 
            new GradientColorKey(new Color(1f, 0.1f, 0.1f, 1f), 0.0f),      // Keskin Kırmızı
            new GradientColorKey(new Color(1f, 0.1f, 0.1f, 1f), 0.245f),    
            new GradientColorKey(new Color(0.1f, 0.5f, 1f, 1f), 0.255f),    // Keskin Mavi (Parlak Glow)
            new GradientColorKey(new Color(0.1f, 0.5f, 1f, 1f), 0.745f),    
            new GradientColorKey(new Color(1f, 0.1f, 0.1f, 1f), 0.755f),    
            new GradientColorKey(new Color(1f, 0.1f, 0.1f, 1f), 1.0f) 
        };

        Gradient gradientGlow = new Gradient();
        gradientGlow.SetKeys(renkler, alphaGlow);
        neonGlow.colorGradient = gradientGlow;

        Gradient gradientCore = new Gradient();
        gradientCore.SetKeys(renkler, alphaCore);
        neonCore.colorGradient = gradientCore;

        int stepCount = 200; // Pürüzsüz Daire Kalitesi (Eski köşeliliği yok et!)
        neonGlow.positionCount = stepCount + 1;
        neonCore.positionCount = stepCount + 1;
        icDuvar.positionCount = stepCount + 1;

        // OLUŞTURMA: Kaliteyi Artırılmış Pürüzsüz Sarı Zemin
        OlusturPuruzsuzZemin(stepCount);

        // Çizgileri Çiz
        CizgileriGuncelle(stepCount);
    }

    void OlusturPuruzsuzZemin(int segments)
    {
        // Eski objeleri temizle
        GameObject eskiSari = GameObject.Find("SariZemin");
        if (eskiSari != null) Destroy(eskiSari);
        GameObject eskiKusursuz = GameObject.Find("KusursuzSariZemin");
        if (eskiKusursuz != null) Destroy(eskiKusursuz);

        // Boş bir obje oluştur
        sariZeminObj = new GameObject("SariZeminOtomatik");
        sariZeminObj.transform.position = this.transform.position + new Vector3(0, 0.03f, 0); // En tepe
        
        // Pürüzsüz Mesh Üret (Cylinder yerine!)
        Mesh mesh = new Mesh();
        Vector3[] verts = new Vector3[segments + 1];
        int[] tris = new int[segments * 3];
        verts[0] = Vector3.zero; // Merkez Düğüm

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Cos(angle) * yaricapX, 0, Mathf.Sin(angle) * yaricapZ);
        }

        for (int i = 0; i < segments; i++)
        {
            // Yukarı bakması için saat yönünde (Clockwise) dizilim olmalı!
            tris[i * 3] = 0;
            tris[i * 3 + 1] = (i + 2 > segments) ? 1 : (i + 2);
            tris[i * 3 + 2] = i + 1;
        }

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        MeshFilter mf = sariZeminObj.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        MeshRenderer mr = sariZeminObj.AddComponent<MeshRenderer>();
        // ANDROID KRİTİK: APK'da pembe ekran olmaması için Sprites/Default shader en güvenli yoldur!
        Shader zeminShader = Shader.Find("Sprites/Default");
        if (zeminShader == null) zeminShader = Shader.Find("UI/Default");
        
        Material zeminMat = new Material(zeminShader);
        zeminMat.color = new Color(0.95f, 0.8f, 0.1f, 1f); // Tok, modern sarı
        mr.material = zeminMat;

        // Çarpışma Kutusu (Mıknatıslar İçin)
        BoxCollider box = sariZeminObj.AddComponent<BoxCollider>();
        box.size = new Vector3(yaricapX * 2f, 100f, yaricapZ * 2f);
        box.center = new Vector3(0, -49.9f, 0); // Yüzey sıfırda, aşağıya 100 birim beton!

        PhysicMaterial noBounceMat = new PhysicMaterial("NoBounceMat");
        noBounceMat.bounciness = 0f;
        noBounceMat.bounceCombine = PhysicMaterialCombine.Minimum;
        box.material = noBounceMat;

        sariZeminObj.layer = LayerMask.NameToLayer("Zemin");
    }

    // ClientRpc ile çağrılır — server değerini client'lara uygular
    public void BoyutuDogrudan(float x, float z)
    {
        yaricapX = x;
        yaricapZ = z;
        if (sariZeminObj != null) Destroy(sariZeminObj);
        OlusturPuruzsuzZemin(200);
        CizgileriGuncelle(200);
    }

    // GameManager tarafından çağrılır — oyuncu sayısına göre saha büyür
    public void BoyutuAyarla(int oyuncuSayisi)
    {
        switch (oyuncuSayisi)
        {
            case 2:  yaricapX = 11f;   yaricapZ = 7.5f; break;
            case 3:  yaricapX = 11f;   yaricapZ = 7.5f; break;
            case 4:  yaricapX = 10.5f; yaricapZ = 7f;   break;
            default: yaricapX = 11f;   yaricapZ = 7.5f; break;
        }
        // Sarı zemini yeniden oluştur
        if (sariZeminObj != null) Destroy(sariZeminObj);
        OlusturPuruzsuzZemin(200);
        CizgileriGuncelle(200);
    }

    void Update()
    {
        // Çözünürlük 200 sabit olduğu için sadece boyut güncellemeleri
        CizgileriGuncelle(200);
    }

    void CizgileriGuncelle(int segments)
    {
        if(neonGlow == null) return;

        // Işıma kalınlığını çok küçülttüm (daha ince bir hale/glow)
        neonGlow.startWidth = ipKalinligi + 0.3f;  
        neonGlow.endWidth = ipKalinligi + 0.3f;

        if(neonCore != null) {
            neonCore.startWidth = ipKalinligi - 0.05f; // Keskin iç hattı da biraz incelttik
            neonCore.endWidth = ipKalinligi - 0.05f;
        }

        if(icDuvar != null) {
            icDuvar.startWidth = ipKalinligi - 0.15f; 
            icDuvar.endWidth = ipKalinligi - 0.15f;
        }

        for (int i = 0; i <= segments; i++)
        {
            float aci = (float)i / segments * Mathf.PI * 2f;
            
            // X ve Z kordinatları (Sarı zemin hizasından çok ufak dışarı)
            float duvarX = Mathf.Cos(aci) * (yaricapX + 0.05f);
            float duvarZ = Mathf.Sin(aci) * (yaricapZ + 0.05f);
            
            // Neon'ları İç duvara çok yakın tut, çok yayılmasınlar!
            float neonX = Mathf.Cos(aci) * (yaricapX + 0.15f);
            float neonZ = Mathf.Sin(aci) * (yaricapZ + 0.15f);

            Vector3 duvarPos = new Vector3(duvarX, 0f, duvarZ);
            Vector3 neonPos = new Vector3(neonX, 0f, neonZ);

            neonGlow.SetPosition(i, neonPos); 
            if(neonCore != null) neonCore.SetPosition(i, neonPos);
            if(icDuvar != null) icDuvar.SetPosition(i, duvarPos);
        }
    }
}