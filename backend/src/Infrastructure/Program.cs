using Amazon.CDK;

namespace Infrastructure;

internal sealed class Program
{
    public static void Main(string[] args)
    {
        var app = new App();

        new DrcsStack(app, "DrcsStack", new StackProps
        {
            Env = new Amazon.CDK.Environment
            {
                // Personal account — `aws sts get-caller-identity --profile personal`
                Account = "533267413526",
                Region = "ap-south-1"
            }
        });

        app.Synth();
    }
}
