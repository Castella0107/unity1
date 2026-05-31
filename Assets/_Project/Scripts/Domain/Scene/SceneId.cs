// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// プロジェクト内の全シーンを識別する列挙型。
/// ロビー・ゲームプレイ・リザルト・PVP フェーズを含む。
/// </summary>
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
    // Per-song difficulty + play settings screen, shown before each PVP song (flow ⑦).
    // Appended at the end so existing ordinals (used by enumValueIndex wiring) are unchanged.
    PVPSongSetup,
    // Online lobby (対戦待合). Between Title's Online menu and Matchmaking.
    PVPLobby,
}
