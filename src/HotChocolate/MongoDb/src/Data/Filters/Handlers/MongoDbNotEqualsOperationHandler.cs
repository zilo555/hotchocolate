using HotChocolate.Configuration;
using HotChocolate.Data.Filters;
using HotChocolate.Language;
using HotChocolate.Types;
using MongoDB.Driver;

namespace HotChocolate.Data.MongoDb.Filters;

/// <summary>
/// This filter operation handler maps a NotEquals operation field to a
/// <see cref="FilterDefinition{TDocument}"/>
/// </summary>
public class MongoDbNotEqualsOperationHandler
    : MongoDbOperationHandlerBase
{
    public MongoDbNotEqualsOperationHandler(InputParser inputParser) : base(inputParser)
    {
    }

    /// <inheritdoc />
    public override bool CanHandle(
        ITypeCompletionContext context,
        IFilterInputTypeConfiguration typeConfiguration,
        IFilterFieldConfiguration fieldConfiguration)
    {
        return fieldConfiguration is FilterOperationFieldConfiguration operationField
            && operationField.Id is DefaultFilterOperations.NotEquals;
    }

    /// <inheritdoc />
    public override MongoDbFilterDefinition HandleOperation(
        MongoDbFilterVisitorContext context,
        IFilterOperationField field,
        IValueNode value,
        object? parsedValue)
    {
        var doc = new MongoDbFilterOperation("$ne", parsedValue);

        return new MongoDbFilterOperation(context.GetMongoFilterScope().GetPath(), doc);
    }
}
