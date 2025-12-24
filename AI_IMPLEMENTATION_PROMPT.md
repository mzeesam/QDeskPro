# QDeskPro AI Implementation Prompt

## Executive Summary

Add comprehensive AI capabilities to QDeskPro, a quarry management system, to enhance sales processing, analytics, reporting, and provide natural language query capabilities. The implementation will leverage OpenAI's `gpt-5-nano` model for cost-effective, fast AI interactions.

---

## Phase 5.5: AI Integration Module

**Insert this phase between Phase 5 (Polish & Optimization) and Phase 6 (Testing & Deployment)**

---

## 1. AI Configuration & Infrastructure

### 1.1 OpenAI Configuration (Copy from DataSphere)

**Add to `appsettings.json`:**
```json
{
  "AI": {
    "OpenAI": {
      "ApiKey": "sk-proj-YOUR_API_KEY_HERE",
      "Model": "gpt-5-nano",
      "EmbeddingModel": "text-embedding-3-small",
      "MaxTokens": 2000,
      "Temperature": 0.7,
      "BaseUrl": "https://api.openai.com/v1"
    },
    "VectorSearch": {
      "TopK": 5,
      "MinSimilarityScore": 0.7,
      "EmbeddingDimensions": 1536
    },
    "Chat": {
      "MaxMessagesInContext": 20,
      "MaxContextCharacters": 3000,
      "MaxFunctionCallIterations": 5,
      "SystemPrompts": {
        "SalesAssistant": "You are QDeskPro AI, an intelligent assistant for quarry sales management...",
        "AnalyticsAssistant": "You are QDeskPro Analytics AI, specializing in sales data analysis...",
        "ReportAssistant": "You are QDeskPro Report AI, helping generate and explain financial reports..."
      }
    },
    "Features": {
      "EnableAIFeatures": true,
      "EnableVectorSearch": true,
      "EnableFunctionCalling": true,
      "EnableAutoSuggestions": true
    }
  }
}
```

### 1.2 AI Configuration Model

**Create `Domain/Models/AI/AIConfiguration.cs`:**
```csharp
public class AIConfiguration
{
    public OpenAIConfig OpenAI { get; set; } = new();
    public VectorSearchConfig VectorSearch { get; set; } = new();
    public ChatConfig Chat { get; set; } = new();
    public AIFeaturesConfig Features { get; set; } = new();
}

public class OpenAIConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-5-nano";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public int MaxTokens { get; set; } = 2000;
    public double Temperature { get; set; } = 0.7;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
}

public class VectorSearchConfig
{
    public int TopK { get; set; } = 5;
    public double MinSimilarityScore { get; set; } = 0.7;
    public int EmbeddingDimensions { get; set; } = 1536;
}

public class ChatConfig
{
    public int MaxMessagesInContext { get; set; } = 20;
    public int MaxContextCharacters { get; set; } = 3000;
    public int MaxFunctionCallIterations { get; set; } = 5;
    public Dictionary<string, string> SystemPrompts { get; set; } = new();
}

public class AIFeaturesConfig
{
    public bool EnableAIFeatures { get; set; } = true;
    public bool EnableVectorSearch { get; set; } = true;
    public bool EnableFunctionCalling { get; set; } = true;
    public bool EnableAutoSuggestions { get; set; } = true;
}
```

### 1.3 NuGet Packages Required

```xml
<!-- Add to QDeskPro.csproj -->
<PackageReference Include="OpenAI" Version="2.*" />
<PackageReference Include="Microsoft.SemanticKernel" Version="1.*" />
```

---

## 2. Database Schema for AI

### 2.1 AI Entities

**Create `Domain/Entities/AIConversation.cs`:**
```csharp
public class AIConversation : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ChatType { get; set; } = "general"; // "sales", "analytics", "report", "general"
    public string? QuarryId { get; set; } // Optional: scope to specific quarry
    public DateTime LastMessageAt { get; set; }

    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Quarry? Quarry { get; set; }
    public virtual ICollection<AIMessage> Messages { get; set; } = new List<AIMessage>();
}
```

