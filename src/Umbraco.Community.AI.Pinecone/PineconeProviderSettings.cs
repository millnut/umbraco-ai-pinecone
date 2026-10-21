using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Umbraco.AI.Core.EditableModels;
using Umbraco.AI.Core.Serialization;

namespace Umbraco.Community.AI.Pinecone;

/// <summary>
/// Connection settings for the Pinecone provider, edited in the backoffice under the connection.
/// </summary>
/// <remarks>
/// Labels are set here rather than through localization keys, so the package ships no backoffice client.
/// </remarks>
public class PineconeProviderSettings
{
    /// <summary>
    /// The Pinecone API key. Being sensitive, it is encrypted at rest and may instead hold a reference to
    /// configuration, such as <c>$Umbraco:AI:Secrets:PineconeApiKey</c>.
    /// </summary>
    [AIField(
        Label = "API key",
        Description = "Your Pinecone API key. To keep it out of the database, enter a configuration reference such as $Umbraco:AI:Secrets:PineconeApiKey and put the key in appsettings or an environment variable.",
        IsSensitive = true,
        SortOrder = 1)]
    [Required]
    public string? ApiKey { get; set; }

    /// <summary>
    /// How Pinecone handles input longer than the model's limit. Empty uses Pinecone's default, END.
    /// </summary>
    [AIField(
        Label = "Truncate",
        Description = "What Pinecone does with text longer than the model accepts. END (the default) cuts the end off, START cuts the start off (llama-text-embed-v2 only), and NONE rejects the request.",
        EditorUiAlias = "Umb.PropertyEditorUi.Dropdown",
        EditorConfig = "[{\"alias\":\"multiple\",\"value\":false},{\"alias\":\"items\",\"value\":[\"END\",\"START\",\"NONE\"]}]",
        SortOrder = 2)]
    [JsonConverter(typeof(DropdownStringJsonConverter))]
    public string? Truncate { get; set; }
}
