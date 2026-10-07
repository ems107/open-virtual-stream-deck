using OVSD.Core.Model;

namespace OVSD.Core.Storage;

/// <summary>Repairs profiles coming from the editor or from imports so the runtime can trust them.</summary>
public static class ProfileNormalizer
{
    public const int MaxRows = 12;
    public const int MaxCols = 16;

    public static Profile Normalize(Profile profile)
    {
        var grid = new GridSize
        {
            Rows = Math.Clamp(profile.Grid.Rows, 1, MaxRows),
            Cols = Math.Clamp(profile.Grid.Cols, 1, MaxCols),
        };

        var pages = profile.Pages.Count > 0
            ? profile.Pages
            : [new Page { Id = Ids.New(), Name = "Home" }];

        var pageIds = pages.Select(p => p.Id).ToHashSet();
        var seenControls = new HashSet<string>();
        var normalizedPages = pages
            .DistinctBy(p => p.Id)
            .Select(page => page with
            {
                ParentId = page.ParentId is not null && page.ParentId != page.Id && pageIds.Contains(page.ParentId)
                    ? page.ParentId
                    : null,
                Controls = page.Controls
                    .Where(c => c.Position.Row < grid.Rows && c.Position.Col < grid.Cols)
                    .Select(c => c with
                    {
                        Id = seenControls.Add(c.Id) ? c.Id : RegisterNew(seenControls),
                        Position = Clamp(c.Position, grid),
                    })
                    .ToList(),
            })
            .ToList();

        var home = pageIds.Contains(profile.HomePageId) ? profile.HomePageId : normalizedPages[0].Id;
        return profile with
        {
            Name = string.IsNullOrWhiteSpace(profile.Name) ? "Profile" : profile.Name.Trim(),
            Grid = grid,
            Pages = normalizedPages,
            HomePageId = home,
        };
    }

    /// <summary>Gives a profile and everything in it fresh ids (used when importing or duplicating).</summary>
    public static Profile WithNewIds(Profile profile)
    {
        var pageMap = profile.Pages.ToDictionary(p => p.Id, _ => Ids.New());
        return profile with
        {
            Id = Ids.New(),
            Revision = 0,
            HomePageId = pageMap.GetValueOrDefault(profile.HomePageId, profile.HomePageId),
            Pages = profile.Pages.Select(p => p with
            {
                Id = pageMap[p.Id],
                ParentId = p.ParentId is null ? null : pageMap.GetValueOrDefault(p.ParentId),
                Controls = p.Controls.Select(c => c with { Id = Ids.New(), Bindings = RemapPages(c.Bindings, pageMap) }).ToList(),
            }).ToList(),
        };
    }

    private static Bindings RemapPages(Bindings b, Dictionary<string, string> pageMap) => b with
    {
        Tap = RemapSteps(b.Tap, pageMap),
        LongPress = RemapSteps(b.LongPress, pageMap),
        DoubleTap = RemapSteps(b.DoubleTap, pageMap),
        Press = RemapSteps(b.Press, pageMap),
        Release = RemapSteps(b.Release, pageMap),
        Change = RemapSteps(b.Change, pageMap),
    };

    private static List<Step>? RemapSteps(List<Step>? steps, Dictionary<string, string> pageMap) =>
        steps?.Select(s => s switch
        {
            ActionStep a when a.Params.TryGetValue("page", out var page) && pageMap.TryGetValue(page, out var mapped) =>
                a with { Params = new Dictionary<string, string>(a.Params) { ["page"] = mapped } },
            IfStep i => i with { Then = RemapSteps(i.Then, pageMap)!, Else = RemapSteps(i.Else, pageMap)! },
            RepeatStep r => r with { Steps = RemapSteps(r.Steps, pageMap)! },
            _ => s,
        }).ToList();

    private static Cell Clamp(Cell cell, GridSize grid)
    {
        var row = Math.Clamp(cell.Row, 0, grid.Rows - 1);
        var col = Math.Clamp(cell.Col, 0, grid.Cols - 1);
        return new Cell
        {
            Row = row,
            Col = col,
            RowSpan = Math.Clamp(cell.RowSpan, 1, grid.Rows - row),
            ColSpan = Math.Clamp(cell.ColSpan, 1, grid.Cols - col),
        };
    }

    private static string RegisterNew(HashSet<string> seen)
    {
        var id = Ids.New();
        seen.Add(id);
        return id;
    }
}
