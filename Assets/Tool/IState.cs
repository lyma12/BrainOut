public interface IState
{
    string StateID { get; }
    void OnEnter();
    void OnExit();
}
