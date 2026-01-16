using UnityEngine;
using UnityEngine.SceneManagement;  

public class mainMenuScript : MonoBehaviour
{
    
    public void playGame() { 
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex+1);
    }

    public void quitGame() {
        Debug.Log("QUITING. . .");
        Application.Quit();
    }
}
