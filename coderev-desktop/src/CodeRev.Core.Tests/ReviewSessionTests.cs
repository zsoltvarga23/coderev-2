using CodeRev.Core.Models;
using CodeRev.Core.Protocol;

namespace CodeRev.Core.Tests;

public class ReviewSessionTests
{
    [Fact]
    public void FoldsStepsAndMeta()
    {
        var s = new ReviewSession();
        s.Apply(new CoderevEvent { Type = EventType.RunStart, Branch = "feature/x", Base = "origin/main" });
        s.Apply(new CoderevEvent { Type = EventType.StepStart, Id = 1, Label = "Diff" });
        s.Apply(new CoderevEvent { Type = EventType.StepInfo, Id = 1, Detail = "2 fájl" });
        s.Apply(new CoderevEvent { Type = EventType.StepDone, Id = 1, DurationMs = 210 });
        s.Apply(new CoderevEvent { Type = EventType.Meta, ChangedFiles = new[] { "a.go" }, Hunks = 5, DiffBytes = 100, PromptBytes = 200 });

        Assert.Equal("feature/x", s.Branch);
        Assert.Single(s.Steps);
        Assert.Equal(StepStatus.Done, s.Steps[0].Status);
        Assert.Equal("2 fájl", s.Steps[0].Detail);
        Assert.Equal(210, s.Steps[0].DurationMs);
        Assert.NotNull(s.Meta);
        Assert.Equal(5, s.Meta!.Hunks);
        Assert.Equal(new[] { "a.go" }, s.Meta.ChangedFiles);
    }

    [Fact]
    public void StepFailRecordsError()
    {
        var s = new ReviewSession();
        s.Apply(new CoderevEvent { Type = EventType.StepStart, Id = 1, Label = "Fetch" });
        s.Apply(new CoderevEvent { Type = EventType.StepFail, Id = 1, Error = "network down" });
        Assert.Equal(StepStatus.Failed, s.Steps[0].Status);
        Assert.Equal("network down", s.Steps[0].Error);
    }

    [Fact]
    public void StreamChunksAccumulateThenReviewWins()
    {
        var s = new ReviewSession();
        s.Apply(new CoderevEvent { Type = EventType.Stream, Chunk = "## Öss" });
        s.Apply(new CoderevEvent { Type = EventType.Stream, Chunk = "zegzés\n" });
        Assert.Equal("## Összegzés\n", s.ReviewMarkdown);

        // The authoritative review event replaces the accumulated text.
        s.Apply(new CoderevEvent { Type = EventType.Review, Markdown = "## Final" });
        Assert.Equal("## Final", s.ReviewMarkdown);
    }

    [Fact]
    public void DoneMarksComplete()
    {
        var s = new ReviewSession();
        Assert.False(s.IsComplete);
        s.Apply(new CoderevEvent { Type = EventType.Summary, TotalMs = 71000, OutPath = "review.md" });
        s.Apply(new CoderevEvent { Type = EventType.Done, ExitCode = 0 });
        Assert.True(s.IsComplete);
        Assert.Equal(0, s.ExitCode);
        Assert.Equal("review.md", s.OutPath);
        Assert.Equal(71000, s.TotalMs);
    }

    [Fact]
    public void AppliedEventFires()
    {
        var s = new ReviewSession();
        var count = 0;
        s.Applied += _ => count++;
        s.Apply(new CoderevEvent { Type = EventType.Warn, Message = "x" });
        s.Apply(new CoderevEvent { Type = EventType.Warn, Message = "y" });
        Assert.Equal(2, count);
        Assert.Equal(new[] { "x", "y" }, s.Warnings);
    }

    [Fact]
    public void StepInfoFallsBackToLatestStepWhenIdMissing()
    {
        var s = new ReviewSession();
        s.Apply(new CoderevEvent { Type = EventType.StepStart, Id = 1, Label = "a" });
        s.Apply(new CoderevEvent { Type = EventType.StepInfo, Detail = "detail-without-id" });
        Assert.Equal("detail-without-id", s.Steps[0].Detail);
    }
}
