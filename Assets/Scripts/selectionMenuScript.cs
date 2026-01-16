using UnityEngine;

public class selectionMenuScript : MonoBehaviour
{
    public void selectGame(int gameIndex) {
        UnityEngine.SceneManagement.SceneManager.LoadScene(gameIndex);
    }

}
