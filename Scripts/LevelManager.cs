using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using TMPro; 
using UnityEngine.UI; 

public class LevelManager : MonoBehaviour
{
    [Header("Goals & UI")]
    public List<Goal> levelGoals = new List<Goal>();

    [Header("Scene Management")]
    [SerializeField] private string nextSceneName = "";
    [SerializeField] private string mainMenuSceneName = "Home";

    [Header("Referances")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private GameObject levelCompletePanelObject; 

    public class Goal
    {
        public string goalName = "Hedef"; // Inspector'da ayırt etmek için
        public int amountRequired;
        public TextMeshProUGUI uiTextElement;
        public int amountRemaining;
    }

    private bool levelComplete = false;


    void Start()
    {
        // Gerekli atamalar yapıldı mı kontrol et
        if (boardManager == null) 
        { 
            enabled = false;
            return; 
        }

        if (levelCompletePanelObject == null)
        {
            Debug.LogError("HATA: LevelCompletePanelObject atanmamış!", this.gameObject);
        }
        else 
        {
            levelCompletePanelObject.SetActive(false);
        } 

        InitializeLevelGoalsAndUI();
        levelComplete = false;
    }


    void InitializeLevelGoalsAndUI()
    {
        if (levelGoals == null || levelGoals.Count == 0)
        { 
            return;
        }

        bool valid = true;
        foreach (Goal g in levelGoals)
        {
            if (string.IsNullOrEmpty(g.targetTag) || g.uiTextElement == null)
            {
                valid = false;
                continue; 
            }

            if (g.amountRequired <= 0) 
            {
                g.amountRequired = 0;
            }

            g.amountRemaining = g.amountRequired;
            UpdateGoalUI(g); 
        }
        if (!valid)
        { 
            enabled = false;
        }
    }

    public void ReportMatch(string matchedTag, int count)
    {
        if (levelComplete || string.IsNullOrEmpty(matchedTag) || count <= 0)
        {
            return;
        }

        foreach (Goal g in levelGoals)
        {
            if (g.targetTag == matchedTag)
            { 
                if (g.amountRemaining > 0)
                {
                    g.amountRemaining -= count;
                    g.amountRemaining = Mathf.Max(0, g.amountRemaining); 
                    UpdateGoalUI(g); CheckLevelComplete(); } break;
            } 
        }
    }

    void CheckLevelComplete()
    {
        if (levelComplete || levelGoals == null || levelGoals.Count == 0)
        {
            return ;
        }
        foreach (Goal g in levelGoals)
        { 
            if (g.amountRemaining > 0)
            {
                return;
            }
              
        } 
        if (!levelComplete) 
        {
            StartCoroutine(CompleteLevelSequence());
        }
    }

  
    IEnumerator CompleteLevelSequence()
    {
        levelComplete = true;

        if (boardManager != null)
        {
        
            yield return new WaitUntil(() => !boardManager.IsAnimating());

            boardManager.SetInteractable(false);
        }

        else 
        { 
            Debug.LogError("BoardManager yok!");
        }

        ShowAndSetupLevelCompletePanel();
    }

    public bool IsLevelComplete()
    {
        return levelComplete; 
    }


    public void ReplayLevel()
    {
        Scene cS = SceneManager.GetActiveScene();
        
        if (levelCompletePanelObject != null)
        {
            levelCompletePanelObject.SetActive(false);
        }
            
        SceneManager.LoadScene(cS.name);
    }
    public void GoToMainMenu()
    
    { 
        if (!string.IsNullOrEmpty(mainMenuSceneName)) 
        { 
            Debug.Log($"Ana Menü: {mainMenuSceneName}");
            if (levelCompletePanelObject != null)
            {
                levelCompletePanelObject.SetActive(false);
            }
                
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else 
        { 
            Debug.LogError("Sahne adı boş!");
        } 
    }
    public void LoadNextLevelScene()
    { 
        if (!string.IsNullOrEmpty(nextSceneName)) 
        {
            if (levelCompletePanelObject != null)
            {
                levelCompletePanelObject.SetActive(false);
            }
               
            SceneManager.LoadScene(nextSceneName);
        }
        else 
        {
            GoToMainMenu();
        }
    }


    void UpdateGoalUI(Goal goal)
    {
        if (goal.uiTextElement != null)
        {
            goal.uiTextElement.text = goal.amountRemaining.ToString(); 
            goal.uiTextElement.color = goal.amountRemaining <= 0 ? Color.gray : Color.white;
        }
    }

    void ShowAndSetupLevelCompletePanel()
    {
        if (levelCompletePanelObject == null)
        { 
            return;
        }


        Button nextBtn = levelCompletePanelObject.transform.Find("NextLevelButton")?.GetComponent<Button>();
        Button replayBtn = levelCompletePanelObject.transform.Find("ReplayButton")?.GetComponent<Button>();
        Button homeBtn = levelCompletePanelObject.transform.Find("HomeButton")?.GetComponent<Button>();

        // Olayları bağla
        if (nextBtn != null)
        {
            nextBtn.onClick.RemoveAllListeners();
            nextBtn.onClick.AddListener(LoadNextLevelScene);
            nextBtn.gameObject.SetActive(!string.IsNullOrEmpty(nextSceneName));
        }
        else 
        { 
            Debug.LogWarning("Panelde 'NextLevelButton' bulunamadı."); 
        }
        if (replayBtn != null) 
        {
            replayBtn.onClick.RemoveAllListeners();
            replayBtn.onClick.AddListener(ReplayLevel);
        }
        else
        { 
            Debug.LogWarning("Panelde 'ReplayButton' bulunamadı.");
        }
        if (homeBtn != null)
        { 
            homeBtn.onClick.RemoveAllListeners();
            homeBtn.onClick.AddListener(GoToMainMenu);
        } 
        else
        {
            Debug.LogWarning("Panelde 'HomeButton' bulunamadı.");
        }

        levelCompletePanelObject.SetActive(true);
        
    }
}