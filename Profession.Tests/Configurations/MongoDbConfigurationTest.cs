namespace Profession.Tests.Configurations;

[TestSubject(typeof(MongoDbConfiguration))]
public class MongoDbConfigurationTest
{

    [Fact]
    public void MongoClient_ShouldReturnMongoClientInstance()
    {
        // Arrange
        var mockConfigurationSection = new Mock<IConfigurationSection>();
        mockConfigurationSection.Setup(x => x["ConnectionString"]).Returns("mongodb://localhost:27017");

        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(x => x.GetSection("MongoDB")).Returns(mockConfigurationSection.Object);

        var mongoDbConfiguration = new MongoDbConfiguration(mockConfiguration.Object);

        // Act
        var client = mongoDbConfiguration.MongoClient();

        // Assert
        Assert.NotNull(client);
        Assert.IsType<MongoClient>(client);
    }
}