using BenchmarkDotNet.Running;
using System;
using System.Reflection;

namespace ReOsuStoryboardPlayer.Core.Benchmark
{
    public class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run();
            //var o = new ParserBenchmark();
            //o.Init();
            //o.ParseSimple();
        }
    }   
}
