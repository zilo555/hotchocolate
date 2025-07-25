using HotChocolate.Execution;
using HotChocolate.Execution.Configuration;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;
using static HotChocolate.Data.Raven.ProjectionVisitorTestBase;

namespace HotChocolate.Data.Raven;

[Collection(SchemaCacheCollectionFixture.DefinitionName)]
public class QueryableProjectionInterfaceTypeTests
{
    private static readonly AbstractType[] s_barEntities =
    [
        new Bar { Name = "Bar", BarProp = "BarProp" },
        new Foo { Name = "Foo", FooProp = "FooProp" }
    ];

    private static readonly NestedObject[] s_barNestedEntities =
    [
        new() { Nested = new Bar { Name = "Bar", BarProp = "BarProp" } },
        new() { Nested = new Foo { Name = "Foo", FooProp = "FooProp" } }
    ];

    private static readonly NestedList[] s_barListEntities =
    [
        new()
        {
            List =
            [
                new Foo { Name = "Foo", FooProp = "FooProp" },
                new Bar { Name = "Bar", BarProp = "BarProp" }
            ]
        },
        new()
        {
            List =
            [
                new Bar { Name = "Bar", BarProp = "BarProp" },
                new Foo { Name = "Foo", FooProp = "FooProp" }
            ]
        }
    ];

    private readonly SchemaCache _cache;

    public QueryableProjectionInterfaceTypeTests(SchemaCache cache)
    {
        _cache = cache;
    }

    [Fact(Skip = "As currently not supported")]
    public async Task Create_Interface()
    {
        // arrange
        var tester =
            _cache.CreateSchema(s_barEntities, configure: ConfigureSchema);

        // act
        var res1 = await tester.ExecuteAsync(
            OperationRequestBuilder.New()
                .SetDocument(
                    @"
                        {
                            root {
                                name
                                ... on Foo {
                                    fooProp
                                }
                                ... on Bar {
                                    barProp
                                }
                            }
                        }")
                .Build());

        // assert
        await Snapshot
            .Create()
            .AddResult(res1)
            .MatchAsync();
    }

    [Fact(Skip = "As currently not supported")]
    public async Task Create_Interface_Pagination()
    {
        // arrange
        var tester =
            _cache.CreateSchema(s_barEntities,
                configure: x =>
                {
                    ConfigureSchema(x);

                    var typeExtension =
                        new ObjectTypeExtension<StubObject<AbstractType>>(
                            y =>
                            {
                                y.Name("Query");
                                y.Field(z => z.Root).UsePaging<InterfaceType<AbstractType>>();
                            });

                    x.AddTypeExtension(typeExtension);
                });

        // act
        var res1 = await tester.ExecuteAsync(
            OperationRequestBuilder.New()
                .SetDocument(
                    @"
                        {
                            root {
                                nodes {
                                    name
                                    ... on Foo {
                                        fooProp
                                    }
                                    ... on Bar {
                                        barProp
                                    }
                                }
                            }
                        }")
                .Build());

        // assert
        await Snapshot
            .Create()
            .AddResult(res1)
            .MatchAsync();
    }

    [Fact(Skip = "As currently not supported")]
    public async Task Create_Interface_Nested()
    {
        // arrange
        var tester = _cache
            .CreateSchema(s_barNestedEntities, configure: ConfigureSchema);

        // act
        var res1 = await tester.ExecuteAsync(
            OperationRequestBuilder.New()
                .SetDocument(
                    @"
                        {
                            root {
                                nested {
                                    name
                                    ... on Foo {
                                        fooProp
                                    }
                                    ... on Bar {
                                        barProp
                                    }
                                }
                            }
                        }")
                .Build());

        // assert
        await Snapshot
            .Create()
            .AddResult(res1)
            .MatchAsync();
    }

    [Fact(Skip = "As currently not supported")]
    public async Task Create_Interface_NestedList()
    {
        // arrange
        var tester = _cache
            .CreateSchema(s_barListEntities, configure: ConfigureSchema);

        // act
        var res1 = await tester.ExecuteAsync(
            OperationRequestBuilder.New()
                .SetDocument(
                    @"
                        {
                            root {
                                list {
                                    name
                                    ... on Foo {
                                        fooProp
                                    }
                                    ... on Bar {
                                        barProp
                                    }
                                }
                            }
                        }")
                .Build());

        // assert
        await Snapshot
            .Create()
            .AddResult(res1)
            .MatchAsync();
    }

    [Fact(Skip = "As currently not supported")]
    public async Task Paging_Interface_List()
    {
        // arrange
        var tester = _cache
            .CreateSchema(
                s_barEntities,
                usePaging: true,
                configure: ConfigureSchema,
                schemaType: typeof(InterfaceType<AbstractType>));

        // act
        var res1 = await tester.ExecuteAsync(
            OperationRequestBuilder.New()
                .SetDocument(
                    @"
                        {
                            root {
                                nodes {
                                    name
                                    ... on Foo {
                                        fooProp
                                    }
                                    ... on Bar {
                                        barProp
                                    }
                                }
                            }
                        }")
                .Build());

        // assert
        await Snapshot
            .Create()
            .AddResult(res1)
            .MatchAsync();
    }

    [Fact(Skip = "As currently not supported")]
    public async Task OffsetPaging_Interface_List()
    {
        // arrange
        var tester = _cache
            .CreateSchema(
                s_barEntities,
                useOffsetPaging: true,
                configure: ConfigureSchema,
                schemaType: typeof(InterfaceType<AbstractType>));

        // act
        var res1 = await tester.ExecuteAsync(
            OperationRequestBuilder.New()
                .SetDocument(
                    @"
                        {
                            root {
                                items {
                                    name
                                    ... on Foo {
                                        fooProp
                                    }
                                    ... on Bar {
                                        barProp
                                    }
                                }
                            }
                        }")
                .Build());

        // assert
        await Snapshot
            .Create()
            .AddResult(res1)
            .MatchAsync();
    }

    [Fact(Skip = "As currently not supported")]
    public async Task Create_Interface_Without_Missing()
    {
        // arrange
        var tester =
            _cache.CreateSchema(s_barEntities, configure: ConfigureSchema);

        // act
        var res1 = await tester.ExecuteAsync(
            OperationRequestBuilder.New()
                .SetDocument(
                    @"
                        {
                            root {
                                ... on Foo {
                                    fooProp
                                }
                            }
                        }")
                .Build());

        // assert
        await Snapshot
            .Create()
            .AddResult(res1)
            .MatchAsync();
    }

    private static void ConfigureSchema(IRequestExecutorBuilder builder)
    {
        builder.AddType(new ObjectType<Foo>(x => x.Implements<InterfaceType<AbstractType>>()))
            .AddType(new ObjectType<Bar>(x => x.Implements<InterfaceType<AbstractType>>()));
    }

    public class NestedList
    {
        public string? Id { get; set; }

        public List<AbstractType> List { get; set; } = null!;
    }

    public class NestedObject
    {
        public string? Id { get; set; }

        public AbstractType Nested { get; set; } = null!;
    }

    public class Foo : AbstractType
    {
        public new string? Id { get; set; }

        public string FooProp { get; set; } = null!;
    }

    [InterfaceType]
    public abstract class AbstractType
    {
        public string? Id { get; set; }

        public string Name { get; set; } = null!;
    }

    public class Bar : AbstractType
    {
        public new string? Id { get; set; }

        public string? BarProp { get; set; }
    }
}