**Create `Domain/Entities/AIMessage.cs`:**
```csharp
public class AIMessage : BaseEntity
{
    public string AIConversationId { get; set; } = string.Empty;
    public string Role { get; set; } = "user"; // "system", "user", "assistant", "tool"
    public string Content { get; set; } = string.Empty;
    public string? ToolCallId { get; set; }
    public string? ToolName { get; set; }
    public string? ToolResult { get; set; }
    public int? TokensUsed { get; set; }

    public virtual AIConversation Conversation { get; set; } = null!;
}
```

**Create `Domain/Entities/SalesEmbedding.cs`:**
```csharp
public class SalesEmbedding : BaseEntity
{
    public string ContentType { get; set; } = "Sale"; // "Sale", "Expense", "Report"
    public string ContentId { get; set; } = string.Empty;
    public string EmbeddingVector { get; set; } = string.Empty; // JSON serialized float[]
    public string ContentSummary { get; set; } = string.Empty; // Searchable text summary
    public string? Metadata { get; set; } // Additional JSON metadata
}
```

### 2.2 Add to AppDbContext

```csharp
public DbSet<AIConversation> AIConversations { get; set; }
public DbSet<AIMessage> AIMessages { get; set; }
public DbSet<SalesEmbedding> SalesEmbeddings { get; set; }
```

---

## 3. AI Services Architecture

### 3.1 Core AI Services

**Create `Domain/Services/AI/AIProviderFactory.cs`:**
- Singleton factory for OpenAI client management
- Thread-safe client initialization
- API key validation with graceful fallback

**Create `Domain/Services/AI/ChatCompletionService.cs`:**
- Main RAG pipeline for AI chat
- Function calling support with quarry data tools
- Conversation persistence
- Context management (last 20 messages)
- Citation extraction from tool results

**Create `Domain/Services/AI/SemanticSearchService.cs`:**
- Vector similarity search for sales/expense data
- Cosine similarity calculation
- Top-K results with relevance scoring

**Create `Domain/Services/AI/SalesEmbeddingService.cs`:**
- Generate embeddings for sales records
- Batch processing for bulk indexing
- Automatic re-indexing on data changes

**Create `Domain/Services/AI/SalesQueryTools.cs`:**
- Function calling tools for natural language queries
- 12+ specialized query tools (see Section 4)

---

## 4. AI Function Calling Tools for Quarry Data

### 4.1 Sales Query Tools

