using System.ComponentModel;
using Core.Dto;
using Core.Services;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

public sealed class LinkkiTool
{
    private readonly LinkkiService _linkkiService;

    public LinkkiTool(LinkkiService linkkiService)
    {
        _linkkiService = linkkiService;
    }

    [McpServerTool(Name = "get_location", Destructive = false, ReadOnly = true)]
    [Description("Gets the current location and status of buses operating on the specified line number.")]
    [return:
        Description(
            "Returns detailed information about buses currently operating on the specified line, including position, speed, direction, and destination.")]
    public async Task<List<LinkkiLocationDetails>> GetLocationAsync(
        [Description("The bus line number or name to search for.")]
        string lineName)
    {
        return await _linkkiService.GetLocationAsync(lineName);
    }

    [McpServerTool(Name = "get_closest_bus_stops", Destructive = false, ReadOnly = true)]
    [Description(
        "Gets the closest bus stops to the given location and within the given distance. The default distance is 300 meters.")]
    [return: Description("Returns the bus stops.")]
    public async Task<List<BusStopLocationDetails>> GetClosestBusStopAsync(
        double longitude, double latitude,
        double distance = 300)
    {
        return await _linkkiService.GetClosestBusStopAsync(longitude, latitude, distance);
    }

    [McpServerTool(Name = "get_bus_stop_names", Destructive = false, ReadOnly = true)]
    [Description(
        "Gets the bus stops names for the given line and tripId.")]
    [return: Description("Returns the bus stops names.")]
    public List<string>? GetBusStops(string lineName, string tripId)
    {
        return _linkkiService.GetBusStops(lineName, tripId);
    }

    [McpServerTool(Name = "get_available_lines", Destructive = false, ReadOnly = true)]
    [Description("Gets the available lines.")]
    [return: Description("Returns the available lines.")]
    public async Task<string[]> GetAvailableLinesAsync()
    {
        return await _linkkiService.GetAvailableLinesAsync();
    }

    [McpServerTool(Name = "get_bus_stop_details_by_name", Destructive = false, ReadOnly = true)]
    [Description(
        "Gets detailed information about a bus stop including its location.")]
    [return:
        Description(
            "Returns complete details about the bus stop including coordinates, distance from user or bus.")]
    public async Task<BusStopLocationDetails?> GetBusStopDetailsByNameAsync(
        [Description("The name of the bus stop to search for")]
        string busStopName,
        [Description("User's or bus line current longitude (0 if unavailable)")]
        double longitude = 0,
        [Description("User's or bus line  current latitude (0 if unavailable)")]
        double latitude = 0)
    {
        return await _linkkiService.GetBusStopDetailsByNameAsync(busStopName, longitude, latitude);
    }

    [McpServerTool(Name = "get_bus_arrival_times", Destructive = false, ReadOnly = true)]
    [Description("Gets upcoming arrival times for buses at a specific stop and line name.")]
    [return:
        Description(
            "Returns a list of upcoming bus arrivals with line name, trip, stop name, arrival time and minutes until arrival.")]
    public List<BusArrival> GetBusArrivalTimes(
        [Description("Name of the bus stop")] string busStopName,
        [Description("Line name")] string lineName)
    {
        return _linkkiService.GetBusArrivalTimes(busStopName, lineName);
    }
}