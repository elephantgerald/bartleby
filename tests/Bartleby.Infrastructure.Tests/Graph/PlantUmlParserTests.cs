using Bartleby.Infrastructure.Graph;

namespace Bartleby.Infrastructure.Tests.Graph;

public class PlantUmlParserTests
{
    private readonly PlantUmlParser _parser = new();

    #region Valid PlantUML Tests

    [Fact]
    public void Parse_EmptyContent_ReturnsEmptyGraph()
    {
        // Arrange
        var content = "";

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Nodes);
        Assert.Empty(result.Edges);
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsEmptyGraph()
    {
        // Arrange
        var content = "   \n\n   \t\t  \n";

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Nodes);
        Assert.Empty(result.Edges);
    }

    [Fact]
    public void Parse_SimpleComponent_ExtractsNodeCorrectly()
    {
        // Arrange
        var content = """
            @startuml
            component "Task A" as A
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Nodes.Count);
        Assert.Equal("Task A", result.Nodes["A"].Title);
        Assert.Equal("A", result.Nodes["A"].Alias);
        Assert.Equal(PlantUmlNodeType.Component, result.Nodes["A"].Type);
    }

    [Fact]
    public void Parse_MultipleComponents_ExtractsAllNodes()
    {
        // Arrange
        var content = """
            @startuml
            component "Task A" as A
            component "Task B" as B
            component "Task C" as C
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.Nodes.Count);
        Assert.True(result.Nodes.ContainsKey("A"));
        Assert.True(result.Nodes.ContainsKey("B"));
        Assert.True(result.Nodes.ContainsKey("C"));
    }

    [Fact]
    public void Parse_ComponentWithAliasOnly_UseAliasAsTitle()
    {
        // Arrange
        var content = """
            @startuml
            component TaskA
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Nodes.Count);
        Assert.Equal("TaskA", result.Nodes["TaskA"].Title);
        Assert.Equal("TaskA", result.Nodes["TaskA"].Alias);
    }

    [Theory]
    [InlineData("component")]
    [InlineData("object")]
    [InlineData("rectangle")]
    [InlineData("node")]
    [InlineData("package")]
    public void Parse_DifferentNodeTypes_ParsesCorrectly(string nodeType)
    {
        // Arrange
        var content = $"""
            @startuml
            {nodeType} "Test" as T
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Nodes.Count);
        Assert.Equal("Test", result.Nodes["T"].Title);
    }

    [Fact]
    public void Parse_SolidArrow_ExtractsEdgeCorrectly()
    {
        // Arrange
        var content = """
            @startuml
            component "Task A" as A
            component "Task B" as B
            A --> B
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Edges.Count);
        Assert.Equal("A", result.Edges[0].From);
        Assert.Equal("B", result.Edges[0].To);
        Assert.Equal(PlantUmlArrowType.Solid, result.Edges[0].ArrowType);
    }

    [Fact]
    public void Parse_DashedArrow_ExtractsEdgeCorrectly()
    {
        // Arrange
        var content = """
            @startuml
            component "Task A" as A
            component "Task B" as B
            A ..> B
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Edges.Count);
        Assert.Equal(PlantUmlArrowType.Dashed, result.Edges[0].ArrowType);
    }

    [Fact]
    public void Parse_BoldArrow_ExtractsEdgeCorrectly()
    {
        // Arrange
        var content = """
            @startuml
            component "Task A" as A
            component "Task B" as B
            A ==> B
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Edges.Count);
        Assert.Equal(PlantUmlArrowType.Bold, result.Edges[0].ArrowType);
    }

    [Fact]
    public void Parse_ArrowWithLabel_ExtractsLabelCorrectly()
    {
        // Arrange
        var content = """
            @startuml
            component "Task A" as A
            component "Task B" as B
            A --> B : depends on
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("depends on", result.Edges[0].Label);
    }

    [Fact]
    public void Parse_ReverseArrow_ExtractsEdgeWithReversedDirection()
    {
        // Arrange
        var content = """
            @startuml
            component "Task A" as A
            component "Task B" as B
            A <-- B
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Edges.Count);
        Assert.Equal("B", result.Edges[0].From);
        Assert.Equal("A", result.Edges[0].To);
    }

    [Fact]
    public void Parse_Comments_IgnoresCommentLines()
    {
        // Arrange
        var content = """
            @startuml
            ' This is a comment
            component "Task A" as A
            ' Another comment
            component "Task B" as B
            ' Edge comment
            A --> B
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Nodes.Count);
        Assert.Equal(1, result.Edges.Count);
    }

    [Fact]
    public void Parse_ComplexDiagram_ExtractsAllElements()
    {
        // Arrange - same as example from story
        var content = """
            @startuml
            component "Task A" as A
            component "Task B" as B
            component "Task C" as C

            A --> B : depends on
            B --> C : depends on
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.Nodes.Count);
        Assert.Equal(2, result.Edges.Count);

        Assert.Equal("A", result.Edges[0].From);
        Assert.Equal("B", result.Edges[0].To);
        Assert.Equal("B", result.Edges[1].From);
        Assert.Equal("C", result.Edges[1].To);
    }

    [Fact]
    public void Parse_CaseInsensitiveNodeTypes_ParsesCorrectly()
    {
        // Arrange
        var content = """
            @startuml
            COMPONENT "Upper" as U
            Component "Mixed" as M
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Nodes.Count);
    }

    #endregion

    #region Malformed PlantUML Tests

    [Fact]
    public void Parse_DuplicateAlias_ReportsError()
    {
        // Arrange
        var content = """
            @startuml
            component "Task A" as A
            component "Task B" as A
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Message.Contains("Duplicate node alias"));
    }

    [Fact]
    public void Parse_EdgeWithUnknownFromNode_ReportsError()
    {
        // Arrange
        var content = """
            @startuml
            component "Task A" as A
            X --> A
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Message.Contains("unknown node 'X'"));
    }

    [Fact]
    public void Parse_EdgeWithUnknownToNode_ReportsError()
    {
        // Arrange
        var content = """
            @startuml
            component "Task A" as A
            A --> Y
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Message.Contains("unknown node 'Y'"));
    }

    [Fact]
    public void Parse_MissingEndUml_ReportsError()
    {
        // Arrange
        var content = """
            @startuml
            component "Task A" as A
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Message.Contains("Missing @enduml"));
    }

    [Fact]
    public void Parse_NestedStartUml_ReportsError()
    {
        // Arrange
        var content = """
            @startuml
            component "Task A" as A
            @startuml
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Message.Contains("Unexpected @startuml"));
    }

    [Fact]
    public void Parse_UnmatchedEndUml_ReportsError()
    {
        // Arrange
        var content = """
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Message.Contains("Unexpected @enduml"));
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Parse_EmptyDiagram_ReturnsEmptyGraph()
    {
        // Arrange
        var content = """
            @startuml
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Nodes);
        Assert.Empty(result.Edges);
    }

    [Fact]
    public void Parse_OnlyComments_ReturnsEmptyGraph()
    {
        // Arrange
        var content = """
            @startuml
            ' This is a comment
            ' Another comment
            ' More comments
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Nodes);
        Assert.Empty(result.Edges);
    }

    [Fact]
    public void Parse_ContentOutsideUmlBlock_IsIgnored()
    {
        // Arrange
        var content = """
            component "Outside" as X
            @startuml
            component "Inside" as A
            @enduml
            component "After" as Y
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Nodes.Count);
        Assert.True(result.Nodes.ContainsKey("A"));
        Assert.False(result.Nodes.ContainsKey("X"));
        Assert.False(result.Nodes.ContainsKey("Y"));
    }

    [Fact]
    public void Parse_NoUmlBlocks_ParsesEverything()
    {
        // Arrange - no @startuml/@enduml means parse everything
        var content = """
            component "Task A" as A
            component "Task B" as B
            A --> B
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Nodes.Count);
        Assert.Equal(1, result.Edges.Count);
    }

    [Fact]
    public void Parse_WindowsLineEndings_ParsesCorrectly()
    {
        // Arrange
        var content = "@startuml\r\ncomponent \"Task A\" as A\r\ncomponent \"Task B\" as B\r\nA --> B\r\n@enduml";

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Nodes.Count);
        Assert.Equal(1, result.Edges.Count);
    }

    [Fact]
    public void Parse_MixedLineEndings_ParsesCorrectly()
    {
        // Arrange
        var content = "@startuml\ncomponent \"Task A\" as A\r\ncomponent \"Task B\" as B\rA --> B\n@enduml";

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Nodes.Count);
        Assert.Equal(1, result.Edges.Count);
    }

    [Fact]
    public void Parse_ExtraWhitespace_ParsesCorrectly()
    {
        // Arrange
        var content = """
            @startuml
                component   "Task A"   as   A
                  A   -->   B   :   label text
                component "Task B" as B
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Nodes.Count);
    }

    [Fact]
    public void Parse_NodeLineNumbers_AreRecorded()
    {
        // Arrange
        var content = """
            @startuml
            component "Task A" as A
            component "Task B" as B
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.Equal(2, result.Nodes["A"].LineNumber);
        Assert.Equal(3, result.Nodes["B"].LineNumber);
    }

    [Fact]
    public void Parse_EdgeLineNumbers_AreRecorded()
    {
        // Arrange
        var content = """
            @startuml
            component "Task A" as A
            component "Task B" as B
            A --> B
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.Equal(4, result.Edges[0].LineNumber);
    }

    [Fact]
    public void Parse_CaseInsensitiveAliasLookup_Works()
    {
        // Arrange
        var content = """
            @startuml
            component "Task A" as TaskA
            component "Task B" as TaskB
            taska --> taskb
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Edges.Count);
    }

    [Fact]
    public void Parse_MultipleEdgesBetweenSameNodes_AllRecorded()
    {
        // Arrange
        var content = """
            @startuml
            component "Task A" as A
            component "Task B" as B
            A --> B : first
            A --> B : second
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Edges.Count);
        Assert.Equal("first", result.Edges[0].Label);
        Assert.Equal("second", result.Edges[1].Label);
    }

    #endregion

    #region Extended Syntax Tests (PR Review Feedback)

    [Theory]
    [InlineData("task-1")]
    [InlineData("my.component")]
    [InlineData("task_name")]
    [InlineData("my-task.v2")]
    [InlineData("Task-With-Hyphens")]
    public void Parse_AliasWithSpecialChars_ParsesCorrectly(string alias)
    {
        // Arrange
        var content = $"""
            @startuml
            component "Test" as {alias}
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Nodes.ContainsKey(alias));
        Assert.Equal(alias, result.Nodes[alias].Alias);
    }

    [Fact]
    public void Parse_SingleDashArrow_ParsesCorrectly()
    {
        // Arrange
        var content = """
            @startuml
            component "A" as A
            component "B" as B
            A -> B
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Edges.Count);
        Assert.Equal("A", result.Edges[0].From);
        Assert.Equal("B", result.Edges[0].To);
        Assert.Equal(PlantUmlArrowType.Solid, result.Edges[0].ArrowType);
    }

    [Theory]
    [InlineData("A --> B", "A", "B")]
    [InlineData("A -> B", "A", "B")]
    [InlineData("A ---> B", "A", "B")]
    [InlineData("A ..> B", "A", "B")]
    [InlineData("A ...> B", "A", "B")]
    [InlineData("A ==> B", "A", "B")]
    [InlineData("A ===> B", "A", "B")]
    public void Parse_VariousArrowLengths_ParsesCorrectly(string arrow, string expectedFrom, string expectedTo)
    {
        // Arrange
        var content = $"""
            @startuml
            component "A" as A
            component "B" as B
            {arrow}
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Edges.Count);
        Assert.Equal(expectedFrom, result.Edges[0].From);
        Assert.Equal(expectedTo, result.Edges[0].To);
    }

    [Theory]
    [InlineData("A -->o B")]
    [InlineData("A o--> B")]
    [InlineData("A -->* B")]
    [InlineData("A *--> B")]
    [InlineData("A --># B")]
    [InlineData("A -->x B")]
    public void Parse_ArrowWithDecorators_ParsesCorrectly(string arrow)
    {
        // Arrange
        var content = $"""
            @startuml
            component "A" as A
            component "B" as B
            {arrow}
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Edges.Count);
        Assert.Equal("A", result.Edges[0].From);
        Assert.Equal("B", result.Edges[0].To);
    }

    [Fact]
    public void Parse_TitleWithEscapedQuotes_UnescapesCorrectly()
    {
        // Arrange
        var content = """
            @startuml
            component "Say \"Hello\" World" as A
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Nodes.Count);
        Assert.Equal("Say \"Hello\" World", result.Nodes["A"].Title);
    }

    [Fact]
    public void Parse_TitleWithEscapedBackslash_UnescapesCorrectly()
    {
        // Arrange
        var content = """
            @startuml
            component "Path: C:\\Users\\test" as A
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Path: C:\\Users\\test", result.Nodes["A"].Title);
    }

    [Fact]
    public void Parse_ReverseArrowWithDecorators_ParsesCorrectly()
    {
        // Arrange
        var content = """
            @startuml
            component "A" as A
            component "B" as B
            A <--o B
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Edges.Count);
        Assert.Equal("B", result.Edges[0].From);
        Assert.Equal("A", result.Edges[0].To);
    }

    [Fact]
    public void Parse_AliasesWithHyphensInEdge_ParsesCorrectly()
    {
        // Arrange
        var content = """
            @startuml
            component "Task 1" as task-1
            component "Task 2" as task-2
            task-1 --> task-2
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Nodes.Count);
        Assert.Equal(1, result.Edges.Count);
        Assert.Equal("task-1", result.Edges[0].From);
        Assert.Equal("task-2", result.Edges[0].To);
    }

    [Fact]
    public void Parse_ComplexDiagramWithExtendedSyntax_ParsesCorrectly()
    {
        // Arrange
        var content = """
            @startuml
            component "Authentication \"OAuth\"" as auth-service
            component "User DB" as user.db
            component "API Gateway" as api_gateway

            auth-service --> user.db : queries
            api_gateway -> auth-service : validates
            user.db <--o api_gateway : caches
            @enduml
            """;

        // Act
        var result = _parser.Parse(content);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.Nodes.Count);
        Assert.Equal("Authentication \"OAuth\"", result.Nodes["auth-service"].Title);
        Assert.Equal(3, result.Edges.Count);
    }

    #endregion
}