```csharp
public static class SalesQueryTools
{
    public static readonly ChatTool[] AllTools = new[]
    {
        // 1. Search sales by date range
        ChatTool.CreateFunctionTool(
            functionName: "search_sales_by_date",
            functionDescription: "Search for sales within a specific date range",
            functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "from_date": {"type": "string", "format": "date"},
                    "to_date": {"type": "string", "format": "date"},
                    "quarry_id": {"type": "string", "description": "Optional quarry ID"},
                    "max_results": {"type": "integer", "default": 10}
                },
                "required": ["from_date", "to_date"]
            }
            """)
        ),

        // 2. Search sales by product
        ChatTool.CreateFunctionTool(
            functionName: "search_sales_by_product",
            functionDescription: "Find sales for a specific product type",
            functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "product_name": {"type": "string", "description": "Product name like Size 6, Size 9, Reject"},
                    "from_date": {"type": "string", "format": "date"},
                    "to_date": {"type": "string", "format": "date"}
                },
                "required": ["product_name"]
            }
            """)
        ),

        // 3. Get sales statistics
        ChatTool.CreateFunctionTool(
            functionName: "get_sales_statistics",
            functionDescription: "Get aggregate statistics: total sales, quantity, revenue, average order value",
            functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "period": {"type": "string", "enum": ["today", "week", "month", "quarter", "year"]},
                    "quarry_id": {"type": "string"}
                },
                "required": ["period"]
            }
            """)
        ),

        // 4. Search sales by vehicle registration
        ChatTool.CreateFunctionTool(
            functionName: "search_by_vehicle",
            functionDescription: "Find all sales for a specific vehicle registration number",
            functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "vehicle_registration": {"type": "string"},
                    "max_results": {"type": "integer", "default": 20}
                },
                "required": ["vehicle_registration"]
            }
            """)
        ),

        // 5. Get unpaid orders
        ChatTool.CreateFunctionTool(
            functionName: "get_unpaid_orders",
            functionDescription: "Get all unpaid/credit sales orders",
            functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "quarry_id": {"type": "string"},
                    "from_date": {"type": "string", "format": "date"},
                    "min_amount": {"type": "number"}
                }
            }
            """)
        ),

        // 6. Get expense breakdown
        ChatTool.CreateFunctionTool(
            functionName: "get_expense_breakdown",
            functionDescription: "Get expenses grouped by category with totals",
            functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "from_date": {"type": "string", "format": "date"},
                    "to_date": {"type": "string", "format": "date"},
                    "category": {"type": "string", "description": "Optional: filter by category"}
                },
                "required": ["from_date", "to_date"]
            }
            """)
        ),

        // 7. Calculate profit margin
        ChatTool.CreateFunctionTool(
            functionName: "calculate_profit_margin",
            functionDescription: "Calculate profit margin (revenue minus all expenses including commissions, loaders fee, land rate)",
            functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "from_date": {"type": "string", "format": "date"},
                    "to_date": {"type": "string", "format": "date"},
                    "quarry_id": {"type": "string"}
                },
                "required": ["from_date", "to_date"]
            }
            """)
        ),

        // 8. Get top customers (by vehicle)
        ChatTool.CreateFunctionTool(
            functionName: "get_top_customers",
            functionDescription: "Get top customers by total purchase amount",
            functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "period": {"type": "string", "enum": ["month", "quarter", "year"]},
                    "limit": {"type": "integer", "default": 10}
                },
                "required": ["period"]
            }
            """)
        ),

        // 9. Compare periods
        ChatTool.CreateFunctionTool(
            functionName: "compare_periods",
            functionDescription: "Compare sales performance between two time periods",
            functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "period1_from": {"type": "string", "format": "date"},
                    "period1_to": {"type": "string", "format": "date"},
                    "period2_from": {"type": "string", "format": "date"},
                    "period2_to": {"type": "string", "format": "date"}
                },
                "required": ["period1_from", "period1_to", "period2_from", "period2_to"]
            }
            """)
        ),

        // 10. Get fuel consumption analytics
        ChatTool.CreateFunctionTool(
            functionName: "get_fuel_analytics",
            functionDescription: "Get fuel usage statistics and trends",
            functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "from_date": {"type": "string", "format": "date"},
                    "to_date": {"type": "string", "format": "date"}
                },
                "required": ["from_date", "to_date"]
            }
            """)
        ),

        // 11. Get daily cash flow
        ChatTool.CreateFunctionTool(
            functionName: "get_daily_cash_flow",
            functionDescription: "Get daily cash flow: opening balance, sales, expenses, banking, closing balance",
            functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "date": {"type": "string", "format": "date"},
                    "quarry_id": {"type": "string"}
                },
                "required": ["date"]
            }
            """)
        ),

        // 12. Search by client/destination
        ChatTool.CreateFunctionTool(
            functionName: "search_by_destination",
            functionDescription: "Find sales by client destination county",
            functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "county": {"type": "string", "description": "Kenya county name"},
                    "from_date": {"type": "string", "format": "date"},
                    "to_date": {"type": "string", "format": "date"}
                },
                "required": ["county"]
            }
            """)
        )
    };
}
```

