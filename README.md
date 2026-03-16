# AspireOllama

A modern AI chat application built with **.NET Aspire** and **Ollama**, featuring multimodal capabilities, tool calling, MCP (Model Context Protocol) integration, and persistent chat history.

## Features

- **Modern Agentic UI** - Dark-themed interface with bot avatars, animated thinking indicators, and smooth transitions
- **Vision-Enabled AI Chat** - Upload images and get AI-powered analysis using the llava vision model
- **Tool Calling** - Automatic function invocation with llama3.1 for calculations, time queries, weather, and more
- **MCP Integration** - Connect to external MCP servers for extensible tool support
- **A2A Protocol** - Agent-to-Agent communication with standardized discovery, task management, and peer-to-peer messaging
- **Multi-Agent System** - Specialized AI agents (Planner, Reviewer, Research, Code) that collaborate on complex tasks
- **Document Analysis** - Upload and analyze PDF, Word, Excel, PowerPoint, and text files
- **Persistent Chat History** - SQLite-backed session storage with full conversation history
- **Session Management** - Create, switch, and delete chat sessions with visual status indicators
- **Multi-File Upload** - Support for images and documents (max 10 files, 20MB each)
- **Local LLM Inference** - Runs entirely on your machine using Ollama with GPU acceleration
- **Cloud-Native Architecture** - Built on .NET Aspire with automatic service discovery and health checks
- **Open WebUI** - Includes Ollama's Open WebUI for direct model interaction

## UI Features

- **Dark Theme** - Modern dark gradient background with purple/blue accents
- **Agent Branding** - Bot icon avatars and "AI Agent" branding throughout
- **Animated States** - Pulsing status indicator, spinning loader, and fade-in message animations
- **Tool Call Display** - Visual feedback showing tool executions and results
- **Welcome Screen** - Capability highlights when starting a new conversation
- **Glass Morphism** - Frosted glass effects on sidebar and input areas
- **Responsive Design** - Sidebar with session history and full-width chat area

## Architecture

```
AspireOllama/
├── AspireOllama.AppHost/         # .NET Aspire orchestrator
├── AspireOllama.ApiService/      # Backend API with chat endpoints + MCP client
├── AspireOllama.McpServer/       # External MCP server with sample tools
├── AspireOllama.Web/             # Blazor Server frontend
├── AspireOllama.Shared/          # Shared DTOs
├── AspireOllama.ServiceDefaults/ # Common service configuration
└── A2A/                          # Agent-to-Agent protocol agents
    ├── AspireOllama.A2A.Shared/      # Shared A2A models and client
    ├── AspireOllama.A2A.PlannerAgent/  # Task planning and orchestration
    ├── AspireOllama.A2A.ReviewerAgent/ # Quality review and validation
    ├── AspireOllama.A2A.ResearchAgent/ # Knowledge gathering and context
    └── AspireOllama.A2A.CodeAgent/     # Code generation and execution
```

| Project | Description |
|---------|-------------|
| `AspireOllama.AppHost` | .NET Aspire orchestrator - manages Ollama, MCP Server, API, A2A Agents, and Web services |
| `AspireOllama.ApiService` | Backend API with chat endpoints, tool calling, MCP client, and SQLite persistence |
| `AspireOllama.McpServer` | HTTP-based MCP server exposing weather, time, and conversion tools |
| `AspireOllama.Web` | Blazor Server frontend with agentic dark-themed UI and file upload |
| `AspireOllama.Shared` | Shared DTOs for API communication including tool call models |
| `AspireOllama.ServiceDefaults` | Common service configuration, health checks, and Aspire client extensions |
| `A2A/AspireOllama.A2A.Shared` | Shared A2A protocol models, agent client, and server base class |
| `A2A/AspireOllama.A2A.PlannerAgent` | AI-powered task planning and multi-agent workflow orchestration |
| `A2A/AspireOllama.A2A.ReviewerAgent` | Quality assurance agent for reviewing responses and code |
| `A2A/AspireOllama.A2A.ResearchAgent` | Knowledge gathering and context synthesis agent |
| `A2A/AspireOllama.A2A.CodeAgent` | Code generation, execution, analysis, and testing agent |

For detailed architecture documentation, see [ARCHITECTURE.md](ARCHITECTURE.md).

## Tech Stack

