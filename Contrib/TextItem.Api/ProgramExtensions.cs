﻿using Dapr.Extensions.Configuration;
using Dapr.Client;
using RecAll.Contrib.TextItem.Api.Data;
using Microsoft.EntityFrameworkCore;
using Polly;
using System.Security.Principal;
using RecAll.Contrib.TextItem.Api.Service;
using Serilog;
namespace RecAll.Contrib.TextItem.Api;

public static class ProgramExtensions
{
    public static readonly string AppName = typeof(ProgramExtensions).Namespace;

    public static void AddCustomConfiguration(this WebApplicationBuilder builder)
    {
        builder.Configuration.AddDaprSecretStore(
            "recall-secretstore",
            new DaprClientBuilder().Build());
    }

    public static void AddCustomSwagger(this WebApplicationBuilder builder) =>
        builder.Services.AddSwaggerGen();

    public static void AddCustomSerilog(this WebApplicationBuilder builder)
    {
        // seq服务器地址
        var seqServerUrl = builder.Configuration["serilog:SeqServerUrl"];

        Log.Logger = new LoggerConfiguration().ReadFrom
            .Configuration(builder.Configuration)
            .WriteTo.Console()
            .WriteTo.Seq(seqServerUrl)
            .Enrich.WithProperty("ApplicationName", AppName)
            .CreateLogger();

        builder.Host.UseSerilog();
    }

    public static void UseCustomSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }


    // public static void AddCustomDatabase(this WebApplicationBuilder builder)
    // {
    //     Console.WriteLine(builder.Configuration["ConnectionString:TextItemContext"]);
    //     builder.Services.AddDbContext<TextItemContext>(
    //         p => p.UseSqlServer(builder.Configuration["ConnectionString:TextItemContext"])
    //     );
    // }
    public static void AddCustomApplicationServices(this WebApplicationBuilder bulider)
    {
        bulider.Services.AddScoped<IIdentityService, MockIndentityService>();
    }

    public static void AddCustomDatabase(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<TextItemContext>(p =>
            p.UseSqlServer(
                builder.Configuration["ConnectionStrings:TextItemContext"]));
    }

    public static void ApplyDatabaseMigration(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var retryPolicy = CreateRetryPolicy();
        var context =
            scope.ServiceProvider.GetRequiredService<TextItemContext>();

        retryPolicy.Execute(context.Database.Migrate);
    }

    private static Policy CreateRetryPolicy()
    {
        return Policy.Handle<Exception>().WaitAndRetryForever(
            sleepDurationProvider: _ => TimeSpan.FromSeconds(5),
            onRetry: (exception, retry, _) =>
        {
            Console.WriteLine(
                "Exception {0} with message {1} detected during database migration (retry attempt {2})",
               exception.GetType().Name, exception.Message, retry);
        });
    }
}
