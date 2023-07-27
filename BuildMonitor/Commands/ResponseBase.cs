namespace WebMessage.Commands
{

    public interface IResponse
    {
        bool ReturnValue { get; set; }
    }

    /// <summary>
    /// The basics of a response to a command.
    /// </summary>
    /// <seealso cref="CommandBase"/>
    public abstract class ResponseBase : IResponse
    {
        /// <summary>
        /// If true the command was successfull, else false.
        /// </summary>
        public bool ReturnValue { get; set; } = true;
    }
}
