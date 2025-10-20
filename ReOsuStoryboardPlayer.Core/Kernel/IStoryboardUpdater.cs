using ReOsuStoryboardPlayer.Core.Base;

namespace ReOsuStoryboardPlayer.Core.Kernel;

public interface IStoryboardUpdater
{
    void AddNeedResortObject(StoryboardObject storyboardObject);
}