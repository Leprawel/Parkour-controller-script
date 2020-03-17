using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuScript : MonoBehaviour
{
    public static bool gamePaused = false;

    public GameObject pauseMenuUI;
    public GameObject playerCamera;
    private float timSkal = 1f;

    void Start()
    {
        pauseMenuUI.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            if(gamePaused){
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    public void Resume()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        playerCamera.GetComponent<PlayerCameraController>().enabled = true;
        pauseMenuUI.SetActive(false);
        Time.timeScale = timSkal;
        gamePaused = false;
    }

    public void Pause()
    {
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
        playerCamera.GetComponent<PlayerCameraController>().enabled = false;
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        gamePaused = true;
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void SetTimeScale(float newVal)
    {
        timSkal = newVal;
    }
}