using Amazon.CDK;
using Amazon.CDK.AWS.Amplify;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.CodeCommit;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;
using Constructs;
using Infrastructure.Helpers;
using System;
using System.Collections.Generic;
using System.IO;

namespace Infrastructure;

public class DrcsStack : Stack
{
    internal DrcsStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        var assetsBucket = new Bucket(this, "AssetsBucket", new BucketProps
        {
            BucketName = "drcs-assets",
            Versioned = true,
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            Encryption = BucketEncryption.S3_MANAGED,
            RemovalPolicy = RemovalPolicy.RETAIN
        });

        var usersTable = DynamoTableFactory.Create(this, new TableDef(
            Name: "drcs-users",
            PartitionKey: "PK",
            SortKey: "SK",
            Gsis: new[]
            {
                new GsiDef("email-index", "email"),
                new GsiDef("nic-index", "nic"),
                new GsiDef("district-index", "area", "name"),
                new GsiDef("role-index", "role", "id")
            }));

        var resourcesTable = DynamoTableFactory.Create(this, new TableDef(
            Name: "drcs-resources",
            PartitionKey: "PK",
            SortKey: "SK",
            Gsis: new[]
            {
                new GsiDef("category-index", "category", "name"),
                new GsiDef("status-index", "status", "name")
            }));

        var disastersTable = DynamoTableFactory.Create(this, new TableDef(
            Name: "drcs-disasters",
            PartitionKey: "PK",
            SortKey: "SK",
            Gsis: new[]
            {
                new GsiDef("entity-index", "entity", "slug")
            }));

        var disasterAssignmentsTable = DynamoTableFactory.Create(this, new TableDef(
            Name: "drcs-disaster-assignments",
            PartitionKey: "PK",
            SortKey: "SK",
            Gsis: new[]
            {
                new GsiDef("user-index", "userSub", "status")
            }));

        var donationsTable = DynamoTableFactory.Create(this, new TableDef(
            Name: "drcs-donations",
            PartitionKey: "PK",
            SortKey: "SK",
            Gsis: new[]
            {
                new GsiDef("user-index", "userSub", "createdAt")
            }));

        var userPool = new UserPool(this, "UserPool", new UserPoolProps
        {
            UserPoolName = "drcs-users-pool",
            SelfSignUpEnabled = false,
            SignInAliases = new SignInAliases { Email = true },
            StandardAttributes = new StandardAttributes
            {
                Email = new StandardAttribute { Required = true, Mutable = false },
                Fullname = new StandardAttribute { Required = false, Mutable = true }
            },
            CustomAttributes = new Dictionary<string, ICustomAttribute>
            {
                ["role"] = new StringAttribute(new StringAttributeProps { Mutable = true }),
                ["department"] = new StringAttribute(new StringAttributeProps { Mutable = true })
            },
            PasswordPolicy = new PasswordPolicy
            {
                MinLength = 8,
                RequireLowercase = true,
                RequireUppercase = false,
                RequireDigits = true,
                RequireSymbols = false
            },
            AccountRecovery = AccountRecovery.EMAIL_ONLY,
            RemovalPolicy = RemovalPolicy.RETAIN
        });

        var userPoolClient = userPool.AddClient("WebClient", new UserPoolClientOptions
        {
            UserPoolClientName = "drcs-web-client",
            GenerateSecret = false,
            AuthFlows = new AuthFlow
            {
                UserPassword = true,
                UserSrp = true
            },
            RefreshTokenValidity = Duration.Days(30),
            AccessTokenValidity = Duration.Hours(1),
            IdTokenValidity = Duration.Hours(1)
        });

        var publicApiPath = Path.Combine(Directory.GetCurrentDirectory(), "src/PublicApi/src/PublicApi");
        var privateApiPath = Path.Combine(Directory.GetCurrentDirectory(), "src/PrivateApi/src/PrivateApi");
        var apiAssetsReady = Directory.Exists(publicApiPath) && Directory.Exists(privateApiPath);

