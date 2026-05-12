using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Simple driver for the NotePool. Spawns notes via buttons and scrolls them.
// Press [Spawn Tap], [Spawn Hold] to spawn one note on a random lane each press.
// Notes scroll from Z~20 to Z=-2 at scrollSpeed units/sec, then auto-release.

public class NotePoolTestUI : MonoBehaviour
{
    [SerializeField] NotePool            _pool;
    [SerializeField] Button              _btnSpawnTap;
    [SerializeField] Button              _btnSpawnHold;
    [SerializeField] Button              _btnReleaseAll;
    [SerializeField] TextMeshProUGUI     _statusText;
    [SerializeField] float               _scrollSpeed = 5f;

    private static readonly LaneRef[] MainLanes = { LaneRef.Lane0, LaneRef.Lane1, LaneRef.Lane2, LaneRef.Lane3 };
    private static readonly LaneRef[] FxLanes   = { LaneRef.FxL, LaneRef.FxR };

    private double              _visualMs;
    private List<NoteController> _activeNotes = new List<NoteController>();
    private int                 _spawnCount;

    private void Start()
    {
        _btnSpawnTap  .onClick.AddListener(() => SpawnNote(isTap:  true));
        _btnSpawnHold .onClick.AddListener(() => SpawnNote(isTap: false));
        _btnReleaseAll.onClick.AddListener(ReleaseAll);
    }

    private void Update()
    {
        _visualMs += Time.deltaTime * 1000.0;

        for (int i = _activeNotes.Count - 1; i >= 0; i--)
        {
            var note = _activeNotes[i];
            if (note == null || !note.IsActive) { _activeNotes.RemoveAt(i); continue; }

            note.UpdatePosition(_visualMs, _scrollSpeed);

            if (note.transform.localPosition.z < LaneLayout.NoteDespawnZ)
            {
                _pool.Release(note);
                _activeNotes.RemoveAt(i);
            }
        }

        if (_statusText != null)
            _statusText.text = string.Format("Active: {0}  Total spawned: {1}", _activeNotes.Count, _spawnCount);
    }

    private void SpawnNote(bool isTap)
    {
        // 50% chance of FX lane
        bool fx = Random.value > 0.6f;

        LaneRef lane;
        NoteType type;
        if (fx)
        {
            lane = FxLanes[Random.Range(0, FxLanes.Length)];
            type = isTap ? NoteType.FxTap : NoteType.FxHold;
        }
        else
        {
            lane = MainLanes[Random.Range(0, MainLanes.Length)];
            type = isTap ? NoteType.Tap : NoteType.Hold;
        }

        var data = new NoteData
        {
            Id         = ++_spawnCount,
            Type       = type,
            Lane       = lane,
            TimeMs     = _visualMs + (LaneLayout.NoteSpawnZ / _scrollSpeed * 1000.0),
            DurationMs = isTap ? 0.0 : 1000.0
        };

        var note = _pool.Acquire(type);
        note.Initialize(data);
        note.UpdatePosition(_visualMs, _scrollSpeed);  // snap to correct Z immediately
        _activeNotes.Add(note);
    }

    private void ReleaseAll()
    {
        foreach (var n in _activeNotes)
            if (n != null) _pool.Release(n);
        _activeNotes.Clear();
    }
}
