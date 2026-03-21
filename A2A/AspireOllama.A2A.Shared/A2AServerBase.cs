using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AspireOllama.A2A.Shared;

/// <summary>
/// Base class for A2A protocol server implementation.
/// Provides task management and common A2A functionality.
/// </summary>
public abstract class A2AServerBase : IA2AServer, ISkillAuthorizationProvider
{
    protected readonly ConcurrentDictionary<string, A2ATask> _tasks = new();
    protected readonly ILogger _logger;
    protected readonly IA2AAgentClient? _a2aClient;

    protected A2AServerBase(ILogger logger, IA2AAgentClient? a2aClient = null)
    {
        _logger = logger;
        _a2aClient = a2aClient;
    }

    /// <summary>
    /// Returns the Agent Card describing this agent's capabilities.
    /// </summary>
    public abstract AgentCard GetAgentCard();

    /// <summary>
    /// Process an incoming message and return a task.
    /// </summary>
    public abstract Task<A2ATask> ProcessMessageAsync(A2AMessage message, CancellationToken ct);

    /// <summary>
    /// Resolves which skill handles a message. Override in each agent to inspect message text.
    /// Returns null if no specific skill — access role check still applies.
    /// </summary>
    public virtual string? ResolveSkill(A2AMessage message) => null;

    /// <summary>
    /// Returns skill-to-role mapping. Override to provide per-skill authorization.
    /// </summary>
    public virtual IReadOnlyDictionary<string, string> GetSkillRoles()
        => new Dictionary<string, string>();

    /// <summary>
    /// Handle a SendMessage request.
    /// </summary>
    public async Task<SendMessageResponse> HandleSendMessageAsync(SendMessageRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Received A2A message: {MessageId}", request.Message.MessageId);

        var task = await ProcessMessageAsync(request.Message, ct);
        _tasks[task.Id] = task;

        return new SendMessageResponse
        {
            Task = task
        };
    }

    /// <summary>
    /// Get a task by ID.
    /// </summary>
    public A2ATask? GetTask(string taskId)
    {
        return _tasks.GetValueOrDefault(taskId);
    }

    /// <summary>
    /// Get all tasks.
    /// </summary>
    public IEnumerable<A2ATask> GetAllTasks()
    {
        return _tasks.Values;
    }

