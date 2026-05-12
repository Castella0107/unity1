// Unity-independent. No UnityEngine references allowed in this assembly.
// Thread-unsafe singleton store — for main-thread use only.
public static class ParameterStore
{
    static ISceneParameters _pending;
    static ISceneParameters _current;

    public static void SetPending(ISceneParameters parameters) => _pending = parameters;

    public static T GetPending<T>() where T : class, ISceneParameters
    {
        var result = _pending as T;
        if (result != null) _current = _pending;
        return result;
    }

    public static T GetCurrent<T>() where T : class, ISceneParameters => _current as T;

    public static bool HasPending<T>() where T : class, ISceneParameters => _pending is T;

    public static void Clear() { _pending = null; _current = null; }
}
