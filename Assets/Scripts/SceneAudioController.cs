// Автор: Марьяновский Владислав Андреевич

using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SceneAudioController : MonoBehaviour
{
    [SerializeField] private string backgroundMusicResourcePath = "Audio/Open World Happiness Full";
    [SerializeField] private float backgroundVolume = 0.35f;

    private void Start()
    {
        var audioSource = GetComponent<AudioSource>();
        if (audioSource.clip == null)
        {
            audioSource.clip = Resources.Load<AudioClip>(backgroundMusicResourcePath);
        }

        if (audioSource.clip == null)
        {
            Debug.LogWarning("Фоновая музыка не найдена");
            return;
        }

        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = backgroundVolume;

        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }
}
