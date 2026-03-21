namespace AspireOllama.A2A.Shared;

/// <summary>
/// Provides per-skill authorization for A2A agents.
/// Agents implement this to map incoming messages to skills and define which roles are required.
/// Used by <see cref="A2AHostExtensions.MapA2AEndpoints{TServer}"/> to enforce fine-grained access control.
/// </summary>
public interface ISkillAuthorizationProvider
{
    /// <summary>
    /// Resolves which skill will handle the given message by inspecting its content.
    /// Called before message processing to determine the required role.
    /// </summary>
    /// <param name="message">The incoming A2A message.</param>
    /// <returns>The skill ID (e.g. "create_plan", "review_code") or <c>null</c> if no specific skill matches.</returns>
    string? ResolveSkill(A2AMessage message);

    /// <summary>
    /// Returns the skill-to-role mapping for this agent.
    /// Keys are skill IDs, values are the required role names.
    /// </summary>
    /// <returns>A dictionary mapping skill IDs to required role names, or empty if no per-skill auth.</returns>
    IReadOnlyDictionary<string, string> GetSkillRoles();
}
