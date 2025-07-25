using HotChocolate.Features;
using HotChocolate.Language;
using HotChocolate.Language.Visitors;
using HotChocolate.Types;

namespace HotChocolate.Validation.Rules;

/// <summary>
/// If any operation defines more than one variable with the same name,
/// it is ambiguous and invalid. It is invalid even if the type of the
/// duplicate variable is the same.
///
/// https://spec.graphql.org/June2018/#sec-Validation.Variables
///
/// AND
///
/// Variables can only be input types. Objects,
/// unions, and interfaces cannot be used as inputs.
///
/// https://spec.graphql.org/June2018/#sec-Variables-Are-Input-Types
///
/// AND
///
/// All variables defined by an operation must be used in that operation
/// or a fragment transitively included by that operation.
///
/// Unused variables cause a validation error.
///
/// https://spec.graphql.org/June2018/#sec-All-Variables-Used
///
/// AND
///
/// Variables are scoped on a per‐operation basis. That means that
/// any variable used within the context of an operation must be defined
/// at the top level of that operation
///
/// https://spec.graphql.org/June2018/#sec-All-Variable-Uses-Defined
///
/// AND
///
/// Variable usages must be compatible with the arguments
/// they are passed to.
///
/// Validation failures occur when variables are used in the context
/// of types that are complete mismatches, or if a nullable type in a
///  variable is passed to a non‐null argument type.
///
/// https://spec.graphql.org/June2018/#sec-All-Variable-Usages-are-Allowed
/// </summary>
internal sealed class VariableVisitor : TypeDocumentValidatorVisitor
{
    public VariableVisitor()
        : base(new SyntaxVisitorOptions
        {
            VisitDirectives = true,
            VisitArguments = true
        })
    {
    }

    protected override ISyntaxVisitorAction Enter(
        OperationDefinitionNode node,
        DocumentValidatorContext context)
    {
        context.Features.GetOrSet<VariableVisitorFeature>().Reset();
        return base.Enter(node, context);
    }

    protected override ISyntaxVisitorAction Leave(
       OperationDefinitionNode node,
       DocumentValidatorContext context)
    {
        var feature = context.Features.GetRequired<VariableVisitorFeature>();
        feature.Unused.ExceptWith(feature.Used);
        feature.Used.ExceptWith(feature.Declared);

        if (feature.Unused.Count > 0)
        {
            context.ReportError(context.VariableNotUsed(node, feature.Unused));
        }

        if (feature.Used.Count > 0)
        {
            context.ReportError(context.VariableNotDeclared(node, feature.Used));
        }

        return base.Leave(node, context);
    }

    protected override ISyntaxVisitorAction Enter(
        VariableDefinitionNode node,
        DocumentValidatorContext context)
    {
        var feature = context.Features.GetRequired<VariableVisitorFeature>();

        base.Enter(node, context);

        var variableName = node.Variable.Name.Value;

        feature.Unused.Add(variableName);
        feature.Declared.Add(variableName);

        if (context.Schema.Types.TryGetType<ITypeDefinition>(
            node.Type.NamedType().Name.Value, out var type)
            && !type.IsInputType())
        {
            context.ReportError(context.VariableNotInputType(node, variableName));
        }

        if (!feature.VariableNames.Add(variableName))
        {
            context.ReportError(context.VariableNameNotUnique(node, variableName));
        }

        return Skip;
    }

    protected override ISyntaxVisitorAction Enter(
        FieldNode node,
        DocumentValidatorContext context)
    {
        if (IntrospectionFieldNames.TypeName.Equals(node.Name.Value, StringComparison.Ordinal))
        {
            if (node.Directives.Count > 0)
            {
                foreach (var directive in node.Directives)
                {
                    var result = Visit(directive, context);
                    if (result.IsBreak())
                    {
                        return result;
                    }
                }
            }

            return Skip;
        }

        if (context.Types.TryPeek(out var type)
            && type.NamedType() is IComplexTypeDefinition ot
            && ot.Fields.TryGetField(node.Name.Value, out var of))
        {
            context.OutputFields.Push(of);
            context.Types.Push(of.Type);
            return Continue;
        }

        context.UnexpectedErrorsDetected = true;
        return Skip;
    }

    protected override ISyntaxVisitorAction Leave(
        FieldNode node,
        DocumentValidatorContext context)
    {
        context.Types.Pop();
        context.OutputFields.Pop();
        return Continue;
    }

    protected override ISyntaxVisitorAction Enter(
        DirectiveNode node,
        DocumentValidatorContext context)
    {
        if (context.Schema.DirectiveDefinitions.TryGetDirective(node.Name.Value, out var d))
        {
            context.Directives.Push(d);
            return Continue;
        }

        context.UnexpectedErrorsDetected = true;
        return Skip;
    }

    protected override ISyntaxVisitorAction Leave(
        DirectiveNode node,
        DocumentValidatorContext context)
    {
        context.Directives.Pop();
        return Continue;
    }

