using System.Diagnostics;

namespace StaticNorth.AsyncApi.Benchmarks;

internal static class ReleaseProbe
{
    private const int Rounds = 5;
    private const int OperationsPerWorker = 100_000;
    private const int SampleInterval = 128;

    internal static async Task RunAsync(string name, Func<ValueTask> operation, int workers = 1)
    {
        await RunWorkersAsync(operation, new long[workers][], 20_000).ConfigureAwait(false);
        var throughput = new double[Rounds];
        var allocations = new double[Rounds];
        var p95 = new double[Rounds];
        var operations = OperationsPerWorker * workers;
        var samplesPerWorker = (OperationsPerWorker + SampleInterval - 1) / SampleInterval;
        var samples = new long[workers][];
        var merged = new long[workers * samplesPerWorker];

        for (var worker = 0; worker < workers; worker++)
        {
            samples[worker] = new long[samplesPerWorker];
        }

        for (var round = 0; round < Rounds; round++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocated = GC.GetTotalAllocatedBytes(precise: true);
            var start = Stopwatch.GetTimestamp();
            await RunWorkersAsync(operation, samples, OperationsPerWorker).ConfigureAwait(false);
            var elapsed = Stopwatch.GetElapsedTime(start);
            allocations[round] = (GC.GetTotalAllocatedBytes(precise: true) - allocated) / (double)operations;
            throughput[round] = operations / elapsed.TotalSeconds;

            for (var worker = 0; worker < workers; worker++)
            {
                samples[worker].CopyTo(merged, worker * samplesPerWorker);
            }

            Array.Sort(merged);
            var percentileIndex = (int)Math.Ceiling(merged.Length * 0.95) - 1;
            p95[round] = (double)merged[percentileIndex] * 1_000_000 / Stopwatch.Frequency;
        }

        Console.WriteLine($"{name,-42} {Median(throughput),12:N0} {Median(allocations),11:F2} {Median(p95),12:F2}");
    }

    private static Task RunWorkersAsync(Func<ValueTask> operation, long[][] samples, int operations)
    {
        if (samples.Length == 1)
        {
            return RunLoopAsync(operation, samples[0], operations);
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = new Task[samples.Length];

        for (var worker = 0; worker < tasks.Length; worker++)
        {
            tasks[worker] = RunWorkerAsync(start.Task, operation, samples[worker], operations);
        }

        start.SetResult();
        return Task.WhenAll(tasks);
    }

    private static async Task RunWorkerAsync(Task start, Func<ValueTask> operation, long[]? samples, int operations)
    {
        await start.ConfigureAwait(false);
        await RunLoopAsync(operation, samples, operations).ConfigureAwait(false);
    }

    private static async Task RunLoopAsync(Func<ValueTask> operation, long[]? samples, int operations)
    {
        var sample = 0;

        for (var iteration = 0; iteration < operations; iteration++)
        {
            if (samples is not null && iteration % SampleInterval == 0)
            {
                var start = Stopwatch.GetTimestamp();
                await operation().ConfigureAwait(false);
                samples[sample++] = Stopwatch.GetTimestamp() - start;
            }
            else
            {
                await operation().ConfigureAwait(false);
            }
        }
    }

    private static double Median(double[] values)
    {
        Array.Sort(values);
        return values[values.Length / 2];
    }
}
