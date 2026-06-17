using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using AssetOptions = Amazon.CDK.AWS.S3.Assets.AssetOptions;

namespace Infrastructure.Helpers;

// Bundles a .NET Lambda asset by running `dotnet publish` inside the CDK
// bundling container. Requires Docker to be running when `cdk synth`/`deploy` runs.
// Usage: DotNetLambdaAsset.FromProject("PublicApi/src/PublicApi")
//   — path is relative to `backend/src`.
public static class DotNetLambdaAsset
{
    private const string SourceRoot = "src";

    public static Code FromProject(string projectRelativePath)
    {
        var publishCommands = new[]
        {
            $"cd /asset-input/{projectRelativePath}",
            "export DOTNET_CLI_HOME=\"/tmp/DOTNET_CLI_HOME\"",
            "export DOTNET_CLI_TELEMETRY_OPTOUT=1",
            "dotnet publish -c Release -r linux-x64 --self-contained false -p:PublishReadyToRun=false -p:DebugType=None -p:DebugSymbols=false -o /tmp/publish",
            "find /tmp/publish -name '*.pdb' -delete",
            "cd /tmp/publish",
            "zip -qr /asset-output/output.zip ."
        };

        return Code.FromAsset(SourceRoot, new AssetOptions
        {
            Exclude = new[]
            {
                "**/bin/**",
                "**/obj/**",
                "**/publish/**",
                "**/.DS_Store",
                "**/*.user",
                "Infrastructure/**"
            },
            Bundling = new BundlingOptions
            {
                Image = Runtime.DOTNET_8.BundlingImage,
                OutputType = BundlingOutput.ARCHIVED,
                Command = new[] { "bash", "-c", string.Join(" && ", publishCommands) }
            }
        });
    }
}
