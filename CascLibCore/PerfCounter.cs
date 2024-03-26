using System;
using System.Diagnostics;

namespace CASCExplorer
{
    public class PerfCounter : IDisposable
    {
        private readonly string _name;
        private readonly Stopwatch _sw;

        public PerfCounter(string name)
        {
            _name = name;
            _sw = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _sw.Stop();

            Console.WriteLine("{0} completed in {1}", _name, _sw.Elapsed);
            Logger.WriteLine("{0} completed in {1}", _name, _sw.Elapsed);
        }
    }
}
