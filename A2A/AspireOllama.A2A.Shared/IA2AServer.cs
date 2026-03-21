namespace AspireOllama.A2A.Shared;

/// <summary>
/// Complete A2A protocol server contract per the Agent-to-Agent (A2A) specification.
/// Defines all JSON-RPC methods that an A2A-compliant server must expose.
/// See: https://a2a-protocol.org/latest/specification/
/// </summary>
public interface IA2AServer
{
    // ── Discovery ──

    /// <summary>
    /// Returns the agent card describing this agent's identity, capabilities, and skills.
    /// The agent card is served at <c>GET /.well-known/agent.json</c> and is always unauthenticated
    /// to allow client discovery before establishing trust.
    /// </summary>
    /// <returns>An <see cref="AgentCard"/> containing the agent's metadata, skills, and supported capabilities.</returns>
    AgentCard GetAgentCard();

    /// <summary>
    /// Returns an extended agent card with additional details available only after authentication.
    /// May include tenant-specific capabilities, rate limits, or internal endpoints.
    /// <para>JSON-RPC method: <c>agent/card</c> (Section 3.1.11)</para>
    /// </summary>
    /// <param name="tenantId">Optional tenant identifier for multi-tenant agents.</param>
    /// <returns>An <see cref="AgentCard"/> with extended metadata.</returns>
    AgentCard GetExtendedAgentCard(string? tenantId = null);

    // ── Messaging ──

    /// <summary>
    /// Processes an incoming A2A message and produces a task representing the work.
    /// This is the core agent logic — each concrete agent implements its own behavior here.
    /// Called internally by <see cref="HandleSendMessageAsync"/> after request unwrapping.
    /// </summary>
    /// <param name="message">The A2A message containing the user's request and any attachments.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>An <see cref="A2ATask"/> tracking the processing state, artifacts, and history.</returns>
    Task<A2ATask> ProcessMessageAsync(A2AMessage message, CancellationToken ct);

    /// <summary>
    /// Receives a message from a client and returns a task that tracks the processing.
    /// The client sends a message with optional configuration (output modes, blocking behavior).
    /// The server creates a task, processes the message, and returns the task with its current state.
    /// <para>JSON-RPC method: <c>message/send</c> (Section 3.1.1)</para>
    /// </summary>
    /// <param name="request">The send message request containing the message and optional configuration.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>A <see cref="SendMessageResponse"/> containing the task or a direct response message.</returns>
    Task<SendMessageResponse> HandleSendMessageAsync(SendMessageRequest request, CancellationToken ct);

    /// <summary>
    /// Receives a message and streams real-time updates as the agent processes it.
    /// Returns an initial task/message followed by a stream of status and artifact update events.
    /// Enables clients to show progressive results (e.g., tokens being generated, steps completing).
    /// <para>JSON-RPC method: <c>message/stream</c> (Section 3.1.2)</para>
    /// </summary>
    /// <param name="request">The send message request containing the message and optional configuration.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>An async stream of <see cref="SendMessageResponse"/> events.</returns>
    IAsyncEnumerable<SendMessageResponse> HandleSendMessageStreamAsync(SendMessageRequest request, CancellationToken ct);

    // ── Task Management ──

    /// <summary>
    /// Retrieves the current state of a task including its status, artifacts, and optionally history.
    /// Clients use this to poll for task completion or check results after disconnection.
    /// <para>JSON-RPC method: <c>tasks/get</c> (Section 3.1.3)</para>
    /// </summary>
    /// <param name="taskId">The unique identifier of the task to retrieve.</param>
    /// <returns>The <see cref="A2ATask"/> if found, or <c>null</c> if the task does not exist.</returns>
    A2ATask? GetTask(string taskId);

