using System.Text;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// スタンドアロンビルドで実測するための軽量プローブ (開発用)。
///
/// エディタ実行のプロファイラはエディタ自身の UI コスト
/// (UIElementsUtility / GameView.Render / IMGUIContainer など各 3ms 超) に支配され、
/// ゲーム側の GC・フレーム時間を測れない。ビルドで動かして Player.log に落とすためのもの。
///
/// 有効化: 起動引数 `--perf-probe` または PlayerPrefs "PerfProbe"==1。
/// 5 秒ごとに「フレーム時間の平均/最悪」と「GC アロケーションの平均/最悪 (bytes/frame)」を
/// 現在のシーン名つきで 1 行出力する。ProfilerRecorder は Development ビルドで動作する。
/// </summary>
public class PerfProbe : MonoBehaviour
{
    const float WindowSec = 5f;

    ProfilerRecorder _gcAlloc;
    ProfilerRecorder _mainThread;

    float  _elapsed;
    int    _frames;
    double _gcSum, _gcMax;
    double _msSum, _msMax;

    readonly StringBuilder _sb = new StringBuilder(200);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        bool on = PlayerPrefs.GetInt("PerfProbe", 0) == 1;
        if (!on)
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
                if (a == "--perf-probe") { on = true; break; }
        }
        if (!on) return;

        var go = new GameObject("[PerfProbe]");
        DontDestroyOnLoad(go);
        go.AddComponent<PerfProbe>();
        Debug.Log("[PerfProbe] 有効 — 5 秒ごとに計測結果を出力します");
    }

    void OnEnable()
    {
        _gcAlloc    = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        _mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1,
                                                ProfilerRecorderOptions.Default);
    }

    void OnDisable()
    {
        _gcAlloc.Dispose();
        _mainThread.Dispose();
    }

    void Update()
    {
        if (_gcAlloc.Valid)
        {
            double v = _gcAlloc.LastValue;
            _gcSum += v;
            if (v > _gcMax) _gcMax = v;
        }
        if (_mainThread.Valid)
        {
            double ms = _mainThread.LastValue * 1e-6;   // ns → ms
            _msSum += ms;
            if (ms > _msMax) _msMax = ms;
        }

        _frames++;
        _elapsed += Time.unscaledDeltaTime;
        if (_elapsed < WindowSec) return;

        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        _sb.Clear();
        _sb.Append("[PerfProbe] scene=").Append(scene)
           .Append(" frames=").Append(_frames)
           .Append(" fps=").Append((_frames / _elapsed).ToString("F1"))
           .Append(" gc_avg=").Append((_gcSum / _frames).ToString("F0")).Append("B")
           .Append(" gc_max=").Append(_gcMax.ToString("F0")).Append("B")
           .Append(" ms_avg=").Append((_msSum / _frames).ToString("F2"))
           .Append(" ms_max=").Append(_msMax.ToString("F2"));
        Debug.Log(_sb.ToString());

        _elapsed = 0f; _frames = 0;
        _gcSum = _gcMax = 0; _msSum = _msMax = 0;
    }
}
