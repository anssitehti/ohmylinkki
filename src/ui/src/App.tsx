import { useState, useEffect, useRef } from 'react'
import './App.css'
import maplibregl from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css'
import { ToastContainer, toast } from 'react-toastify';
import 'react-toastify/dist/ReactToastify.css';

interface Message {
  text: string;
  timestamp: Date;
  isUser: boolean;
}

interface LinkkiPoint {
  id: string,
  line: string;
  location: { type: string, coordinates: [number, number] };
}

function App() {
  const [message, setMessage] = useState('')
  const [messages, setMessages] = useState<Message[]>([])
  const [socket, setSocket] = useState<WebSocket | null>(null)
  const [userId] = useState(() => crypto.randomUUID())
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const [_mapPoints, setMapPoints] = useState<LinkkiPoint[]>([]);
  const mapRef = useRef<maplibregl.Map | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const initializeConnection = async () => {
      try {

        const response = await fetch(`api/negotiate?id=${userId}`)

        const data = await response.json()

        const ws = new WebSocket(data.url)

        ws.onmessage = (event) => {
          const data: LinkkiPoint[] = JSON.parse(event.data);

          setMapPoints(data);

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
        }

        setSocket(ws)
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
      } catch (error) {
        toast.error('Connection failure. Please refresh the page.')
      }
    }

    initializeConnection()

    return () => {
      if (socket) {
        socket.close()
      }
    }
  }, [socket, userId])

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
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  const handleSend = async () => {
    if (message.trim() && socket) {
      const userMessage: Message = {
        text: message,
        timestamp: new Date(),
        isUser: true
      };
      setMessages(prev => [...prev, userMessage]);
      setMessage('');

      const response = await fetch(`api/chat?message=${message}&userId=${userId}`);
      if (!response.ok) {
        toast.error('Failed to send message...');
        return;
      }

      const json = await response.json();

      const aiMessage: Message = {
        text: json.message,
        timestamp: new Date(),
        isUser: false
      };
      setMessages(prev => [...prev, aiMessage]);
    }
  }

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
  }

  return (
    <div className="flex flex-col bg-gray-100">
      <header className="bg-gradient-to-r from-blue-500 to-blue-600 text-white p-6 flex items-center justify-center gap-3 shadow-sm rounded-xl">
        <img
          src="/linkki-logo.png"
          alt="Linkki Logo"
          className="h-10 w-auto"
        />
        <h1 className="text-2xl font-bold tracking-tight">
          OhMyLinkki <span className="text-sm font-normal opacity-75">AI Assistant</span>
        </h1>
      </header>

      <main className="p-8">
        <div className="flex gap-8">
          {/* Chat container */}
          <div className="w-[600px] h-[800px] flex flex-col bg-white rounded-xl shadow-sm">
            {/* Scrollable messages area */}
            <div className="flex-1 overflow-y-auto p-6 space-y-8 h-full">
              {messages.map((msg, index) => (
                <div key={index} className={`flex ${msg.isUser ? 'justify-end' : 'justify-start'}`}>
                  {!msg.isUser && (
                    <div className="w-8 h-8 rounded-full bg-blue-500 flex items-center justify-center mr-3">
                      <span className="text-white text-sm">AI</span>
                    </div>
                  )}
                  <div
                    className={`p-4 rounded-2xl max-w-[70%] shadow-sm ${msg.isUser
                      ? 'bg-blue-500 text-white'
                      : 'bg-gray-100'
                      }`}
                  >
                    <div>{msg.text}</div>
                    <div className={`text-xs mt-1 ${msg.isUser ? 'text-blue-100' : 'text-gray-500'}`}>
                      {msg.timestamp.toLocaleTimeString()}
                    </div>
                  </div>
                </div>
              ))}
              <div ref={messagesEndRef} />
            </div>

            {/* Fixed input area */}
            <div className="p-6 bg-gray-50 border-t">
              <div className="flex gap-4">
                <input
                  type="text"
                  value={message}
                  onChange={(e) => setMessage(e.target.value)}
                  onKeyDown={handleKeyPress}
                  className="flex-1 w-full p-4 border rounded-lg bg-white focus:outline-none focus:ring-2 focus:ring-blue-400"
                  placeholder="Type your message..."
                />
                <button
                  onClick={handleSend}
                  className="px-6 py-4 bg-blue-500 text-white rounded-lg hover:bg-blue-600 transition-colors shadow-sm"
                >
                  Send
                </button>
              </div>
            </div>
          </div>

          {/* Map container */}
          <div className="h-[800px] flex-1 bg-gray-50 rounded-lg shadow-sm p-6">
            <div id="map" className="w-full h-full rounded-lg" />
          </div>
        </div>
      </main>
      <ToastContainer />
    </div>
  )
}

export default App
