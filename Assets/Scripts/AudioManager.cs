using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [Header("Ses Oynatýcýlarý (Audio Sources)")]
    public AudioSource muzikKaynagi;  // Arka plan müziði için
    public AudioSource sfxKaynagi;    // Çarpýþma ve týklama sesleri için

    [Header("Ses Dosyalarý (Audio Clips)")]
    public AudioClip arkaPlanMuzigi;
    public AudioClip tasKoymaSesi;
    public AudioClip carpsimaSesi;
    public AudioClip uiGecisSesi;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (arkaPlanMuzigi != null && muzikKaynagi != null)
        {
            muzikKaynagi.clip = arkaPlanMuzigi;
            muzikKaynagi.loop = true;
            muzikKaynagi.volume = 0.3f;
            muzikKaynagi.Play();
        }
    }

    public void PlayTasKoymaSesi()
    {
        if (tasKoymaSesi != null && sfxKaynagi != null)
        {
            sfxKaynagi.pitch = 1.0f; 
            sfxKaynagi.PlayOneShot(tasKoymaSesi);
        }
    }

    public void PlayCarpismaSesi()
    {
        if (carpsimaSesi != null && sfxKaynagi != null)
        {
            // Sesi çalmadan önce kalýnlýðýný/tizliðini rastgele çok hafif deðiþtirir
            sfxKaynagi.pitch = Random.Range(0.85f, 1.15f);
            sfxKaynagi.PlayOneShot(carpsimaSesi);
        }
    }

    public void PlayUIGecisSesi()
    {
        if (uiGecisSesi != null && sfxKaynagi != null)
        {
            sfxKaynagi.pitch = 1.0f; // Sesi normale döndürür
            sfxKaynagi.PlayOneShot(uiGecisSesi);
        }
    }
}
