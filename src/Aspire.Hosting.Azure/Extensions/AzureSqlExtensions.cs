// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Provisioning.Sql;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding the Azure SQL resources to the application model.
/// </summary>
public static class AzureSqlExtensions
{
    internal static IResourceBuilder<SqlServerServerResource> PublishAsAzureSqlDatabase(this IResourceBuilder<SqlServerServerResource> builder, Action<IResourceBuilder<AzureSqlServerResource>, ResourceModuleConstruct, SqlServer, IEnumerable<SqlDatabase>>? configureResource = null, bool useProvisioner = false)
    {
        var configureConstruct = (ResourceModuleConstruct construct) =>
        {
            var sqlServer = new SqlServer(construct, builder.Resource.Name);

            sqlServer.AssignProperty(x => x.Administrators.AdministratorType, "'ActiveDirectory'");
            sqlServer.AssignProperty(x => x.Administrators.IsAzureADOnlyAuthenticationEnabled, "true");
            sqlServer.AssignProperty(x => x.Administrators.Sid, construct.PrincipalIdParameter);
            sqlServer.AssignProperty(x => x.Administrators.Login, construct.PrincipalNameParameter);
            sqlServer.AssignProperty(x => x.Administrators.TenantId, "subscription().tenantId");

            sqlServer.Properties.Tags["aspire-resource-name"] = construct.Resource.Name;

            var azureServicesFirewallRule = new SqlFirewallRule(construct, sqlServer, "AllowAllAzureIps");
            azureServicesFirewallRule.AssignProperty(x => x.StartIPAddress, "'0.0.0.0'");
            azureServicesFirewallRule.AssignProperty(x => x.EndIPAddress, "'0.0.0.0'");

            if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
            {
                // When in run mode we inject the users identity and we need to specify
                // the principalType.
                sqlServer.AssignProperty(x => x.Administrators.PrincipalType, construct.PrincipalTypeParameter);

                var sqlFirewall = new SqlFirewallRule(construct, sqlServer);
                sqlFirewall.AssignProperty(x => x.StartIPAddress, "'0.0.0.0'");
                sqlFirewall.AssignProperty(x => x.EndIPAddress, "'255.255.255.255'");
            }

            List<SqlDatabase> sqlDatabases = new List<SqlDatabase>();
            foreach (var databaseNames in builder.Resource.Databases)
            {
                var databaseName = databaseNames.Value;
                var sqlDatabase = new SqlDatabase(construct, sqlServer, databaseName);
                sqlDatabases.Add(sqlDatabase);
            }

            sqlServer.AddOutput("sqlServerFqdn", x => x.FullyQualifiedDomainName);

            if (configureResource != null)
            {
                var resource = (AzureSqlServerResource)construct.Resource;
                var resourceBuilder = builder.ApplicationBuilder.CreateResourceBuilder(resource);
                configureResource(resourceBuilder, construct, sqlServer, sqlDatabases);
            }
        };

        var resource = new AzureSqlServerResource(builder.Resource, configureConstruct);
        var azureSqlDatabase = builder.ApplicationBuilder.CreateResourceBuilder(resource);
        azureSqlDatabase.WithParameter(AzureBicepResource.KnownParameters.PrincipalId)
                        .WithParameter(AzureBicepResource.KnownParameters.PrincipalName)
                        .WithManifestPublishingCallback(resource.WriteToManifest);

        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            azureSqlDatabase.WithParameter(AzureBicepResource.KnownParameters.PrincipalType);
        }

        if (useProvisioner)
        {
            // Used to hold a reference to the azure surrogate for use with the provisioner.
            builder.WithAnnotation(new AzureBicepResourceAnnotation(resource));
            builder.WithConnectionStringRedirection(resource);

            // Remove the container annotation so that DCP doesn't do anything with it.
            if (builder.Resource.Annotations.OfType<ContainerImageAnnotation>().SingleOrDefault() is { } containerAnnotation)
            {
                builder.Resource.Annotations.Remove(containerAnnotation);
            }
        }

        return builder;
    }

    /// <summary>
    /// Configures SQL Server resource to be deployed as Azure SQL Database (server).
    /// </summary>
    /// <param name="builder">The builder for the SQL Server resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<SqlServerServerResource> PublishAsAzureSqlDatabase(this IResourceBuilder<SqlServerServerResource> builder)
    {
#pragma warning disable CA2252 // This API requires opting into preview features
        return builder.PublishAsAzureSqlDatabase((_, _, _, _) => { });
#pragma warning restore CA2252 // This API requires opting into preview features
    }

    /// <summary>
    /// Configures SQL Server resource to be deployed as Azure SQL Database (server).
    /// </summary>
    /// <param name="builder">The builder for the SQL Server resource.</param>
    /// <param name="configureResource">Callback to customize the Azure resources that will be provisioned in Azure.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [RequiresPreviewFeatures]
    public static IResourceBuilder<SqlServerServerResource> PublishAsAzureSqlDatabase(this IResourceBuilder<SqlServerServerResource> builder, Action<IResourceBuilder<AzureSqlServerResource>, ResourceModuleConstruct, SqlServer, IEnumerable<SqlDatabase>>? configureResource = null)
    {
        return builder.PublishAsAzureSqlDatabase(configureResource, useProvisioner: false);
    }

    /// <summary>
    /// Configures SQL Server resource to be deployed as Azure SQL Database (server).
    /// </summary>
    /// <param name="builder">The builder for the SQL Server resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<SqlServerServerResource> AsAzureSqlDatabase(this IResourceBuilder<SqlServerServerResource> builder)
    {
#pragma warning disable CA2252 // This API requires opting into preview features
        return builder.AsAzureSqlDatabase((_, _, _, _) => { });
#pragma warning restore CA2252 // This API requires opting into preview features
    }

    /// <summary>
    /// Configures SQL Server resource to be deployed as Azure SQL Database (server).
    /// </summary>
    /// <param name="builder">The builder for the SQL Server resource.</param>
    /// <param name="configureResource">Callback to customize the Azure resources that will be provisioned in Azure.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [RequiresPreviewFeatures]
    public static IResourceBuilder<SqlServerServerResource> AsAzureSqlDatabase(this IResourceBuilder<SqlServerServerResource> builder, Action<IResourceBuilder<AzureSqlServerResource>, ResourceModuleConstruct, SqlServer, IEnumerable<SqlDatabase>>? configureResource = null)
    {
        return builder.PublishAsAzureSqlDatabase(configureResource, useProvisioner: true);
    }
}
