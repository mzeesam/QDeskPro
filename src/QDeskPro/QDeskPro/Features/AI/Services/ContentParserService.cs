using System.Text.Json;
using QDeskPro.Domain.Entities;
using QDeskPro.Features.AI.Models;

namespace QDeskPro.Features.AI.Services;

/// <summary>
/// Service to parse AI messages into rich content blocks for rendering
/// </summary>
public interface IContentParserService
{
    /// <summary>
    /// Parse a message into renderable content blocks
    /// </summary>
    List<RichContentBlock> Parse(AIMessage message, List<ToolResultData>? toolResults = null);

    /// <summary>
    /// Parse tool results into content blocks
    /// </summary>
    List<RichContentBlock> ParseToolResults(List<ToolResultData> toolResults);
}

public class ContentParserService : IContentParserService
{
    // Tools that produce tabular data only (no chart)
    private static readonly HashSet<string> TableOnlyTools =
    [
        "get_sales_by_date_range",
        "get_expenses_by_date_range",
        "get_fuel_usage",
        "get_banking_records",
        "get_unpaid_orders"
    ];

    // Tools that produce chart + table (side by side)
    private static readonly HashSet<string> ChartTableTools =
    [
        "get_sales_by_product",
        "get_sales_by_broker",
        "get_expenses_by_category",
        "get_top_products"
    ];

    // Chart type mapping for tools
    private static readonly Dictionary<string, AIChartType> ToolChartTypes = new()
    {
        ["get_sales_by_product"] = AIChartType.Pie,
        ["get_sales_by_broker"] = AIChartType.Bar,
        ["get_expenses_by_category"] = AIChartType.Doughnut,
        ["get_top_products"] = AIChartType.Bar
    };

    public List<RichContentBlock> Parse(AIMessage message, List<ToolResultData>? toolResults = null)
    {
        var blocks = new List<RichContentBlock>();

        // Parse tool results if provided
        if (toolResults != null && toolResults.Count > 0)
        {
            blocks.AddRange(ParseToolResults(toolResults));
        }

        // Parse assistant message content as markdown
        if (!string.IsNullOrEmpty(message.Content) && message.Role == "assistant")
        {
            blocks.Add(new MarkdownBlock(message.Content));
        }

        return blocks;
    }

    public List<RichContentBlock> ParseToolResults(List<ToolResultData> toolResults)
    {
        var blocks = new List<RichContentBlock>();

        foreach (var tool in toolResults)
        {
            try
            {
                var parsed = ParseSingleToolResult(tool);
                if (parsed != null)
                {
                    blocks.Add(parsed);
                }
            }
            catch (Exception)
            {
                // If parsing fails, show as code block
                blocks.Add(new CodeBlock(tool.Result, "json"));
            }
        }

        return blocks;
    }

