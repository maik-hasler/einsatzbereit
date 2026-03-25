using Xunit;

namespace IntegrationTests;

[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection
    : ICollectionFixture<IntegrationTestFixture>;
