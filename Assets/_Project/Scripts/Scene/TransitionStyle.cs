/// <summary>
/// シーン遷移時のエフェクト種別を定義する列挙型。SceneRouter および TransitionFx で使用される。
/// </summary>
public enum TransitionStyle
{
    None,        // Instant cut — Phase 1 only implemented style
    FadeBlack,   // Black fade — Phase 2
    FadeWhite,   // White fade (Result screen) — Phase 2
    SlideLeft,   // Slide left (back) — Phase 2
    SlideRight,  // Slide right (forward) — Phase 2
    FastCut,     // Instant cut for PVP rapid transitions — Phase 2
    GameStart,   // Special GamePlay intro — Phase 2
}
