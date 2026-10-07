namespace OVSD.Core.Model;

/// <summary>A deck layout: a grid shared by all its pages. Each device shows one profile at a time.</summary>
public sealed record Profile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>Incremented by the server on every save, so clients can tell their own writes from others.</summary>
    public int Revision { get; init; }
    public GridSize Grid { get; init; } = new() { Rows = 3, Cols = 5 };
    public required string HomePageId { get; init; }
    public List<Page> Pages { get; init; } = [];
    public Theme Theme { get; init; } = new();
    /// <summary>Foreground apps that make devices in automatic mode switch to this profile.</summary>
    public List<MatchRule> MatchRules { get; init; } = [];

    public Page? FindPage(string pageId) => Pages.Find(p => p.Id == pageId);
}

public sealed record GridSize
{
    public required int Rows { get; init; }
    public required int Cols { get; init; }
}

public sealed record Theme
{
    public string Background { get; init; } = "#111318";
    public string TileBackground { get; init; } = "#1f232b";
    public string TextColor { get; init; } = "#e8eaf0";
    public int Gap { get; init; } = 8;
    public int Radius { get; init; } = 14;
}

public sealed record MatchRule
{
    /// <summary>Process name without ".exe", case-insensitive, '*' wildcards allowed.</summary>
    public string? Process { get; init; }
    /// <summary>Case-insensitive substring of the window title.</summary>
    public string? TitleContains { get; init; }
}

/// <summary>A page of controls. Pages with a parent are folders: they get an automatic "back" tile.</summary>
public sealed record Page
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? ParentId { get; init; }
    public List<Control> Controls { get; init; } = [];
}
