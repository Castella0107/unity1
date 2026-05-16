using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tap / Hold / FxTap / FxHold の 4 種類のノートプレハブを管理するオブジェクトプール。
/// 起動時に各種 100 個を事前生成し、Acquire() / Release() でノートコントローラーの貸し出し・返却を行う。
/// プール枯渇時は警告を出しながら動的に拡張する。
/// </summary>
public class NotePool : MonoBehaviour
{
    [SerializeField] GameObject _tapPrefab;
    [SerializeField] GameObject _holdPrefab;
    [SerializeField] GameObject _fxTapPrefab;
    [SerializeField] GameObject _fxHoldPrefab;

    private const int PRE_WARM = 100;

    private Dictionary<NoteType, Queue<NoteController>> _pools;
    private Dictionary<NoteType, GameObject>            _prefabs;

    private void Awake()
    {
        _prefabs = new Dictionary<NoteType, GameObject>
        {
            { NoteType.Tap,    _tapPrefab    },
            { NoteType.Hold,   _holdPrefab   },
            { NoteType.FxTap,  _fxTapPrefab  },
            { NoteType.FxHold, _fxHoldPrefab },
        };

        _pools = new Dictionary<NoteType, Queue<NoteController>>();
        PreWarm(NoteType.Tap,    _tapPrefab);
        PreWarm(NoteType.Hold,   _holdPrefab);
        PreWarm(NoteType.FxTap,  _fxTapPrefab);
        PreWarm(NoteType.FxHold, _fxHoldPrefab);
    }

    private void PreWarm(NoteType type, GameObject prefab)
    {
        var queue = new Queue<NoteController>(PRE_WARM);
        for (int i = 0; i < PRE_WARM; i++)
        {
            var go   = Instantiate(prefab, transform);
            var ctrl = go.GetComponent<NoteController>();
            ctrl.PoolType = type;
            go.SetActive(false);
            queue.Enqueue(ctrl);
        }
        _pools[type] = queue;
    }

    public NoteController Acquire(NoteType type)
    {
        if (_pools.TryGetValue(type, out var queue) && queue.Count > 0)
            return queue.Dequeue();

        // Pool empty — expand gracefully (should not happen with PRE_WARM=100)
        Debug.LogWarning($"[NotePool] {type} pool empty, expanding.");
        var ctrl = Instantiate(GetPrefab(type), transform).GetComponent<NoteController>();
        ctrl.PoolType = type;
        return ctrl;
    }

    public void Release(NoteController note)
    {
        if (note == null) return;
        note.gameObject.SetActive(false);
        if (_pools.TryGetValue(note.PoolType, out var queue))
            queue.Enqueue(note);
    }

    private GameObject GetPrefab(NoteType type)
    {
        return _prefabs.TryGetValue(type, out var prefab) ? prefab : _tapPrefab;
    }
}
