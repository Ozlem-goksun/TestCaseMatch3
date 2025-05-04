using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    [Header("Sahne İsimleri")]
    [SerializeField] private string level1SceneName = "Level1";
    [SerializeField] private string level2SceneName = "Level2"; 
    [SerializeField] private string level3SceneName = "Level3";

    // Butonlar bu public fonksiyonları çağıracak

    public void LoadLevel1() { LoadSceneByName(level1SceneName); }
    public void LoadLevel2() { LoadSceneByName(level2SceneName); }
    public void LoadLevel3() { LoadSceneByName(level3SceneName); }

    // Sahneyi ismine göre yükler
    private void LoadSceneByName(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError("Yüklenecek sahne adı boş!");
        }
    }

}