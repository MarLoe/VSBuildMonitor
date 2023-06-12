using System.Text.Json.Serialization;

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
    /// The basics of a command for WebOS
    /// </summary>
    public abstract class CommandBase : ICommand, ICommandCustom
    {
        [JsonIgnore]
        public abstract string Uri { get; }

        [JsonIgnore]
        public string? CustomId { get; set; }

        [JsonIgnore]
        public string? CustomType { get; set; }
    }
}