- **.NET 10** / **C# 13**
- **.NET Aspire 13** - Cloud-native orchestration
- **Ollama** - Local LLM inference
- **llava** - Vision-capable language model (image analysis)
- **llama3.1** - Tool-calling language model (function invocation)
- **Model Context Protocol (MCP)** - Extensible tool integration
- **Blazor Server** - Interactive web UI
- **Entity Framework Core** - SQLite persistence
- **Microsoft.Extensions.AI** - Unified AI abstractions
- **FastEndpoints** - Structured API routing
- **CommunityToolkit.Aspire.OllamaSharp** - Ollama integration for Aspire
- **DocumentFormat.OpenXml** - Word, Excel, PowerPoint processing
- **PdfPig** - PDF text extraction

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- GPU recommended for faster inference

### Run the Application

```bash
dotnet run --project AspireOllama.AppHost
```

The first run will download the llava and llama3.1 models. This may take several minutes depending on your internet connection.

### Access Points

Once running, the Aspire Dashboard will show all service endpoints:

- **Aspire Dashboard** - Service management, logs, and traces
- **Web Frontend** - Main chat interface
- **API Service** - Backend REST API with Scalar docs at `/scalar/v1`
- **MCP Server** - External tool server
- **Open WebUI** - Direct Ollama interaction

## Tool Calling

AspireOllama supports automatic tool calling through llama3.1. When you ask questions that require tools, the AI will automatically invoke them.

### Built-in Tools

| Tool | Description | Enabled by Default |
|------|-------------|-------------------|
| Calculator | Evaluate mathematical expressions | Yes |
| Web Search | Search the web (requires API key) | No |
| Code Execution | Run Python/JavaScript/C# code | No |
| File Operations | List/read/write files in sandbox | No |

### MCP Server Tools

The external MCP server provides additional tools:

| Tool | Description |
|------|-------------|
| `get_weather` | Get weather for a city (demo data) |
| `get_time` | Get current time in a timezone |
| `convert_units` | Convert between units (km/miles, kg/lbs, celsius/fahrenheit) |

### Example Queries

- "What is 15 * 7 + 23?" → Uses built-in calculator tool
- "What's the weather in London?" → Uses MCP get_weather tool
- "What time is it in Tokyo?" → Uses MCP get_time tool
- "Convert 100 km to miles" → Uses MCP convert_units tool

## A2A Agents

