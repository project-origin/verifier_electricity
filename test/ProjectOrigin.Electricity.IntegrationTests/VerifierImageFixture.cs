using System.IO;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Images;
using Xunit;

namespace ProjectOrigin.Electricity.IntegrationTests;

[CollectionDefinition("VerifierImageCollection")]
public class VerifierImageCollection : ICollectionFixture<VerifierImageFixture>
{
    // This class is just a marker to associate the fixture with the collection, to only create the image once
}

public class VerifierImageFixture : IAsyncLifetime
{
    private readonly ModifiedDockerfile _modifiedDockerfile;
    private readonly IFutureDockerImage _verifierImage;

    public IFutureDockerImage Image => _verifierImage;

    public VerifierImageFixture()
    {
        var solutionDirectory = CommonDirectoryPath.GetSolutionDirectory().DirectoryPath;

        // Testcontainers doesn't support some functionality in Dockerfiles
        _modifiedDockerfile = new ModifiedDockerfile(Path.Combine(solutionDirectory, "Electricity.Dockerfile"), content => content
            .Replace(" --platform=$BUILDPLATFORM", "") // not supported by Testcontainers
            .Replace("-noble-chiseled-extra", "")); // not supported by Testcontainers because of user permissions

        _verifierImage = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(solutionDirectory)
            .WithDockerfile(_modifiedDockerfile.FileName)
            .Build();
    }

    public virtual async Task InitializeAsync()
    {
        await _verifierImage.CreateAsync();
    }

    public virtual Task DisposeAsync()
    {
        _modifiedDockerfile.Dispose();
        return Task.CompletedTask;
    }
}
