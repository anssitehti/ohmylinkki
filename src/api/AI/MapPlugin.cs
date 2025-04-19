using System.ComponentModel;
using Core.Dto;
using Core.Services;
using Microsoft.SemanticKernel;


namespace Api.AI;

public class MapPlugin
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    private readonly LinkkiHubService _linkkiHubService;

    public MapPlugin(LinkkiHubService linkkiHubService,
        IHttpContextAccessor httpContextAccessor)
    {
        _linkkiHubService = linkkiHubService;
        _httpContextAccessor = httpContextAccessor;
    }
    
    [KernelFunction("filter_bus_lines_on_map")]
    [Description(
        "Filters the bus lines shown on the map.")]
    private async Task FilterBusLinesOnMapAsync(
        [Description("The bus line names to filter on the map")]
        List<string> lineNames)
    {
        var userId = _httpContextAccessor.HttpContext?.Items["userId"] as string;
        if (lineNames.Count == 0 || string.IsNullOrEmpty(userId))
        {
            return;
        }

        await _linkkiHubService.FilterBusLinesOnMapAsync(userId, lineNames);
    }

    [KernelFunction("show_bus_stop_on_map")]
    [Description(
        "Shows a fixed bus stop location on the map. This is only for displaying stationary bus stops, not for tracking moving bus vehicles.")]
    private async Task ShowBusStopOnMapAsync(
        [Description("The name of the bus stop (not a bus or vehicle)")]
        string busStopName,
        [Description("The longitude coordinate of the fixed bus stop")]
        double longitude,
        [Description("The latitude coordinate of the fixed bus stop")]
        double latitude)
    {
        var userId = _httpContextAccessor.HttpContext?.Items["userId"] as string;
        if (longitude == 0 || latitude == 0 || string.IsNullOrEmpty(userId))
        {
            return;
        }

        await _linkkiHubService.ShowBusStopOnMapAsync(userId, busStopName, longitude, latitude);
    }
}