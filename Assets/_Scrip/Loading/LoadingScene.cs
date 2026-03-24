using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class LoadingScene : MonoBehaviour
{
    public Slider loadingBar;
    public TextMeshProUGUI loadingText;

    void Start()
    {
        StartCoroutine(LoadAsyncScene("MenuGame")); 
    }

    IEnumerator LoadAsyncScene(string sceneName)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);

            loadingBar.value = progress;
            loadingText.text = (progress * 100f).ToString("F0") + "%";
            if (progress >= 1f)
            {
                yield return new WaitForSeconds(0.5f);
                operation.allowSceneActivation = true;
            }
            yield return null;
        }
    }
}