using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

// copied from vsatisfy
namespace CopeSeetheMeld;

// base class for automation tasks
// all tasks are cancellable, and all continuations are executed on the main thread (in framework update)
// tasks also support progress reporting
// note: it's assumed that any created task will be executed (either by calling Run directly or by passing to Automation.Start)
public abstract class AutoTask
{
    // debug context scope
    protected readonly struct DebugContext : IDisposable
    {
        private readonly AutoTask ctx;
        private readonly int depth;

        public DebugContext(AutoTask ctx, string name)
        {
            this.ctx = ctx;
            depth = this.ctx.debugContext.Count;
            this.ctx.debugContext.Add(name);
            this.ctx.Log("Scope enter");
        }

        public void Dispose()
        {
            ctx.Log($"Scope exit (depth={depth}, cur={ctx.debugContext.Count - 1})");
            if (depth < ctx.debugContext.Count)
                ctx.debugContext.RemoveRange(depth, ctx.debugContext.Count - depth);
        }

        public void Rename(string newName)
        {
            ctx.Log($"Transition to {newName} @ {depth}");
            if (depth < ctx.debugContext.Count)
                ctx.debugContext[depth] = newName;
        }
    }

    public string Status { get; protected set; } = ""; // user-facing status string
    private readonly CancellationTokenSource cts = new();
    private readonly List<string> debugContext = [];

    public void Cancel() => cts.Cancel();

    public void Run(Action completed)
    {
        Plugin.Framework.Run(async () =>
        {
            var task = Execute();
            await task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing); // we don't really care about cancelation...
            if (task.IsFaulted)
                Plugin.Log.Warning($"Task ended with error: {task.Exception}");
            completed();
            cts.Dispose();
        }, cts.Token);
    }

    // implementations are typically expected to be async (coroutines)
    protected abstract Task Execute();

    protected CancellationToken CancelToken => cts.Token;

    // wait for a few frames
    protected Task NextFrame(int numFramesToWait = 1) => Plugin.Framework.DelayTicks(numFramesToWait, cts.Token);

    // wait until condition function returns false, checking once every N frames
    protected async Task WaitWhile(Func<bool> condition, string scopeName, int checkFrequency = 1)
    {
        using var scope = BeginScope(scopeName);
        while (condition())
        {
            Log("waiting...");
            await NextFrame(checkFrequency);
        }
    }

    protected void Log(string message) => Plugin.Log.Debug($"[{GetType().Name}] [{string.Join(" > ", debugContext)}] {message}");

    // start a new debug context; should be disposed, so usually should be assigned to RAII variable
    protected DebugContext BeginScope(string name) => new(this, name);

    // abort a task unconditionally
    protected void Error(string message)
    {
        Log($"Error: {message}");
        throw new Exception($"[{GetType().Name}] [{string.Join(" > ", debugContext)}] {message}");
    }

    // abort a task if condition is true
    protected void ErrorIf(bool condition, string message)
    {
        if (condition)
            Error(message);
    }
}

// utility that allows concurrently executing only one task; starting a new task if one is already in progress automatically cancels olds one
public sealed class Automation : IDisposable
{
    public AutoTask? CurrentTask { get; private set; }

    public bool Running => CurrentTask != null;

    public void Dispose() => Stop();

    // stop executing any running task
    // this requires tasks to cooperate by checking the token
    public void Stop()
    {
        CurrentTask?.Cancel();
        CurrentTask = null;
    }

    // if any other task is running, it's cancelled
    public void Start(AutoTask task)
    {
        Stop();
        CurrentTask = task;
        task.Run(() =>
        {
            if (CurrentTask == task)
                CurrentTask = null;
            // else: some other task is now executing
        });
    }
}

