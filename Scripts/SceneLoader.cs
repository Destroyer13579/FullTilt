using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadTable()
    {
        SceneManager.LoadScene("Table");
    }

    public void LoadLobby()
    {
        SceneManager.LoadScene("lobby");
    }

    public void LoadLogin()
    {
        SceneManager.LoadScene("Login");
    }
}
