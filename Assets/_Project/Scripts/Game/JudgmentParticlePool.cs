using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JudgmentParticlePool : MonoBehaviour
{
    [SerializeField] GameObject _particlePrefab;
    [SerializeField] int        _preWarm = 30;

    readonly Queue<ParticleSystem> _available = new Queue<ParticleSystem>();
    readonly List<ParticleSystem>  _all       = new List<ParticleSystem>();

    void Awake()
    {
        for (int i = 0; i < _preWarm; i++)
        {
            var ps = CreateOne();
            ps.gameObject.SetActive(false);
            _available.Enqueue(ps);
        }
    }

    public void Spawn(Vector3 worldPos, Color color, float countMultiplier)
    {
        ParticleSystem ps;
        if (_available.Count > 0)
        {
            ps = _available.Dequeue();
        }
        else
        {
            Debug.LogWarning("[JudgmentParticlePool] Pool exhausted — expanding");
            ps = CreateOne();
        }

        ps.gameObject.SetActive(true);
        ps.transform.position = worldPos;

        var main = ps.main;
        main.startColor = color;

        var emission = ps.emission;
        var burst = emission.GetBurst(0);
        burst.count = new ParticleSystem.MinMaxCurve(Mathf.RoundToInt(18 * countMultiplier));
        emission.SetBurst(0, burst);

        ps.Clear();
        ps.Play();

        StartCoroutine(ReturnAfter(ps, main.duration + main.startLifetime.constantMax + 0.1f));
    }

    public void ReleaseAll()
    {
        foreach (var ps in _all)
        {
            if (ps == null || !ps.gameObject.activeSelf) continue;
            ps.Stop();
            ps.gameObject.SetActive(false);
            if (!_available.Contains(ps)) _available.Enqueue(ps);
        }
    }

    ParticleSystem CreateOne()
    {
        var go = Instantiate(_particlePrefab, transform);
        var ps = go.GetComponent<ParticleSystem>();
        _all.Add(ps);
        return ps;
    }

    IEnumerator ReturnAfter(ParticleSystem ps, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (ps != null && ps.gameObject != null)
        {
            ps.gameObject.SetActive(false);
            _available.Enqueue(ps);
        }
    }
}
