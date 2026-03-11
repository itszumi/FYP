using UnityEngine;

public class AudioManager : MonoBehaviour
{

    public void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }

    
    public void ChangeVolume(float value)
    {
        AudioListener.volume = value;
    }


}