    /// <summary>
    /// Retrieves a filtered and paginated list of tasks.
    /// Can be filtered by context ID or status to find related tasks.
    /// <para>JSON-RPC method: <c>tasks/list</c> (Section 3.1.4)</para>
    /// </summary>
    /// <returns>An enumerable of <see cref="A2ATask"/> objects.</returns>
    IEnumerable<A2ATask> GetAllTasks();

    /// <summary>
    /// Requests cancellation of an ongoing task.
    /// The agent should stop processing and transition the task to the Canceled state.
    /// Cancellation is best-effort — the agent may have already completed the task.
    /// <para>JSON-RPC method: <c>tasks/cancel</c> (Section 3.1.5)</para>
    /// </summary>
    /// <param name="taskId">The unique identifier of the task to cancel.</param>
    /// <returns><c>true</c> if the task was found and cancellation was initiated; <c>false</c> if the task was not found.</returns>
    bool CancelTask(string taskId);

    /// <summary>
    /// Establishes a streaming subscription for real-time updates on an existing task.
    /// Returns a stream of task status changes (Working, Completed, Failed, etc.).
    /// Used for long-running tasks where the client wants to monitor progress without polling.
    /// <para>JSON-RPC method: <c>tasks/subscribe</c> (Section 3.1.6)</para>
    /// </summary>
    /// <param name="taskId">The unique identifier of the task to subscribe to.</param>
    /// <param name="ct">Cancellation token to end the subscription.</param>
    /// <returns>An async stream of <see cref="A2ATaskStatus"/> updates.</returns>
    IAsyncEnumerable<A2ATaskStatus> SubscribeToTaskAsync(string taskId, CancellationToken ct);

    // ── Push Notifications ──

    /// <summary>
    /// Creates a webhook configuration for receiving asynchronous task update notifications.
    /// The agent will POST status and artifact updates to the specified URL as the task progresses.
    /// Enables fire-and-forget messaging where the client doesn't maintain an open connection.
    /// <para>JSON-RPC method: <c>tasks/pushNotification/create</c> (Section 3.1.7)</para>
    /// </summary>
    /// <param name="taskId">The task to receive notifications for.</param>
    /// <param name="webhookUrl">The HTTPS URL where notifications should be delivered.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>A <see cref="PushNotificationConfig"/> with the assigned configuration ID.</returns>
    Task<PushNotificationConfig> CreatePushNotificationAsync(string taskId, string webhookUrl, CancellationToken ct);

    /// <summary>
    /// Retrieves the details of a specific push notification configuration.
    /// <para>JSON-RPC method: <c>tasks/pushNotification/get</c> (Section 3.1.8)</para>
    /// </summary>
    /// <param name="taskId">The task the configuration belongs to.</param>
    /// <param name="configId">The unique identifier of the push notification configuration.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The <see cref="PushNotificationConfig"/> if found, or <c>null</c> if it does not exist.</returns>
    Task<PushNotificationConfig?> GetPushNotificationAsync(string taskId, string configId, CancellationToken ct);

    /// <summary>
    /// Retrieves all push notification configurations for a task.
    /// <para>JSON-RPC method: <c>tasks/pushNotification/list</c> (Section 3.1.9)</para>
    /// </summary>
    /// <param name="taskId">The task to list configurations for.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>An enumerable of <see cref="PushNotificationConfig"/> objects.</returns>
    Task<IEnumerable<PushNotificationConfig>> ListPushNotificationsAsync(string taskId, CancellationToken ct);

    /// <summary>
    /// Permanently removes a push notification configuration.
    /// The agent will stop sending notifications to the configured webhook URL.
    /// <para>JSON-RPC method: <c>tasks/pushNotification/delete</c> (Section 3.1.10)</para>
    /// </summary>
    /// <param name="taskId">The task the configuration belongs to.</param>
    /// <param name="configId">The unique identifier of the configuration to delete.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns><c>true</c> if the configuration was found and deleted; <c>false</c> if it did not exist.</returns>
    Task<bool> DeletePushNotificationAsync(string taskId, string configId, CancellationToken ct);
}
