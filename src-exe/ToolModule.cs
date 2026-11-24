using System.Drawing;
using System.Windows.Forms;

namespace DshExternalTools
{
    /// <summary>
    /// One independently built tool. Adding a tool means adding a subclass and
    /// registering it in MainForm; the home grid and module host need no changes.
    /// </summary>
    public abstract class ToolModule
    {
        public abstract string Id { get; }
        public abstract string Title { get; }
        public abstract string Description { get; }
        public abstract string Category { get; }
        public abstract string IconId { get; }
        public abstract Color Accent { get; }
        public abstract Panel CreateView(MainForm host);
        public virtual void Refresh(MainForm host) { }
    }
}
