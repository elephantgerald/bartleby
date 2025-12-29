using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using Bartleby.Core.Interfaces;
using Bartleby.Infrastructure.Persistence;
using Bartleby.Infrastructure.WorkSources;
using Bartleby.Infrastructure.AIProviders;
using Bartleby.Infrastructure.Graph;
using Bartleby.Services;
using Bartleby.Services.Prompts;
using Bartleby.App.Views;
using Bartleby.App.ViewModels;

namespace Bartleby.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register Database Context (Singleton)
        builder.Services.AddSingleton<LiteDbContext>(_ =>
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "Bartleby.db");
            return new LiteDbContext(dbPath);
        });

        // Register Repositories
        builder.Services.AddSingleton<IWorkItemRepository, WorkItemRepository>();
        builder.Services.AddSingleton<IBlockedQuestionRepository, BlockedQuestionRepository>();
        builder.Services.AddSingleton<IWorkSessionRepository, WorkSessionRepository>();
        builder.Services.AddSingleton<ISettingsRepository, SettingsRepository>();

        // Register Infrastructure Services (using stubs for MVP)
        builder.Services.AddSingleton<IWorkSource, StubWorkSource>();
        builder.Services.AddSingleton<IAIProvider, StubAIProvider>();
        builder.Services.AddSingleton<IGraphStore>(_ =>
        {
            var graphPath = Path.Combine(FileSystem.AppDataDirectory, "dependencies.puml");
            return new PlantUmlGraphStore(graphPath);
        });

        // Register Application Services
        builder.Services.AddSingleton<IPromptTemplateProvider, PromptTemplateProvider>();
        builder.Services.AddSingleton<IDependencyResolver, DependencyResolver>();
        builder.Services.AddSingleton<IWorkExecutor, WorkExecutor>();
        builder.Services.AddSingleton<IOrchestratorService, OrchestratorService>();

        // Register ViewModels
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<WorkItemsViewModel>();
        builder.Services.AddTransient<BlockedViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Register Views
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<WorkItemsPage>();
        builder.Services.AddTransient<BlockedPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