### 4.2 Sales Query Service

**Create `Domain/Services/AI/SalesQueryService.cs`:**
```csharp
public class SalesQueryService
{
    // Implement each tool's execution logic
    public async Task<string> SearchSalesByDateAsync(DateTime from, DateTime to, string? quarryId, int maxResults);
    public async Task<string> SearchSalesByProductAsync(string productName, DateTime? from, DateTime? to);
    public async Task<string> GetSalesStatisticsAsync(string period, string? quarryId);
    public async Task<string> SearchByVehicleAsync(string vehicleReg, int maxResults);
    public async Task<string> GetUnpaidOrdersAsync(string? quarryId, DateTime? from, double? minAmount);
    public async Task<string> GetExpenseBreakdownAsync(DateTime from, DateTime to, string? category);
    public async Task<string> CalculateProfitMarginAsync(DateTime from, DateTime to, string? quarryId);
    public async Task<string> GetTopCustomersAsync(string period, int limit);
    public async Task<string> ComparePeriodsAsync(DateTime p1From, DateTime p1To, DateTime p2From, DateTime p2To);
    public async Task<string> GetFuelAnalyticsAsync(DateTime from, DateTime to);
    public async Task<string> GetDailyCashFlowAsync(DateTime date, string? quarryId);
    public async Task<string> SearchByDestinationAsync(string county, DateTime? from, DateTime? to);

    // Tool execution dispatcher
    public async Task<string> ExecuteToolAsync(string toolName, JsonDocument arguments);
}
```

---

## 5. AI Chat Interface

### 5.1 Chat Component

**Create `Features/AI/Components/AIChatComponent.razor`:**

```razor
@using QDeskPro.Domain.Services.AI
@inject ChatCompletionService ChatService
@inject IJSRuntime JS

<MudPaper Class="ai-chat-container" Elevation="2">
    <MudToolBar Dense="true" Class="ai-chat-header">
        <MudIcon Icon="@Icons.Material.Filled.SmartToy" Class="mr-2" />
        <MudText Typo="Typo.h6">QDeskPro AI Assistant</MudText>
        <MudSpacer />
        <MudIconButton Icon="@Icons.Material.Filled.History" OnClick="ShowHistory" />
        <MudIconButton Icon="@Icons.Material.Filled.Add" OnClick="NewConversation" />
    </MudToolBar>

    <div class="ai-chat-messages" @ref="_messagesContainer">
        @foreach (var message in _messages)
        {
            <div class="ai-message @(message.Role == "user" ? "user-message" : "assistant-message")">
                <MudAvatar Size="Size.Small" Class="mr-2">
                    @if (message.Role == "user")
                    {
                        <MudIcon Icon="@Icons.Material.Filled.Person" />
                    }
                    else
                    {
                        <MudIcon Icon="@Icons.Material.Filled.SmartToy" />
                    }
                </MudAvatar>
                <div class="message-content">
                    <MudMarkdown Value="@message.Content" />
                    @if (message.Citations?.Any() == true)
                    {
                        <div class="message-citations">
                            <MudText Typo="Typo.caption">Sources:</MudText>
                            @foreach (var citation in message.Citations)
                            {
                                <MudChip Size="Size.Small" Variant="Variant.Outlined">
                                    @citation
                                </MudChip>
                            }
                        </div>
                    }
                </div>
            </div>
        }

        @if (_isTyping)
        {
            <div class="ai-message assistant-message">
                <MudAvatar Size="Size.Small" Class="mr-2">
                    <MudIcon Icon="@Icons.Material.Filled.SmartToy" />
                </MudAvatar>
                <div class="typing-indicator">
                    <span></span><span></span><span></span>
                </div>
            </div>
        }
    </div>

    <div class="ai-chat-input">
        <MudTextField @bind-Value="_userInput"
                     Placeholder="Ask about sales, expenses, reports..."
                     Variant="Variant.Outlined"
                     Adornment="Adornment.End"
                     AdornmentIcon="@Icons.Material.Filled.Send"
                     OnAdornmentClick="SendMessage"
                     OnKeyDown="HandleKeyDown"
                     Immediate="true"
                     Disabled="@_isTyping" />
    </div>

    <!-- Quick Action Chips -->
    <div class="ai-quick-actions">
        <MudChip Size="Size.Small" OnClick="@(() => AskQuestion("What are today's sales?"))">
            Today's Sales
        </MudChip>
        <MudChip Size="Size.Small" OnClick="@(() => AskQuestion("Show unpaid orders"))">
            Unpaid Orders
        </MudChip>
        <MudChip Size="Size.Small" OnClick="@(() => AskQuestion("Compare this week to last week"))">
            Weekly Comparison
        </MudChip>
        <MudChip Size="Size.Small" OnClick="@(() => AskQuestion("Top selling products this month"))">
            Top Products
        </MudChip>
    </div>
</MudPaper>
```

