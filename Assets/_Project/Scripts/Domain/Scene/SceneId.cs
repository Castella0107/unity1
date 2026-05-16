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
}
