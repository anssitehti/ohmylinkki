

interface Route {
    id: string;
    lineName: string;
    busStops: TripBusStop[];
}

interface TripBusStop {
    tripId: string
    stops: BusStopDetails[]
}

interface BusStopDetails {
    id: string;
    name: string;
    //stopSequence: number;
    arrivalTime: string;
    //departureTime: string;
}

interface Trip {
    routeId: string;
    tripId: string;
    direction: number
}

interface StopTime {
    tripId: string;
    stopId: string;
    stopSequence: number;
    arrivalTime: string;
    departureTime: string;
}

interface BusStop {
    id: string;
    name: string;
    type: string;
    location: {
        type: string,
        coordinates: [number, number]
    };
}

async function main() {

    const routeCsv = await Bun.file("./routes.txt").text();
    const tripsCsv = await Bun.file("./trips.txt").text();
    const stopTimesCsv = await Bun.file("./stop_times.txt").text();
    const stopsCsv = await Bun.file("./stops.txt").text();


    const stops: BusStop[] = stopsCsv.split("\n").slice(1) // Skip header 
        .filter(line => line.trim())
        .map(line => {
            const [stopId, stopCode, stopName, lat, lon] = line
                .replace(/"/g, '')
                .split(',');
            return {
                id: stopId,
                name: stopName,
                type: "stop",
                location: {
                    type: "Point",
                    coordinates: [parseFloat(lon), parseFloat(lat)]
                }
            } as BusStop;
        });

    const stopTimes: StopTime[] = stopTimesCsv.split("\n")
        .slice(1) // Skip header
        .filter(line => line.trim())
        .map(line => {
            const [tripId, arrivalTime, departureTime, stopId, stopSequence] = line
                .replace(/"/g, '')
                .split(',');
            return { tripId, stopId: stopId, arrivalTime, departureTime, stopSequence: parseInt(stopSequence) } as StopTime;
        });

    const trips: Trip[] = tripsCsv
        .split("\n")
        .slice(1) // Skip header
        .filter(line => line.trim())
        .map(line => {
            const [routeId, serviceId, tripId, tripHeadsign, direction] = line
                .replace(/"/g, '')
                .split(',');
            return { routeId: routeId, tripId, direction: parseInt(direction) } as Trip;
        });

    const routes: Route[] = routeCsv
        .split("\n")
        .slice(1) // Skip header
        .filter(line => line.trim())
        .map(line => {
            const [routeId, agency, lineName] = line
                .replace(/"/g, '')
                .split(',');

            return { id: routeId, lineName, busStops: [] } as Route;
        });

    await writeStops(stops);

    await writeRoutes(routes, trips, stopTimes, stops);

    console.log("✅ Done!");

}

async function writeStops(stops: BusStop[]) {
    await Bun.write("./stops.json", JSON.stringify(stops, null, 2)
    );
}

function writeRoutes(routes: Route[], trips: Trip[], stopTimes: StopTime[], stops: BusStop[]) {

    for (const route of routes) {
        const routeTrips = trips.filter(trip => trip.routeId === route.id);

        for (const trip of routeTrips) {
            const tripStops = stopTimes.filter(stopTime => stopTime.tripId === trip.tripId);
            const busStops: TripBusStop = {
                tripId: trip.tripId,
                stops: tripStops.map(stopTime => {
                    const stop = stops.find(s => s.id === stopTime.stopId);
                    return {
                        id: stop?.id,
                        name: stop?.name,
                        //stopSequence: stopTime.stopSequence,
                        arrivalTime: stopTime.arrivalTime,
                        //departureTime: stopTime.departureTime
                    } as BusStopDetails;
                })
            }
            route.busStops.push(busStops);
        }
    }

    const fileCount= 5;
    const routesPerFile = Math.ceil(routes.length / fileCount);
    
    console.warn(`Writing ${routes.length} routes to ${fileCount} files so that each file has ${routesPerFile} routes`);

    for (let i = 0; i < fileCount; i++) {
        const start = i * routesPerFile;
        const end = start + routesPerFile;
        const fileRoutes = routes.slice(start, end);
        console.warn(`Writing routes ${start} to ${end}`);
        Bun.write(`./routes-${i+1}.json`, JSON.stringify(fileRoutes, null, 2));
    }


    //return Bun.write("./routes.json", JSON.stringify(routes, null, 2)
    
}



main();
