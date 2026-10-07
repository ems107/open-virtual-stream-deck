using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OVSD.Core.Model;
using OVSD.Core.Protocol;
using OVSD.Core.Storage;

namespace OVSD.Core.Tests;

public sealed class TempDataDir : IDisposable
{
    public DataPaths Paths { get; } = new(Path.Combine(Path.GetTempPath(), "ovsd-test-" + Ids.New()));
    public void Dispose() => Directory.Delete(Paths.Root, recursive: true);
}

public class ProfileRepositoryTests : IDisposable
{
    private readonly TempDataDir _dir = new();
    public void Dispose() => _dir.Dispose();

    private ProfileRepository NewRepo() => new(_dir.Paths, NullLogger<ProfileRepository>.Instance);

    [Fact]
    public void FirstRunCreatesSampleProfile()
    {
        var repo = NewRepo();
        var profile = Assert.Single(repo.All);
        Assert.Equal(2, profile.Pages.Count);
        Assert.True(File.Exists(Path.Combine(_dir.Paths.Profiles, profile.Id + ".json")));
    }

    [Fact]
    public void SaveBumpsRevisionKeepsBackupAndSurvivesReload()
    {
        var repo = NewRepo();
        var profile = repo.All[0];
        string? changed = null;
        repo.Changed += id => changed = id;

        var saved = repo.Save(profile with { Name = "Renamed" });

        Assert.Equal(profile.Revision + 1, saved.Revision);
        Assert.Equal(profile.Id, changed);
        Assert.Single(repo.GetBackups(profile.Id));
        Assert.Equal("Renamed", NewRepo().Get(profile.Id)!.Name);
    }

    [Fact]
    public void RestoreBringsBackPreviousVersion()
    {
        var repo = NewRepo();
        var original = repo.All[0];
        repo.Save(original with { Name = "Changed" });
        var backup = repo.GetBackups(original.Id)[0];

        var restored = repo.Restore(original.Id, backup.Name);

        Assert.Equal(original.Name, restored!.Name);
    }

    [Fact]
    public void DeleteRemovesFileButKeepsBackup()
    {
        var repo = NewRepo();
        var id = repo.All[0].Id;
        Assert.True(repo.Delete(id));
        Assert.Null(repo.Get(id));
        Assert.Contains(repo.GetBackups(id), b => b.Name.EndsWith("-deleted"));
    }

    [Fact]
    public void RejectsPathTraversalIds() =>
        Assert.Throws<ArgumentException>(() => NewRepo().Save(SampleProfile.Create() with { Id = "../evil" }));

    [Fact]
    public void ProfileRoundTripsThroughJsonWithPolymorphicSteps()
    {
        var profile = SampleProfile.Create();
        var json = JsonSerializer.Serialize(profile, ProtocolJson.Options);
        var back = JsonSerializer.Deserialize<Profile>(json, ProtocolJson.Options)!;
        var counter = back.Pages[0].Controls.Single(c => c.Appearance.Icon == "mdi:counter");
        Assert.IsType<SetVariableStep>(counter.Bindings.Tap![0]);
        Assert.Contains("\"type\":\"set\"", json);
    }

    [Fact]
    public void AcceptsDiscriminatorNotFirst()
    {
        var step = JsonSerializer.Deserialize<Step>("""{"ms":5,"type":"delay"}""", ProtocolJson.Options);
        Assert.Equal(5, Assert.IsType<DelayStep>(step).Ms);
    }
}

public class ProfileNormalizerTests
{
    private static Profile Make(params Control[] controls) => new()
    {
        Id = "p",
        Name = " ",
        Grid = new GridSize { Rows = 2, Cols = 3 },
        HomePageId = "missing",
        Pages = [new Page { Id = "a", Name = "A", ParentId = "nope", Controls = [.. controls] }],
    };

    private static Control At(string id, int row, int col, int rowSpan = 1, int colSpan = 1) =>
        new() { Id = id, Position = new Cell { Row = row, Col = col, RowSpan = rowSpan, ColSpan = colSpan } };

    [Fact]
    public void FixesNameHomeParentAndSpans()
    {
        var p = ProfileNormalizer.Normalize(Make(At("x", 1, 2, 5, 5)));
        Assert.Equal("Profile", p.Name);
        Assert.Equal("a", p.HomePageId);
        Assert.Null(p.Pages[0].ParentId);
        var cell = p.Pages[0].Controls[0].Position;
        Assert.Equal((1, 1), (cell.RowSpan, cell.ColSpan));
    }

    [Fact]
    public void DropsControlsOutsideGridAndDedupesIds()
    {
        var p = ProfileNormalizer.Normalize(Make(At("x", 0, 0), At("x", 0, 1), At("y", 9, 0)));
        var ids = p.Pages[0].Controls.Select(c => c.Id).ToList();
        Assert.Equal(2, ids.Count);
        Assert.Equal(2, ids.Distinct().Count());
    }

    [Fact]
    public void WithNewIdsRemapsPageReferences()
    {
        var sample = SampleProfile.Create();
        var copy = ProfileNormalizer.WithNewIds(sample);
        var folderButton = copy.Pages[0].Controls.Single(c => c.Appearance.Text == "Apps");
        var target = ((ActionStep)folderButton.Bindings.Tap![0]).Params["page"];
        Assert.NotEqual(sample.Id, copy.Id);
        Assert.Equal(copy.Pages[1].Id, target);
        Assert.Equal(copy.Pages[0].Id, copy.Pages[1].ParentId);
    }
}

public class MediaStoreTests : IDisposable
{
    private readonly TempDataDir _dir = new();
    public void Dispose() => _dir.Dispose();

    [Fact]
    public void StoresByContentHash()
    {
        var store = new MediaStore(_dir.Paths);
        var a = store.Save([1, 2, 3], ".PNG");
        var b = store.Save([1, 2, 3], ".png");
        Assert.Equal(a, b);
        Assert.NotNull(store.Find(MediaStore.FileNameFromUrl(a)!));
    }

    [Theory]
    [InlineData("../config.json")]
    [InlineData("abc.png")]
    public void FindRejectsForeignNames(string name) => Assert.Null(new MediaStore(_dir.Paths).Find(name));

    [Fact]
    public void RejectsNonImages() => Assert.Throws<ArgumentException>(() => new MediaStore(_dir.Paths).Save([1], ".exe"));
}
