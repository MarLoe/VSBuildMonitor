namespace WebMessage.Exceptions
{
    public class CommandException : Exception
    {
        public CommandException(string error) : base(error)
        {
        }
    }
}
