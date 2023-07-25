namespace WebMessage.Commands.Api
{
    /// <summary>
    /// 
    /// </summary>
    public class BuildProgressCommand : CommandBase
    {
        public override string Uri { get; } = "build/progress";

        public BuildProgressCommand()
        {
        }
    }

    /// <summary>
    /// Response for <seealso cref="BuildProgressCommand"/>.
    /// </summary>
    public class BuildProgressResponse : ResponseBase
    {
        public BuildProgressResponse()
        {
        }

        public BuildProgressResponse(float progress)
        {
            Progress = progress;
            ReturnValue = true;
        }

        public float Progress
        {
            get;
            private set;
        }

    }
}
