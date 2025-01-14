import { useCallback, useEffect, useRef } from 'react';
import maplibregl from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css'
import useWebSocket from 'react-use-websocket';

interface LinkkiPoint {
  id: string,
  line: string;
  location: { type: string, coordinates: [number, number] };
}

function LinkkiMap({ userId }: { userId: string }) {

  const mapRef = useRef<maplibregl.Map | null>(null);


  const getSocketUrl = useCallback(async () => {
    const response = await fetch(`api/negotiate?id=${userId}`)
    const data = await response.json()
    return data.url;

  }, [userId]);

  const { lastJsonMessage } = useWebSocket(getSocketUrl, {
    shouldReconnect: () => true,
    retryOnError: true,
    reconnectAttempts: 100,
    reconnectInterval: (attemptNumber) =>
      Math.min(Math.pow(2, attemptNumber) * 1000, 10000),
  });

  useEffect(() => {
    const map = new maplibregl.Map({
      container: 'map',
      style: 'https://api.maptiler.com/maps/basic/style.json?key=Zmuer6TZwYpzssYfvTcK',
      center: [25.7473, 62.2426],
      zoom: 11
    });

    map.addControl(new maplibregl.NavigationControl(), 'top-right');

    mapRef.current = map;

    map.on('load', async () => {

      map.addSource('points', {
        type: 'geojson',
        data: {
          type: 'FeatureCollection',
          features: []
        }
      });

      map.addLayer({
        id: 'points',
        type: 'circle',
        source: 'points',
        paint: {
          'circle-radius': 15,
          'circle-color': '#129d2d',
          'circle-stroke-width': 1,
          'circle-stroke-color': '#fff'
        }
      });

      map.addLayer({
        id: 'point-labels',
        type: 'symbol',
        source: 'points',
        layout: {
          'text-field': ['get', 'description'],
          'text-justify': 'center',
        }
      });

    });

    return () => {
      map.remove();
    };
  }, []);



  useEffect(() => {
    const data: LinkkiPoint[] = lastJsonMessage as LinkkiPoint[];
    requestAnimationFrame(() => {
      if (mapRef.current) {
        const source = mapRef.current.getSource('points') as maplibregl.GeoJSONSource;
        if (source) {
          source.setData({
            type: 'FeatureCollection',
            features: data.map(point => ({
              type: 'Feature',
              geometry: {
                type: 'Point',
                coordinates: point.location.coordinates
              },
              properties: {
                id: point.id,
                description: point.line
              }
            }))
          });
        }
      }
    });
  }, [lastJsonMessage])


  return (
    <div className="h-[800px] flex-1 bg-gray-50 rounded-lg shadow-sm p-6">
      <div id="map" className="w-full h-full rounded-lg" />
    </div>
  )
}

export default LinkkiMap