### 5.2 Chat Page

**Create `Features/AI/Pages/AIChat.razor`:**
- Full-page chat interface for managers/admins
- Conversation history sidebar
- Export chat to PDF option

### 5.3 Floating Chat Widget

**Create `Shared/Components/AIChatWidget.razor`:**
- Floating action button in corner
- Expandable chat panel
- Available on all pages for quick queries

---

## 6. AI-Powered Features

### 6.1 Smart Sales Entry Suggestions

**In Sales Entry Form:**
- Auto-suggest price based on recent sales patterns
- Predict likely product based on vehicle history
- Suggest broker based on destination/client patterns
- Alert if price deviates significantly from average

### 6.2 Intelligent Dashboard Insights

**Create `Features/Dashboard/Components/AIInsights.razor`:**
```razor
<MudCard Class="ai-insights-card">
    <MudCardHeader>
        <CardHeaderAvatar>
            <MudIcon Icon="@Icons.Material.Filled.Insights" Color="Color.Primary" />
        </CardHeaderAvatar>
        <CardHeaderContent>
            <MudText Typo="Typo.h6">AI Insights</MudText>
        </CardHeaderContent>
    </MudCardHeader>
    <MudCardContent>
        @foreach (var insight in _insights)
        {
            <MudAlert Severity="@insight.Severity" Class="mb-2" Dense="true">
                <MudText Typo="Typo.body2">@insight.Message</MudText>
            </MudAlert>
        }
    </MudCardContent>
</MudCard>
```

**AI Insight Types:**
- Sales trend anomalies ("Sales down 20% compared to last week")
- Unpaid order alerts ("5 orders worth KES 45,000 are unpaid for >7 days")
- Product performance ("Size 9 is your best-selling product this month")
- Fuel efficiency alerts ("Fuel consumption 15% higher than average")
- Cash flow warnings ("Closing balance below KES 10,000")

### 6.3 Natural Language Report Generation

**Enhance Report Generator:**
```csharp
// User types: "Generate a report for last month showing top customers and product breakdown"
// AI interprets and generates customized report parameters
```

### 6.4 Predictive Analytics

**Create `Domain/Services/AI/PredictiveAnalyticsService.cs`:**
- Sales forecast for next 7/30 days
- Recommended pricing suggestions
- Demand prediction by product type
- Cash flow projection

---

## 7. UI/UX for AI Features

### 7.1 AI Chat Styles

