using UnityEngine;
using System.Collections;

public class PlayMusic : MonoBehaviour 
{
    private AudioSource musicSource;

    void Awake()
    {
        musicSource = GetComponent<AudioSource>();
    }

    public void Play()
    {
        musicSource.Play();
    }

    public void Pause()
    {
        musicSource.Pause();
    }

    public void UnPause()
    {
        musicSource.UnPause();
    }

    public void PlayLevelMusic()
    {
        musicSource.Play();
    }
}
