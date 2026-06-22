public enum AnimationBackend
{
    Spine,      // SkeletonAnimation (most common)
    Animator,   // Unity Animator + AnimatorController
    DOTween,    // DOTween sequence — no component required on target
}
