using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;

namespace com.lobger.vsbuildmonitor
{
    public class InsertTextHandler : CommandHandler
    {
        protected override void Run()
        {
            var textBuffer = IdeApp.Workbench.ActiveDocument.GetContent<ITextBuffer>();
            var textView = IdeApp.Workbench.ActiveDocument.GetContent<ITextView>();
            textBuffer.Insert(textView.Caret.Position.BufferPosition.Position, "// Hello");
        }

        protected override void Update(CommandInfo info)
        {
            var textBuffer = IdeApp.Workbench.ActiveDocument.GetContent<ITextBuffer>();
            if (textBuffer?.AsTextContainer() is SourceTextContainer container)
            {
                var document = container.GetTextBuffer();
                info.Enabled = document is not null;
            }
        }
    }

    public enum SampleCommands
    {
        InsertText,
    }

}
