using System;

namespace CronScheduler.WebHost.DTO
{
    public class Job
    {
        public string Expression { get; }

        public Guid Id { get; }

        public string Name { get; }

        public Job(Guid id, string name, string expression)
        {
            Id = id;
            Name = name;
            Expression = expression;
        }
    }
}
