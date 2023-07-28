namespace WebMessage.Commands
{

    public interface ICommand
    {
        string Uri { get; }
    }

    public interface ICommandCustom
    {
        string? CustomId { get; set; }

        string? CustomType { get; set; }
    }

    /// <summary>
    /// The basics of a command
    /// </summary>
    public abstract class CommandBase : ICommand, ICommandCustom
    {
        public abstract string Uri { get; }

        public string? CustomId { get; set; }

        public string? CustomType { get; set; }
    }
}
