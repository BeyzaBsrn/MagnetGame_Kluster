using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ResponsiveCamera : MonoBehaviour
{
    [Header("Otomatik Fitleme Ayarları")]
    public GameObject background; 
    
    private Camera cam;
    public float defaultOrthographicSize = 5f; // Başlangıç değeri verildi (Hata önleyici)
    private float defaultAspectRatio = 1.77f; 

    void Awake()
    {
        cam = GetComponent<Camera>();
        // Eğer sahnede kameran zaten ayarlıysa onun değerini al
        if(cam.orthographicSize > 0) defaultOrthographicSize = cam.orthographicSize;
        AdjustCamera();
    }

    void AdjustCamera()
    {
        if (cam == null) cam = GetComponent<Camera>();
        
        // Ekran yüksekliğinin sıfır olmamasını garanti ediyoruz (Hata önleyici)
        float h = Mathf.Max(1, Screen.height);
        float w = Mathf.Max(1, Screen.width);
        float currentAspectRatio = w / h;

        if (currentAspectRatio < defaultAspectRatio)
        {
            float differenceInSize = defaultAspectRatio / currentAspectRatio;
            cam.orthographicSize = defaultOrthographicSize * differenceInSize;
        }
        else
        {
            cam.orthographicSize = defaultOrthographicSize;
        }
    }

    // Editörde objeyi sürüklediğinde veya değer değiştirdiğinde siyah ekran olmasın diye:
    #if UNITY_EDITOR
    void OnValidate()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam != null && defaultOrthographicSize <= 0) defaultOrthographicSize = 5f;
        AdjustCamera();
    }
    #endif
}