AspireOllama includes specialized AI agents that communicate via the [Agent-to-Agent (A2A) Protocol](https://github.com/google/a2a-protocol). Each agent exposes standardized endpoints for discovery and task management.

### Agent Capabilities

| Agent | Description | Skills |
|-------|-------------|--------|
| **Planner** | Task planning and workflow orchestration | create_plan, assess_complexity, suggest_agents, orchestrate_workflow |
| **Reviewer** | Quality assurance and validation | review_response, review_code, review_plan, provide_feedback |
| **Research** | Knowledge gathering and context synthesis | search_knowledge, get_topic_details, gather_context, suggest_topics |
| **Code** | Code generation, execution, and analysis | execute_csharp, generate_code, analyze_code, generate_tests, refactor_code |

### A2A Endpoints

Each agent exposes:

| Endpoint | Description |
|----------|-------------|
| `GET /.well-known/agent.json` | Agent Card - describes capabilities, skills, and metadata |
| `POST /a2a/message:send` | Send a message to the agent and receive a task |
| `GET /a2a/tasks/{taskId}` | Get task status and results |
| `POST /a2a/tasks/{taskId}:cancel` | Cancel a running task |

### Inter-Agent Communication

Agents can discover and call each other directly:
- **Planner** calls Research for context and Reviewer for validation
- **Code** calls Reviewer for code review feedback
- All agents use Aspire service discovery for URL resolution

For detailed A2A documentation, see [A2A/README.md](A2A/README.md).

### Tool Configuration

Configure built-in tools in `appsettings.json`:

```json
{
  "Tools": {
    "EnableCalculator": true,
    "EnableWebSearch": false,
    "EnableCodeExecution": false,
    "EnableFileOperations": false,
    "SandboxPath": "./sandbox"
  }
}
```

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/chat` | Send a message with optional images/documents |
| `POST` | `/sessions` | Create a new chat session |
| `GET` | `/sessions` | List all chat sessions |
| `GET` | `/sessions/{id}` | Get session with message history |
| `DELETE` | `/sessions/{id}` | Delete a chat session |
| `GET` | `/debug/mcp` | Debug MCP connection status |
| `GET` | `/test-ollama` | Test Ollama connection |

## Supported File Formats

**Images (processed by llava):**
- JPEG (.jpg, .jpeg)
- PNG (.png)
- GIF (.gif)
- WebP (.webp)

**Documents (text extracted for context):**
- PDF (.pdf)
- Microsoft Word (.doc, .docx)
- Microsoft Excel (.xls, .xlsx)
- Microsoft PowerPoint (.ppt, .pptx)
- Text files (.txt, .csv, .md, .json, .xml, .log)

### Limits

- Maximum 10 files per message
- Maximum 20MB per file
- HTTP client timeout: 10 minutes (for large file processing)

## Development

### Building

```bash
dotnet build
```

### Database

The SQLite database is automatically created at `AspireOllama.ApiService/chat.db` on first run.

### Project Structure

```
AspireOllama.ApiService/
├── Program.cs
├── Data/
│   └── ChatDbContext.cs
├── Endpoints/
│   ├── ChatEndpoint.cs
│   ├── CreateSessionEndpoint.cs
│   ├── GetSessionsEndpoint.cs
│   ├── GetSessionEndpoint.cs
│   ├── DeleteSessionEndpoint.cs
│   ├── TestOllamaEndpoint.cs
│   └── McpDebugEndpoint.cs
└── Services/
    ├── AI/
    │   ├── IAiChatService.cs
    │   ├── AiChatService.cs
    │   └── AiChatResult.cs
    ├── Session/
    │   ├── ISessionService.cs
    │   └── SessionService.cs
    ├── Message/
    │   ├── IChatMessageService.cs
    │   └── ChatMessageService.cs
    ├── Document/
    │   ├── IDocumentProcessingService.cs
    │   └── DocumentProcessingService.cs
    ├── Tools/
    │   ├── ITool.cs
    │   ├── IToolRegistry.cs
    │   ├── ToolRegistry.cs
    │   ├── CalculatorTool.cs
    │   ├── WebSearchTool.cs
    │   ├── CodeExecutionTool.cs
    │   └── FileOperationsTool.cs
    └── Mcp/
        ├── IMcpService.cs
        └── McpService.cs

AspireOllama.McpServer/
└── Program.cs                    # MCP tools: get_weather, get_time, convert_units

AspireOllama.Web/
├── Program.cs
├── ChatApiClient.cs
└── Components/Pages/Chat.razor

AspireOllama.Shared/
├── ChatSession.cs
├── ChatSessionDetails.cs
├── ChatHistoryMessage.cs
├── ChatMessageRequest.cs
├── ChatMessageResponse.cs
├── ImageAttachment.cs
├── FileAttachment.cs
├── ToolCall.cs
└── ToolConfiguration.cs

AspireOllama.AppHost/
└── AppHost.cs                    # Aspire orchestration
```

## Troubleshooting

### Timeout Errors

If you encounter timeout errors when uploading images:
- The llava model requires significant processing time for images
- HTTP timeouts are set to 10 minutes
- Ensure Ollama has finished loading the model (check Aspire Dashboard logs)

### 500 Errors from Ollama

- Verify the llava and llama3.1 models are fully downloaded
- Check Ollama container logs in the Aspire Dashboard
- Test basic chat functionality at `/test-ollama`

### MCP Connection Issues

- Check `/debug/mcp` endpoint for connection status
- Verify MCP server is running in Aspire Dashboard
- Check for retry attempts in API service logs
- Ensure `app.MapMcp("/mcp")` is present in MCP server's Program.cs

### A2A Agent Connection Issues

- Verify agents are running in Aspire Dashboard
- Check that `AddOlamaSharpClient("llama3.1")` is called in each agent's Program.cs
- Agents read Ollama connection from `ConnectionStrings:ollama` (injected by Aspire)
- Check agent logs for "Failed to connect" errors

### Tool Calls Not Working

- Ensure you're asking questions that require tools
- llava (vision model) does not support tool calling
- Only llama3.1 (text model) can invoke tools

### Build Errors (File Locked)

Stop the running application before rebuilding:
- Press `Ctrl+C` in the terminal running the app
- Then run `dotnet build` or `dotnet run`

## License

MIT
