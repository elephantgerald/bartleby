using Bartleby.Infrastructure.Graph;
using FluentAssertions;

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
        result.Success.Should().BeTrue();
        result.Nodes.Should().BeEmpty();
        result.Edges.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsEmptyGraph()
    {
        // Arrange
        var content = "   \n\n   \t\t  \n";

        // Act
        var result = _parser.Parse(content);

        // Assert
        result.Success.Should().BeTrue();
        result.Nodes.Should().BeEmpty();
        result.Edges.Should().BeEmpty();
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
        result.Success.Should().BeTrue();
        result.Nodes.Should().HaveCount(1);
        result.Nodes["A"].Title.Should().Be("Task A");
        result.Nodes["A"].Alias.Should().Be("A");
        result.Nodes["A"].Type.Should().Be(PlantUmlNodeType.Component);
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
        result.Success.Should().BeTrue();
        result.Nodes.Should().HaveCount(3);
        result.Nodes.Should().ContainKey("A");
        result.Nodes.Should().ContainKey("B");
        result.Nodes.Should().ContainKey("C");
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
        result.Success.Should().BeTrue();
        result.Nodes.Should().HaveCount(1);
        result.Nodes["TaskA"].Title.Should().Be("TaskA");
        result.Nodes["TaskA"].Alias.Should().Be("TaskA");
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
        result.Success.Should().BeTrue();
        result.Nodes.Should().HaveCount(1);
        result.Nodes["T"].Title.Should().Be("Test");
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
        result.Success.Should().BeTrue();
        result.Edges.Should().HaveCount(1);
        result.Edges[0].From.Should().Be("A");
        result.Edges[0].To.Should().Be("B");
        result.Edges[0].ArrowType.Should().Be(PlantUmlArrowType.Solid);
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
        result.Success.Should().BeTrue();
        result.Edges.Should().HaveCount(1);
        result.Edges[0].ArrowType.Should().Be(PlantUmlArrowType.Dashed);
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
        result.Success.Should().BeTrue();
        result.Edges.Should().HaveCount(1);
        result.Edges[0].ArrowType.Should().Be(PlantUmlArrowType.Bold);
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
        result.Success.Should().BeTrue();
        result.Edges[0].Label.Should().Be("depends on");
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
        result.Success.Should().BeTrue();
        result.Edges.Should().HaveCount(1);
        result.Edges[0].From.Should().Be("B");
        result.Edges[0].To.Should().Be("A");
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
        result.Success.Should().BeTrue();
        result.Nodes.Should().HaveCount(2);
        result.Edges.Should().HaveCount(1);
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
        result.Success.Should().BeTrue();
        result.Nodes.Should().HaveCount(3);
        result.Edges.Should().HaveCount(2);

        result.Edges[0].From.Should().Be("A");
        result.Edges[0].To.Should().Be("B");
        result.Edges[1].From.Should().Be("B");
        result.Edges[1].To.Should().Be("C");
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
        result.Success.Should().BeTrue();
        result.Nodes.Should().HaveCount(2);
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
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("Duplicate node alias"));
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
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("unknown node 'X'"));
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
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("unknown node 'Y'"));
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
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("Missing @enduml"));
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
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("Unexpected @startuml"));
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
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("Unexpected @enduml"));
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
        result.Success.Should().BeTrue();
        result.Nodes.Should().BeEmpty();
        result.Edges.Should().BeEmpty();
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
        result.Success.Should().BeTrue();
        result.Nodes.Should().BeEmpty();
        result.Edges.Should().BeEmpty();
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
        result.Success.Should().BeTrue();
        result.Nodes.Should().HaveCount(1);
        result.Nodes.Should().ContainKey("A");
        result.Nodes.Should().NotContainKey("X");
        result.Nodes.Should().NotContainKey("Y");
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
        result.Success.Should().BeTrue();
        result.Nodes.Should().HaveCount(2);
        result.Edges.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_WindowsLineEndings_ParsesCorrectly()
    {
        // Arrange
        var content = "@startuml\r\ncomponent \"Task A\" as A\r\ncomponent \"Task B\" as B\r\nA --> B\r\n@enduml";

        // Act
        var result = _parser.Parse(content);

        // Assert
        result.Success.Should().BeTrue();
        result.Nodes.Should().HaveCount(2);
        result.Edges.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_MixedLineEndings_ParsesCorrectly()
    {
        // Arrange
        var content = "@startuml\ncomponent \"Task A\" as A\r\ncomponent \"Task B\" as B\rA --> B\n@enduml";

        // Act
        var result = _parser.Parse(content);

        // Assert
        result.Success.Should().BeTrue();
        result.Nodes.Should().HaveCount(2);
        result.Edges.Should().HaveCount(1);
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
        result.Success.Should().BeTrue();
        result.Nodes.Should().HaveCount(2);
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
        result.Nodes["A"].LineNumber.Should().Be(2);
        result.Nodes["B"].LineNumber.Should().Be(3);
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
        result.Edges[0].LineNumber.Should().Be(4);
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
        result.Success.Should().BeTrue();
        result.Edges.Should().HaveCount(1);
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
        result.Success.Should().BeTrue();
        result.Edges.Should().HaveCount(2);
        result.Edges[0].Label.Should().Be("first");
        result.Edges[1].Label.Should().Be("second");
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
        result.Success.Should().BeTrue();
        result.Nodes.Should().ContainKey(alias);
        result.Nodes[alias].Alias.Should().Be(alias);
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
        result.Success.Should().BeTrue();
        result.Edges.Should().HaveCount(1);
        result.Edges[0].From.Should().Be("A");
        result.Edges[0].To.Should().Be("B");
        result.Edges[0].ArrowType.Should().Be(PlantUmlArrowType.Solid);
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
        result.Success.Should().BeTrue();
        result.Edges.Should().HaveCount(1);
        result.Edges[0].From.Should().Be(expectedFrom);
        result.Edges[0].To.Should().Be(expectedTo);
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
        result.Success.Should().BeTrue();
        result.Edges.Should().HaveCount(1);
        result.Edges[0].From.Should().Be("A");
        result.Edges[0].To.Should().Be("B");
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
        result.Success.Should().BeTrue();
        result.Nodes.Should().HaveCount(1);
        result.Nodes["A"].Title.Should().Be("Say \"Hello\" World");
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
        result.Success.Should().BeTrue();
        result.Nodes["A"].Title.Should().Be("Path: C:\\Users\\test");
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
        result.Success.Should().BeTrue();
        result.Edges.Should().HaveCount(1);
        result.Edges[0].From.Should().Be("B");
        result.Edges[0].To.Should().Be("A");
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
        result.Success.Should().BeTrue();
        result.Nodes.Should().HaveCount(2);
        result.Edges.Should().HaveCount(1);
        result.Edges[0].From.Should().Be("task-1");
        result.Edges[0].To.Should().Be("task-2");
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
        result.Success.Should().BeTrue();
        result.Nodes.Should().HaveCount(3);
        result.Nodes["auth-service"].Title.Should().Be("Authentication \"OAuth\"");
        result.Edges.Should().HaveCount(3);
    }

    #endregion
}