        if (apiAssetsReady)
        {
            var sesFromEmail = (this.Node.TryGetContext("sesFromEmail") as string) ?? string.Empty;

            var lambdaEnv = new Dictionary<string, string>
            {
                ["ENVIRONMENT"] = "prod",
                ["USERS_TABLE"] = usersTable.TableName,
                ["RESOURCES_TABLE"] = resourcesTable.TableName,
                ["DISASTERS_TABLE"] = disastersTable.TableName,
                ["ASSIGNMENTS_TABLE"] = disasterAssignmentsTable.TableName,
                ["DONATIONS_TABLE"] = donationsTable.TableName,
                ["ASSETS_BUCKET"] = assetsBucket.BucketName,
                ["COGNITO_USER_POOL_ID"] = userPool.UserPoolId,
                ["COGNITO_CLIENT_ID"] = userPoolClient.UserPoolClientId,
                ["SES_FROM_EMAIL"] = sesFromEmail
            };

            var publicApiLambda = new Function(this, "PublicApiLambda", new FunctionProps
            {
                FunctionName = "drcs-public-api",
                Runtime = Runtime.DOTNET_8,
                Handler = "PublicApi::PublicApi.LambdaEntryPoint::FunctionHandlerAsync",
                Code = DotNetLambdaAsset.FromProject("PublicApi/src/PublicApi"),
                Architecture = Architecture.X86_64,
                Timeout = Duration.Seconds(30),
                MemorySize = 512,
                Environment = new Dictionary<string, string>(lambdaEnv)
            });

            var privateApiLambda = new Function(this, "PrivateApiLambda", new FunctionProps
            {
                FunctionName = "drcs-private-api",
                Runtime = Runtime.DOTNET_8,
                Handler = "PrivateApi::PrivateApi.LambdaEntryPoint::FunctionHandlerAsync",
                Code = DotNetLambdaAsset.FromProject("PrivateApi/src/PrivateApi"),
                Architecture = Architecture.X86_64,
                Timeout = Duration.Seconds(30),
                MemorySize = 512,
                Environment = new Dictionary<string, string>(lambdaEnv)
            });

            usersTable.GrantReadWriteData(publicApiLambda);
            resourcesTable.GrantReadData(publicApiLambda);
            disastersTable.GrantReadData(publicApiLambda);
            disasterAssignmentsTable.GrantReadData(publicApiLambda);
            donationsTable.GrantReadData(publicApiLambda);
            assetsBucket.GrantReadWrite(publicApiLambda);

            publicApiLambda.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[]
                {
                    "cognito-idp:AdminCreateUser",
                    "cognito-idp:AdminSetUserPassword",
                    "cognito-idp:AdminGetUser"
                },
                Resources = new[] { userPool.UserPoolArn }
            }));

            usersTable.GrantReadWriteData(privateApiLambda);
            resourcesTable.GrantReadWriteData(privateApiLambda);
            disastersTable.GrantReadWriteData(privateApiLambda);
            disasterAssignmentsTable.GrantReadWriteData(privateApiLambda);
            donationsTable.GrantReadWriteData(privateApiLambda);
            assetsBucket.GrantReadWrite(privateApiLambda);

            privateApiLambda.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[]
                {
                    "cognito-idp:AdminDisableUser",
                    "cognito-idp:AdminEnableUser",
                    "cognito-idp:AdminUpdateUserAttributes",
                    "cognito-idp:AdminGetUser",
                    "cognito-idp:AdminSetUserPassword"
                },
                Resources = new[] { userPool.UserPoolArn }
            }));

            privateApiLambda.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[]
                {
                    "ses:SendEmail",
                    "ses:SendRawEmail"
                },
                Resources = new[] { "*" }
            }));

            var api = new RestApi(this, "DrcsApi", new RestApiProps
            {
                RestApiName = "drcs-api",
                Description = "DRCS API Gateway",
                DeployOptions = new StageOptions { StageName = "prod" },
                DefaultCorsPreflightOptions = new CorsOptions
                {
                    AllowOrigins = Cors.ALL_ORIGINS,
                    AllowMethods = Cors.ALL_METHODS,
                    AllowHeaders = Cors.DEFAULT_HEADERS,
                    AllowCredentials = true
                },
                EndpointConfiguration = new EndpointConfiguration
                {
                    Types = new[] { EndpointType.REGIONAL }
                },
                BinaryMediaTypes = new[] { "multipart/form-data", "image/*", "application/octet-stream" }
            });

            var cognitoAuthorizer = new CognitoUserPoolsAuthorizer(this, "CognitoAuthorizer",
                new CognitoUserPoolsAuthorizerProps
                {
                    CognitoUserPools = new[] { userPool },
                    IdentitySource = IdentitySource.Header("Authorization"),
                    AuthorizerName = "drcs-cognito-authorizer"
                });

            var authedMethod = new MethodOptions
            {
                AuthorizationType = AuthorizationType.COGNITO,
                Authorizer = cognitoAuthorizer
            };

            var publicIntegration = new LambdaIntegration(publicApiLambda, new LambdaIntegrationOptions
            {
                Timeout = Duration.Seconds(29)
            });

            var privateIntegration = new LambdaIntegration(privateApiLambda, new LambdaIntegrationOptions
            {
                Timeout = Duration.Seconds(29)
            });

            var publicResource = api.Root.AddResource("public");
            publicResource.AddProxy(new ProxyResourceOptions
            {
                AnyMethod = true,
                DefaultIntegration = publicIntegration,
                DefaultMethodOptions = new MethodOptions
                {
                    AuthorizationType = AuthorizationType.NONE
                }
            });

            var privateResource = api.Root.AddResource("private");
            privateResource.AddProxy(new ProxyResourceOptions
            {
                AnyMethod = true,
                DefaultIntegration = privateIntegration,
                DefaultMethodOptions = authedMethod
            });

            _ = new CfnOutput(this, "ApiUrl", new CfnOutputProps
            {
                Value = api.Url,
                Description = "Base API URL"
            });

            var codeRepo = new Repository(this, "CodeCommitRepo", new RepositoryProps
            {
                RepositoryName = "drcs",
                Description = "DRCS monorepo"
            });

            var nextAuthSecret = (this.Node.TryGetContext("nextAuthSecret") as string) ?? "set-me-in-amplify-console";
            var jwtSecret = (this.Node.TryGetContext("jwtSecret") as string) ?? "set-me-in-amplify-console";

            var amplifyRole = new Role(this, "AmplifyServiceRole", new RoleProps
            {
                RoleName = "drcs-amplify-service-role",
                AssumedBy = new ServicePrincipal("amplify.amazonaws.com"),
                ManagedPolicies = new[]
                {
                    ManagedPolicy.FromAwsManagedPolicyName("AdministratorAccess-Amplify"),
                    ManagedPolicy.FromAwsManagedPolicyName("AWSCodeCommitReadOnly")
                }
            });

            var amplifyBuildSpec = string.Join("\n", new[]
            {
                "version: 1",
                "applications:",
                "  - appRoot: frontend",
                "    frontend:",
                "      phases:",
                "        preBuild:",
                "          commands:",
                "            - npm ci --cache .npm --prefer-offline",
                "            - env | grep -e NEXT_PUBLIC_ -e NEXTAUTH_SECRET -e JWT_SECRET >> .env.production || true",
                "            - echo \"NEXTAUTH_URL=https://${AWS_BRANCH}.${AWS_APP_ID}.amplifyapp.com\" >> .env.production",
                "        build:",
                "          commands:",
                "            - npm run build",
                "      artifacts:",
                "        baseDirectory: .next",
                "        files:",
                "          - '**/*'",
                "      cache:",
                "        paths:",
                "          - .next/cache/**/*",
                "          - .npm/**/*"
            });

            var amplifyApp = new CfnApp(this, "AmplifyApp", new CfnAppProps
            {
                Name = "drcs-frontend",
                Description = "DRCS frontend",
                Platform = "WEB_COMPUTE",
                IamServiceRole = amplifyRole.RoleArn,
                Repository = codeRepo.RepositoryCloneUrlHttp,
                BuildSpec = amplifyBuildSpec
            });

            amplifyApp.EnvironmentVariables = new[]
            {
                new CfnApp.EnvironmentVariableProperty { Name = "NEXT_PUBLIC_USER_POOL_ID", Value = userPool.UserPoolId },
                new CfnApp.EnvironmentVariableProperty { Name = "NEXT_PUBLIC_USER_POOL_CLIENT_ID", Value = userPoolClient.UserPoolClientId },
                new CfnApp.EnvironmentVariableProperty { Name = "NEXT_PUBLIC_PUBLIC_API_URL", Value = $"{api.Url}public" },
                new CfnApp.EnvironmentVariableProperty { Name = "NEXT_PUBLIC_PRIVATE_API_URL", Value = $"{api.Url}private" },
                new CfnApp.EnvironmentVariableProperty { Name = "NEXTAUTH_SECRET", Value = nextAuthSecret },
                new CfnApp.EnvironmentVariableProperty { Name = "JWT_SECRET", Value = jwtSecret },
                new CfnApp.EnvironmentVariableProperty { Name = "AMPLIFY_MONOREPO_APP_ROOT", Value = "frontend" },
                new CfnApp.EnvironmentVariableProperty { Name = "AMPLIFY_DIFF_DEPLOY", Value = "false" },
                new CfnApp.EnvironmentVariableProperty { Name = "_LIVE_UPDATES", Value = "[{\"name\":\"Next.js version\",\"pkg\":\"next-version\",\"type\":\"internal\",\"version\":\"latest\"}]" }
            };

            var amplifyBranch = new CfnBranch(this, "AmplifyMainBranch", new CfnBranchProps
            {
                AppId = amplifyApp.AttrAppId,
                BranchName = "main",
                Stage = "PRODUCTION",
                EnableAutoBuild = true,
                Framework = "Next.js - SSR"
            });

            _ = new CfnOutput(this, "CodeCommitCloneUrl", new CfnOutputProps
            {
                Value = codeRepo.RepositoryCloneUrlHttp,
                Description = "CodeCommit clone URL"
            });

            _ = new CfnOutput(this, "AmplifyAppId", new CfnOutputProps
            {
                Value = amplifyApp.AttrAppId,
                Description = "Amplify app ID"
            });

            _ = new CfnOutput(this, "AmplifyUrl", new CfnOutputProps
            {
                Value = $"https://{amplifyBranch.BranchName}.{amplifyApp.AttrDefaultDomain}",
                Description = "Frontend URL"
            });
        }
        else
        {
            Console.WriteLine(
                "[DrcsStack] Skipping Lambda + API Gateway: PublicApi/PrivateApi asset dirs not found. " +
                "Create them and re-run `cdk synth` to wire up the API layer.");
        }

        _ = new CfnOutput(this, "UserPoolId", new CfnOutputProps
        {
            Value = userPool.UserPoolId,
            Description = "Cognito User Pool ID"
        });

        _ = new CfnOutput(this, "UserPoolClientId", new CfnOutputProps
        {
            Value = userPoolClient.UserPoolClientId,
            Description = "Cognito App Client ID"
        });

        _ = new CfnOutput(this, "AssetsBucketName", new CfnOutputProps
        {
            Value = assetsBucket.BucketName,
            Description = "S3 assets bucket"
        });
    }
}
