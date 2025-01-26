using System.Collections.ObjectModel;

public class BusStop {

    public string StopId { get; set; }
    public string Name { get; set; }
    public ReadOnlyCollection<double> Coordinates { get; set; }
}