**Add to `wwwroot/css/ai-chat.css`:**
```css
.ai-chat-container {
    display: flex;
    flex-direction: column;
    height: 600px;
    max-height: 80vh;
    border-radius: 12px;
    overflow: hidden;
}

.ai-chat-header {
    background: linear-gradient(135deg, #1976D2 0%, #1565C0 100%);
    color: white;
}

.ai-chat-messages {
    flex: 1;
    overflow-y: auto;
    padding: 16px;
    background: #FAFAFA;
}

.ai-message {
    display: flex;
    align-items: flex-start;
    margin-bottom: 16px;
    animation: fadeIn 0.3s ease-in;
}

.user-message {
    flex-direction: row-reverse;
}

.user-message .message-content {
    background: #1976D2;
    color: white;
    border-radius: 18px 18px 4px 18px;
}

.assistant-message .message-content {
    background: white;
    border-radius: 18px 18px 18px 4px;
    box-shadow: 0 1px 2px rgba(0,0,0,0.1);
}

.message-content {
    max-width: 80%;
    padding: 12px 16px;
}

.typing-indicator {
    display: flex;
    gap: 4px;
    padding: 12px 16px;
}

.typing-indicator span {
    width: 8px;
    height: 8px;
    background: #1976D2;
    border-radius: 50%;
    animation: bounce 1.4s infinite ease-in-out;
}

.typing-indicator span:nth-child(1) { animation-delay: -0.32s; }
.typing-indicator span:nth-child(2) { animation-delay: -0.16s; }

@keyframes bounce {
    0%, 80%, 100% { transform: scale(0); }
    40% { transform: scale(1); }
}

.ai-chat-input {
    padding: 16px;
    background: white;
    border-top: 1px solid #E0E0E0;
}

.ai-quick-actions {
    padding: 8px 16px;
    display: flex;
    gap: 8px;
    flex-wrap: wrap;
    background: white;
    border-top: 1px solid #E0E0E0;
}

.message-citations {
    margin-top: 8px;
    padding-top: 8px;
    border-top: 1px dashed rgba(0,0,0,0.1);
}

/* Floating Widget */
.ai-chat-fab {
    position: fixed;
    bottom: 24px;
    right: 24px;
    z-index: 1000;
}

.ai-chat-panel {
    position: fixed;
    bottom: 88px;
    right: 24px;
    width: 400px;
    z-index: 999;
    box-shadow: 0 8px 32px rgba(0,0,0,0.2);
}

@media (max-width: 600px) {
    .ai-chat-panel {
        width: calc(100vw - 32px);
        right: 16px;
        bottom: 72px;
    }
}
```

---

## 8. API Endpoints for AI

### 8.1 AI Chat Endpoints

**Create `Api/Endpoints/AIEndpoints.cs`:**
```csharp
public static class AIEndpoints
{
    public static void MapAIEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ai").RequireAuthorization();

        // Chat endpoints
        group.MapPost("/conversations", CreateConversation);
        group.MapGet("/conversations", GetConversations);
        group.MapGet("/conversations/{id}", GetConversation);
        group.MapPost("/conversations/{id}/messages", SendMessage);
        group.MapDelete("/conversations/{id}", DeleteConversation);

        // Quick query endpoint (no conversation persistence)
        group.MapPost("/query", QuickQuery);

        // Insights endpoint
        group.MapGet("/insights", GetAIInsights);

        // Predictions endpoint
        group.MapGet("/predictions/sales", GetSalesPredictions);
        group.MapGet("/predictions/cashflow", GetCashFlowPredictions);
    }
}
```

---

## 9. Implementation Tasks Checklist

### Phase 5.5.1: AI Infrastructure Setup
- [ ] Add NuGet packages (OpenAI, SemanticKernel)
- [ ] Create AIConfiguration model
- [ ] Add AI settings to appsettings.json (copy from DataSphere)
- [ ] Create AIProviderFactory service
- [ ] Add AI entities (AIConversation, AIMessage, SalesEmbedding)
- [ ] Add DbSets to AppDbContext
- [ ] Create and apply migration

### Phase 5.5.2: Core AI Services
- [ ] Create ChatCompletionService
- [ ] Create SalesQueryTools (12 tools)
- [ ] Create SalesQueryService
- [ ] Create SemanticSearchService (optional, for advanced search)
- [ ] Register all services in Program.cs

