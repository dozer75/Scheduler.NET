namespace Foralla.Scheduler.EventArgs
{
    internal class AddJobEventArgs : System.EventArgs
    {
        public IJob Job { get; }

        public bool Success { get; set; }

        public AddJobEventArgs(IJob job)
        {
            Job = job;
        }
    }
}
