// AvatarManager.cs
// Singleton MonoBehaviour: attach to a GameObject in the Login scene.
// Drag avatar sprites into the Avatars array in the Inspector (slots 0-5).
// Other scripts call AvatarManager.Instance.GetSprite(index).

using UnityEngine;

public class AvatarManager : MonoBehaviour
{
    public static AvatarManager Instance { get; private set; }

    [Header("Assign 6 avatar sprites in the Inspector")]
    public Sprite[] Avatars = new Sprite[6];

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Returns the sprite for the given index (clamped to valid range).</summary>
    public Sprite GetSprite(int index)
    {
        if (Avatars == null || Avatars.Length == 0) return null;
        index = Mathf.Clamp(index, 0, Avatars.Length - 1);
        return Avatars[index];
    }

    public int AvatarCount => (Avatars != null) ? Avatars.Length : 0;
}