    private RichContentBlock? ParseSingleToolResult(ToolResultData tool)
    {
        if (string.IsNullOrEmpty(tool.Result))
            return null;

        JsonElement root;
        try
        {
            root = JsonDocument.Parse(tool.Result).RootElement;
        }
        catch
        {
            return new CodeBlock(tool.Result, "json");
        }

        // Check for error response
        if (root.TryGetProperty("error", out _))
        {
            return new MarkdownBlock($"**Error:** {tool.Result}");
        }

        // Route to appropriate parser based on tool
        if (ChartTableTools.Contains(tool.ToolName))
        {
            return ParseChartTableResult(tool.ToolName, root);
        }

        if (TableOnlyTools.Contains(tool.ToolName))
        {
            return ParseTableResult(tool.ToolName, root);
        }

        // Handle summary tools
        return tool.ToolName switch
        {
            "get_sales_summary" => ParseSalesSummary(root),
            "get_expenses_summary" => ParseExpensesSummary(root),
            "get_daily_sales_report" => ParseDailySalesReport(root),
            _ => new CodeBlock(
                JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true }),
                "json")
        };
    }

    private ChartTableComboBlock ParseChartTableResult(string toolName, JsonElement root)
    {
        var chartType = ToolChartTypes.GetValueOrDefault(toolName, AIChartType.Bar);
        var title = GetTitleForTool(toolName);

        // Extract data based on tool type
        var (labels, values, tableData) = toolName switch
        {
            "get_sales_by_product" => ExtractProductData(root),
            "get_sales_by_broker" => ExtractBrokerData(root),
            "get_expenses_by_category" => ExtractCategoryData(root),
            "get_top_products" => ExtractTopProductsData(root),
            _ => (new List<string>(), new List<double>(), new List<Dictionary<string, object?>>())
        };

        var chart = new ChartBlock
        {
            Title = title,
            ChartType = chartType,
            Labels = labels,
            Datasets =
            [
                new ChartDataset
                {
                    Label = GetChartLabel(toolName),
                    Data = values
                }
            ],
            Options = new AIChartOptions
            {
                ShowLegend = chartType is AIChartType.Pie or AIChartType.Doughnut,
                YAxisFormat = "currency"
            }
        };

        var headers = tableData.Count > 0
            ? tableData[0].Keys.ToList()
            : new List<string>();

        var table = new DataTableBlock
        {
            Title = title,
            Headers = headers,
            Rows = tableData,
            ToolName = toolName
        };

        return new ChartTableComboBlock
        {
            Title = title,
            Chart = chart,
            Table = table
        };
    }

    private DataTableBlock ParseTableResult(string toolName, JsonElement root)
    {
        var title = GetTitleForTool(toolName);
        var (headers, rows) = toolName switch
        {
            "get_sales_by_date_range" => ExtractSalesDateRangeData(root),
            "get_expenses_by_date_range" => ExtractExpensesDateRangeData(root),
            "get_fuel_usage" => ExtractFuelUsageData(root),
            "get_banking_records" => ExtractBankingData(root),
            "get_unpaid_orders" => ExtractUnpaidOrdersData(root),
            _ => (new List<string>(), new List<Dictionary<string, object?>>())
        };

        return new DataTableBlock
        {
            Title = title,
            Headers = headers,
            Rows = rows,
            ToolName = toolName
        };
    }

    #region Data Extractors

    private (List<string> labels, List<double> values, List<Dictionary<string, object?>> tableData)
        ExtractProductData(JsonElement root)
    {
        var labels = new List<string>();
        var values = new List<double>();
        var tableData = new List<Dictionary<string, object?>>();

        if (root.TryGetProperty("products", out var products) && products.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in products.EnumerateArray())
            {
                var product = item.GetProperty("product").GetString() ?? "Unknown";
                var revenue = item.TryGetProperty("revenue", out var rev) ? rev.GetDouble() : 0;
                var quantity = item.TryGetProperty("quantity", out var qty) ? qty.GetDouble() : 0;
                var orders = item.TryGetProperty("orders", out var ord) ? ord.GetInt32() : 0;

                labels.Add(product);
                values.Add(revenue);
                tableData.Add(new Dictionary<string, object?>
                {
                    ["Product"] = product,
                    ["Quantity"] = quantity,
                    ["Revenue"] = revenue,
                    ["Orders"] = orders
                });
            }
        }

        return (labels, values, tableData);
    }

    private (List<string> labels, List<double> values, List<Dictionary<string, object?>> tableData)
        ExtractBrokerData(JsonElement root)
    {
        var labels = new List<string>();
        var values = new List<double>();
        var tableData = new List<Dictionary<string, object?>>();

        if (root.TryGetProperty("brokers", out var brokers) && brokers.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in brokers.EnumerateArray())
            {
                var broker = item.GetProperty("broker").GetString() ?? "Unknown";
                var revenue = item.TryGetProperty("revenue", out var rev) ? rev.GetDouble() : 0;
                var quantity = item.TryGetProperty("quantity", out var qty) ? qty.GetDouble() : 0;
                var commission = item.TryGetProperty("commission", out var comm) ? comm.GetDouble() : 0;
                var orders = item.TryGetProperty("orders", out var ord) ? ord.GetInt32() : 0;

                labels.Add(broker);
                values.Add(revenue);
                tableData.Add(new Dictionary<string, object?>
                {
                    ["Broker"] = broker,
                    ["Orders"] = orders,
                    ["Quantity"] = quantity,
                    ["Revenue"] = revenue,
                    ["Commission"] = commission
                });
            }
        }

        return (labels, values, tableData);
    }

    private (List<string> labels, List<double> values, List<Dictionary<string, object?>> tableData)
        ExtractCategoryData(JsonElement root)
    {
        var labels = new List<string>();
        var values = new List<double>();
        var tableData = new List<Dictionary<string, object?>>();

        if (root.TryGetProperty("categories", out var categories) && categories.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in categories.EnumerateArray())
            {
                var category = item.GetProperty("category").GetString() ?? "Unknown";
                var total = item.TryGetProperty("total", out var t) ? t.GetDouble() : 0;
                var count = item.TryGetProperty("count", out var c) ? c.GetInt32() : 0;

                labels.Add(category);
                values.Add(total);
                tableData.Add(new Dictionary<string, object?>
                {
                    ["Category"] = category,
                    ["Total"] = total,
                    ["Count"] = count
                });
            }
        }

        return (labels, values, tableData);
    }

    private (List<string> labels, List<double> values, List<Dictionary<string, object?>> tableData)
        ExtractTopProductsData(JsonElement root)
    {
        return ExtractProductData(root); // Same structure as sales by product
    }

    private (List<string> headers, List<Dictionary<string, object?>> rows)
        ExtractSalesDateRangeData(JsonElement root)
    {
        var rows = new List<Dictionary<string, object?>>();

        // Check for detailed sales
        if (root.TryGetProperty("sales", out var sales) && sales.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in sales.EnumerateArray())
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["Date"] = item.TryGetProperty("date", out var d) ? d.GetString() : "",
                    ["Vehicle"] = item.TryGetProperty("vehicle", out var v) ? v.GetString() : "",
                    ["Product"] = item.TryGetProperty("product", out var p) ? p.GetString() : "",
                    ["Quantity"] = item.TryGetProperty("quantity", out var q) ? q.GetDouble() : 0,
                    ["Amount"] = item.TryGetProperty("amount", out var a) ? a.GetDouble() : 0,
                    ["Status"] = item.TryGetProperty("payment_status", out var s) ? s.GetString() : ""
                });
            }

            return (["Date", "Vehicle", "Product", "Quantity", "Amount", "Status"], rows);
        }

        // Check for daily summary
        if (root.TryGetProperty("daily_summary", out var summary) && summary.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in summary.EnumerateArray())
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["Date"] = item.TryGetProperty("date", out var d) ? d.GetString() : "",
                    ["Orders"] = item.TryGetProperty("orders", out var o) ? o.GetInt32() : 0,
                    ["Quantity"] = item.TryGetProperty("quantity", out var q) ? q.GetDouble() : 0,
                    ["Total"] = item.TryGetProperty("total", out var t) ? t.GetDouble() : 0
                });
            }

            return (["Date", "Orders", "Quantity", "Total"], rows);
        }

        return (new List<string>(), rows);
    }

    private (List<string> headers, List<Dictionary<string, object?>> rows)
        ExtractExpensesDateRangeData(JsonElement root)
    {
        var rows = new List<Dictionary<string, object?>>();

        if (root.TryGetProperty("expenses", out var expenses) && expenses.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in expenses.EnumerateArray())
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["Date"] = item.TryGetProperty("date", out var d) ? d.GetString() : "",
                    ["Item"] = item.TryGetProperty("item", out var i) ? i.GetString() : "",
                    ["Category"] = item.TryGetProperty("category", out var c) ? c.GetString() : "",
                    ["Amount"] = item.TryGetProperty("amount", out var a) ? a.GetDouble() : 0
                });
            }
        }

        return (["Date", "Item", "Category", "Amount"], rows);
    }

    private (List<string> headers, List<Dictionary<string, object?>> rows)
        ExtractFuelUsageData(JsonElement root)
    {
        var rows = new List<Dictionary<string, object?>>();

        if (root.TryGetProperty("fuel_usage", out var usage) && usage.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in usage.EnumerateArray())
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["Date"] = item.TryGetProperty("date", out var d) ? d.GetString() : "",
                    ["Old Stock"] = item.TryGetProperty("old_stock", out var os) ? os.GetDouble() : 0,
                    ["New Stock"] = item.TryGetProperty("new_stock", out var ns) ? ns.GetDouble() : 0,
                    ["Machines"] = item.TryGetProperty("machines", out var m) ? m.GetDouble() : 0,
                    ["Wheel Loaders"] = item.TryGetProperty("wheel_loaders", out var wl) ? wl.GetDouble() : 0,
                    ["Balance"] = item.TryGetProperty("balance", out var b) ? b.GetDouble() : 0
                });
            }
        }

        return (["Date", "Old Stock", "New Stock", "Machines", "Wheel Loaders", "Balance"], rows);
    }

    private (List<string> headers, List<Dictionary<string, object?>> rows)
        ExtractBankingData(JsonElement root)
    {
        var rows = new List<Dictionary<string, object?>>();

        if (root.TryGetProperty("banking_records", out var records) && records.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in records.EnumerateArray())
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["Date"] = item.TryGetProperty("date", out var d) ? d.GetString() : "",
                    ["Description"] = item.TryGetProperty("item", out var i) ? i.GetString() : "",
                    ["Amount"] = item.TryGetProperty("amount", out var a) ? a.GetDouble() : 0,
                    ["Reference"] = item.TryGetProperty("reference", out var r) ? r.GetString() : ""
                });
            }
        }

        return (["Date", "Description", "Amount", "Reference"], rows);
    }

    private (List<string> headers, List<Dictionary<string, object?>> rows)
        ExtractUnpaidOrdersData(JsonElement root)
    {
        var rows = new List<Dictionary<string, object?>>();

        if (root.TryGetProperty("unpaid_orders", out var orders) && orders.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in orders.EnumerateArray())
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["Date"] = item.TryGetProperty("date", out var d) ? d.GetString() : "",
                    ["Vehicle"] = item.TryGetProperty("vehicle", out var v) ? v.GetString() : "",
                    ["Client"] = item.TryGetProperty("client", out var c) ? c.GetString() : "",
                    ["Phone"] = item.TryGetProperty("phone", out var p) ? p.GetString() : "",
                    ["Product"] = item.TryGetProperty("product", out var pr) ? pr.GetString() : "",
                    ["Quantity"] = item.TryGetProperty("quantity", out var q) ? q.GetDouble() : 0,
                    ["Amount"] = item.TryGetProperty("amount", out var a) ? a.GetDouble() : 0
                });
            }
        }

        return (["Date", "Vehicle", "Client", "Phone", "Product", "Quantity", "Amount"], rows);
    }

    #endregion

    #region Summary Parsers

    private MarkdownBlock ParseSalesSummary(JsonElement root)
    {
        var period = root.TryGetProperty("period", out var p) ? p.GetString() : "unknown";
        var totalOrders = root.TryGetProperty("total_orders", out var to) ? to.GetInt32() : 0;
        var totalQuantity = root.TryGetProperty("total_quantity", out var tq) ? tq.GetDouble() : 0;
        var totalSales = root.TryGetProperty("total_sales", out var ts) ? ts.GetDouble() : 0;
        var unpaidAmount = root.TryGetProperty("unpaid_amount", out var ua) ? ua.GetDouble() : 0;
        var unpaidCount = root.TryGetProperty("unpaid_count", out var uc) ? uc.GetInt32() : 0;

        var markdown = $"""
            ### Sales Summary ({period})

            | Metric | Value |
            |--------|-------|
            | Total Orders | {totalOrders:N0} |
            | Total Quantity | {totalQuantity:N0} pieces |
            | Total Sales | KES {totalSales:N0} |
            | Unpaid Amount | KES {unpaidAmount:N0} ({unpaidCount} orders) |
            """;

        return new MarkdownBlock(markdown);
    }

    private MarkdownBlock ParseExpensesSummary(JsonElement root)
    {
        var period = root.TryGetProperty("period", out var p) ? p.GetString() : "unknown";
        var manual = root.TryGetProperty("manual_expenses", out var m) ? m.GetDouble() : 0;
        var commission = root.TryGetProperty("commission", out var c) ? c.GetDouble() : 0;
        var loaders = root.TryGetProperty("loaders_fee", out var l) ? l.GetDouble() : 0;
        var landRate = root.TryGetProperty("land_rate_fee", out var lr) ? lr.GetDouble() : 0;
        var total = root.TryGetProperty("total_expenses", out var t) ? t.GetDouble() : 0;

        var markdown = $"""
            ### Expenses Summary ({period})

            | Category | Amount |
            |----------|--------|
            | Manual Expenses | KES {manual:N0} |
            | Commission | KES {commission:N0} |
            | Loaders Fee | KES {loaders:N0} |
            | Land Rate Fee | KES {landRate:N0} |
            | **Total** | **KES {total:N0}** |
            """;

        return new MarkdownBlock(markdown);
    }

    private MarkdownBlock ParseDailySalesReport(JsonElement root)
    {
        var date = root.TryGetProperty("date", out var d) ? d.GetString() : "Unknown";
        var openingBalance = root.TryGetProperty("opening_balance", out var ob) ? ob.GetDouble() : 0;

        // Sales section
        var sales = root.TryGetProperty("sales", out var s) ? s : default;
        var totalOrders = sales.TryGetProperty("total_orders", out var to) ? to.GetInt32() : 0;
        var totalQuantity = sales.TryGetProperty("total_quantity", out var tq) ? tq.GetDouble() : 0;
        var totalAmount = sales.TryGetProperty("total_amount", out var ta) ? ta.GetDouble() : 0;
        var unpaidAmount = sales.TryGetProperty("unpaid_amount", out var ua) ? ua.GetDouble() : 0;

        // Expenses section
        var expenses = root.TryGetProperty("expenses", out var e) ? e : default;
        var manualExp = expenses.TryGetProperty("manual", out var me) ? me.GetDouble() : 0;
        var commission = expenses.TryGetProperty("commission", out var c) ? c.GetDouble() : 0;
        var loaders = expenses.TryGetProperty("loaders_fee", out var l) ? l.GetDouble() : 0;
        var landRate = expenses.TryGetProperty("land_rate", out var lr) ? lr.GetDouble() : 0;
        var totalExp = expenses.TryGetProperty("total", out var te) ? te.GetDouble() : 0;

        // Summary section
        var summary = root.TryGetProperty("summary", out var sum) ? sum : default;
        var earnings = summary.TryGetProperty("earnings", out var earn) ? earn.GetDouble() : 0;
        var netEarnings = summary.TryGetProperty("net_earnings", out var ne) ? ne.GetDouble() : 0;
        var closingBalance = summary.TryGetProperty("closing_balance", out var cb) ? cb.GetDouble() : 0;

        // Banking
        var banking = root.TryGetProperty("banking", out var b) ? b : default;
        var banked = banking.TryGetProperty("amount_banked", out var ab) ? ab.GetDouble() : 0;

        var markdown = $"""
            ## Daily Sales Report - {date}

            ### Sales
            | Metric | Value |
            |--------|-------|
            | Total Orders | {totalOrders:N0} |
            | Total Quantity | {totalQuantity:N0} pieces |
            | Total Amount | KES {totalAmount:N0} |
            | Unpaid | KES {unpaidAmount:N0} |

            ### Expenses
            | Category | Amount |
            |----------|--------|
            | Manual Expenses | KES {manualExp:N0} |
            | Commission | KES {commission:N0} |
            | Loaders Fee | KES {loaders:N0} |
            | Land Rate | KES {landRate:N0} |
            | **Total** | **KES {totalExp:N0}** |

            ### Summary
            | | Amount |
            |-|--------|
            | Opening Balance | KES {openingBalance:N0} |
            | Earnings | KES {earnings:N0} |
            | Net Earnings | KES {netEarnings:N0} |
            | Banked | KES {banked:N0} |
            | **Closing Balance** | **KES {closingBalance:N0}** |
            """;

        return new MarkdownBlock(markdown);
    }

    #endregion

    #region Helpers

    private static string GetTitleForTool(string toolName) => toolName switch
    {
        "get_sales_by_product" => "Sales by Product",
        "get_sales_by_broker" => "Sales by Broker",
        "get_sales_by_date_range" => "Sales Details",
        "get_expenses_by_category" => "Expenses by Category",
        "get_expenses_by_date_range" => "Expense Details",
        "get_top_products" => "Top Products",
        "get_unpaid_orders" => "Unpaid Orders",
        "get_fuel_usage" => "Fuel Usage",
        "get_banking_records" => "Banking Records",
        _ => "Data"
    };

    private static string GetChartLabel(string toolName) => toolName switch
    {
        "get_sales_by_product" => "Revenue",
        "get_sales_by_broker" => "Revenue",
        "get_expenses_by_category" => "Amount",
        "get_top_products" => "Revenue",
        _ => "Value"
    };

    #endregion
}