    protected override ISyntaxVisitorAction Enter(
        ArgumentNode node,
        DocumentValidatorContext context)
    {
        if (context.Directives.TryPeek(out var directive))
        {
            if (directive.Arguments.TryGetField(node.Name.Value, out var argument))
            {
                context.InputFields.Push(argument);
                context.Types.Push(argument.Type);
                return Continue;
            }
            context.UnexpectedErrorsDetected = true;
            return Skip;
        }

        if (context.OutputFields.TryPeek(out var field))
        {
            if (field.Arguments.TryGetField(node.Name.Value, out var argument))
            {
                context.InputFields.Push(argument);
                context.Types.Push(argument.Type);
                return Continue;
            }
        }

        context.UnexpectedErrorsDetected = true;
        return Skip;
    }

    protected override ISyntaxVisitorAction Leave(
        ArgumentNode node,
        DocumentValidatorContext context)
    {
        context.InputFields.Pop();
        context.Types.Pop();
        return Continue;
    }

    protected override ISyntaxVisitorAction Enter(
        ObjectFieldNode node,
        DocumentValidatorContext context)
    {
        if (context.Types.TryPeek(out var type)
            && type.NamedType() is IInputObjectTypeDefinition it
            && it.Fields.TryGetField(node.Name.Value, out var field))
        {
            context.InputFields.Push(field);
            context.Types.Push(field.Type);
            return Continue;
        }

        return Skip;
    }

    protected override ISyntaxVisitorAction Leave(
        ObjectFieldNode node,
        DocumentValidatorContext context)
    {
        context.InputFields.Pop();
        context.Types.Pop();
        return Continue;
    }

    protected override ISyntaxVisitorAction Enter(
        VariableNode node,
        DocumentValidatorContext context)
    {
        context.Features.GetRequired<VariableVisitorFeature>().Used.Add(node.Name.Value);

        var parent = context.Path.Peek();

        var defaultValue = parent.Kind switch
        {
            SyntaxKind.Argument => context.InputFields.Peek().DefaultValue,
            SyntaxKind.ObjectField => context.InputFields.Peek().DefaultValue,
            _ => null
        };

        if (context.Variables.TryGetValue(
                node.Name.Value,
                out var variableDefinition)
            && !IsVariableUsageAllowed(variableDefinition, context.Types.Peek(), defaultValue))
        {
            context.ReportError(context.VariableIsNotCompatible(node, variableDefinition));
        }

        return Skip;
    }

    protected override ISyntaxVisitorAction Enter(
        ListValueNode node,
        DocumentValidatorContext context)
    {
        if (context.Types.TryPeek(out var type) && type.IsListType())
        {
            context.Types.Push(type.ElementType());
            return Continue;
        }
        return Break;
    }

    protected override ISyntaxVisitorAction Leave(
        ListValueNode node,
        DocumentValidatorContext context)
    {
        context.Types.Pop();
        return Continue;
    }

    // http://facebook.github.io/graphql/June2018/#IsVariableUsageAllowed()
    private bool IsVariableUsageAllowed(
        VariableDefinitionNode variableDefinition,
        IType locationType,
        IValueNode? locationDefault)
    {
        if (locationType.IsNonNullType()
            && !variableDefinition.Type.IsNonNullType())
        {
            if (variableDefinition.DefaultValue.IsNull()
                && locationDefault.IsNull())
            {
                return false;
            }

            return AreTypesCompatible(
                variableDefinition.Type,
                locationType.NullableType());
        }

        return AreTypesCompatible(
            variableDefinition.Type,
            locationType);
    }

    // http://facebook.github.io/graphql/June2018/#AreTypesCompatible()
    private bool AreTypesCompatible(
        ITypeNode variableType,
        IType locationType)
    {
        if (locationType.IsNonNullType())
        {
            if (variableType.IsNonNullType())
            {
                return AreTypesCompatible(
                    variableType.InnerType(),
                    locationType.InnerType());
            }
            return false;
        }

        if (variableType.IsNonNullType())
        {
            return AreTypesCompatible(
                variableType.InnerType(),
                locationType);
        }

        if (locationType.IsListType())
        {
            if (variableType.IsListType())
            {
                return AreTypesCompatible(
                    variableType.InnerType(),
                    locationType.InnerType());
            }
            return false;
        }

        if (variableType.IsListType())
        {
            return false;
        }

        if (variableType is NamedTypeNode vn
            && locationType is ITypeDefinition lt)
        {
            return string.Equals(
                vn.Name.Value,
                lt.Name,
                StringComparison.Ordinal);
        }

        return false;
    }

    private sealed class VariableVisitorFeature : ValidatorFeature
    {
        public HashSet<string> VariableNames { get; } = [];

        public HashSet<string> Used { get; } = [];

        public HashSet<string> Declared { get; } = [];

        public HashSet<string> Unused { get; } = [];

        protected internal override void Reset()
        {
            VariableNames.Clear();
            Used.Clear();
            Declared.Clear();
            Unused.Clear();
        }
    }
}
