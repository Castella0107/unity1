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
    // Phase 5 (Go フロー):
    Matchmaking,
    PVPPrematch,
    PVPSongPick,    // 統合ドラフト画面 (PvpDraftController、交互ターン制+BAN を内包)
    PVPResult,
    PVPMatchEnd,
    // Online lobby (対戦待合). Between Title's Online menu and Matchmaking.
    PVPLobby,
    // 楽曲別ランキング (SongSelect の R キーで遷移、選択曲のランキング表示)。
    // Appended at the end so existing ordinals (used by enumValueIndex wiring) are unchanged.
    SongRanking,
    // ログイン/新規登録 (Go サーバー移行 M1)。起動時 Bootstrap → Login → Title。
    Login,
}
