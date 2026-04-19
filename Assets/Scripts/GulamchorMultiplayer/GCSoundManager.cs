

using UnityEngine;

public class GCSoundManager : MonoBehaviour
{
    public static GCSoundManager Instance { get; private set; }

    [Header("Card Sounds")]
    [Tooltip("Assets/Free UI Click Sound Effects Pack/AUDIO/Sci-Fi/SFX_UI_Click_Designed_Scifi_Movement_Open_1.wav")]
    public AudioClip cardDiscardSound;   // played when pair is discarded

    [Tooltip("Assets/Free UI Click Sound Effects Pack/AUDIO/Button/SFX_UI_Button_Keyboard_Space_Thick_1.wav")]
    public AudioClip cardPickSound;     // played when picking card from opponent

    [Header("Volume")]
    [Range(0f, 1f)] public float volume = 0.8f;

    private AudioSource _audioSource;

    void Awake()
    {
        // Allow multiple instances (one per scene) or make singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.volume = volume;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void PlayDiscard()
    {
        Play(cardDiscardSound);
    }

    public void PlayCardPick()
    {
        Play(cardPickSound);
    }

    private void Play(AudioClip clip)
    {
        if (clip == null || _audioSource == null) return;
        _audioSource.PlayOneShot(clip, volume);
    }
}