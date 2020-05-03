using System.Threading.Tasks;

namespace Foralla.Scheduler.EventArgs
{
    internal class RemoveJobEventArgs : System.EventArgs
    {
        public string Name { get; }

        public Task<bool> Success { get; set; }

        public RemoveJobEventArgs(string name)
        {
            Name = name;
        }
    }
}
