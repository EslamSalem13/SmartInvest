using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartInvest.Application.Common;

namespace SmartInvest.Infrastructure.Services;

public class PlanApprovalNotificationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PlanApprovalNotificationOptions _options;
    private readonly ILogger<PlanApprovalNotificationWorker> _logger;

    public PlanApprovalNotificationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<PlanApprovalNotificationOptions> options,
        ILogger<PlanApprovalNotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Plan approval notification worker is disabled");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Clamp(_options.PollingIntervalSeconds, 2, 300));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<PlanApprovalNotificationProcessor>();
                await processor.ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Plan approval notification worker cycle failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
