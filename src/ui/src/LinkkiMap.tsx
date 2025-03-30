import { useCallback, useEffect, useRef, useState } from 'react';
import maplibregl from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css'
import useWebSocket from 'react-use-websocket';
import linkkiMapIcon from "./assets/linkki-map-icon.svg";
import { createRoot } from 'react-dom/client';
import { TbBusStop } from 'react-icons/tb';


interface LinkkiPoint {
  id: string,
  line: string;
  coordinates: [number, number];
  bearing: number;
}

interface BusStop {
  id: string;
  name: string;
  coordinates: [number, number];
}

interface WebSocketEvent {
  type: 'linkki-location' | 'show-bus-stop' | 'filter-bus-lines';
  data: LinkkiPoint[] | BusStop | string[];
}

function LinkkiMap({ userId }: { userId: string }) {

  const mapRef = useRef<maplibregl.Map | null>(null);
  const markerRef = useRef<maplibregl.Marker | null>(null); // useRef to hold the marker instance
  const [busLinesToShow, setBusLinesToShow] = useState<string[]>([]);

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

    map.addControl(
      new maplibregl.GeolocateControl({
        positionOptions: {
          enableHighAccuracy: true
        },
        trackUserLocation: true
      })
    );

    mapRef.current = map;

    map.on('load', async () => {

      map.addSource('points', {
        type: 'geojson',
        data: {
          type: 'FeatureCollection',
          features: []
        }
      });

      const svgImage = new Image(35, 35);
      svgImage.src = linkkiMapIcon;
      svgImage.onload = () => {
        map.addImage('linkki-icon', svgImage)
      }

      map.addLayer({
        id: 'points',
        type: 'symbol',
        source: 'points',
        layout: {
          'icon-image': 'linkki-icon',
          'icon-size': 1,
          'icon-allow-overlap': true,
          'icon-rotate': ['get', 'bearing'],
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
    const event: WebSocketEvent = lastJsonMessage as WebSocketEvent;
    if (!event) return;
    if (event.type === 'linkki-location') {
      const data: LinkkiPoint[] = (event.data as LinkkiPoint[]).filter(point => {
        if (busLinesToShow.length === 0) return true;
        return busLinesToShow.includes(point.line);
      });

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
                  coordinates: point.coordinates
                },
                properties: {
                  id: point.id,
                  description: point.line,
                  bearing: point.bearing
                }
              }))
            });
          }
        }
      });
    }
    
    if (event.type === 'filter-bus-lines') {
      setBusLinesToShow(event.data as string[]);
    }

    if (event.type === 'show-bus-stop') {
      const data: BusStop = event.data as BusStop;
      if (mapRef.current) {

        if (markerRef.current) {
          markerRef.current.remove();
        }

        const markerEl = document.createElement('div')
        const root = createRoot(markerEl)
        root.render(
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
            <TbBusStop size={40} color="black" />
            <div className="label" style={{ marginTop: '5px', textAlign: 'center' }}>{data.name}</div>
          </div>
        )


        const marker = new maplibregl.Marker({ element: markerEl })
          .setLngLat(data.coordinates)
          .addTo(mapRef.current);

        markerRef.current = marker;

        mapRef.current.flyTo({
          center: data.coordinates,
          zoom: 16,
          essential: true
        });

        setTimeout(() => {
          if (markerRef.current) {
            markerRef.current.remove();
            markerRef.current = null; // Clear the ref
          }
          mapRef.current?.flyTo({
            center: [25.7473, 62.2426],
            zoom: 11,
            essential: true
          });
        }, 10000);
      }

    }
  }, [lastJsonMessage])

  return (
    <div className="h-[600px] md:h-[800px] bg-gray-50 rounded-lg shadow-sm p-6">
      <div id="map" className="w-full h-full rounded-lg" />
    </div>
  )
}

export default LinkkiMap