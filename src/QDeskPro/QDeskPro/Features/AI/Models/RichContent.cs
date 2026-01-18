using System.Text.Json;
using System.Text.Json.Serialization;

namespace QDeskPro.Features.AI.Models;

/// <summary>
/// Represents the type of rich content in a message
/// </summary>
public enum ContentType
{
    PlainText,
    Markdown,
    DataTable,
    Chart,
    CodeBlock,
    ChartWithTable,
    Mixed
}

/// <summary>
/// Chart types supported for AI chat visualization (prefixed to avoid conflict with MudBlazor.ChartType)
/// </summary>
public enum AIChartType
{
    Bar,
    Line,
    Pie,
    Doughnut
}

/// <summary>
/// Base class for all rich content blocks
/// </summary>
public abstract record RichContentBlock
{
    public abstract ContentType Type { get; }
    public string? Title { get; init; }
}

/// <summary>
/// Markdown/prose content block
/// </summary>
public record MarkdownBlock(string Content) : RichContentBlock
{
    public override ContentType Type => ContentType.Markdown;
}

/// <summary>
/// Data table content block
/// </summary>
public record DataTableBlock : RichContentBlock
{
    public override ContentType Type => ContentType.DataTable;
    public required List<string> Headers { get; init; }
    public required List<Dictionary<string, object?>> Rows { get; init; }
    public string Currency { get; init; } = "KES";
    public bool Exportable { get; init; } = true;
    public string? ToolName { get; init; }
}

/// <summary>
/// Chart content block
/// </summary>
public record ChartBlock : RichContentBlock
{
    public override ContentType Type => ContentType.Chart;
    public required AIChartType ChartType { get; init; }
    public required List<string> Labels { get; init; }
    public required List<ChartDataset> Datasets { get; init; }
    public AIChartOptions? Options { get; init; }
}

/// <summary>
/// Combined chart and table block (side by side)
/// </summary>
public record ChartTableComboBlock : RichContentBlock
{
    public override ContentType Type => ContentType.ChartWithTable;
    public required ChartBlock Chart { get; init; }
    public required DataTableBlock Table { get; init; }
}

/// <summary>
/// Code block with syntax highlighting
/// </summary>
public record CodeBlock(string Code, string? Language = null) : RichContentBlock
{
    public override ContentType Type => ContentType.CodeBlock;
}

/// <summary>
/// Dataset for charts
/// </summary>
public record ChartDataset
{
    public required string Label { get; init; }
    public required List<double> Data { get; init; }
    public string? BackgroundColor { get; init; }
    public string? BorderColor { get; init; }
}

/// <summary>
/// Chart rendering options (prefixed to avoid conflict with MudBlazor)
/// </summary>
public record AIChartOptions
{
    public bool ShowLegend { get; init; } = true;
    public string? YAxisFormat { get; init; }
    public bool Responsive { get; init; } = true;
}

/// <summary>
/// Export request model
/// </summary>
public record ExportRequest
{
    public required string Format { get; init; } // "excel" or "image"
    public required string FileName { get; init; }
    public DataTableBlock? TableData { get; init; }
}

/// <summary>
/// Tool result structure for parsing
/// </summary>
public class ToolResultData
{
    public string ToolName { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public JsonElement? ParsedResult { get; set; }
}
