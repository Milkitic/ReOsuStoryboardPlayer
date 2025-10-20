using System;
using System.Collections.Generic;
using ReOsuStoryboardPlayer.Core.Base;
using ReOsuStoryboardPlayer.Core.Commands.Group.Trigger;
using ReOsuStoryboardPlayer.Core.Utils;

namespace ReOsuStoryboardPlayer.Core.Kernel;

public class StoryboardUpdater : IStoryboardUpdater
{
    private static readonly IComparer<StoryboardObject> ZComparer = new ZAxisComparer();

    private static readonly Comparison<StoryboardObject> FrameTimeComparison =
        (a, b) => a.FrameStartTime.CompareTo(b.FrameStartTime);

    private readonly HashSet<StoryboardObject> _needResortObjects = [];
    private readonly object _resortLock = new();
    private readonly HashSet<StoryboardObject> _resortSetPool = [];
    private readonly List<StoryboardObject> _toRemovePool = [];

    private int _currentIndex = 0;
    private float _prevTime = float.MinValue;
    private List<StoryboardObject> _tempListPool = [];

    public StoryboardUpdater(List<StoryboardObject> objects)
    {
        var backgroundObj = objects.Find(c => c is StoryboardBackgroundObject);

        if (backgroundObj != null && objects.Exists(c =>
                c.ImageFilePath == backgroundObj.ImageFilePath && c is not StoryboardBackgroundObject))
        {
            Log.User("Found another same background image object and delete all background objects.");
            objects.RemoveAll(x => x is StoryboardBackgroundObject);
        }
        else if (backgroundObj != null)
        {
            backgroundObj.Z = -1;
        }

        objects.Sort(FrameTimeComparison);
        StoryboardObjectList = objects;

        var limitUpdateCount = StoryboardObjectList.CalculateMaxUpdatingObjectsCount();
        UpdatingStoryboardObjects = new List<StoryboardObject>(limitUpdateCount);

        Flush();
    }

    public List<StoryboardObject> StoryboardObjectList { get; private set; }
    public List<StoryboardObject> UpdatingStoryboardObjects { get; private set; }

    private void Flush()
    {
        UpdatingStoryboardObjects.Clear();
        _currentIndex = 0;
        TriggerListener.DefaultListener.Reset();
    }

    private void Scan(float currentTime)
    {
        ProcessResortList(currentTime);

        while (_currentIndex < StoryboardObjectList.Count)
        {
            var obj = StoryboardObjectList[_currentIndex];

            if (obj.FrameStartTime > currentTime)
                break;

            if (currentTime <= obj.FrameEndTime)
            {
                AddToUpdating(obj);
            }

            _currentIndex++;
        }
    }

    /// <summary>
    /// 零分配版本的重排处理
    /// </summary>
    private void ProcessResortList(float currentTime)
    {
        lock (_resortLock)
        {
            if (_needResortObjects.Count == 0)
                return;

            // 🔥 复用 HashSet，避免 new
            _resortSetPool.Clear();
            foreach (var obj in _needResortObjects)
                _resortSetPool.Add(obj);

            for (int i = UpdatingStoryboardObjects.Count - 1; i >= 0; i--)
            {
                if (_resortSetPool.Contains(UpdatingStoryboardObjects[i]))
                {
                    UpdatingStoryboardObjects[i].CurrentUpdater = null;
                    UpdatingStoryboardObjects.RemoveAt(i);
                }
            }

            _tempListPool.Clear();
            _tempListPool.Capacity = StoryboardObjectList.Count; // 预留容量

            // 将不需要重排的对象加入
            foreach (var obj in StoryboardObjectList)
            {
                if (!_resortSetPool.Contains(obj))
                {
                    _tempListPool.Add(obj);
                }
            }

            // 加入需要重排的对象
            foreach (var obj in _needResortObjects)
            {
                _tempListPool.Add(obj);
            }

            // 排序
            _tempListPool.Sort(FrameTimeComparison);
            (StoryboardObjectList, _tempListPool) = (_tempListPool, StoryboardObjectList);

            RecalculateCurrentIndex(currentTime);

            // 重新加入需要更新的对象
            foreach (var obj in _needResortObjects)
            {
                if (obj.FrameStartTime <= currentTime && currentTime <= obj.FrameEndTime)
                {
                    AddToUpdating(obj);
                }
            }

            _needResortObjects.Clear();
        }
    }

    private void RecalculateCurrentIndex(float currentTime)
    {
        int low = 0, high = StoryboardObjectList.Count - 1;
        int resultIndex = StoryboardObjectList.Count;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            if (StoryboardObjectList[mid].FrameStartTime >= currentTime)
            {
                resultIndex = mid;
                high = mid - 1; // 继续向左找，寻找最早的匹配项
            }
            else
            {
                low = mid + 1;
            }
        }

        _currentIndex = resultIndex;
    }

    private void AddToUpdating(StoryboardObject obj)
    {
        obj.ResetTransform();
        obj.CurrentUpdater = this;

        int insertPos = UpdatingStoryboardObjects.BinarySearch(obj, ZComparer);
        if (insertPos < 0) insertPos = ~insertPos;
        UpdatingStoryboardObjects.Insert(insertPos, obj);
    }

    public void Update(float currentTime)
    {
        if (currentTime < _prevTime)
        {
            Flush();
        }
        else
        {
            for (int i = UpdatingStoryboardObjects.Count - 1; i >= 0; i--)
            {
                var obj = UpdatingStoryboardObjects[i];
                if (currentTime > obj.FrameEndTime || currentTime < obj.FrameStartTime)
                {
                    obj.CurrentUpdater = null;
                    UpdatingStoryboardObjects.RemoveAt(i);
                }
            }
        }

        _prevTime = currentTime;

        Scan(currentTime);

        var needParallel = UpdatingStoryboardObjects.Count >= Setting.ParallelUpdateObjectsLimitCount
                           && Setting.ParallelUpdateObjectsLimitCount != 0;

        ParallelableForeachExecutor.Foreach(needParallel, UpdatingStoryboardObjects,
            obj => obj.Update(currentTime));
    }

    public void AddNeedResortObject(StoryboardObject obj)
    {
        lock (_resortLock)
        {
            _needResortObjects.Add(obj);
        }
    }

    private class ZAxisComparer : IComparer<StoryboardObject>
    {
        public int Compare(StoryboardObject x, StoryboardObject y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            return x.Z.CompareTo(y.Z);
        }
    }
}