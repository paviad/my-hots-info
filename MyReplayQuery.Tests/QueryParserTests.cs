using MyReplayQuery.Query;

namespace MyReplayQuery.Tests;

public class QueryParserTests {
    [Fact]
    public void ParsesLetsSequencesAndModifiers() {
        var program = QueryParser.Parse("""
            # a comment
            let land = cast(hero: Deathwing, slot: D) near(me, 6)
            find land then all death(victim: Deathwing, credit: me|ally) within 45s
            find death(victim: $p) not followed by spawn(player: $p) within 1.5m
            find death(victim: enemy) count >= 3 within 12s
            """);

        Assert.Equal(4, program.Statements.Count);

        var let = Assert.IsType<LetStatement>(program.Statements[0]);
        var atom = Assert.IsType<AtomPattern>(let.Pattern);
        Assert.Equal("cast", atom.Kind);
        Assert.Equal(6, Assert.Single(atom.Near).Distance);

        var then = Assert.IsType<SeqPattern>(Assert.IsType<FindStatement>(program.Statements[1]).Pattern);
        Assert.Equal(SeqOp.Then, then.Op);
        Assert.True(then.All);
        Assert.Equal(45, then.Within);
        Assert.IsType<RefPattern>(then.Left);
        var credit = Assert.IsType<AtomPattern>(then.Right).Filters[1];
        Assert.Equal(2, Assert.IsType<AltValue>(credit.Value).Options.Count);

        var notFollowed = Assert.IsType<SeqPattern>(Assert.IsType<FindStatement>(program.Statements[2]).Pattern);
        Assert.Equal(SeqOp.NotFollowedBy, notFollowed.Op);
        Assert.Equal(90, notFollowed.Within);

        var count = Assert.IsType<CountPattern>(Assert.IsType<FindStatement>(program.Statements[3]).Pattern);
        Assert.Equal(3, count.Min);
        Assert.Equal(12, count.Within);
    }

    [Fact]
    public void FindTextEchoesTheWholePattern() {
        var program = QueryParser.Parse("find chat(text: \"gg*\") # trailing comment\nfind ping then ping within 500ms");

        Assert.Equal("chat(text: \"gg*\")", ((FindStatement)program.Statements[0]).Text);
        var second = (FindStatement)program.Statements[1];
        Assert.Equal("ping then ping within 500ms", second.Text);
        Assert.Equal(0.5, ((SeqPattern)second.Pattern).Within);
    }

    [Fact]
    public void SequencesAreLeftAssociative() {
        var program = QueryParser.Parse("find ping then chat within 5s preceded by death within 10s");
        var outer = Assert.IsType<SeqPattern>(((FindStatement)program.Statements[0]).Pattern);

        Assert.Equal(SeqOp.PrecededBy, outer.Op);
        Assert.Equal(SeqOp.Then, Assert.IsType<SeqPattern>(outer.Left).Op);
    }

    [Theory]
    [InlineData("find death(victm: me)", 1, 12, "no field 'victm'")]
    [InlineData("find death then ping", 1, 21, "Expected 'within'")]
    [InlineData("find explosion", 1, 6, "Unknown event or name")]
    [InlineData("find chat(text: \"oops)", 1, 17, "Unterminated string")]
    [InlineData("let x = ping", 1, 13, "no 'find'")]
    [InlineData("find ping then ping within 5x", 1, 28, "Unknown duration unit")]
    [InlineData("find (ping then ping within 5s) near(me, 3)", 1, 33, "'near' applies to a single event")]
    [InlineData("let death = ping find death", 1, 5, "reserved")]
    public void ReportsErrorsWithPositions(string source, int line, int column, string message) {
        var e = Assert.Throws<QueryException>(() => QueryParser.Parse(source));

        Assert.Contains(message, e.Message);
        Assert.Equal(new SourcePos(line, column), e.Pos);
    }

    [Fact]
    public void RendersCaretUnderError() {
        const string source = "find ping\nfind death(victm: me)";
        var e = Assert.Throws<QueryException>(() => QueryParser.Parse(source));

        var lines = e.Render(source).Split('\n');
        Assert.Equal("  find death(victm: me)", lines[1]);
        Assert.Equal("             ^", lines[2]);
    }
}
