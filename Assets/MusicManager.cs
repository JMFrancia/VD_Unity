using System;
using System.Collections.Generic;
using UnityEngine;
using EditorAttributes;

public class MusicManager : MonoBehaviour
{
    [SerializeField] private SFXManager _manager;
    [SerializeField] private List<AudioClip> _songs;
    [SerializeField] private int _startingIndex;
    [SerializeField] private float _volume = 1f;
    [SerializeField] private float _startingFadeTime = 3f;
    [SerializeField] private float _crossFadeTime = 3f;

    int _songIndex;
    int _currentSongID;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        ValidateData();

        _songIndex = _startingIndex;
        _currentSongID = _manager.FadeInLoopingSFX(_songs[_songIndex], _volume, _startingFadeTime);
    }

    void ValidateData()
    {
        if (_manager == null)
            throw new InvalidOperationException($"MusicManager on '{name}': {nameof(_manager)} is not assigned");

        if (_songs == null || _songs.Count == 0)
            throw new InvalidOperationException($"MusicManager on '{name}': {nameof(_songs)} is empty");

        for (int n = 0; n < _songs.Count; n++)
        {
            if (_songs[n] == null)
                throw new InvalidOperationException($"MusicManager on '{name}': {nameof(_songs)}[{n}] is empty");
        }

        if (_startingIndex < 0 || _startingIndex >= _songs.Count)
            throw new InvalidOperationException($"MusicManager on '{name}': {nameof(_startingIndex)} is {_startingIndex}, outside {nameof(_songs)} (0..{_songs.Count - 1})");
    }

    [Button("Play Next")]
    void PlayNextSong()
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException("MusicManager: Play Next requires play mode");

        _songIndex = (_songIndex + 1) % _songs.Count;

        // Captured because _currentSongID is reassigned below, while the callback fires a
        // crossfade later — by then the field refers to the incoming track, not this one.
        int outgoingID = _currentSongID;

        // FadeOutLoopingSFX only ramps the volume to zero; without the explicit stop the
        // outgoing track loops silently forever and never returns its source to the pool.
        _manager.FadeOutLoopingSFX(outgoingID, _crossFadeTime, () => _manager.StopSFX(outgoingID));

        _currentSongID = _manager.FadeInLoopingSFX(_songs[_songIndex], _volume, _crossFadeTime);
    }
}
