using System.Linq.Expressions;
using HotChocolate.Configuration;
using HotChocolate.Language;
using HotChocolate.Types;
using HotChocolate.Utilities;

namespace HotChocolate.Data.Filters.Expressions;

public class QueryableListAnyOperationHandler
    : QueryableOperationHandlerBase
{
    public QueryableListAnyOperationHandler(
        ITypeConverter typeConverter,
        InputParser inputParser)
        : base(inputParser)
    {
        TypeConverter = typeConverter;
        CanBeNull = false;
    }

    protected ITypeConverter TypeConverter { get; }

    public override bool CanHandle(
        ITypeCompletionContext context,
        IFilterInputTypeConfiguration typeConfiguration,
        IFilterFieldConfiguration fieldConfiguration)
    {
        return context.Type is IListFilterInputType
            && fieldConfiguration is FilterOperationFieldConfiguration operationField
            && operationField.Id == DefaultFilterOperations.Any;
    }

    public override Expression HandleOperation(
        QueryableFilterContext context,
        IFilterOperationField field,
        IValueNode value,
        object? parsedValue)
    {
        if (context.RuntimeTypes.Count > 0
            && context.RuntimeTypes.Peek().TypeArguments is { Count: > 0 } args
            && parsedValue is bool parsedBool)
        {
            var property = context.GetInstance();

            Expression expression;
            if (parsedBool)
            {
                expression = FilterExpressionBuilder.Any(args[0].Source, property);
            }
            else
            {
                expression = FilterExpressionBuilder.Not(
                    FilterExpressionBuilder.Any(args[0].Source, property));
            }

            if (context.InMemory)
            {
                expression = FilterExpressionBuilder.NotNullAndAlso(property, expression);
            }

            return expression;
        }

        throw ThrowHelper.Filtering_CouldNotParseValue(this, value, field.Type, field);
    }
}
