using System.Collections.Generic;

namespace Foralla.Scheduler.EventArgs
{
    internal class GetJobsEventArgs : System.EventArgs
    {
        public IEnumerable<IJob> Jobs { get; set; }

        public bool SystemJobs { get; }

        public GetJobsEventArgs(bool systemJobs)
        {
            SystemJobs = systemJobs;
        }
    }
}
