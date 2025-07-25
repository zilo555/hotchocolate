using System.Text.Json;
using HotChocolate.Buffers;
using static HotChocolate.Transport.Properties.TransportAbstractionResources;
using static HotChocolate.Transport.Serialization.Utf8GraphQLResultProperties;

namespace HotChocolate.Transport;

/// <summary>
/// Represents the result of a GraphQL operation.
/// </summary>
public sealed class OperationResult : IDisposable
{
    private readonly IDisposable? _memoryOwner;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationResult"/> class with the
    /// specified JSON document and optional data, errors, and extensions.
    /// </summary>
    /// <param name="memoryOwner">
    /// The memory owner of the JSON elements.
    /// </param>
    /// <param name="data">
    /// A <see cref="JsonElement"/> object representing the data returned by the operation.
    /// </param>
    /// <param name="errors">
    /// A <see cref="JsonElement"/> object representing any errors that occurred during
    /// the operation.
    /// </param>
    /// <param name="extensions">
    /// A <see cref="JsonElement"/> object representing any extensions returned by the
    /// operation.
    /// </param>
    /// <param name="requestIndex">
    /// The request index of this result. This is only set if the result is part of a batched operation.
    /// </param>
    /// <param name="variableIndex">
    /// The variable index of this result. This is only set if the result is part of a variable batch operation.
    /// </param>
    public OperationResult(
        IDisposable? memoryOwner = null,
        JsonElement data = default,
        JsonElement errors = default,
        JsonElement extensions = default,
        int? requestIndex = null,
        int? variableIndex = null)
    {
        _memoryOwner = memoryOwner;
        Data = data;
        Errors = errors;
        Extensions = extensions;
        RequestIndex = requestIndex;
        VariableIndex = variableIndex;
    }

    /// <summary>
    /// Gets the request index of this result. This is only set if the result is part of a batched operation.
    /// </summary>
    public int? RequestIndex { get; }

    /// <summary>
    /// Gets the variable index of this result. This is only set if the result is part of a variable batch operation.
    /// </summary>
    public int? VariableIndex { get; }

    /// <summary>
    /// Gets the <see cref="JsonElement"/> object representing the data returned by
    /// the operation.
    /// </summary>
    public JsonElement Data { get; }

    /// <summary>
    /// Gets the <see cref="JsonElement"/> object representing any errors that occurred
    /// during the operation.
    /// </summary>
    public JsonElement Errors { get; }

    /// <summary>
    /// Gets the <see cref="JsonElement"/> object representing any extensions returned
    /// by the operation.
    /// </summary>
    public JsonElement Extensions { get; }

    /// <summary>
    /// Releases all resources used by the <see cref="OperationResult"/> object.
    /// </summary>
    public void Dispose()
        => _memoryOwner?.Dispose();

    public static OperationResult Parse(JsonDocumentOwner documentOwner)
    {
        ArgumentNullException.ThrowIfNull(documentOwner);

        var root = documentOwner.Document.RootElement;

        return new OperationResult(
            documentOwner,
            root.TryGetProperty(DataProp, out var data) ? data : default,
            root.TryGetProperty(ErrorsProp, out var errors) ? errors : default,
            root.TryGetProperty(ExtensionsProp, out var extensions) ? extensions : default,
            root.TryGetProperty(RequestIndexProp, out var requestIndex) ? requestIndex.GetInt32() : null,
            root.TryGetProperty(VariableIndexProp, out var variableIndex) ? variableIndex.GetInt32() : null);
    }

    public static OperationResult Parse(JsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var root = document.RootElement;

        return new OperationResult(
            document,
            root.TryGetProperty(DataProp, out var data) ? data : default,
            root.TryGetProperty(ErrorsProp, out var errors) ? errors : default,
            root.TryGetProperty(ExtensionsProp, out var extensions) ? extensions : default,
            root.TryGetProperty(RequestIndexProp, out var requestIndex) ? requestIndex.GetInt32() : null,
            root.TryGetProperty(VariableIndexProp, out var variableIndex) ? variableIndex.GetInt32() : null);
    }

    public static OperationResult Parse(ReadOnlySpan<byte> span)
    {
        if (span.Length == 0)
        {
            throw new ArgumentException(
                OperationResult_Parse_JsonDataIsEmpty,
                nameof(span));
        }

        var buffer = new PooledArrayWriter();
        var bufferSpan = buffer.GetSpan(span.Length);
        span.CopyTo(bufferSpan);
        buffer.Advance(span.Length);

        var document = JsonDocument.Parse(buffer.WrittenMemory);
        var root = document.RootElement;
        var documentOwner = new JsonDocumentOwner(document, buffer);

        return new OperationResult(
            documentOwner,
            root.TryGetProperty(DataProp, out var data) ? data : default,
            root.TryGetProperty(ErrorsProp, out var errors) ? errors : default,
            root.TryGetProperty(ExtensionsProp, out var extensions) ? extensions : default,
            root.TryGetProperty(RequestIndexProp, out var requestIndex) ? requestIndex.GetInt32() : null,
            root.TryGetProperty(VariableIndexProp, out var variableIndex) ? variableIndex.GetInt32() : null);
    }

    public static OperationResult Parse(PooledArrayWriter buffer)
    {
        if (buffer.WrittenSpan.Length == 0)
        {
            throw new ArgumentException(
                OperationResult_Parse_JsonDataIsEmpty,
                nameof(buffer));
        }

        var document = JsonDocument.Parse(buffer.WrittenMemory);
        var root = document.RootElement;
        var documentOwner = new JsonDocumentOwner(document, buffer);

        return new OperationResult(
            documentOwner,
            root.TryGetProperty(DataProp, out var data) ? data : default,
            root.TryGetProperty(ErrorsProp, out var errors) ? errors : default,
            root.TryGetProperty(ExtensionsProp, out var extensions) ? extensions : default,
            root.TryGetProperty(RequestIndexProp, out var requestIndex) ? requestIndex.GetInt32() : null,
            root.TryGetProperty(VariableIndexProp, out var variableIndex) ? variableIndex.GetInt32() : null);
    }
}
