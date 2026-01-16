using UnityEngine;
using UnityEngine.SceneManagement;

public class topPanelScript : MonoBehaviour
{
    public void goHome() {
        
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);

    }

    public void pauseGame() { 
        Time.timeScale = 0f;
        gameObject.SetActive(false);
    }
    

    // Update is called once per frame
    void Update()
    {
        
    }
}
