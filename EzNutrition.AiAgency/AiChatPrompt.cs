namespace EzNutrition.AiAgency;

/// <summary>
/// Represents the two role-separated messages supplied to a chat model.
/// </summary>
public sealed record AiChatPrompt(string SystemMessage, string UserMessage);
