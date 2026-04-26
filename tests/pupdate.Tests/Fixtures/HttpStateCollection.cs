namespace Pannella.Tests.Fixtures;

[CollectionDefinition(Name)]
public class HttpStateCollection : ICollectionFixture<WireMockFixture>
{
    public const string Name = "HttpStateful";
}
