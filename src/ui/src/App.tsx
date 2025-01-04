import { useState, useEffect } from 'react'
import './App.css'

// Add interface for message structure
interface Message {
  text: string;
  timestamp: Date;
  isUser: boolean;
}

function App() {
  const [message, setMessage] = useState('')
  const [messages, setMessages] = useState<Message[]>([])
  const [socket, setSocket] = useState<WebSocket | null>(null)
  const [userId] = useState(() => Math.random().toString(36).substring(2, 15))

  useEffect(() => {
    const initializeConnection = async () => {
      try {
        // Negotiate connection
        console.log(`userId=${userId}`)
        const response = await fetch(`api/negotiate?id=${userId}`)

        const data = await response.json()

        // Create WebSocket connection
        const ws = new WebSocket(data.url)

        ws.onopen = () => {
          console.log('Connected to WebSocket')
        }

        ws.onmessage = (event) => {
          const message = event.data;
          //TODO update mapp
          console.log('Received message:', message)
        }

        ws.onclose = () => {
          console.log('Disconnected from WebSocket')
        }

        setSocket(ws)
      } catch (error) {
        console.error('ws connection failed:', error)
      }
    }

    initializeConnection()

    // Cleanup on unmount
    return () => {
      if (socket) {
        socket.close()
      }
    }
  }, [userId])

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
    <div className="h-screen flex flex-col bg-gray-100">
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
      
      <main className="flex-1 p-8">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-8 h-full">
          <div className="md:col-span-2 flex flex-col bg-white rounded-xl shadow-sm overflow-hidden">
            <div className="flex-1 overflow-y-auto p-6 space-y-8">
              {messages.map((msg, index) => (
                <div key={index} className={`flex ${msg.isUser ? 'justify-end' : 'justify-start'}`}>
                  {!msg.isUser && (
                    <div className="w-8 h-8 rounded-full bg-blue-500 flex items-center justify-center mr-3">
                      <span className="text-white text-sm">AI</span>
                    </div>
                  )}
                  <div
                    className={`p-4 rounded-2xl max-w-[70%] shadow-sm ${
                      msg.isUser 
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
            </div>

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

          <div className="h-full bg-gray-50 rounded-lg shadow-sm p-6">
            <div className="h-full w-full">
              Map will go here
            </div>
          </div>
        </div>
      </main>
    </div>
  )
}

export default App
