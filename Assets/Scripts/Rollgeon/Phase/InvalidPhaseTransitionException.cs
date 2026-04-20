namespace Rollgeon.Phase
{
    public class InvalidPhaseTransitionException : System.InvalidOperationException
    {
        public InvalidPhaseTransitionException(string message) : base(message) { }
    }
}
