using CodeRev.Core.Protocol;

namespace CodeRev.Core.Tests;

public class EventStreamReaderTests
{
    private static async Task<List<CoderevEvent>> ReadAll(string ndjson)
    {
        var reader = new EventStreamReader();
        var events = new List<CoderevEvent>();
        using var sr = new StringReader(ndjson);
        await foreach (var ev in reader.ReadAsync(sr))
            events.Add(ev);
        return events;
    }

    [Fact]
    public async Task ParsesFullSequence()
    {
        const string ndjson = """
            {"type":"run_start","ts":1,"protocol_version":1,"branch":"feature/x","base":"origin/main","lang":"hu"}
            {"type":"step_start","ts":2,"id":1,"label":"Diff"}
            {"type":"step_info","ts":3,"id":1,"detail":"2 fájl"}
            {"type":"step_done","ts":4,"id":1,"duration_ms":210}
            {"type":"meta","ts":5,"changed_files":["a.go","b.go"],"hunks":47,"diff_bytes":38912,"prompt_bytes":9216}
            {"type":"review","ts":6,"markdown":"## Összegzés"}
            {"type":"done","ts":7,"exit_code":0}
            """;
        var events = await ReadAll(ndjson);

        Assert.Equal(7, events.Count);
        Assert.Equal(EventType.RunStart, events[0].Type);
        Assert.Equal("feature/x", events[0].Branch);
        Assert.Equal(47, events[4].Hunks);
        Assert.Equal(new[] { "a.go", "b.go" }, events[4].ChangedFiles);
        Assert.Equal(0, events[^1].ExitCode);
    }

    [Fact]
    public async Task SkipsBlankLines()
    {
        const string ndjson = "\n  \n{\"type\":\"warn\",\"ts\":1,\"message\":\"x\"}\n\n";
        var events = await ReadAll(ndjson);
        Assert.Single(events);
        Assert.Equal("x", events[0].Message);
    }

    [Fact]
    public async Task SkipsMalformedLineButContinues()
    {
        const string ndjson = """
            {"type":"step_start","ts":1,"id":1,"label":"a"}
            {this is not json}
            {"type":"step_done","ts":2,"id":1}
            """;
        var events = await ReadAll(ndjson);
        Assert.Equal(2, events.Count);
        Assert.Equal(EventType.StepStart, events[0].Type);
        Assert.Equal(EventType.StepDone, events[1].Type);
    }

    [Fact]
    public async Task ToleratesUnknownEventType()
    {
        // Forward compatibility: a future event type still deserializes.
        const string ndjson = """
            {"type":"future_thing","ts":1,"some_new_field":42}
            {"type":"done","ts":2,"exit_code":0}
            """;
        var events = await ReadAll(ndjson);
        Assert.Equal(2, events.Count);
        Assert.Equal("future_thing", events[0].Type);
    }

    [Fact]
    public void TryParseLine_RejectsTypelessObject()
    {
        Assert.False(EventStreamReader.TryParseLine("{\"ts\":1}", out var ev));
        Assert.Null(ev);
    }
}
