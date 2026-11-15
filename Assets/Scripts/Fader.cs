using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransition : MonoBehaviour
{
    [SerializeField] string sceneName;
    [SerializeField] float fadeDuration = 1f;

    float alpha;
    bool transitioning;

    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"Trigger hit by {other.name}, tag: {other.tag}");
        if (transitioning || !other.CompareTag("Player")) return;
        if (other.TryGetComponent(out ChickenMovement player))
        {
        player.canMove = false;
        }

        StartCoroutine(FadeAndLoad());

    }

    IEnumerator FadeAndLoad()
    {
        transitioning = true;

        while (alpha < 1f)
        {
            alpha += Time.deltaTime / fadeDuration;
            yield return null;
        }

        SceneManager.LoadScene(sceneName);
    }

    void OnGUI()
    {
        if (alpha <= 0f) return;

        GUI.depth = -1000; // draw on top of everything
        GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
    }
}