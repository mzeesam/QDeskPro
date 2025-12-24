using System.Text.Json;
using OpenAI.Chat;

namespace QDeskPro.Domain.Services.AI;

/// <summary>
/// Defines OpenAI function calling tools for querying sales and expense data
/// </summary>
public static class SalesQueryTools
{
    /// <summary>
    /// Get all available tools for the AI assistant
    /// </summary>
    public static IEnumerable<ChatTool> GetAllTools()
    {
        yield return GetSalesSummaryTool;
        yield return GetSalesByDateRangeTool;
        yield return GetSalesByProductTool;
        yield return GetSalesByBrokerTool;
        yield return GetExpensesSummaryTool;
        yield return GetExpensesByDateRangeTool;
        yield return GetExpensesByCategoryTool;
        yield return GetDailySalesReportTool;
        yield return GetTopProductsTool;
        yield return GetUnpaidOrdersTool;
        yield return GetFuelUsageTool;
        yield return GetBankingRecordsTool;
    }

    public static ChatTool GetSalesSummaryTool => ChatTool.CreateFunctionTool(
        functionName: "get_sales_summary",
        functionDescription: "Get a summary of sales for a specific period. Returns total sales, quantity, and order count.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "period": {
                    "type": "string",
                    "enum": ["today", "yesterday", "this_week", "last_week", "this_month", "last_month", "this_year"],
                    "description": "The time period for the sales summary"
                },
                "quarry_id": {
                    "type": "string",
                    "description": "Optional quarry ID to filter results"
                }
            },
            "required": ["period"]
        }
        """)
    );

    public static ChatTool GetSalesByDateRangeTool => ChatTool.CreateFunctionTool(
        functionName: "get_sales_by_date_range",
        functionDescription: "Get detailed sales data for a specific date range. Use this for custom date queries.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "start_date": {
                    "type": "string",
                    "format": "date",
                    "description": "Start date in YYYY-MM-DD format"
                },
                "end_date": {
                    "type": "string",
                    "format": "date",
                    "description": "End date in YYYY-MM-DD format"
                },
                "quarry_id": {
                    "type": "string",
                    "description": "Optional quarry ID to filter results"
                },
                "include_details": {
                    "type": "boolean",
                    "description": "Whether to include individual sale details or just summary"
                }
            },
            "required": ["start_date", "end_date"]
        }
        """)
    );

    public static ChatTool GetSalesByProductTool => ChatTool.CreateFunctionTool(
        functionName: "get_sales_by_product",
        functionDescription: "Get sales breakdown by product type. Shows quantity and revenue per product.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "period": {
                    "type": "string",
                    "enum": ["today", "yesterday", "this_week", "last_week", "this_month", "last_month", "this_year"],
                    "description": "The time period for the analysis"
                },
                "product_name": {
                    "type": "string",
                    "description": "Optional specific product name to filter (e.g., 'Size 6', 'Reject')"
                },
                "quarry_id": {
                    "type": "string",
                    "description": "Optional quarry ID to filter results"
                }
            },
            "required": ["period"]
        }
        """)
    );

    public static ChatTool GetSalesByBrokerTool => ChatTool.CreateFunctionTool(
        functionName: "get_sales_by_broker",
        functionDescription: "Get sales performance by broker. Shows sales volume and commission per broker.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "period": {
                    "type": "string",
                    "enum": ["today", "yesterday", "this_week", "last_week", "this_month", "last_month", "this_year"],
                    "description": "The time period for the analysis"
                },
                "broker_name": {
                    "type": "string",
                    "description": "Optional specific broker name to filter"
                },
                "quarry_id": {
                    "type": "string",
                    "description": "Optional quarry ID to filter results"
                }
            },
            "required": ["period"]
        }
        """)
    );

    public static ChatTool GetExpensesSummaryTool => ChatTool.CreateFunctionTool(
        functionName: "get_expenses_summary",
        functionDescription: "Get a summary of all expenses including manual expenses, commissions, loaders fees, and land rate fees.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "period": {
                    "type": "string",
                    "enum": ["today", "yesterday", "this_week", "last_week", "this_month", "last_month", "this_year"],
                    "description": "The time period for the expense summary"
                },
                "quarry_id": {
                    "type": "string",
                    "description": "Optional quarry ID to filter results"
                }
            },
            "required": ["period"]
        }
        """)
    );

    public static ChatTool GetExpensesByDateRangeTool => ChatTool.CreateFunctionTool(
        functionName: "get_expenses_by_date_range",
        functionDescription: "Get detailed expenses for a specific date range.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "start_date": {
                    "type": "string",
                    "format": "date",
                    "description": "Start date in YYYY-MM-DD format"
                },
                "end_date": {
                    "type": "string",
                    "format": "date",
                    "description": "End date in YYYY-MM-DD format"
                },
                "quarry_id": {
                    "type": "string",
                    "description": "Optional quarry ID to filter results"
                }
            },
            "required": ["start_date", "end_date"]
        }
        """)
    );

    public static ChatTool GetExpensesByCategoryTool => ChatTool.CreateFunctionTool(
        functionName: "get_expenses_by_category",
        functionDescription: "Get expense breakdown by category. Shows total amount per expense category.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "period": {
                    "type": "string",
                    "enum": ["today", "yesterday", "this_week", "last_week", "this_month", "last_month", "this_year"],
                    "description": "The time period for the analysis"
                },
                "category": {
                    "type": "string",
                    "description": "Optional specific category to filter"
                },
                "quarry_id": {
                    "type": "string",
                    "description": "Optional quarry ID to filter results"
                }
            },
            "required": ["period"]
        }
        """)
    );

    public static ChatTool GetDailySalesReportTool => ChatTool.CreateFunctionTool(
        functionName: "get_daily_sales_report",
        functionDescription: "Generate a comprehensive daily sales report including sales, expenses, fuel usage, and banking. Calculates net earnings and closing balance.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "date": {
                    "type": "string",
                    "format": "date",
                    "description": "The date for the report in YYYY-MM-DD format. Defaults to today."
                },
                "quarry_id": {
                    "type": "string",
                    "description": "Optional quarry ID to filter results"
                }
            },
            "required": []
        }
        """)
    );

    public static ChatTool GetTopProductsTool => ChatTool.CreateFunctionTool(
        functionName: "get_top_products",
        functionDescription: "Get the top selling products by quantity or revenue.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "period": {
                    "type": "string",
                    "enum": ["today", "yesterday", "this_week", "last_week", "this_month", "last_month", "this_year"],
                    "description": "The time period for the analysis"
                },
                "sort_by": {
                    "type": "string",
                    "enum": ["quantity", "revenue"],
                    "description": "Sort by quantity sold or total revenue"
                },
                "limit": {
                    "type": "integer",
                    "description": "Number of top products to return (default 5)"
                },
                "quarry_id": {
                    "type": "string",
                    "description": "Optional quarry ID to filter results"
                }
            },
            "required": ["period"]
        }
        """)
    );

    public static ChatTool GetUnpaidOrdersTool => ChatTool.CreateFunctionTool(
        functionName: "get_unpaid_orders",
        functionDescription: "Get list of unpaid/credit sales orders. Shows outstanding amounts.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "period": {
                    "type": "string",
                    "enum": ["today", "yesterday", "this_week", "last_week", "this_month", "last_month", "this_year", "all"],
                    "description": "The time period to check for unpaid orders"
                },
                "quarry_id": {
                    "type": "string",
                    "description": "Optional quarry ID to filter results"
                }
            },
            "required": ["period"]
        }
        """)
    );

    public static ChatTool GetFuelUsageTool => ChatTool.CreateFunctionTool(
        functionName: "get_fuel_usage",
        functionDescription: "Get fuel usage records showing consumption by machines and wheel loaders.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "period": {
                    "type": "string",
                    "enum": ["today", "yesterday", "this_week", "last_week", "this_month", "last_month"],
                    "description": "The time period for fuel usage data"
                },
                "quarry_id": {
                    "type": "string",
                    "description": "Optional quarry ID to filter results"
                }
            },
            "required": ["period"]
        }
        """)
    );

    public static ChatTool GetBankingRecordsTool => ChatTool.CreateFunctionTool(
        functionName: "get_banking_records",
        functionDescription: "Get banking/deposit records showing amounts banked.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "period": {
                    "type": "string",
                    "enum": ["today", "yesterday", "this_week", "last_week", "this_month", "last_month"],
                    "description": "The time period for banking records"
                },
                "quarry_id": {
                    "type": "string",
                    "description": "Optional quarry ID to filter results"
                }
            },
            "required": ["period"]
        }
        """)
    );
}
