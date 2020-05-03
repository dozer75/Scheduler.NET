# CronScheduler demo project

This project demonstrates the use of the Foralla.Scheduler package with cron expressions.

The project initializes a system job during service configuration that is triggered every 30th second. When the system job is triggered it checks if there 
is any non-system job running. If it is it removes the running job, otherwise it adds it.

The child jobs triggered are executed every 10th seconds but not after 30 seconds after the child job start.

## Build

Clone the solution and build using your favorite build environment.