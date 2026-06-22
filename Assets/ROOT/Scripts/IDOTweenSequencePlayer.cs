/// <summary>
/// Implement this on any MonoBehaviour that owns DOTween sequences.
/// ActionExecutor calls Play() by sequence ID without importing DOTween directly.
/// </summary>
public interface IDOTweenSequencePlayer
{
    void Play(string sequenceID, bool loop);
}
