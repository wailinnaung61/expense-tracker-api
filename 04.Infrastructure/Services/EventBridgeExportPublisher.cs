using System.Text.Json;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Infrastructure.AWS.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace _04.Infrastructure.Services;

public class EventBridgeExportPublisher : IExportEventPublisher
{
    private readonly IAmazonEventBridge _eventBridge;
    private readonly AwsSettings _aws;
    private readonly ILogger<EventBridgeExportPublisher> _logger;

    public EventBridgeExportPublisher(
        IAmazonEventBridge eventBridge,
        IOptions<AwsSettings> awsOptions,
        ILogger<EventBridgeExportPublisher> logger)
    {
        _eventBridge = eventBridge;
        _aws = awsOptions.Value;
        _logger = logger;
    }

    public async Task PublishExportRequestedAsync(ExportEventDetail detail)
    {
        var response = await _eventBridge.PutEventsAsync(new PutEventsRequest
        {
            Entries =
            [
                new PutEventsRequestEntry
                {
                    EventBusName = _aws.EventBridge.EventBusName,   // "default"
                    Source       = _aws.EventBridge.Source,           // "expense-tracker.export"
                    DetailType   = "ExportRequested",
                    Detail       = JsonSerializer.Serialize(detail)
                }
            ]
        });

        if (response.FailedEntryCount > 0)
        {
            _logger.LogError("EventBridge PutEvents failed: {Errors}",
                JsonSerializer.Serialize(response.Entries));
            throw new InvalidOperationException("Failed to publish export event to EventBridge.");
        }

        _logger.LogInformation("Export event published to EventBridge for job {JobId}", detail.JobId);
    }
}