    /// <summary>
    /// Cancel a task.
    /// </summary>
    public bool CancelTask(string taskId)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            if (task.Status.State == TaskState.Working || task.Status.State == TaskState.Submitted)
            {
                task.Status.State = TaskState.Canceled;
                task.Status.Message = "Task canceled by request";
                task.UpdatedAt = DateTime.UtcNow;
                _logger.LogInformation("Canceled task: {TaskId}", taskId);
                return true;
            }
        }
        return false;
    }

    // ── A2A Protocol: Not yet supported — override in concrete agents when ready ──

    /// <summary>message/stream (3.1.2) — override to support streaming</summary>
    public virtual IAsyncEnumerable<SendMessageResponse> HandleSendMessageStreamAsync(SendMessageRequest request, CancellationToken ct)
        => throw new NotSupportedException("Streaming is not supported by this agent.");

    /// <summary>tasks/subscribe (3.1.6) — override to support task subscriptions</summary>
    public virtual IAsyncEnumerable<A2ATaskStatus> SubscribeToTaskAsync(string taskId, CancellationToken ct)
        => throw new NotSupportedException("Task subscription is not supported by this agent.");

    /// <summary>tasks/pushNotification/create (3.1.7)</summary>
    public virtual Task<PushNotificationConfig> CreatePushNotificationAsync(string taskId, string webhookUrl, CancellationToken ct)
        => throw new NotSupportedException("Push notifications are not supported by this agent.");

    /// <summary>tasks/pushNotification/get (3.1.8)</summary>
    public virtual Task<PushNotificationConfig?> GetPushNotificationAsync(string taskId, string configId, CancellationToken ct)
        => throw new NotSupportedException("Push notifications are not supported by this agent.");

    /// <summary>tasks/pushNotification/list (3.1.9)</summary>
    public virtual Task<IEnumerable<PushNotificationConfig>> ListPushNotificationsAsync(string taskId, CancellationToken ct)
        => throw new NotSupportedException("Push notifications are not supported by this agent.");

    /// <summary>tasks/pushNotification/delete (3.1.10)</summary>
    public virtual Task<bool> DeletePushNotificationAsync(string taskId, string configId, CancellationToken ct)
        => throw new NotSupportedException("Push notifications are not supported by this agent.");

    /// <summary>agent/card (3.1.11) — override to return tenant-specific card</summary>
    public virtual AgentCard GetExtendedAgentCard(string? tenantId = null)
        => GetAgentCard();

    // ── Helpers ──

    /// <summary>
    /// Create a new task for a message.
    /// </summary>
    protected A2ATask CreateTask(A2AMessage message)
    {
        var task = new A2ATask
        {
            ContextId = message.ContextId ?? Guid.NewGuid().ToString(),
            Status = new A2ATaskStatus { State = TaskState.Submitted },
            History = [message]
        };

        _tasks[task.Id] = task;
        _logger.LogInformation("Created task: {TaskId} for message: {MessageId}", task.Id, message.MessageId);

        return task;
    }

    /// <summary>
    /// Update task status.
    /// </summary>
    protected void UpdateTaskStatus(A2ATask task, TaskState state, string? message = null, int? progress = null)
    {
        task.Status.State = state;
        task.Status.Message = message;
        task.Status.Progress = progress;
        task.UpdatedAt = DateTime.UtcNow;

        if (state == TaskState.Completed || state == TaskState.Failed || state == TaskState.Canceled)
        {
            task.CompletedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Add an artifact to a task.
    /// </summary>
    protected void AddArtifact(A2ATask task, object data, string? name = null, string mediaType = "application/json")
    {
        task.Artifacts.Add(new A2AArtifact
        {
            Name = name,
            Parts = [A2APart.FromData(data, mediaType)]
        });
    }

    /// <summary>
    /// Add a text artifact to a task.
    /// </summary>
    protected void AddTextArtifact(A2ATask task, string text, string? name = null)
    {
        task.Artifacts.Add(new A2AArtifact
        {
            Name = name,
            Parts = [A2APart.FromText(text)]
        });
    }

    /// <summary>
    /// Add a response message to task history.
    /// </summary>
    protected void AddResponseToHistory(A2ATask task, string text)
    {
        task.History.Add(new A2AMessage
        {
            Role = MessageRole.Agent,
            TaskId = task.Id,
            ContextId = task.ContextId,
            Parts = [A2APart.FromText(text)]
        });
    }

    /// <summary>
    /// Call another agent via A2A protocol.
    /// </summary>
    protected async Task<SendMessageResponse?> CallAgentAsync(string agentName, string message, CancellationToken ct)
    {
        if (_a2aClient == null)
        {
            _logger.LogWarning("A2A client not available, cannot call agent: {Agent}", agentName);
            return null;
        }

        _logger.LogInformation("Calling agent via A2A: {Agent}", agentName);
        return await _a2aClient.SendTextAsync(agentName, message, ct);
    }

    /// <summary>
    /// Get text content from a message.
    /// </summary>
    protected static string GetTextFromMessage(A2AMessage message)
    {
        return message.Parts
            .Where(p => p.Text is not null)
            .Select(p => p.Text!)
            .FirstOrDefault() ?? "";
    }

    /// <summary>
    /// Get text content from an artifact.
    /// </summary>
    protected static string? GetTextFromArtifact(A2AArtifact artifact)
    {
        return artifact.Parts
            .Where(p => p.Text is not null)
            .Select(p => p.Text!)
            .FirstOrDefault();
    }

    /// <summary>
    /// Get data from task artifacts.
    /// </summary>
    protected static object? GetDataFromTask(A2ATask? task)
    {
        return task?.Artifacts
            .SelectMany(a => a.Parts)
            .Where(p => p.Data is not null)
            .Select(p => p.Data)
            .FirstOrDefault();
    }
}
