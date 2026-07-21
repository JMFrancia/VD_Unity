using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * All-purpose SFX manager for games that don't need spacial SFX
 */
public class SFXManager : MonoBehaviour
{
    [SerializeField] int _nAudiosources = 10;
    [SerializeField] bool _expandSize = true;

    public static SFXManager Instance { get; private set; }

    Queue<AudioSource> _sources;
    Dictionary<int, AudioSource> _activeSources;
    Dictionary<int, Coroutine> _activeCoroutines;
    Dictionary<int, int> _sequentialActiveSources;

    /*
     * Returns volume of SFX with given ID
     */
    public float GetSFXVolume(int id)
    {
        return _activeSources[id].volume;
    }

    /*
     * Sets volume of SFX with given ID
     */
    public void SetSFXVolume(int id, float volume) {
        _activeSources[id].volume = volume;
    }

    /*
     * Plays an SFX on loop repeatedly
     * Returns ID for SFX to adjust later
     * */
    public int PlayLoopingSFX(AudioClip clip, float volume = 1f, float pitch = 1f) {
        return PlaySFXInternal(clip, volume, pitch, true);
    }

    /*
    * Plays an SFX once
    * Returns ID for SFX to adjust later
    */
    public int PlaySFX(AudioClip clip, float volume = 1f, float pitch =1, System.Action callback = null)
    {
        return PlaySFXInternal(clip, volume, pitch, false, callback);
    }

    /*
     * Stops an SFX with given ID
     */
    public void StopSFX(int id)
    {
        AudioSource src;
        if (_activeSources.TryGetValue(id, out src))
        {
            src.Stop();
            ResetSource(src);
        }
    }

    /*
     * Stops all non-sequential SFX
     */
    public void StopAllSFX() {
        foreach(int key in _activeSources.Keys)
        {
            StopSFX(key);
        }
    }

    /*
     * Plays an array of SFX in a row
     * Returns int ID for adjustment later
     */
    public int PlaySequentialSFX(AudioClip[] clips, float gap = 0f, System.Action callback = null) {
        int id = clips.GetHashCode();
        Coroutine routine = StartCoroutine(PlaySequentialSFXInteral(id, clips, gap, callback));
        _activeCoroutines[id] = routine;
        return id;
    }

    /*
     * Stops a sequential SFX of given ID
     */
    public bool StopSequentialSFX(int id) {
        Coroutine routine;
        if (_activeCoroutines.TryGetValue(id, out routine))
        {
            StopSFX(_sequentialActiveSources[id]);
            StopCoroutine(_activeCoroutines[id]);
            _activeCoroutines.Remove(id);
            _sequentialActiveSources.Remove(id);
            return true;
        }
        return false;
    }

    public int FadeInLoopingSFX(AudioClip clip, float volume, float fadeTime, System.Action callback = null)
    {
        int result = PlayLoopingSFX(clip, 0f);
        StartCoroutine(FadeSFX(result, 0f, volume, fadeTime, callback));
        return result;
    }

    public void FadeOutLoopingSFX(int id, float fadeTime, System.Action callback = null)
    {
        FadeOutSFX(id, fadeTime, callback);
    }

    public void FadeOutSFX(int id, float fadeTime, System.Action callback = null)
    {
        StartCoroutine(FadeSFX(id, GetSFXVolume(id), 0f, fadeTime, callback));
    }

    public int FadeInSFX(AudioClip clip, float volume, float fadeTime, System.Action callback = null)
    {
        int result = PlaySFX(clip, 0f);
        StartCoroutine(FadeSFX(result, 0f, volume, fadeTime, callback));
        return result;
    }

    public void FadeInOutSFX(AudioClip clip, float volume, float fadeInTime, float fadeOutTime, System.Action callback = null) {
        int id = PlaySFX(clip, 0);
        StartCoroutine(FadeSFX(id, 0f, volume, fadeInTime));
        StartCoroutine(StartFadeoutAtEnd(id, fadeOutTime, callback));
    }

    IEnumerator StartFadeoutAtEnd(int id, float fadeTime, System.Action callback = null) {
        yield return new WaitForSeconds(_activeSources[id].clip.length - fadeTime);
        FadeOutSFX(id, fadeTime, callback);
    }

    IEnumerator FadeSFX(int id, float from, float to, float fadeTime, System.Action callback = null)
    {
        float timePassed = 0;
        WaitForEndOfFrame wait = new WaitForEndOfFrame();
        while (Mathf.Abs(_activeSources[id].volume - to) > .05f)
        {
            float newVolume = Mathf.Lerp(from, to, timePassed / fadeTime);
            SetSFXVolume(id, newVolume);
            timePassed = Mathf.Min(fadeTime, timePassed + Time.deltaTime);
            yield return wait;
        }
        SetSFXVolume(id, to);
        callback?.Invoke();
    }

    IEnumerator PlaySequentialSFXInteral(int id, AudioClip[] clips, float gap = 0f, System.Action callback = null) {
        for (int n = 0; n < clips.Length; n++) {
            _sequentialActiveSources[id] = PlaySFX(clips[n]);
            PlaySFX(clips[n]);
            yield return new WaitForSeconds(clips[n].length + gap);
        }
        _sequentialActiveSources.Remove(id);
        callback?.Invoke();
        _activeCoroutines.Remove(id);
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }

        _sources = new Queue<AudioSource>();
        _activeSources = new Dictionary<int, AudioSource>();
        _activeCoroutines = new Dictionary<int, Coroutine>();
        _sequentialActiveSources = new Dictionary<int, int>();
        for (int n = 0; n < _nAudiosources; n++)
        {
            AddAudioSource();
        }
    }

    int PlaySFXInternal(AudioClip clip, float volume, float pitch, bool loop, System.Action callback = null) {
        if (!FreeSourceAvailable())
        {
            Debug.LogError($"Cannot play SFX {clip.name}; out of sources and no expansion");
            return -1;
        }
        AudioSource src = _sources.Dequeue();
        src.loop = loop;
        src.clip = clip;
        src.volume = volume;
        src.pitch = pitch;
        int id = src.GetInstanceID();
        _activeSources.Add(id, src);
        src.Play();
        if (!loop)
        {
            _activeCoroutines.Add(id, StartCoroutine(OnSFXComplete(src, callback)));
        }
        return id;
    }

    IEnumerator OnSFXComplete(AudioSource src, System.Action callback = null)
    {
        yield return new WaitForSeconds(src.clip.length);
        ResetSource(src);
        callback?.Invoke();
    }

    void ResetSource(AudioSource src) {
        src.clip = null;
        src.loop = false;
        _sources.Enqueue(src);
        int id = src.GetInstanceID();
        _activeSources.Remove(id);

        Coroutine routine;
        if (_activeCoroutines.TryGetValue(id, out routine))
        {
            StopCoroutine(routine);
            _activeCoroutines.Remove(id);
        }
    }

    bool FreeSourceAvailable()
    {
        if (_sources.Count == 0)
        {
            if (_expandSize)
            {
                AddAudioSource();
                return true;
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    void AddAudioSource() {
        AudioSource newSource = gameObject.AddComponent<AudioSource>();
        newSource.playOnAwake = false;
        _sources.Enqueue(newSource);
    }
}
