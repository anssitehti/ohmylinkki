import { useEffect, useRef, useState } from "react";
import { toast } from "react-toastify";
import { FaRobot, FaUserCircle, FaLocationArrow } from 'react-icons/fa';
import Markdown from 'react-markdown'
import remarkGfm from 'remark-gfm'

interface Message {
    text: string;
    timestamp: Date;
    isUser: boolean;
}

interface ChatMessageRequest {
    userId: string;
    message: string;
}


function LinkkiAiAssistant({ userId }: { userId: string }) {
    const [message, setMessage] = useState('')
    const [messages, setMessages] = useState<Message[]>([
        {
            text: 'Hello, how can I help you?',
            timestamp: new Date(),
            isUser: false
        }
    ]);
    const [isLoading, setIsLoading] = useState(false);
    const [isFetchingLocation, setIsFetchingLocation] = useState(false);
    const messagesEndRef = useRef<HTMLDivElement>(null);

    const sendMessage = async (message: string) => {
        const userMessage: Message = {
            text: message.trim(),
            timestamp: new Date(),
            isUser: true
        };
        setMessages(prev => [...prev, userMessage]);
        setMessage('');
        setIsLoading(true);

        const chatMessageRequest: ChatMessageRequest = {
            userId: userId,
            message: message.trim()
        }

        const response = await fetch(`api/chat`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(chatMessageRequest) });
        setIsLoading(false);
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
    };

    const handleSend = async () => {
        if (message.trim() === '') {
            return;
        }
        await sendMessage(message);
    }

    const handleKeyPress = (e: React.KeyboardEvent) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault()
            handleSend()
        }
    }
    const shareLocation = () => {
        setIsFetchingLocation(true);
        if (navigator.geolocation) {
            navigator.geolocation.getCurrentPosition(
                async (position) => {
                    setIsFetchingLocation(false);
                    const { latitude, longitude } = position.coords;
                    const locationMessage = `Here is my location: [${latitude},${longitude}]`;
                    await sendMessage(locationMessage);
                },
                (error) => {
                    console.error("Error fetching location:", error);
                }
            );
        } else {
            console.error("Geolocation is not supported by this browser.");
        }
        setIsFetchingLocation(false);
    };

    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
    }, [messages]);
    return (
        <div className="w-[600px] h-[800px] flex flex-col bg-white rounded-xl shadow-sm">
            {/* Scrollable messages area */}
            <div className="flex-1 overflow-y-auto p-6 space-y-8 h-full">
                {messages.map((msg, index) => (
                    <div key={index} className={`flex ${msg.isUser ? 'justify-end' : 'justify-start'}`}>
                        {!msg.isUser && (
                            <div className="w-8 h-8 rounded-full bg-gray-500 flex items-center justify-center mr-3">
                                <FaRobot className="text-white text-sm" />
                            </div>
                        )}
                        {msg.isUser && (
                            <div className="w-8 h-8 rounded-full bg-blue-500 flex items-center justify-center mr-3">
                                <FaUserCircle className="text-white text-sm" />
                            </div>
                        )}
                        <div
                            className={`p-4 rounded-2xl max-w-[70%] shadow-sm ${msg.isUser
                                ? 'bg-blue-500 text-white'
                                : 'bg-gray-100'
                                }`}
                        >
                            <div className="justify-left"><Markdown className="text-left" remarkPlugins={[remarkGfm]}>{msg.text}</Markdown></div>
                            <div className={`text-xs mt-1 ${msg.isUser ? 'text-blue-100' : 'text-gray-500'}`}>
                                {msg.timestamp.toLocaleTimeString()}
                            </div>
                        </div>
                    </div>
                ))}
                {isLoading && (
                    <div className="flex justify-start">
                        <div className="w-8 h-8 rounded-full bg-gray-500 flex items-center justify-center mr-3">
                            <FaRobot className="text-white text-sm" />
                        </div>
                        <div className="p-4 rounded-2xl max-w-[70%] shadow-sm bg-gray-100">
                            <span className="text-gray-500 italic text-sm">
                                AI Assistant is typing...
                            </span>
                        </div>
                    </div>
                )}
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
                        className={`px-6 py-4 rounded-lg transition-colors shadow-sm ${message.trim() === '' ? 'bg-gray-300 cursor-not-allowed' : 'bg-blue-500 text-white hover:bg-blue-600'}`}
                        disabled={message.trim() === ''}
                    >
                        Send
                    </button>
                    <button
                        onClick={shareLocation}
                        disabled={isFetchingLocation}
                        className="text-green-500 hover:text-green-700 p-2 rounded"
                        title="Share Location"
                    >
                        <FaLocationArrow className="w-5 h-5" />
                    </button>
                </div>
            </div>
        </div>
    );
}


export default LinkkiAiAssistant