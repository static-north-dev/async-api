# AsyncAPI transport benchmark

Run from the repository root with the SDK pinned in `global.json`:

```sh
dotnet run --project benchmarks/StaticNorth.AsyncApi.Benchmarks --configuration Release
```

The benchmark uses the real generated publisher and subscriber with the in-memory transport. It measures default client construction, payloadless publishing, and typed publishing and receiving with 66-byte and 1,026-byte JSON payloads. The asynchronous handler yields once per delivery. The concurrent scenario uses four workers publishing through the same generated client to the asynchronous subscriber.

Each scenario warms up with 20,000 operations per worker, then reports the median of five rounds with 100,000 operations per worker. Latency is measured on every 128th operation and includes the time until delivery completes. Allocations come from the process-wide GC counter, so work that resumes on another thread is included. The counter also includes benchmark overhead and thread pool activity.

Compare separate Release processes on the same machine. Run them in alternating order when measuring small changes. These in-memory results describe library and scheduling costs. Broker and network costs require a separate workload.
