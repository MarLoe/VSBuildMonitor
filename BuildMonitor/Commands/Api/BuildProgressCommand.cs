namespace WebMessage.Commands.Api
{
    /// <summary>
    /// Command for establishing pairing
    /// </summary>
    public class BuildProgressCommand : CommandBase
    {
        public override string Uri { get; } = "build/progress";

        public BuildProgressCommand()
        {
        }
    }

    /// <summary>
    /// Response for <seealso cref="HandshakeCommand"/>.
    /// </summary>
    public class BuildProgressResponse : ResponseBase
    {
        public BuildProgressResponse()
        {
            ReturnValue = true;
        }
    }
}
