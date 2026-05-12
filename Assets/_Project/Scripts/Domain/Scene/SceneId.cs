// Unity-independent. No UnityEngine references allowed in this assembly.
public enum SceneId
{
    Bootstrap,
    Persistent,     // _Persistent — always-loaded additive scene for singletons
    Title,
    SongSelect,
    GamePlay,
    Result,
    Config,
    History,
    // Phase 5:
    Matchmaking,
    PVPPrematch,
    PVPSongPick,
    PVPBanPhase,
    PVPResult,
    PVPMatchEnd,
}
