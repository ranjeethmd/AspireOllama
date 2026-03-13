# AspireOllama

A modern AI chat application built with **.NET Aspire** and **Ollama**, featuring multimodal capabilities with image analysis and persistent chat history.

## Features

- **Vision-Enabled AI Chat** - Upload images and get AI-powered analysis using the llava vision model
- **Document Analysis** - Upload and analyze PDF, Word, Excel, PowerPoint, and text files
- **Persistent Chat History** - SQLite-backed session storage with full conversation history
- **Session Management** - Create, switch, and delete chat sessions
- **Multi-File Upload** - Support for images and documents (max 10 files, 20MB each)
- **Local LLM Inference** - Runs entirely on your machine using Ollama with GPU acceleration
- **Cloud-Native Architecture** - Built on .NET Aspire with automatic service discovery and health checks
- **Open WebUI** - Includes Ollama's Open WebUI for direct model interaction

## Architecture

```
AspireOllama/
├── AspireOllama.AppHost/        # .NET Aspire orchestrator
├── AspireOllama.ApiService/     # Backend API with chat endpoints
├── AspireOllama.Web/            # Blazor Server frontend
├── AspireOllama.Shared/         # Shared DTOs
└── AspireOllama.ServiceDefaults/ # Common service configuration
```

| Project | Description |
|---------|-------------|
| `AspireOllama.AppHost` | .NET Aspire orchestrator - manages Ollama, API, and Web services |
| `AspireOllama.ApiService` | Backend API with chat endpoints, session management, and SQLite persistence |
| `AspireOllama.Web` | Blazor Server frontend with interactive chat UI and image upload |
| `AspireOllama.Shared` | Shared DTOs for API communication |
| `AspireOllama.ServiceDefaults` | Common service configuration and health checks |

## Tech Stack

- **.NET 10** / **C# 13**
- **.NET Aspire 9** - Cloud-native orchestration
- **Ollama** - Local LLM inference
- **llava** - Vision-capable language model
- **Blazor Server** - Interactive web UI
- **Entity Framework Core** - SQLite persistence
- **Microsoft.Extensions.AI** - Unified AI abstractions
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

The first run will download the llava model (~4GB). This may take several minutes depending on your internet connection.

### Access Points

Once running, the Aspire Dashboard will show all service endpoints:

- **Aspire Dashboard** - Service management, logs, and traces
- **Web Frontend** - Main chat interface
- **API Service** - Backend REST API
- **Open WebUI** - Direct Ollama interaction

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/chat` | Send a message with optional images |
| `POST` | `/sessions` | Create a new chat session |
| `GET` | `/sessions` | List all chat sessions |
| `GET` | `/sessions/{id}` | Get session with message history |
| `DELETE` | `/sessions/{id}` | Delete a chat session |
| `GET` | `/test-ollama` | Test Ollama connection |

## Configuration

### Supported File Formats

**Images:**
- JPEG (.jpg, .jpeg)
- PNG (.png)
- GIF (.gif)
- WebP (.webp)

**Documents:**
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

### Project Structure

```
AspireOllama.Shared/
├── ChatMessageRequest.cs    # Request DTO with session, content, images
├── ChatMessageResponse.cs   # Response DTO with AI response
├── ChatSession.cs           # Session metadata
├── ChatSessionDetails.cs    # Session with messages
├── ChatHistoryMessage.cs    # Stored message with role and content
└── ImageAttachment.cs       # Base64 encoded image data

AspireOllama.ApiService/
├── Program.cs               # API configuration and endpoints
├── Data/
│   └── ChatDbContext.cs     # EF Core context and entities
└── Services/
    └── ChatHistoryService.cs # CRUD operations for chat history

AspireOllama.Web/
├── Program.cs               # Web app configuration
├── ChatApiClient.cs         # HTTP client for API calls
└── Components/
    └── Pages/
        └── Chat.razor       # Main chat UI component
```

### Building

```bash
dotnet build
```

### Database

The SQLite database is automatically created at `AspireOllama.ApiService/chat.db` on first run.

## Troubleshooting

### Timeout Errors

If you encounter timeout errors when uploading images:
- The llava model requires significant processing time for images
- HTTP timeouts are set to 10 minutes
- Ensure Ollama has finished loading the model (check Aspire Dashboard logs)

### 500 Errors from Ollama

- Verify the llava model is fully downloaded
- Check Ollama container logs in the Aspire Dashboard
- Test basic chat functionality at `/test-ollama`

### Build Errors (File Locked)

Stop the running application before rebuilding:
- Press `Ctrl+C` in the terminal running the app
- Then run `dotnet build` or `dotnet run`

## License

MIT
