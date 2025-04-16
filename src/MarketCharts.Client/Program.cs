using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MarketCharts.Client;
using MarketCharts.Client.Interfaces;
using MarketCharts.Client.Models;
using MarketCharts.Client.Models.Chart;
using MarketCharts.Client.Models.Configuration;
using MarketCharts.Client.Services.ApiServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register HttpClient
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register configuration
builder.Services.AddScoped(sp => 
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    try
    {
        var config = httpClient.GetFromJsonAsync<AppConfiguration>("config/appsettings.json").Result;
        return config ?? new AppConfiguration();
    }
    catch
    {
        return new AppConfiguration();
    }
});

// Register API configuration
builder.Services.AddScoped<ApiConfiguration>(sp => 
{
    var appConfig = sp.GetRequiredService<AppConfiguration>();
    return appConfig.ApiConfig;
});

// Register HTTP clients
builder.Services.AddHttpClient();

// Register API services
builder.Services.AddScoped<PrimaryApiService>(sp => 
{
    var httpClient = new HttpClient { BaseAddress = new Uri("https://www.alphavantage.co/") };
    var apiConfig = sp.GetRequiredService<ApiConfiguration>();
    var logger = sp.GetRequiredService<ILogger<PrimaryApiService>>();
    return new PrimaryApiService(apiConfig, httpClient, logger);
});

builder.Services.AddScoped<BackupApiService>(sp => 
{
    var httpClient = new HttpClient { BaseAddress = new Uri("https://api.stockdata.org/") };
    var apiConfig = sp.GetRequiredService<ApiConfiguration>();
    var logger = sp.GetRequiredService<ILogger<BackupApiService>>();
    return new BackupApiService(apiConfig, httpClient, logger);
});

// Register API services as IStockApiService
builder.Services.AddScoped<IStockApiService>(sp => sp.GetRequiredService<PrimaryApiService>());
builder.Services.AddScoped<IStockApiService>(sp => sp.GetRequiredService<BackupApiService>());

// Register API service factory
builder.Services.AddScoped<IApiServiceFactory, ApiServiceFactory>(sp => 
{
    var primaryApiService = sp.GetRequiredService<PrimaryApiService>();
    var backupApiService = sp.GetRequiredService<BackupApiService>();
    var apiConfig = sp.GetRequiredService<ApiConfiguration>();
    var logger = sp.GetRequiredService<ILogger<ApiServiceFactory>>();
    return new ApiServiceFactory(
        primaryApiService, 
        backupApiService, 
        apiConfig, 
        logger);
});

// Register other services (interfaces will be implemented later)
builder.Services.AddScoped<IStockDataRepository, StockDataRepository>();
builder.Services.AddScoped<IStockDataService, StockDataService>();
builder.Services.AddScoped<IChartDataProcessor, ChartDataProcessor>();

await builder.Build().RunAsync();

// These are placeholder classes that will be implemented later
// They are defined here to allow the application to compile
namespace MarketCharts.Client
{

    public class StockDataRepository : IStockDataRepository
    {
        public Task<bool> BackupDatabaseAsync(string backupPath) => throw new NotImplementedException();
        public Task<bool> CompactDatabaseAsync() => throw new NotImplementedException();
        public void Dispose() { }
        public Task<StockIndexData?> GetLatestStockDataAsync(string indexName) => throw new NotImplementedException();
        public Task<StockIndexData?> GetStockDataByDateAndIndexAsync(DateTime date, string indexName) => throw new NotImplementedException();
        public Task<StockIndexData?> GetStockDataByIdAsync(int id) => throw new NotImplementedException();
        public Task<List<StockIndexData>> GetStockDataByDateRangeAsync(DateTime startDate, DateTime endDate, string? indexName = null) => throw new NotImplementedException();
        public Task<bool> InitializeSchemaAsync() => throw new NotImplementedException();
        public Task<bool> NeedsCompactionAsync() => throw new NotImplementedException();
        public Task<int> SaveStockDataAsync(StockIndexData data) => throw new NotImplementedException();
        public Task<int> SaveStockDataBatchAsync(List<StockIndexData> dataList) => throw new NotImplementedException();
        public Task<bool> UpdateStockDataAsync(StockIndexData data) => throw new NotImplementedException();
        public Task<bool> VerifyDatabaseIntegrityAsync() => throw new NotImplementedException();
    }

    public class StockDataService : IStockDataService
    {
        public bool AreMarketsClosed(DateTime date) => throw new NotImplementedException();
        public Task<bool> CheckAndUpdateDataAsync() => throw new NotImplementedException();
        public Task<List<StockIndexData>> FillDataGapsAsync(List<StockIndexData> data) => throw new NotImplementedException();
        public Task<Dictionary<string, List<StockIndexData>>> GetInaugurationToPresent() => throw new NotImplementedException();
        public DateTime? GetLastUpdateTime() => throw new NotImplementedException();
        public Task<Dictionary<string, StockIndexData>> GetLatestDataForAllIndices() => throw new NotImplementedException();
        public Task<Dictionary<string, List<StockIndexData>>> GetPreviousAdministrationData() => throw new NotImplementedException();
        public Task<Dictionary<string, List<StockIndexData>>> GetTariffAnnouncementToPresent() => throw new NotImplementedException();
        public Task InitializeAsync() => throw new NotImplementedException();
        public Task ScheduleDailyUpdatesAsync(TimeSpan updateTime) => throw new NotImplementedException();
        public Task<bool> VerifyDataConsistencyAsync(List<StockIndexData> data) => throw new NotImplementedException();
    }

    public class ChartDataProcessor : IChartDataProcessor
    {
        public ChartData ApplyTechnicalIndicators(ChartData data, List<string> indicators) => throw new NotImplementedException();
        public List<ChartDataSeries> AlignDataSeries(List<ChartDataSeries> series) => throw new NotImplementedException();
        public Dictionary<string, List<decimal>> CalculatePercentageChanges(Dictionary<string, List<StockIndexData>> data, DateTime baseDate) => throw new NotImplementedException();
        public List<TechnicalIndicator> CalculateMovingAverages(ChartDataSeries data, List<int> periods) => throw new NotImplementedException();
        public TechnicalIndicator CalculateRSI(ChartDataSeries data, int period = 14) => throw new NotImplementedException();
        public TechnicalIndicator CalculateVolatility(ChartDataSeries data, int period = 20) => throw new NotImplementedException();
        public ChartData FormatDataForChart(Dictionary<string, List<StockIndexData>> data, string title, DateTime startDate, DateTime endDate) => throw new NotImplementedException();
        public ChartData GenerateAnnotations(ChartData data, Dictionary<DateTime, string> events) => throw new NotImplementedException();
        public ChartData GenerateComparisonData(ChartData currentData, Dictionary<string, List<StockIndexData>> previousData) => throw new NotImplementedException();
        public List<string> GenerateChartLabels(DateTime startDate, DateTime endDate, int dataPoints) => throw new NotImplementedException();
        public ChartData HandleMissingDates(ChartData data) => throw new NotImplementedException();
        public List<string> IdentifyTrends(ChartDataSeries data) => throw new NotImplementedException();
        public List<ChartDataSeries> NormalizeData(List<ChartDataSeries> series) => throw new NotImplementedException();
        public ChartData OptimizeDataPoints(ChartData data, int maxPoints) => throw new NotImplementedException();
    }
}