### Phase 5.5.3: AI Chat UI
- [ ] Create AIChatComponent.razor
- [ ] Create AIChat.razor page
- [ ] Create AIChatWidget.razor (floating widget)
- [ ] Add AI chat styles (ai-chat.css)
- [ ] Add navigation menu items for AI Chat

### Phase 5.5.4: AI-Powered Features
- [ ] Create AIInsights component
- [ ] Add insights to Manager Dashboard
- [ ] Add smart suggestions to Sales Entry form
- [ ] Create PredictiveAnalyticsService (sales/cashflow forecasts)

### Phase 5.5.5: API Layer
- [ ] Create AIEndpoints.cs
- [ ] Register AI endpoints in EndpointExtensions.cs
- [ ] Add rate limiting for AI endpoints
- [ ] Add token usage tracking

### Phase 5.5.6: Testing & Polish
- [ ] Test all 12 query tools with sample queries
- [ ] Test conversation persistence
- [ ] Test error handling (API key missing, rate limits)
- [ ] Mobile responsive testing for chat UI
- [ ] Performance optimization (caching, context limits)

---

## 10. Sample Natural Language Queries

The AI should be able to answer queries like:

**Sales Queries:**
- "What are today's total sales?"
- "Show me sales for KBZ 123A"
- "How much Size 6 did we sell this week?"
- "List all unpaid orders over 10,000 KES"
- "Who are our top 5 customers this month?"
- "Compare this week's sales to last week"

**Financial Queries:**
- "What's our profit margin for December?"
- "Show expense breakdown by category"
- "How much commission did we pay this month?"
- "What's the total loaders fee for today?"
- "Calculate net income for the last 7 days"

**Operational Queries:**
- "How much fuel did we use this week?"
- "What's the average fuel consumption per day?"
- "Show banking records for this week"
- "What's our closing balance for yesterday?"

**Predictive Queries:**
- "Predict next week's sales"
- "What products should we focus on?"
- "When might we run low on cash?"

---

## 11. Security Considerations

- API key stored securely (use Azure Key Vault in production)
- Rate limiting on AI endpoints (prevent abuse)
- User-scoped conversations (can't see other users' chats)
- Quarry-scoped data access (respects existing authorization)
- Token usage monitoring and alerts
- Input sanitization before sending to AI
- No PII in embeddings or logs

---

## 12. Cost Optimization

**Model Selection:**
- Use `gpt-5-nano` for fast, cost-effective responses
- Consider `text-embedding-3-small` for embeddings (cheaper than large)

**Token Management:**
- Limit context to 20 messages max
- Truncate context to 3000 characters
- Cache frequent queries
- Use function calling to reduce token usage

**Monitoring:**
- Track token usage per user/day
- Alert on unusual consumption
- Monthly budget caps if needed

---

## 13. Success Metrics

- [ ] AI responds to queries in < 3 seconds
- [ ] 90%+ accuracy on sales data queries
- [ ] Conversation context maintained correctly
- [ ] Function calling works reliably
- [ ] Mobile UI is responsive and usable
- [ ] No API key exposure in logs or client
- [ ] Token usage is tracked and optimized

---

## Sources & References

- [Gartner: The Role of AI in Sales 2025](https://www.gartner.com/en/sales/topics/sales-ai)
- [McKinsey: The State of AI 2025](https://www.mckinsey.com/capabilities/quantumblack/our-insights/the-state-of-ai)
- [Bain: AI Transforming Sales Productivity](https://www.bain.com/insights/ai-transforming-productivity-sales-remains-new-frontier-technology-report-2025/)
- [IBM: AI in Sales Enablement](https://www.ibm.com/think/topics/ai-sales-enablement)
- [Creatio: AI for Sales](https://www.creatio.com/glossary/ai-for-sales)
- DataSphere Solution AI Implementation (internal reference)
