﻿using System.Collections.Generic;
using System.Windows.Forms;

public class SaveData
{
    public string CurrentSegment { get; set; }
    public Dictionary<string, object> GlobalState { get; set; }
    public Dictionary<string, object> PersistentState { get; set; }
}
public class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel()
    {
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        this.SetStyle(ControlStyles.UserPaint, true);
        this.UpdateStyles();
    }
}
public class Settings
{
    public string AudioLanguage { get; set; }
    public string SubtitleLanguage { get; set; }
    public bool CustomStoryChangingNotification { get; set; }
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
    public TimeoutSegment TimeoutSegment { get; set; }
    public string LayoutType { get; set; }
    public List<Notification> Notification { get; set; }
}

public class Notification
{
    public int StartMs { get; set; }
    public int EndMs { get; set; }
    public string Text { get; set; }
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
    public ImpressionData ImpressionData { get; set; }
    public string sg { get; set; }
    public string Exception { get; set; }
}

public class ImpressionData
{
    public ImpressionDataDetails Data { get; set; }
}

public class ImpressionDataDetails
{
    public Dictionary<string, object> Global { get; set; }
    public Dictionary<string, object> Persistent { get; set; }
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
    public TimeoutSegment TimeoutSegment { get; set; }
    public string LayoutType { get; set; }
    public List<Notification> Notification { get; set; }
}

public class TimeoutSegment
{
    public string SegmentId { get; set; }
}