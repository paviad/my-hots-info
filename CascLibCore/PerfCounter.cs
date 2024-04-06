using System.Diagnostics;

namespace CascLibCore;

public class PerfCounter(string name) : IDisposable {
    private readonly Stopwatch _sw = Stopwatch.StartNew();

    public void Dispose() {
        _sw.Stop();

        Console.WriteLine("{0} completed in {1}", name, _sw.Elapsed);
        Logger.WriteLine("{0} completed in {1}", name, _sw.Elapsed);
    }
}
