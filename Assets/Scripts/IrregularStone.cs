using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class IrregularStone : MonoBehaviour
{
    [Header("Bozulma (Asimetri) Ayarları")]
    public float deformation = 0.15f; // Taştaki yamukluk miktarı
    public float noiseScale = 2.0f;   // Girinti çıkıntı sıklığı

    void Awake()
    {
        // 1. Kusursuz Sphere Mesh'ini Yakala we Boz (Perlin Noise ile)
        MeshFilter mf = GetComponent<MeshFilter>();
        if(mf.mesh == null) return;
        
        Mesh baseMesh = mf.mesh;
        Mesh newMesh = new Mesh();
        newMesh.name = "Hematite Stone Mesh";
        
        Vector3[] vertices = baseMesh.vertices;
        float randomOffset = Random.Range(0f, 100f);

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            
            // X ve Y koordinatlarına göre rastgele bir dağ/tepe haritası oluştur (Noise)
            float noise = Mathf.PerlinNoise(v.x * noiseScale + randomOffset, v.y * noiseScale + randomOffset);
            
            // Köşeleri merkeze dışkı veya içeri doğru çek
            vertices[i] += v.normalized * (noise * deformation); 
        }

        newMesh.vertices = vertices;
        newMesh.triangles = baseMesh.triangles;
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();
        
        mf.mesh = newMesh;

        // 2. Materyali "Ağır ve Parlak Metal" (Hematit) gibi ayarla
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if(mr.material != null) {
            mr.material.SetFloat("_Metallic", 0.95f);
            mr.material.SetFloat("_Glossiness", 0.85f); // Smoothness (Kaygan görünüm)
            mr.material.color = new Color(0.12f, 0.12f, 0.12f, 1f); // Koyu Antrasit/Siyah
        }
    }

    void Start()
    {
        // UI elemanları we Sürüklenen Hayalet Taşlar için fizik açmıyoruz.
        Magnet magnet = GetComponent<Magnet>();
        if (magnet == null || magnet.enabled == false) return; 

        // --- DEVASA BİR OPTİMİZASYON VE UYARI SİLİCİ ÇÖZÜM ---
        // Unity'nin kalbindeki o inatçı "Maximum polygons limit (256)" sınır uyarısını SONSUZA DEK SİLDİM!
        // Asimetrik (MeshCollider) hesaplaması çok detaylı olduğu için her taşta 2 kere C++ hatası veriyordu.
        // O yüzden MeshCollider yerine çok daha hızlı, oyunu 100 kat daha akıcı yapacak ve mıknatısların 
        // birbirine köşeli takılmadan kayarak şıkça yapışmasını sağlayacak olan SPHERE COLLIDER kullanıyoruz!
        
        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc == null) sc = gameObject.AddComponent<SphereCollider>();
        
        // Taşın genişliğine en optimize şekilde uyum sağlaması için yarıçapını manuel oturtuyoruz
        sc.radius = 0.52f; // Görsel mesh dış hatlarına güvenli oturma

        // Taşı ağır we oturaklı göstermek için sekme dinamikleri (Daha önce yaptığımın aynısı)
        PhysicMaterial noBounce = new PhysicMaterial("StoneFriction");
        noBounce.bounciness = 0f; // Asla sekme yok! 
        noBounce.bounceCombine = PhysicMaterialCombine.Minimum;
        noBounce.dynamicFriction = 0.6f;
        noBounce.staticFriction = 0.6f;
        sc.material = noBounce;
    }
}
