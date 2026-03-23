# AspireOllama Architecture

This document provides a comprehensive visual overview of the AspireOllama architecture, including component relationships and data flows.

---

## Table of Contents

1. [System Overview](#system-overview)
2. [High-Level Architecture](#high-level-architecture)
3. [Component Diagram](#component-diagram)
4. [Chat Request Flow](#chat-request-flow)
5. [Model Selection Flow](#model-selection-flow)
6. [Tool Calling Flow](#tool-calling-flow)
7. [MCP Integration Flow](#mcp-integration-flow)
8. [A2A Agent Architecture](#a2a-agent-architecture)
9. [Session & Message Flow](#session--message-flow)
10. [Data Persistence Flow](#data-persistence-flow)
11. [Service Dependencies](#service-dependencies)
12. [Dual-Model Architecture](#dual-model-architecture)
13. [Coordinator Agent](#coordinator-agent)
14. [Infrastructure: MongoDB & Qdrant](#infrastructure-mongodb--qdrant)
15. [Infrastructure: RAG Pipeline](#infrastructure-rag-pipeline)
16. [Infrastructure: Redis Token Cache](#infrastructure-redis-token-cache)
17. [Infrastructure: Observability (New Relic)](#infrastructure-observability-new-relic)
18. [Infrastructure: YARP Gateway & Let's Encrypt](#infrastructure-yarp-gateway--lets-encrypt)
19. [Infrastructure: Kubernetes Deployment](#infrastructure-kubernetes-deployment)
20. [User Sessions & Scoping](#user-sessions--scoping)
21. [Timeout Configuration](#timeout-configuration)
22. [Heartbeat Logging](#heartbeat-logging)
23. [Chat UI](#chat-ui)
24. [Workflow UI](#workflow-ui)

---

## System Overview

AspireOllama is a distributed AI chat application with the following key capabilities:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          AspireOllama Capabilities                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐    │
│   │   Vision    │   │  Built-in   │   │    MCP      │   │  Document   │    │
│   │   Chat      │   │   Tools     │   │   Tools     │   │  Analysis   │    │
│   │             │   │             │   │             │   │             │    │
│   │  Analyze    │   │ Calculator  │   │  Weather    │   │    PDF      │    │
│   │  images     │   │             │   │  Time       │   │   Word      │    │
│   │  with AI    │   │             │   │  Convert    │   │   Excel     │    │
│   └─────────────┘   └─────────────┘   └─────────────┘   └─────────────┘    │
│                                                                             │
│   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐    │
│   │    A2A      │   │   Local     │   │   Cloud     │   │   Modern    │    │
│   │   Agents    │   │    LLM      │   │   Native    │   │    UI       │    │
│   │             │   │             │   │             │   │             │    │
│   │ Coordinator │   │   Ollama    │   │   Aspire    │   │   Dark      │    │
│   │  Planner    │   │   GPU       │   │  Discovery  │   │   Theme     │    │
│   │  Reviewer   │   │             │   │             │   │             │    │
│   │ Research    │   │             │   │             │   │             │    │
│   │  Code       │   │             │   │             │   │             │    │
│   └─────────────┘   └─────────────┘   └─────────────┘   └─────────────┘    │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                                                                             │
│                         .NET ASPIRE ORCHESTRATOR                            │
│                                                                             │
│   Manages service lifecycle, health checks, and service discovery           │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
        ┌─────────────────────────────┼─────────────────────────────┐
        │                             │                             │
        ▼                             ▼                             ▼
┌───────────────────┐       ┌───────────────────┐       ┌───────────────────┐
│                   │       │                   │       │                   │
│   WEB FRONTEND    │       │   API SERVICE     │       │   MCP SERVER      │
│                   │       │                   │       │                   │
│   ┌───────────┐   │       │   ┌───────────┐   │       │   ┌───────────┐   │
│   │  Blazor   │   │──────▶│   │   REST    │   │◀─────▶│   │   HTTP    │   │
│   │  Server   │   │       │   │   API     │   │       │   │ Transport │   │
│   └───────────┘   │       │   └───────────┘   │       │   └───────────┘   │
│                   │       │                   │       │                   │
│   ┌───────────┐   │       │   ┌───────────┐   │       │   ┌───────────┐   │
│   │  Chat UI  │   │       │   │   Tools   │   │       │   │  Weather  │   │
│   │  Session  │   │       │   │  Registry │   │       │   │Time/Conv  │   │
│   └───────────┘   │       │   └───────────┘   │       │   └───────────┘   │
│                   │       │                   │       │                   │
└───────────────────┘       │   ┌───────────┐   │       └───────────────────┘
                            │   │  MongoDB  │   │
                            │   │ Database  │   │
                            │   └───────────┘   │
                            │                   │
                            └─────────┬─────────┘
                                      │
                    ┌─────────────────┼─────────────────┐
                    │                 │                 │
                    ▼                 ▼                 ▼
            ┌───────────────┐ ┌───────────────┐ ┌───────────────┐
            │    OLLAMA     │ │ Qwen2.5-VL   │ │    Qwen3      │
            │   Container   │ │   (32B)       │ │    (32B)      │
            │               │ │               │ │               │
            │   GPU Accel   │ │   Vision      │ │    Tools      │
            │   Data Vol    │ │   Images      │ │   Functions   │
            └───────────────┘ └───────────────┘ └───────────────┘
```

---

## Component Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           PROJECT STRUCTURE                                 │
└─────────────────────────────────────────────────────────────────────────────┘


    ┌─────────────────────────────────────────────────────────────────────┐
    │                        AspireOllama.AppHost                         │
    │                                                                     │
    │                    Orchestrates all services                        │
    │                    Manages dependencies                             │
    │                    Service discovery                                │
    └─────────────────────────────────────────────────────────────────────┘
                                      │
                                      │ orchestrates
                                      ▼
    ┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
    │                 │     │                 │     │                 │
    │  AspireOllama   │     │  AspireOllama   │     │  AspireOllama   │
    │     .Web        │────▶│   .ApiService   │◀───▶│   .McpServer    │
    │                 │     │                 │     │                 │
    │  Blazor UI      │     │  REST + MCP     │     │  MCP Tools      │
    │  Chat Interface │     │  Client         │     │  Weather/Time/  │
    │                 │     │  Persistence    │     │  Convert        │
    └─────────────────┘     └─────────────────┘     └─────────────────┘
            │                       │                       │
            │                       │                       │
            └───────────────────────┼───────────────────────┘
                                    │
                                    ▼
                        ┌─────────────────────┐
                        │                     │
                        │  AspireOllama       │
                        │     .Shared         │
                        │                     │
                        │  DTOs & Models      │
                        │  Common Types       │
                        │                     │
                        └─────────────────────┘
                                    │
                                    ▼
                        ┌─────────────────────┐
                        │                     │
                        │  AspireOllama       │
                        │  .ServiceDefaults   │
                        │                     │
                        │  Health Checks      │
                        │  Telemetry          │
                        │  Client Extensions: │
                        │  - AddOlamaSharp    │
                        │  - AddMcpServer     │
                        │  - AddA2AClient     │
                        │                     │
                        └─────────────────────┘
```

---

## Chat Request Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         COMPLETE CHAT REQUEST FLOW                          │
└─────────────────────────────────────────────────────────────────────────────┘


    ┌──────────┐
    │   USER   │
    └────┬─────┘
         │
         │ 1. Types message
         │    Attaches images/documents
         │
         ▼
    ┌──────────────────────────────────────────────────────────────────────┐
    │                         WEB FRONTEND                                 │
    │                                                                      │
    │    ┌─────────────┐     ┌─────────────┐     ┌─────────────┐          │
    │    │   Input     │────▶│   Upload    │────▶│   Send      │          │
    │    │   Message   │     │   Files     │     │   Request   │          │
    │    └─────────────┘     └─────────────┘     └─────────────┘          │
    │                                                                      │
    └──────────────────────────────────┬───────────────────────────────────┘
                                       │
                                       │ 2. POST /chat
                                       │    {sessionId, content, images, files}
                                       ▼
    ┌──────────────────────────────────────────────────────────────────────┐
    │                         API SERVICE                                  │
    │                                                                      │
    │    ┌─────────────────────────────────────────────────────────────┐   │
    │    │                     Chat Endpoint                           │   │
    │    └─────────────────────────────────────────────────────────────┘   │
    │                                │                                     │
    │         ┌──────────────────────┼──────────────────────┐              │
    │         │                      │                      │              │
    │         ▼                      ▼                      ▼              │
    │    ┌─────────┐           ┌─────────┐           ┌─────────┐          │
    │    │  Load   │           │ Extract │           │ Collect │          │
    │    │ History │           │  Text   │           │  Tools  │          │
    │    │  from   │           │  from   │           │         │          │
    │    │   DB    │           │  Docs   │           │ Built-in│          │
    │    └─────────┘           └─────────┘           │  + MCP  │          │
    │                                                └─────────┘          │
    │                                │                      │              │
    │                                └──────────┬───────────┘              │
    │                                           │                          │
    │                                           ▼                          │
    │                                   ┌───────────────┐                  │
    │                                   │    Select     │                  │
    │                                   │    Model      │                  │
    │                                   │               │                  │
    │                                   │ Images? qwen2.5vl │                  │
    │                                   │ Text? qwen3│                  │
    │                                   └───────┬───────┘                  │
    │                                           │                          │
    └───────────────────────────────────────────┼──────────────────────────┘
                                                │
                                                │ 3. Send to AI Model
                                                ▼
    ┌──────────────────────────────────────────────────────────────────────┐
    │                            OLLAMA                                    │
    │                                                                      │
    │         ┌─────────────────┐         ┌─────────────────┐             │
    │         │                 │         │                 │             │
    │         │     qwen2.5vl       │   OR    │    qwen3     │             │
    │         │                 │         │                 │             │
    │         │  Vision Model   │         │  Tool-Calling   │             │
    │         │  Image Analysis │         │    Model        │             │
    │         │                 │         │                 │             │
    │         └─────────────────┘         └─────────────────┘             │
    │                                                                      │
    └──────────────────────────────────┬───────────────────────────────────┘
                                       │
                                       │ 4. AI Response
                                       │    (may include tool calls)
                                       ▼
    ┌──────────────────────────────────────────────────────────────────────┐
    │                         API SERVICE                                  │
    │                                                                      │
    │    ┌─────────────────────────────────────────────────────────────┐   │
    │    │                   Process Response                          │   │
    │    │                                                             │   │
    │    │   ┌───────────┐   ┌───────────┐   ┌───────────┐            │   │
    │    │   │  Execute  │   │   Save    │   │  Update   │            │   │
    │    │   │   Tools   │   │ Messages  │   │  Session  │            │   │
    │    │   │(if needed)│   │   to DB   │   │  Title    │            │   │
    │    │   └───────────┘   └───────────┘   └───────────┘            │   │
    │    │                                                             │   │
    │    └─────────────────────────────────────────────────────────────┘   │
    │                                                                      │
    └──────────────────────────────────┬───────────────────────────────────┘
                                       │
                                       │ 5. Return Response
                                       │    {response, toolCalls}
                                       ▼
    ┌──────────────────────────────────────────────────────────────────────┐
    │                         WEB FRONTEND                                 │
    │                                                                      │
    │    ┌─────────────┐     ┌─────────────┐     ┌─────────────┐          │
    │    │  Display    │     │  Display    │     │  Update     │          │
    │    │  Response   │     │  Tool Calls │     │  History    │          │
    │    └─────────────┘     └─────────────┘     └─────────────┘          │
    │                                                                      │
    └──────────────────────────────────────────────────────────────────────┘
                                       │
                                       │ 6. Show to User
                                       ▼
                                 ┌──────────┐
                                 │   USER   │
                                 └──────────┘
```

---

## Model Selection Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          MODEL SELECTION LOGIC                              │
└─────────────────────────────────────────────────────────────────────────────┘


                         ┌───────────────────────┐
                         │   Incoming Request    │
                         │                       │
                         │   - Text content      │
                         │   - Images (optional) │
                         │   - Documents (opt)   │
                         └───────────┬───────────┘
                                     │
                                     ▼
                         ┌───────────────────────┐
                         │                       │
                         │   Does request have   │
                         │      images?          │
                         │                       │
                         └───────────┬───────────┘
                                     │
                    ┌────────────────┴────────────────┐
                    │                                 │
                    ▼ YES                             ▼ NO
        ┌───────────────────────┐         ┌───────────────────────┐
        │                       │         │                       │
        │    USE qwen2.5vl MODEL    │         │  USE qwen3 MODEL   │
        │                       │         │                       │
        │  ┌─────────────────┐  │         │  ┌─────────────────┐  │
        │  │ Vision Capable  │  │         │  │  Tool Capable   │  │
        │  │                 │  │         │  │                 │  │
        │  │ Can analyze:    │  │         │  │ Can call:       │  │
        │  │ - Photos        │  │         │  │ - Calculator    │  │
        │  │ - Screenshots   │  │         │  │ - Weather       │  │
        │  │ - Diagrams      │  │         │  │ - Time          │  │
        │  │ - Documents     │  │         │  │ - Custom tools  │  │
        │  └─────────────────┘  │         │  └─────────────────┘  │
        │                       │         │                       │
        │  ┌─────────────────┐  │         │  ┌─────────────────┐  │
        │  │ NO Tool Support │  │         │  │ NO Vision       │  │
        │  └─────────────────┘  │         │  └─────────────────┘  │
        │                       │         │                       │
        └───────────┬───────────┘         └───────────┬───────────┘
                    │                                 │
                    ▼                                 ▼
        ┌───────────────────────┐         ┌───────────────────────┐
        │                       │         │                       │
        │   Response: Text      │         │   Response: Text      │
        │   Tool Calls: None    │         │   Tool Calls: [...]   │
        │                       │         │                       │
        └───────────────────────┘         └───────────────────────┘
```

---

## Tool Calling Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          TOOL CALLING WORKFLOW                              │
└─────────────────────────────────────────────────────────────────────────────┘


    ┌──────────────────────────────────────────────────────────────────────┐
    │                                                                      │
    │   User: "What is 15 * 7 + 23?"                                       │
    │                                                                      │
    └──────────────────────────────────┬───────────────────────────────────┘
                                       │
                                       ▼
    ┌──────────────────────────────────────────────────────────────────────┐
    │                         TOOL COLLECTION                              │
    │                                                                      │
    │    ┌─────────────────────────────────────────────────────────────┐   │
    │    │                    Built-in Tools                           │   │
    │    │                                                             │   │
    │    │   ┌────────────┐  ┌────────────┐  ┌────────────┐           │   │
    │    │   │ Calculator │  │ Web Search │  │ File Ops   │           │   │
    │    │   │ (enabled)  │  │ (disabled) │  │ (disabled) │           │   │
    │    │   └────────────┘  └────────────┘  └────────────┘           │   │
    │    │                                                             │   │
    │    └─────────────────────────────────────────────────────────────┘   │
    │                                   +                                  │
    │    ┌─────────────────────────────────────────────────────────────┐   │
    │    │                      MCP Tools                              │   │
    │    │                                                             │   │
    │    │   ┌────────────┐  ┌────────────┐  ┌──────────────┐         │   │
    │    │   │get_weather │  │  get_time  │  │convert_units │         │   │
    │    │   └────────────┘  └────────────┘  └──────────────┘         │   │
    │    │                                                             │   │
    │    └─────────────────────────────────────────────────────────────┘   │
    │                                                                      │
    └──────────────────────────────────┬───────────────────────────────────┘
                                       │
                                       ▼
    ┌──────────────────────────────────────────────────────────────────────┐
    │                         qwen3 MODEL                               │
    │                                                                      │
    │    ┌─────────────────────────────────────────────────────────────┐   │
    │    │                                                             │   │
    │    │   Analyzes: "What is 15 * 7 + 23?"                          │   │
    │    │                                                             │   │
    │    │   Decision: I need to use the calculator tool               │   │
    │    │                                                             │   │
    │    │   Output: Tool Call Request                                 │   │
    │    │           - Tool: calculator                                │   │
    │    │           - Args: {expression: "15 * 7 + 23"}               │   │
    │    │                                                             │   │
    │    └─────────────────────────────────────────────────────────────┘   │
    │                                                                      │
    └──────────────────────────────────┬───────────────────────────────────┘
                                       │
                                       ▼
    ┌──────────────────────────────────────────────────────────────────────┐
    │                      TOOL EXECUTION ENGINE                           │
    │                                                                      │
    │    ┌─────────────────────────────────────────────────────────────┐   │
    │    │                                                             │   │
    │    │   Intercepts tool call request                              │   │
    │    │                                                             │   │
    │    │   ┌─────────────────────────────────────────────────────┐   │   │
    │    │   │                                                     │   │   │
    │    │   │   Execute: Calculator.Calculate("15 * 7 + 23")      │   │   │
    │    │   │                                                     │   │   │
    │    │   │   Result: "128"                                     │   │   │
    │    │   │                                                     │   │   │
    │    │   └─────────────────────────────────────────────────────┘   │   │
    │    │                                                             │   │
    │    │   Send result back to model                                 │   │
    │    │                                                             │   │
    │    └─────────────────────────────────────────────────────────────┘   │
    │                                                                      │
    └──────────────────────────────────┬───────────────────────────────────┘
                                       │
                                       ▼
    ┌──────────────────────────────────────────────────────────────────────┐
    │                         qwen3 MODEL                               │
    │                                                                      │
    │    ┌─────────────────────────────────────────────────────────────┐   │
    │    │                                                             │   │
    │    │   Receives tool result: "128"                               │   │
    │    │                                                             │   │
    │    │   Generates final response:                                 │   │
    │    │   "15 * 7 + 23 equals 128"                                  │   │
    │    │                                                             │   │
    │    └─────────────────────────────────────────────────────────────┘   │
    │                                                                      │
    └──────────────────────────────────┬───────────────────────────────────┘
                                       │
                                       ▼
    ┌──────────────────────────────────────────────────────────────────────┐
    │                          FINAL RESPONSE                              │
    │                                                                      │
    │    Response: "15 * 7 + 23 equals 128"                                │
    │                                                                      │
    │    Tool Calls:                                                       │
    │    ┌─────────────────────────────────────────────────────────────┐   │
    │    │  Tool: calculator                                           │   │
    │    │  Arguments: {expression: "15 * 7 + 23"}                     │   │
    │    │  Result: "128"                                              │   │
    │    │  Status: Completed                                          │   │
    │    └─────────────────────────────────────────────────────────────┘   │
    │                                                                      │
    └──────────────────────────────────────────────────────────────────────┘
```

---

## MCP Integration Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       MCP CLIENT-SERVER INTEGRATION                         │
└─────────────────────────────────────────────────────────────────────────────┘


                    APPLICATION STARTUP
                           │
                           ▼
    ┌──────────────────────────────────────────────────────────────────────┐
    │                        API SERVICE                                   │
    │                                                                      │
    │                     MCP Service Initialization                       │
    │                                                                      │
    └──────────────────────────────────┬───────────────────────────────────┘
                                       │
                                       │ 1. Connect via Aspire
                                       │    Service Discovery
                                       ▼
    ┌──────────────────────────────────────────────────────────────────────┐
    │                        MCP SERVER                                    │
    │                                                                      │
    │                    HTTP Transport at /mcp                            │
    │                                                                      │
    └──────────────────────────────────────────────────────────────────────┘
                                       │
                                       │ 2. Initialize Connection
                                       │
                                       ▼
    ┌────────────────────────────────────────────────────────────────────┐
    │                                                                    │
    │   API SERVICE                           MCP SERVER                 │
    │                                                                    │
    │   ┌───────────────┐                    ┌───────────────┐          │
    │   │               │  ── Initialize ──▶ │               │          │
    │   │   MCP Client  │                    │  MCP Server   │          │
    │   │               │ ◀── Server Info ── │               │          │
    │   └───────────────┘                    └───────────────┘          │
    │                                                                    │
    └────────────────────────────────────────────────────────────────────┘
                                       │
                                       │ 3. Discover Tools
                                       │
                                       ▼
    ┌────────────────────────────────────────────────────────────────────┐
    │                                                                    │
    │   API SERVICE                           MCP SERVER                 │
    │                                                                    │
    │   ┌───────────────┐                    ┌───────────────┐          │
    │   │               │  ── List Tools ──▶ │               │          │
    │   │   MCP Client  │                    │  MCP Server   │          │
    │   │               │ ◀── Tool List ──── │               │          │
    │   └───────────────┘                    │               │          │
    │         │                              │ - get_weather │          │
    │         │                              │ - get_time    │          │
    │         │                              │ - convert_units          │
    │         ▼                              └───────────────┘          │
    │   ┌───────────────┐                                               │
    │   │ Store tools   │                                               │
    │   │ for later use │                                               │
    │   └───────────────┘                                               │
    │                                                                    │
    └────────────────────────────────────────────────────────────────────┘
                                       │
                                       │
                    AT CHAT TIME       │
                           │           │
                           ▼           │
    ┌────────────────────────────────────────────────────────────────────┐
    │                                                                    │
    │   API SERVICE                           MCP SERVER                 │
    │                                                                    │
    │   ┌───────────────┐                    ┌───────────────┐          │
    │   │  AI requests  │  ── Call Tool ───▶ │               │          │
    │   │  get_weather  │     {city:London}  │   Execute     │          │
    │   │  tool         │                    │  get_weather  │          │
    │   │               │ ◀── Result ─────── │               │          │
    │   └───────────────┘    "Weather: 33°C" └───────────────┘          │
    │         │                                                          │
    │         ▼                                                          │
    │   ┌───────────────┐                                               │
    │   │ Return result │                                               │
    │   │ to AI model   │                                               │
    │   └───────────────┘                                               │
    │                                                                    │
    └────────────────────────────────────────────────────────────────────┘
```

### MCP Server Tool Registration

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      MCP SERVER TOOL DISCOVERY                              │
└─────────────────────────────────────────────────────────────────────────────┘


    ┌─────────────────────────────────────────────────────────────────────┐
    │                         MCP SERVER                                  │
    │                                                                     │
    │    Application Startup                                              │
    │                                                                     │
    │    ┌─────────────────────────────────────────────────────────────┐  │
    │    │                  Assembly Scanner                           │  │
    │    │                                                             │  │
    │    │   Finds classes marked as MCP Tool Types                    │  │
    │    │                                                             │  │
    │    └──────────────────────────┬──────────────────────────────────┘  │
    │                               │                                     │
    │         ┌──────────────────────┼──────────────────────┐            │
    │         │                      │                      │            │
    │         ▼                      ▼                      ▼            │
    │    ┌───────────┐         ┌───────────┐         ┌─────────────┐    │
    │    │WeatherTool│         │ TimeTool  │         │ConvertTool  │    │
    │    │           │         │           │         │             │    │
    │    │get_weather│         │ get_time  │         │convert_units│    │
    │    └───────────┘         └───────────┘         └─────────────┘    │
    │         │                      │                      │            │
    │         └──────────────────────┼──────────────────────┘            │
    │                               │                                     │
    │                               ▼                                     │
    │    ┌─────────────────────────────────────────────────────────────┐  │
    │    │                   /mcp Endpoint                             │  │
    │    │                                                             │  │
    │    │   Handles MCP Protocol:                                     │  │
    │    │   - initialize                                              │  │
    │    │   - tools/list                                              │  │
    │    │   - tools/call                                              │  │
    │    │                                                             │  │
    │    └─────────────────────────────────────────────────────────────┘  │
    │                                                                     │
    └─────────────────────────────────────────────────────────────────────┘
```

---

## A2A Agent Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    AGENT-TO-AGENT (A2A) PROTOCOL ARCHITECTURE               │
└─────────────────────────────────────────────────────────────────────────────┘


                              A2A PROTOCOL LAYER
    ┌─────────────────────────────────────────────────────────────────────┐
    │                                                                     │
    │   Each agent exposes:                                               │
    │   • GET  /.well-known/agent.json  (Agent Card - discovery)          │
    │   • POST /a2a/message:send        (Send message, get task)          │
    │   • GET  /a2a/tasks/{id}          (Get task status/results)         │
    │   • POST /a2a/tasks/{id}:cancel   (Cancel running task)             │
    │                                                                     │
    └─────────────────────────────────────────────────────────────────────┘
                                      │
         ┌───────────────┬────────────┼────────────┬───────────────┐
         │               │            │            │               │
         ▼               ▼            ▼            ▼               │
    ┌─────────┐     ┌─────────┐  ┌─────────┐  ┌─────────┐          │
    │ Planner │◄───►│Reviewer │◄─┤Research │◄─┤  Code   │          │
    │  Agent  │     │  Agent  │  │  Agent  │  │  Agent  │          │
    └────┬────┘     └────┬────┘  └────┬────┘  └────┬────┘          │
         │               │            │            │               │
         │ Skills:       │ Skills:    │ Skills:    │ Skills:       │
         │ create_plan   │ review_    │ search_    │ execute_      │
         │ assess_       │  response  │  knowledge │  csharp       │
         │  complexity   │ review_    │ get_topic_ │ generate_     │
         │ suggest_      │  code      │  details   │  code         │
         │  agents       │ provide_   │ gather_    │ analyze_      │
         │               │  feedback  │  context   │  code         │
         │ (3 skills)    │            │ suggest_   │ generate_     │
         │               │ (4 skills) │  topics    │  tests        │
         │               │            │            │ refactor_     │
         │               │            │ (4 skills) │  code         │
         │               │            │            │ (5 skills)    │
         └───────────────┴────────────┴────────────┘               │
                                      │                            │
                              ┌───────▼───────┐                    │
                              │    Ollama     │◄───────────────────┘
                              │  (Qwen3 32B) │
                              └───────────────┘

    Coordinator knows all 17 skills across 4 agents and uses AI-driven
    planning to select skills and route tasks.
```

### A2A Class Hierarchy

Protocol models use `AspireOllama.A2A.Protocol` namespace with spec-aligned names
(`Task`, `Message`, `Part`, `Artifact`). `Task` intentionally clashes with
`System.Threading.Tasks.Task` — to keep protocol naming pristine, resolved via
`using Task = System.Threading.Tasks.Task;`. Protocol task is always `Protocol.Task`.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        A2A CLASS HIERARCHY                                   │
└─────────────────────────────────────────────────────────────────────────────┘

    AspireOllama.A2A.Protocol (namespace — A2A spec models):
      Task, TaskStatus, TaskState, Message, MessageRole, Part, Artifact,
      AgentCard, AgentSkill, SendMessageRequest, SendMessageResponse,
      PushNotificationConfig

    IA2AServer (interface)                ISkillAuthorizationProvider (interface)
    │  Pure A2A protocol spec             │  ResolveSkill(message) → skillId
    │  11 JSON-RPC operations             │  GetSkillRoles() → { skillId: role }
    │                                     │
    └────────────────┬────────────────────┘
                     │ implements both
                     ▼
    ┌─────────────────────────────────────────────────────────────────────┐
    │                     A2AServerBase (abstract)                        │
    │                                                                     │
    │  Implements:  task CRUD, message handling, helper methods           │
    │  Virtual:     ResolveSkill() → null, GetSkillRoles() → empty       │
    │  Virtual:     streaming, push notifications → NotSupportedException │
    │  Abstract:    GetAgentCard(), ProcessMessageAsync()                 │
    └────────────────────────────────┬────────────────────────────────────┘
                                     │ extends
          ┌──────────────┬───────────┼───────────┬──────────────┐
          ▼              ▼           ▼           ▼              ▼
    ┌───────────┐  ┌───────────┐  ┌─────────┐  ┌───────────┐  ┌───────────┐
    │ Planner   │  │ Reviewer  │  │Research │  │   Code    │  │Coordinator│
    │ A2AServer │  │ A2AServer │  │A2AServer│  │ A2AServer │  │ A2AServer │
    │           │  │           │  │         │  │           │  │           │
    │ Overrides:│  │ Overrides:│  │Overrides│  │ Overrides:│  │ Overrides:│
    │ Resolve   │  │ Resolve   │  │Resolve  │  │ Resolve   │  │ Resolve   │
    │  Skill    │  │  Skill    │  │ Skill   │  │  Skill    │  │  Skill    │
    │ GetSkill  │  │ GetSkill  │  │GetSkill │  │ GetSkill  │  │ GetSkill  │
    │  Roles    │  │  Roles    │  │ Roles   │  │  Roles    │  │  Roles    │
    │ Process   │  │ Process   │  │Process  │  │ Process   │  │ Process   │
    │  Message  │  │  Message  │  │ Message │  │  Message  │  │  Message  │
    └───────────┘  └───────────┘  └─────────┘  └───────────┘  └───────────┘
```

### Per-Skill Authorization Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      A2A PER-SKILL AUTHORIZATION                            │
└─────────────────────────────────────────────────────────────────────────────┘

    POST /a2a/message:send  (or message:stream)
          │
          ▼
    ┌──────────────────────────────┐
    │ JWT Bearer + accessRole      │  ← endpoint-level auth
    └──────────────┬───────────────┘
                   │
                   ▼
    ┌──────────────────────────────┐
    │ IsSkillForbidden(server,     │  ← pattern match: server is
    │   httpContext, message)       │    ISkillAuthorizationProvider?
    │                              │
    │ server is ISkillAuthProvider? │─── No ──► Allow
    │      │ Yes                   │
    │      ▼                       │
    │ ResolveSkill(message)        │  ← switch expression on text
    │ → e.g. "review_code"        │
    │      │                       │
    │      ▼                       │
    │ GetSkillRoles()["review_code"]│
    │ → "A2A.Reviewer.ReviewCode"  │
    │      │                       │
    │      ▼                       │
    │ User.IsInRole(role)?         │
    │   Yes → Allow                │
    │   No  → 403 Forbid           │
    └──────────────────────────────┘

    Extensions (A2A.Shared):
      AddA2AServices()       — known agents, HTTP clients, rate limiting
      AddA2AServer<T>()      — registers server singleton (constraint: IA2AServer)
      MapA2AEndpoints<T>()   — maps all endpoints, auth, rate limiting,
                               per-skill auth via ISkillAuthorizationProvider,
                               501 for unsupported operations
```

### Agent Card Structure

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           AGENT CARD (/.well-known/agent.json)              │
└─────────────────────────────────────────────────────────────────────────────┘

    {
      "name": "Planner Agent",
      "description": "AI-powered task planning and complexity assessment",
      "version": "2.0.0",
      "url": "http://planner-agent",
      "provider": { "organization": "AspireOllama" },
      "capabilities": { "streaming": false, "pushNotifications": false },
      "skills": [
        {
          "id": "create_plan",
          "name": "Create Plan",
          "description": "Breaks complex tasks into steps",
          "tags": ["planning", "orchestration"],
          "examples": ["Create a plan for building a REST API"]
        }
      ],
      "defaultInputModes": ["text/plain"],
      "defaultOutputModes": ["text/plain", "application/json"]
    }
```

### A2A Task Lifecycle

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                             A2A TASK LIFECYCLE                              │
└─────────────────────────────────────────────────────────────────────────────┘


    ┌──────────────┐
    │   SUBMITTED  │  ◄──── POST /a2a/message:send
    └──────┬───────┘
           │
           ▼
    ┌──────────────┐
    │   WORKING    │  ◄──── Agent processing (may call other agents)
    └──────┬───────┘
           │
           ├──────────────────────────────┐
           │                              │
           ▼                              ▼
    ┌──────────────┐               ┌──────────────┐
    │  COMPLETED   │               │    FAILED    │
    │              │               │              │
    │  Artifacts:  │               │  Error msg   │
    │  - Results   │               │              │
    │  - History   │               │              │
    └──────────────┘               └──────────────┘


    Other states: CANCELED, INPUT_REQUIRED, AUTH_REQUIRED
```

### Inter-Agent Communication Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         MULTI-AGENT WORKFLOW EXAMPLE                        │
└─────────────────────────────────────────────────────────────────────────────┘


    User Request: "Build a REST API with authentication"
          │
          ▼
    ┌──────────────────────────────────────────────────────────────────────┐
    │                         PLANNER AGENT                                │
    │   1. Assess complexity                                               │
    │   2. Call Research Agent for context                                 │
    │   3. Create detailed plan                                            │
    │   4. Call Reviewer Agent to validate plan                            │
    └──────────────────────────────────────┬───────────────────────────────┘
                                           │
              ┌────────────────────────────┼────────────────────────────┐
              │                            │                            │
              ▼                            ▼                            ▼
    ┌─────────────────┐          ┌─────────────────┐          ┌─────────────────┐
    │ RESEARCH AGENT  │          │ REVIEWER AGENT  │          │   CODE AGENT    │
    │ gather_context  │          │ review_plan     │          │ generate_code   │
    └────────┬────────┘          └────────┬────────┘          └────────┬────────┘
             │                            │                            │
             └────────────────────────────┼────────────────────────────┘
                                          │
                                          ▼
                               ┌─────────────────────┐
                               │   PLANNER AGENT     │
                               │   Combines results  │
                               │   Returns complete  │
                               │   task with all     │
                               │   artifacts         │
                               └─────────────────────┘
```

---

## Session & Message Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         SESSION LIFECYCLE                                   │
└─────────────────────────────────────────────────────────────────────────────┘


    CREATE SESSION                 SEND MESSAGE                 DELETE SESSION
          │                              │                              │
          ▼                              ▼                              ▼
    ┌───────────────┐            ┌───────────────┐            ┌───────────────┐
    │               │            │               │            │               │
    │  POST         │            │  POST         │            │  DELETE       │
    │  /sessions    │            │  /chat        │            │  /sessions/id │
    │               │            │               │            │               │
    └───────┬───────┘            └───────┬───────┘            └───────┬───────┘
            │                            │                            │
            ▼                            ▼                            ▼
    ┌───────────────┐            ┌───────────────┐            ┌───────────────┐
    │               │            │               │            │               │
    │ Generate UUID │            │ Load History  │            │ Find Session  │
    │               │            │               │            │               │
    │ Set Title =   │            │ Get AI        │            │ Delete All    │
    │ "New Chat"    │            │ Response      │            │ Messages      │
    │               │            │               │            │ (CASCADE)     │
    │ Set Timestamps│            │ Save User Msg │            │               │
    │               │            │               │            │ Delete        │
    │               │            │ Save AI Msg   │            │ Session       │
    │               │            │               │            │               │
    │               │            │ Update Title  │            │               │
    │               │            │ (if first)    │            │               │
    │               │            │               │            │               │
    │               │            │ Update        │            │               │
    │               │            │ Timestamp     │            │               │
    │               │            │               │            │               │
    └───────┬───────┘            └───────┬───────┘            └───────┬───────┘
            │                            │                            │
            ▼                            ▼                            ▼
    ┌───────────────────────────────────────────────────────────────────────┐
    │                                                                       │
    │                      MongoDB DATABASE (scoped by userId)             │
    │                                                                       │
    │   ┌─────────────────────┐           ┌─────────────────────────────┐  │
    │   │    ChatSessions     │           │       ChatMessages          │  │
    │   │                     │           │                             │  │
    │   │  - Id (UUID)        │◀──────────│  - SessionId (FK)           │  │
    │   │  - Title            │    1:N    │  - Role (user/assistant)    │  │
    │   │  - CreatedAt        │           │  - Content                  │  │
    │   │  - UpdatedAt        │           │  - ImagesJson               │  │
    │   │                     │           │  - FilesJson                │  │
    │   │                     │           │  - Timestamp                │  │
    │   └─────────────────────┘           └─────────────────────────────┘  │
    │                                                                       │
    └───────────────────────────────────────────────────────────────────────┘
```

---

## Data Persistence Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       MESSAGE PERSISTENCE FLOW                              │
└─────────────────────────────────────────────────────────────────────────────┘


    ┌──────────────────────────────────────────────────────────────────────┐
    │                      INCOMING CHAT REQUEST                           │
    │                                                                      │
    │   ┌────────────────────────────────────────────────────────────────┐ │
    │   │                                                                │ │
    │   │  Session ID: "abc-123-def"                                     │ │
    │   │  Content: "Analyze this document"                              │ │
    │   │  Images: [screenshot.png]                                      │ │
    │   │  Files: [report.pdf]                                           │ │
    │   │                                                                │ │
    │   └────────────────────────────────────────────────────────────────┘ │
    │                                                                      │
    └──────────────────────────────────┬───────────────────────────────────┘
                                       │
                                       ▼
    ┌──────────────────────────────────────────────────────────────────────┐
    │                      SAVE USER MESSAGE                               │
    │                                                                      │
    │   ┌─────────────────────────────────────────────────────────────┐    │
    │   │                                                             │    │
    │   │  Role: "user"                                               │    │
    │   │  Content: "Analyze this document [Attached: report.pdf]"    │    │
    │   │                                                             │    │
    │   │  ImagesJson: [                                              │    │
    │   │    {                                                        │    │
    │   │      fileName: "screenshot.png",                            │    │
    │   │      contentType: "image/png",                              │    │
    │   │      base64Data: "iVBORw0KGgo..."                           │    │
    │   │    }                                                        │    │
    │   │  ]                                                          │    │
    │   │                                                             │    │
    │   │  FilesJson: [                                               │    │
    │   │    {                                                        │    │
    │   │      fileName: "report.pdf",                                │    │
    │   │      contentType: "application/pdf",                        │    │
    │   │      base64Data: "JVBERi0x..."                              │    │
    │   │    }                                                        │    │
    │   │  ]                                                          │    │
    │   │                                                             │    │
    │   └─────────────────────────────────────────────────────────────┘    │
    │                                                                      │
    └──────────────────────────────────┬───────────────────────────────────┘
                                       │
                                       ▼
    ┌──────────────────────────────────────────────────────────────────────┐
    │                      AI PROCESSING                                   │
    │                                                                      │
    │   ┌─────────────────────────────────────────────────────────────┐    │
    │   │                                                             │    │
    │   │  1. Extract text from PDF                                   │    │
    │   │  2. Analyze image with qwen2.5vl                                │    │
    │   │  3. Generate response                                       │    │
    │   │                                                             │    │
    │   └─────────────────────────────────────────────────────────────┘    │
    │                                                                      │
    └──────────────────────────────────┬───────────────────────────────────┘
                                       │
                                       ▼
    ┌──────────────────────────────────────────────────────────────────────┐
    │                      SAVE AI RESPONSE                                │
    │                                                                      │
    │   ┌─────────────────────────────────────────────────────────────┐    │
    │   │                                                             │    │
    │   │  Role: "assistant"                                          │    │
    │   │  Content: "Based on the document, I can see..."             │    │
    │   │  ImagesJson: []                                             │    │
    │   │  FilesJson: []                                              │    │
    │   │                                                             │    │
    │   └─────────────────────────────────────────────────────────────┘    │
    │                                                                      │
    └──────────────────────────────────┬───────────────────────────────────┘
                                       │
                                       ▼
    ┌──────────────────────────────────────────────────────────────────────┐
    │                      UPDATE SESSION                                  │
    │                                                                      │
    │   ┌─────────────────────────────────────────────────────────────┐    │
    │   │                                                             │    │
    │   │  If first message:                                          │    │
    │   │    Set Title = "Analyze this document"                      │    │
    │   │                                                             │    │
    │   │  Always:                                                    │    │
    │   │    Set UpdatedAt = Current Time                             │    │
    │   │                                                             │    │
    │   └─────────────────────────────────────────────────────────────┘    │
    │                                                                      │
    └──────────────────────────────────────────────────────────────────────┘
```

---

## Service Dependencies

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       SERVICE DEPENDENCY GRAPH                              │
└─────────────────────────────────────────────────────────────────────────────┘


                              ┌─────────────────┐
                              │   ChatEndpoint  │
                              └────────┬────────┘
                                       │
              ┌────────────────────────┼────────────────────────┐
              │                        │                        │
              ▼                        ▼                        ▼
    ┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐
    │ ISessionService │      │IChatMessageSvc  │      │ IAiChatService  │
    └────────┬────────┘      └────────┬────────┘      └────────┬────────┘
             │                        │                        │
             │                        │           ┌────────────┼────────────┐
             │                        │           │            │            │
             ▼                        ▼           ▼            ▼            ▼
    ┌─────────────────────────────────────┐  ┌────────┐  ┌────────┐  ┌────────┐
    │           ChatDbContext             │  │ Vision │  │ Tools  │  │Document│
    │              (SQLite)               │  │ Client │  │ Client │  │ Process│
    │                                     │  │(qwen2.5vl) │  │(llama) │  │ Service│
    └─────────────────────────────────────┘  └────────┘  └───┬────┘  └────────┘
                                                             │
                                             ┌───────────────┼───────────────┐
                                             │               │               │
                                             ▼               ▼               ▼
                                       ┌──────────┐   ┌──────────┐   ┌──────────┐
                                       │   Tool   │   │   MCP    │   │  Ollama  │
                                       │ Registry │   │ Service  │   │Container │
                                       └──────────┘   └────┬─────┘   └──────────┘
                                             │             │
                    ┌────────────────────────┘             │
                    │                                      │
           ┌────────┼────────┬────────────────┐            │
           │        │        │                │            │
           ▼        ▼        ▼                ▼            ▼
      ┌────────┐┌────────┐┌────────┐     ┌────────┐   ┌────────┐
      │Calculat││WebSrch ││FileOps │     │CodeExec│   │  MCP   │
      │  Tool  ││  Tool  ││  Tool  │     │  Tool  │   │ Server │
      └────────┘└────────┘└────────┘     └────────┘   └────────┘
                                                           │
                                              ┌────────────┼────────────┐
                                              │            │            │
                                              ▼            ▼            ▼
                                         ┌────────┐  ┌────────┐  ┌────────┐
                                         │Weather │  │  Time  │  │Convert │
                                         │  Tool  │  │  Tool  │  │  Tool  │
                                         └────────┘  └────────┘  └────────┘
```

---

## Service Connectivity

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    ASPIRE SERVICE CONNECTION PATTERN                         │
└─────────────────────────────────────────────────────────────────────────────┘


    ServiceDefaults/Extensions.cs provides extension methods for service connectivity:

    ┌─────────────────────────────────────────────────────────────────────┐
    │                     Extension Methods                               │
    │                                                                     │
    │   AddOlamaSharpClient(model)     - Ollama AI connection            │
    │   AddMcpServerClient()           - MCP server connection           │
    │   AddA2AClient(name, connection) - A2A agent connections           │
    │                                                                     │
    └─────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
    ┌─────────────────────────────────────────────────────────────────────┐
    │                     SafeConnectionString()                          │
    │                                                                     │
    │   1. Read from builder.Configuration.GetConnectionString()          │
    │   2. Parse "Endpoint=http://..." format from Aspire                 │
    │   3. Fallback to "http://{serviceName}" for local development       │
    │                                                                     │
    └─────────────────────────────────────────────────────────────────────┘


    USAGE EXAMPLES:

    ┌─────────────────────────────────────────────────────────────────────┐
    │  A2A Agents:                                                        │
    │    builder.AddOlamaSharpClient("qwen3");                         │
    │                                                                     │
    │  API Service:                                                       │
    │    builder.AddMcpServerClient();                                    │
    │    builder.AddA2AClient("planner", "planner-agent");                │
    │                                                                     │
    └─────────────────────────────────────────────────────────────────────┘
```

---

## Key Design Principles

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          DESIGN PRINCIPLES                                  │
└─────────────────────────────────────────────────────────────────────────────┘


    ┌─────────────────────────────────────────────────────────────────────┐
    │                                                                     │
    │   1. SINGLE RESPONSIBILITY                                          │
    │                                                                     │
    │   ┌───────────────┐  ┌───────────────┐  ┌───────────────┐          │
    │   │SessionService │  │MessageService │  │  AiService    │          │
    │   │               │  │               │  │               │          │
    │   │ Only session  │  │ Only message  │  │  Only AI      │          │
    │   │    CRUD       │  │    CRUD       │  │ integration   │          │
    │   └───────────────┘  └───────────────┘  └───────────────┘          │
    │                                                                     │
    └─────────────────────────────────────────────────────────────────────┘

    ┌─────────────────────────────────────────────────────────────────────┐
    │                                                                     │
    │   2. DUAL MODEL ARCHITECTURE                                        │
    │                                                                     │
    │   ┌───────────────────────────────────────────────────────────────┐ │
    │   │                                                               │ │
    │   │        Images?                                                │ │
    │   │           │                                                   │ │
    │   │     ┌─────┴─────┐                                             │ │
    │   │     │           │                                             │ │
    │   │   YES          NO                                             │ │
    │   │     │           │                                             │ │
    │   │     ▼           ▼                                             │ │
    │   │   qwen2.5vl     qwen3                                          │ │
    │   │  (vision)   (tools)                                           │ │
    │   │                                                               │ │
    │   └───────────────────────────────────────────────────────────────┘ │
    │                                                                     │
    └─────────────────────────────────────────────────────────────────────┘

    ┌─────────────────────────────────────────────────────────────────────┐
    │                                                                     │
    │   3. EXTERNAL MCP SERVER                                            │
    │                                                                     │
    │   ┌───────────────────────────────────────────────────────────────┐ │
    │   │                                                               │ │
    │   │   API Service ──── HTTP ────▶ MCP Server                      │ │
    │   │   (consumer)                  (provider)                      │ │
    │   │                                                               │ │
    │   │   Benefits:                                                   │ │
    │   │   - Tool isolation                                            │ │
    │   │   - Easy to add new tools                                     │ │
    │   │   - Service discovery via Aspire                              │ │
    │   │                                                               │ │
    │   └───────────────────────────────────────────────────────────────┘ │
    │                                                                     │
    └─────────────────────────────────────────────────────────────────────┘

    ┌─────────────────────────────────────────────────────────────────────┐
    │                                                                     │
    │   4. RESILIENCE PATTERNS                                            │
    │                                                                     │
    │   ┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐  │
    │   │                 │   │                 │   │                 │  │
    │   │  Retry Logic    │   │ Extended        │   │  Graceful       │  │
    │   │  (MCP: 3x)      │   │ Timeouts        │   │  Degradation    │  │
    │   │                 │   │ (10 min)        │   │                 │  │
    │   └─────────────────┘   └─────────────────┘   └─────────────────┘  │
    │                                                                     │
    └─────────────────────────────────────────────────────────────────────┘
```

---

## Technology Stack

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          TECHNOLOGY STACK                                   │
└─────────────────────────────────────────────────────────────────────────────┘


    ┌───────────────────────────────────────────────────────────────────────┐
    │                           PRESENTATION                                │
    │                                                                       │
    │   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐                │
    │   │   Blazor    │   │    HTML     │   │    CSS      │                │
    │   │   Server    │   │     5       │   │  (Dark UI)  │                │
    │   └─────────────┘   └─────────────┘   └─────────────┘                │
    │                                                                       │
    └───────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
    ┌───────────────────────────────────────────────────────────────────────┐
    │                              API                                      │
    │                                                                       │
    │   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐                │
    │   │    Fast     │   │   ASP.NET   │   │   OpenAPI   │                │
    │   │  Endpoints  │   │    Core     │   │   Scalar    │                │
    │   └─────────────┘   └─────────────┘   └─────────────┘                │
    │                                                                       │
    └───────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
    ┌───────────────────────────────────────────────────────────────────────┐
    │                            SERVICES                                   │
    │                                                                       │
    │   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐                │
    │   │ Microsoft   │   │    Model    │   │   Entity    │                │
    │   │ Extensions  │   │   Context   │   │  Framework  │                │
    │   │    .AI      │   │  Protocol   │   │    Core     │                │
    │   └─────────────┘   └─────────────┘   └─────────────┘                │
    │                                                                       │
    └───────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
    ┌───────────────────────────────────────────────────────────────────────┐
    │                         INFRASTRUCTURE                                │
    │                                                                       │
    │  ┌───────┐ ┌───────┐ ┌───────┐ ┌───────┐ ┌───────┐ ┌───────┐ ┌────────┐│
    │  │ .NET  │ │Ollama │ │MongoDB│ │Qdrant │ │ Redis │ │  New  │ │ Let's  ││
    │  │Aspire │ │(Docker│ │(Chat) │ │(RAG)  │ │(Token │ │ Relic │ │Encrypt ││
    │  │       │ │  GPU) │ │       │ │       │ │Cache) │ │(OTLP) │ │(ACME)  ││
    │  └───────┘ └───────┘ └───────┘ └───────┘ └───────┘ └───────┘ └────────┘│
    │                                                                       │
    └───────────────────────────────────────────────────────────────────────┘
```

---

## Dual-Model Architecture

Two LLMs behind one unified persona. The user sees one assistant.

```
User sends message
  │
  ├─ Text only ──────────────────────→ Qwen3 (32B)
  │                                      │
  │                                      ├─ General chat → direct response
  │                                      ├─ Document question → search_knowledge_base tool → Qdrant
  │                                      └─ Image follow-up → analyze_image tool ─┐
  │                                                                                 │
  ├─ Image upload ──→ Qwen3 sees      ─→ analyze_image tool ───────────────────────┤
  │                   "[User uploaded                                                │
  │                    image.png]"                                                   ▼
  │                                                                          Qwen2.5-VL (32B)
  └─ Document upload ──→ Text extracted inline → Qwen3 (no RAG search)       (vision model)
     via chat              [DO NOT use search_knowledge_base]
```

**Model names centralized** in `AspireOllama.Shared/OllamaModels.cs` — one file change switches all models.

---

## Coordinator Agent

The Coordinator Agent orchestrates complex multi-agent workflows via the A2A protocol.

```
User: "Plan and review a security feature in C#"
  │
  ▼
API Service ──→ Coordinator Agent (single A2A call)
                  │
                  ├─ Phase 1: Assess complexity ───→ Planner
                  ├─ Phase 2: Create plan ──────────→ Planner
                  ├─ Phase 3: Execute subtasks ─────→ Research ──┐
                  │                                   Code ──────┤ (parallel)
                  ├─ Phase 4: Review + conflicts ───→ Reviewer   │
                  │   ├─ Conflicts found? ──→ Re-run with feedback
                  │   └─ No conflicts ──→ continue
                  └─ Phase 5: Aggregate results ────→ Local LLM (Qwen3)
                                                      │
                                                      ▼
                                               Coherent response
```

**Error handling**: 2 retries per subtask, exponential backoff, 5-minute timeout.

---

## Infrastructure: MongoDB & Qdrant

MongoDB stores chat persistence, Qdrant stores document vectors for RAG.

```
┌───────────────────────────────────────────────────────────────────┐
│                        Data Layer                                  │
│                                                                    │
│  ┌─────────────────────┐          ┌─────────────────────────┐    │
│  │      MongoDB        │          │        Qdrant            │    │
│  │                     │          │                          │    │
│  │  chat_sessions      │          │  document_chunks         │    │
│  │  • Id, Title        │          │  (dot product distance)  │    │
│  │  • CreatedAt        │          │                          │    │
│  │  • UpdatedAt        │          │  • vector (float[])      │    │
│  │                     │          │  • file_name             │    │
│  │  chat_messages      │          │  • chunk_index           │    │
│  │  • SessionId        │          │  • text                  │    │
│  │  • Role, Content    │          │                          │    │
│  │  • Images, Files    │          │  Global — not scoped     │    │
│  │  • Timestamp        │          │  to sessions             │    │
│  └─────────────────────┘          └─────────────────────────┘    │
│                                                                    │
└───────────────────────────────────────────────────────────────────┘
```

---

## Infrastructure: RAG Pipeline

```
┌─────────────────────────────────────────────────────────────────────┐
│  Document Ingestion (Admin: /documents page)                        │
│                                                                     │
│  File (PDF/Word/Excel/PPT/Text, up to 100MB)                      │
│    │                                                                │
│    ▼                                                                │
│  DocumentProcessingService.ExtractText()                            │
│    │                                                                │
│    ▼                                                                │
│  TextChunkingService.ChunkText()                                    │
│  (512 chars, 64 overlap, sentence-boundary splitting)               │
│    │                                                                │
│    ▼                                                                │
│  OllamaEmbeddingService.GetEmbeddingsAsync()                       │
│  (nomic-embed-text model)                                           │
│    │                                                                │
│    ▼                                                                │
│  Qdrant.UpsertAsync() — stored with dot product distance            │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│  Chat Query (Any user, any session)                                 │
│                                                                     │
│  User question                                                      │
│    │                                                                │
│    ▼                                                                │
│  OllamaEmbeddingService.GetEmbeddingAsync()                        │
│    │                                                                │
│    ▼                                                                │
│  Qdrant.SearchAsync() — dot product, top 5, score > 0.3            │
│  (searches ALL documents globally)                                  │
│    │                                                                │
│    ▼                                                                │
│  [RELEVANT CONTEXT from uploaded documents]                         │
│  [From report.pdf, section 3]: ...chunk text...                     │
│  [END CONTEXT]                                                      │
│                                                                     │
│  User's question here                                               │
│    │                                                                │
│    ▼                                                                │
│  LLM (Qwen3 32B) responds using retrieved context                   │
└─────────────────────────────────────────────────────────────────────┘
```

**Authorization:**
- Upload: `Api.Admin` or `Api.Documents.Manage` (access token)
- Search: All authenticated users (automatic during chat)
- UI: `UserRoleService` reads roles from `GET /api/me` (access token), cached per circuit

---

## Infrastructure: Redis Token Cache

Redis is deployed by Aspire as a distributed cache for MSAL token storage.

```
┌──────────────────┐     ┌──────────────────┐     ┌──────────────────┐
│   Web Frontend   │     │      Redis       │     │   Azure AD       │
│   (Blazor)       │     │  (Token Cache)   │     │   (Entra ID)     │
└────────┬─────────┘     └────────┬─────────┘     └────────┬─────────┘
         │                        │                        │
         │ 1. User signs in       │                        │
         │        (OIDC + PKCE)   │                        │
         │────────────────────────────────────────────────▶│
         │                        │                        │
         │ 2. Tokens received     │                        │
         │◀────────────────────────────────────────────────│
         │                        │                        │
         │ 3. Cache tokens        │                        │
         │  (AddDistributed       │                        │
         │   TokenCaches)         │                        │
         │───────────────────────▶│                        │
         │                        │                        │
         │ 4. Later: OBO call     │                        │
         │  (check cache first)   │                        │
         │───────────────────────▶│                        │
         │  Cache hit → use token │                        │
         │◀───────────────────────│                        │
         │                        │                        │
```

**Key points:**
- Replaces `AddInMemoryTokenCaches()` with `AddDistributedTokenCaches()`
- Tokens survive app restarts and scale across instances
- `ChatApiClient.EnsureAuthenticatedAsync()` checks auth state before MSAL calls, preventing `user_null` exceptions on Blazor circuit reconnect

---

## Infrastructure: Observability (New Relic)

All services export OpenTelemetry data to New Relic via OTLP.

```
┌────────────────────────────────────────────────────────────────────┐
│                        AppHost Configuration                       │
│                                                                    │
│  NewRelic:LicenseKey ─────┐                                       │
│  NewRelic:OtlpEndpoint ───┤  Sets env vars on all services:       │
│  Otel:ServiceNamespace ───┤  OTEL_SERVICE_NAME                    │
│  Otel:ServiceVersion ─────┤  OTEL_Service_Namespace               │
│  Otel:DeploymentEnv ──────┘  OTEL_Service_Version                 │
│                              OTEL_Deployment_Environment           │
│                              OTEL_EXPORTER_OTLP_ENDPOINT          │
│                              OTEL_EXPORTER_OTLP_HEADERS           │
│                              OTEL_EXPORTER_OTLP_PROTOCOL          │
└────────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│ ApiService  │  │ Web Frontend│  │ McpServer   │  │ A2A Agents  │
│             │  │             │  │             │  │ (4 agents)  │
└──────┬──────┘  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘
       │                │                │                │
       └────────────────┴────────────────┴────────────────┘
                                │
                    ┌───────────▼───────────┐
                    │   ServiceDefaults     │
                    │   ConfigureResource:  │
                    │   • service.name      │
                    │   • service.namespace │
                    │   • service.version   │
                    │   • deployment.env    │
                    │                       │
                    │   UseOtlpExporter()   │
                    └───────────┬───────────┘
                                │ OTLP (http/protobuf)
                                ▼
                    ┌───────────────────────┐
                    │      New Relic        │
                    │  otlp.nr-data.net     │
                    │                       │
                    │  Traces │ Metrics │   │
                    │         │ Logs    │   │
                    └───────────────────────┘
```

---

## Infrastructure: YARP Gateway & Let's Encrypt

The YARP Gateway (`AspireOllama.Gateway`) is a standalone project that serves as the single entry point for all traffic. It uses LettuceEncrypt for automatic TLS certificate provisioning via Let's Encrypt ACME protocol.

```
                         ┌───────────────────────┐
                         │    Let's Encrypt       │
                         │   (ACME CA Server)     │
                         └───────────┬───────────┘
                                     │
                          ACME HTTP-01 Challenge
                          (auto-provision + renew)
                                     │
┌────────────────────────────────────┼────────────────────────────────┐
│                           YARP Gateway                              │
│                    (AspireOllama.Gateway)                            │
│                                    │                                │
│  ┌──────────────┐   ┌─────────────▼──────────────┐                 │
│  │  Kestrel     │   │     LettuceEncrypt         │                 │
│  │  :8080 HTTP  │   │  • Auto TLS provisioning   │                 │
│  │  :8443 HTTPS │◀──│  • Certificate renewal     │                 │
│  │              │   │  • ACME challenge handler   │                 │
│  └──────┬───────┘   └────────────────────────────┘                 │
│         │                                                           │
│  ┌──────▼───────────────────────────────────────────────────────┐  │
│  │                    YARP Reverse Proxy                         │  │
│  │  (config-based routes with Aspire service discovery)         │  │
│  │                                                              │  │
│  │  /api/*            → apiservice                              │  │
│  │  /mcp/*            → mcpserver                               │  │
│  │  /a2a/planner/*    → planner-agent                           │  │
│  │  /a2a/reviewer/*   → reviewer-agent                          │  │
│  │  /a2a/research/*   → research-agent                          │  │
│  │  /a2a/code/*       → code-agent                              │  │
│  │  /scalar/*         → scalar                                  │  │
│  │  /* (catch-all)    → webfrontend                             │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                    OTEL Proxy Logging                        │  │
│  │  • Structured logs per request (route, cluster, status, ms) │  │
│  │  • Activity tags: gateway.route, gateway.cluster,           │  │
│  │    gateway.upstream.status_code, gateway.upstream.duration   │  │
│  └──────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
         │
         │  https+http:// (Aspire service discovery)
         ▼
┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
│ Web Frontend│ │ API Service │ │ MCP Server  │ │ A2A Agents  │
│ (Blazor)    │ │ (Chat API)  │ │ (Tools)     │ │ (4 agents)  │
└─────────────┘ └─────────────┘ └─────────────┘ └─────────────┘
```

**Let's Encrypt behavior:**
- **Domain configured** (`LettuceEncrypt:DomainNames` non-empty): LettuceEncrypt provisions TLS cert via ACME HTTP-01 challenge on port 8080, serves HTTPS on 8443, auto-renews before expiry
- **Domain empty** (Aspire dev): LettuceEncrypt and Kestrel port config are skipped — Aspire controls ports
- Certificates persisted in a Docker volume (`letsencrypt-certs`) to survive container restarts

---

## Infrastructure: Kubernetes Deployment

In Kubernetes, the YARP Gateway runs as a deployment with a LoadBalancer or Ingress in front.

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Kubernetes Cluster                           │
│                        Namespace: aspireollama                      │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                YARP Gateway (+ Let's Encrypt)                │   │
│  │  /api/*  → apiservice      /a2a/planner/*  → planner-agent  │   │
│  │  /mcp/*  → mcpserver       /a2a/reviewer/* → reviewer-agent │   │
│  │  /scalar/* → scalar        /a2a/research/* → research-agent │   │
│  │  /*      → webfrontend     /a2a/code/*     → code-agent     │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                │                                    │
│       ┌──────────┬─────────────┼─────────────┬──────────┐          │
│       ▼          ▼             ▼             ▼          ▼          │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐    │
│  │   Web   │ │   API   │ │   MCP   │ │  A2A x4 │ │  Ollama │    │
│  │Frontend │ │ Service │ │ Server  │ │ Agents  │ │  (GPU)  │    │
│  └────┬────┘ └─────────┘ └─────────┘ └─────────┘ └─────────┘    │
│       │                                                            │
│       ▼                                                            │
│  ┌─────────┐     ConfigMaps:          Secrets:                     │
│  │  Redis  │     • otel-config        • azure-ad-web               │
│  │  (PVC)  │     • service-discovery  • azure-ad-backend           │
│  └─────────┘     • downstream-apis    • newrelic                   │
│                  • azure-ad-common                                  │
└─────────────────────────────────────────────────────────────────────┘
```

**Docker build** uses a single multi-stage `Dockerfile`:
```bash
docker build --build-arg PROJECT=AspireOllama.Web -t aspireollama-web .
```

**Deploy** with Kustomize:
```bash
kubectl apply -k k8s/base/
```

---

## User Sessions & Scoping

Chat sessions are scoped by `userId` in MongoDB. Each user sees only their own sessions and messages.

```
User authenticates via Azure AD
  │
  ▼
GET /api/me → returns { name, email, roles } from access token
  │
  ▼
Sessions filtered by userId in MongoDB queries
  │
  ├─ POST /api/sessions        → creates session with userId
  ├─ GET  /api/sessions        → returns only user's sessions
  ├─ GET  /api/sessions/{id}   → validates userId ownership
  └─ DELETE /api/sessions/{id} → validates userId ownership
```

---

## Timeout Configuration

Consistent timeouts are configured across all services to handle long-running LLM operations.

```
┌────────────────────────────────┬──────────────────────┐
│  Connection                    │  Timeout             │
├────────────────────────────────┼──────────────────────┤
│  Ollama HTTP client            │  10 minutes          │
│  MCP client                    │  10 minutes          │
│  A2A agent client              │  10 minutes          │
│  Gateway → Coordinator/API     │  15 minutes          │
│  Gateway → other services      │  10 minutes          │
└────────────────────────────────┴──────────────────────┘
```

The 15-minute gateway timeout for Coordinator and API Service routes allows multi-agent workflows to complete without premature termination.

---

## Heartbeat Logging

Aspire health-check heartbeat logging is suppressed in `ServiceDefaults` to reduce log noise. The `Microsoft.Extensions.Diagnostics.HealthChecks` logger category is filtered to `Warning` level, so only non-healthy heartbeat results appear in logs.

---

## Chat UI

The Chat page (`Chat.razor`) is the primary user-facing interface.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           CHAT UI LAYOUT                                     │
│                                                                              │
│  ┌──────────────┐  ┌──────────────────────────────────────────────────────┐  │
│  │  Session      │  │  Chat Header (AI Assistant title)                   │  │
│  │  Sidebar      │  ├──────────────────────────────────────────────────────┤  │
│  │               │  │                                                      │  │
│  │  [+ New Chat] │  │  Messages Area (auto-scroll to bottom)              │  │
│  │               │  │                                                      │  │
│  │  Session 1    │  │  ┌─────────────────────────────────────────────┐    │  │
│  │  Session 2    │  │  │ 👤 User: plain text, blue card, right-align│    │  │
│  │  Session 3    │  │  └─────────────────────────────────────────────┘    │  │
│  │  ...          │  │                                                      │  │
│  │               │  │  ┌─────────────────────────────────────────────────┐│  │
│  │  [Agents]     │  │  │ 🤖 Agent: markdown via Markdig (90% width)     ││  │
│  │  [Documents]  │  │  │  - Headings, code blocks, tables, lists        ││  │
│  │               │  │  │  - Blockquotes, links, inline code             ││  │
│  └──────────────┘  │  └─────────────────────────────────────────────────┘│  │
│                     │                                                      │  │
│                     ├──────────────────────────────────────────────────────┤  │
│                     │  Input Area (text + file/image attachments)          │  │
│                     └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘

    Message Rendering:
    ┌──────────────────────────────────────────────┐
    │  User messages  → plain text, card effect     │
    │  Agent messages → Markdig HTML rendering      │
    │                   (static MarkdownPipeline)   │
    │  Auto-scroll    → JS interop after each       │
    │                   send, receive, session load  │
    └──────────────────────────────────────────────┘
```

## Workflow UI

The Agents page (`Agents.razor`) provides a visual multi-agent workflow experience.

```
┌─────────────────────────────────────────────────────────┐
│                    Workflow UI Layout                     │
│                                                          │
│  ┌────────────────────────────────────────────────────┐  │
│  │  Hub Diagram                                       │  │
│  │  Coordinator plan → [Agents box] → Coordinator     │  │
│  │                      aggregate                     │  │
│  └────────────────────────────────────────────────────┘  │
│                                                          │
│  ┌────────────────────────────────────────────────────┐  │
│  │  Preset Buttons                                    │  │
│  │  [Plan & Review]  [Research & Code]  [Full Flow]   │  │
│  └────────────────────────────────────────────────────┘  │
│                                                          │
│  ┌────────────────────────────────────────────────────┐  │
│  │  Expandable Blocks (per phase)                     │  │
│  │  ▶ Assess   ▶ Plan   ▶ Execute   ▶ Review         │  │
│  │  ▶ Aggregate                                       │  │
│  └────────────────────────────────────────────────────┘  │
│                                                          │
│  ┌────────────────────────────────────────────────────┐  │
│  │  Call Summary (expandable)                         │  │
│  │  Final Result (expandable)                         │  │
│  └────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

Agent skill counts are displayed per agent in the UI (Planner: 3, Research: 4, Code: 5, Reviewer: 4).
