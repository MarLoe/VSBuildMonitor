namespace BuildMonitor.Exceptions
{
    public class CommandException : Exception
    {
        public CommandException(string error) : base(error)
        {
        }
    }
}
