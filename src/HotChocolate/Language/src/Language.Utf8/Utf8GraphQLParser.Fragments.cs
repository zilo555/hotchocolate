namespace HotChocolate.Language;

// Implements the parsing rules in the Fragments section.
public ref partial struct Utf8GraphQLParser
{
    /// <summary>
    /// Parses a fragment spread or inline fragment within a selection set.
    /// <see cref="ParseFragmentSpread" /> and
    /// <see cref="ParseInlineFragment" />.
    /// </summary>
    private ISelectionNode ParseFragment()
    {
        var start = Start();

        ExpectSpread();
        var isOnKeyword = _reader.Value.SequenceEqual(GraphQLKeywords.On);

        if (!isOnKeyword && _reader.Kind == TokenKind.Name)
        {
            return ParseFragmentSpread(in start);
        }

        NamedTypeNode? typeCondition = null;

        if (isOnKeyword)
        {
            MoveNext();
            typeCondition = ParseNamedType();
        }

        return ParseInlineFragment(in start, typeCondition);
    }

    /// <summary>
    /// Parses a fragment definition.
    /// <see cref="FragmentDefinitionNode" />:
    /// Description? fragment FragmentName on TypeCondition Directives? SelectionSet
    /// </summary>
    private FragmentDefinitionNode ParseFragmentDefinition()
    {
        var start = Start();

        ExpectFragmentKeyword();

        // Experimental support for defining variables within fragments
        // changes the grammar of FragmentDefinition:
        // fragment FragmentName VariableDefinitions? on
        //    TypeCondition Directives? SelectionSet
        if (_allowFragmentVars)
        {
            var name = ParseFragmentName();
            var variableDefinitions =
              ParseVariableDefinitions();
            ExpectOnKeyword();
            var typeCondition = ParseNamedType();
            var directives = ParseDirectives(false);
            var selectionSet = ParseSelectionSet();
            var location = CreateLocation(in start);

            return new FragmentDefinitionNode
            (
              location,
              name,
              TakeDescription(),
              variableDefinitions,
              typeCondition,
              directives,
              selectionSet
            );
        }
        else
        {
            var name = ParseFragmentName();
            ExpectOnKeyword();
            var typeCondition = ParseNamedType();
            var directives = ParseDirectives(false);
            var selectionSet = ParseSelectionSet();
            var location = CreateLocation(in start);

            return new FragmentDefinitionNode
            (
              location,
              name,
              TakeDescription(),
              [],
              typeCondition,
              directives,
              selectionSet
            );
        }
    }

    /// <summary>
    /// Parses a fragment spread.
    /// <see cref="FragmentSpreadNode" />:
    /// ... FragmentName Directives?
    /// </summary>
    /// <param name="start">
    /// The start token of the current fragment node.
    /// </param>
    private FragmentSpreadNode ParseFragmentSpread(in TokenInfo start)
    {
        var name = ParseFragmentName();
        var directives = ParseDirectives(false);
        var location = CreateLocation(in start);

        return new FragmentSpreadNode
        (
            location,
            name,
            directives
        );
    }

    /// <summary>
    /// Parses an inline fragment.
    /// <see cref="FragmentSpreadNode" />:
    /// ... TypeCondition? Directives? SelectionSet
    /// </summary>
    /// <param name="start">
    /// The start token of the current fragment node.
    /// </param>
    /// <param name="typeCondition">
    /// The fragment type condition.
    /// </param>
    private InlineFragmentNode ParseInlineFragment(
        in TokenInfo start,
        NamedTypeNode? typeCondition)
    {
        var directives = ParseDirectives(false);
        var selectionSet = ParseSelectionSet();
        var location = CreateLocation(in start);

        return new InlineFragmentNode
        (
            location,
            typeCondition,
            directives,
            selectionSet
        );
    }

    /// <summary>
    /// Parse fragment name.
    /// <see cref="NameNode" />:
    /// Name
    /// </summary>
    private NameNode ParseFragmentName()
    {
        if (_reader.Value.SequenceEqual(GraphQLKeywords.On))
        {
            throw Unexpected(_reader.Kind);
        }
        return ParseName();
    }
}
