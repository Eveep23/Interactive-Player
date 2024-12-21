using System.Collections.Generic;

public class SaveData
{
    public string CurrentSegment { get; set; }
    public Dictionary<string, bool> State { get; set; } = new Dictionary<string, bool>();
}

public class Settings
{
    public string AudioLanguage { get; set; }
    public string SubtitleLanguage { get; set; }
    public int UISpeed { get; set; }
}
public class Segment
{
    public string Id { get; set; }
    public int StartTimeMs { get; set; }
    public int EndTimeMs { get; set; }
    public string DefaultNext { get; set; }
    public bool IsStartingSegment { get; set; }
    public int ChoiceDisplayTimeMs { get; set; }
    public int HideChoiceTimeMs { get; set; }
    public List<Choice> Choices { get; set; }
    public int? DefaultChoiceIndex { get; set; }
}

public class SegmentGroup
{
    public string Segment { get; set; }
    public string Precondition { get; set; }
}

public class Choice
{
    public string Text { get; set; }
    public string SegmentId { get; set; }
    public Background Background { get; set; }
    public Background Icon { get; set; }
    public string Id { get; set; }
}

public class Background
{
    public VisualStates VisualStates { get; set; }
}

public class VisualStates
{
    public State Default { get; set; }
}

public class State
{
    public ImageData Image { get; set; }
}

public class ImageData
{
    public string Url { get; set; }
}

public class Moment
{
    public string Type { get; set; }
    public int? UIDisplayMS { get; set; }
    public int? HideTimeoutUiMS { get; set; }
    public List<Choice> Choices { get; set; }
}
