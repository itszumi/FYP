using UnityEngine;
using UnityEngine.SceneManagement;

public class pauseMenuScript : MonoBehaviour
{
    public void pauseGame() { 
        Time.timeScale = 0f;
        gameObject.SetActive(true);
    }
    public void resumeGame() {
        gameObject.SetActive(false);
        Time.timeScale = 1f;
    }
    public void RestartGame()
    {
        
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        // when a game over screen appears, you must set it back to 1
        // for the game to run correctly after restart
        Time.timeScale = 1f;
    }

    public void goHome() { 
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(1);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
