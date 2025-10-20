using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using ReOsuStoryboardPlayer.Core.Base;
using ReOsuStoryboardPlayer.Core.Parser.Collection;
using ReOsuStoryboardPlayer.Core.Parser.Reader;
using ReOsuStoryboardPlayer.Core.Parser.Stream;

namespace ReOsuStoryboardPlayer.Core.Benchmark
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class UpdaterBenchmark
    {
        private const int RandomSeed = 42;

        private Legacy.StoryboardUpdater _legacyUpdater;
        private Kernel.StoryboardUpdater _kernalUpdater;

        private List<StoryboardObject> _objects;

        [Params(0, 65000)]
        public int StartTime;

        private List<float> _timeList = null!;
        private List<float> _shuffleList = null!;

        [GlobalSetup]
        public void Init()
        {
            using var stream = File.OpenRead("OsbFiles/NOMA - LOUDER MACHINE (Skystar).osb");
            using var reader = new OsuFileReader(stream);

            var collection = new VariableCollection(new VariableReader(reader).EnumValues());
            var eventReader = new EventReader(reader, collection);
            var storyboardReader = new StoryboardReader(eventReader);

            _objects = storyboardReader.EnumValues().ToList();
            _objects.RemoveAll(c => c == null);

            foreach (var obj in _objects)
                obj.CalculateAndApplyBaseFrameTime();
            
            _legacyUpdater = new ReOsuStoryboardPlayer.Core.Benchmark.Legacy.StoryboardUpdater(_objects);
            _kernalUpdater = new ReOsuStoryboardPlayer.Core.Kernel.StoryboardUpdater(_objects);

            var interval = 16.66667f; // 60fps
            _timeList = new List<float>();
            for (float time = StartTime; time < StartTime + 500; time += interval)
            {
                _timeList.Add(time);
            }

            _shuffleList = new List<float>(_timeList);
            _shuffleList.Shuffle(new Random(RandomSeed));
        }

        [Benchmark(Baseline = true)]
        public void LegacyContinuously()
        {
            foreach (var time in _timeList)
            {
                _legacyUpdater.Update(time);
            }
        }

        [Benchmark]
        public void LegacyRandomly()
        {
            foreach (var time in _shuffleList)
            {
                _legacyUpdater.Update(time);
            }
        }

        [Benchmark]
        public void KernalContinuously()
        {
            foreach (var time in _timeList)
            {
                _kernalUpdater.Update(time);
            }
        }

        [Benchmark]
        public void KernalRandomly()
        {
            foreach (var time in _shuffleList)
            {
                _kernalUpdater.Update(time);
            }
        }
    }
}
