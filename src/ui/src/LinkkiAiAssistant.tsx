import { useEffect, useRef, useState } from "react";
import { toast } from "react-toastify";
import { FaRobot, FaUserCircle, FaArrowUp, FaTrash } from 'react-icons/fa';
import Markdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { FaLocationCrosshairs } from "react-icons/fa6";

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
    const [isWaitingAi, setWaitingAi] = useState(false);
    const [isFetchingUserLocation, setIsFetchingUserLocation] = useState(false);
    const [isClearing, setIsClearing] = useState(false);
    const messagesEndRef = useRef<HTMLDivElement>(null);

    const sendMessage = async (message: string) => {
        const userMessage: Message = {
            text: message.trim(),
            timestamp: new Date(),
            isUser: true
        };
        setMessages(prev => [...prev, userMessage]);
        setMessage('');
        setWaitingAi(true);

        const chatMessageRequest: ChatMessageRequest = {
            userId: userId,
            message: message.trim()
        }

        const response = await fetch(`api/chat`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(chatMessageRequest) });
        setWaitingAi(false);
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

        if (navigator.geolocation) {
            setIsFetchingUserLocation(true);
            navigator.geolocation.getCurrentPosition(
                async (position) => {
                    setIsFetchingUserLocation(false);
                    const { longitude, latitude } = position.coords;

                    const locationMessage = `My location is: Longitude: ${longitude}, Latitude: ${latitude}]`;
                    await sendMessage(locationMessage);
                },
                (error) => {
                    setIsFetchingUserLocation(false);
                    console.error("Error fetching location:", error);
                }
            );
        } else {
            console.error("Geolocation is not supported by this browser.");
        }
    };

    const clearChatHistory = async () => {
        setIsClearing(true);
        try {
            const response = await fetch(`api/clear-chat-history`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ userId })
            });

            if (!response.ok) {
                throw new Error('Failed to clear chat history');
            }

            // Reset messages to initial state
            setMessages([{
                text: 'Hello, how can I help you?',
                timestamp: new Date(),
                isUser: false
            }]);

        } catch {
            toast.error('Failed to clear chat history');
        } finally {
            setIsClearing(false);
        }
    };

    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
    }, [messages]);
    return (
        <div className="h-[500px] md:h-[800px] flex flex-col bg-white rounded-xl shadow-sm overflow-hidden">
            {/* Scrollable messages area */}
            <div className="flex-1 overflow-y-auto p-6 space-y-8">
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
                {isWaitingAi && (
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
                {isFetchingUserLocation && (
                    <div className="flex justify-end">
                        <div className="w-8 h-8 rounded-full bg-gray-500 flex items-center justify-center mr-3">
                            <FaUserCircle className="text-white text-sm" />
                        </div>
                        <div className="p-4 rounded-2xl max-w-[70%] shadow-sm bg-gray-100">
                            <span className="text-gray-500 italic text-sm">
                                Trying to get my location...
                            </span>
                        </div>
                    </div>
                )}
                <div ref={messagesEndRef} />
            </div>

            {/* Fixed input area */}
            <div className="p-6 bg-gray-50 border-t">
                <div className="flex flex-col lg:flex-row gap-4">
                    <input
                        type="text"
                        value={message}
                        onChange={(e) => setMessage(e.target.value)}
                        onKeyDown={handleKeyPress}
                        className="w-full p-4 border rounded-lg bg-white focus:outline-none focus:ring-2 focus:ring-blue-400"
                        placeholder="Type your message..."
                    />
                    <div className="flex gap-4 w-full lg:w-auto justify-center lg:justify-end">
                        <button
                            onClick={handleSend}
                            className="px-6 py-4 rounded-full transition-colors shadow-sm bg-gray-500 text-white hover:bg-gray-600"
                            disabled={message.trim() === ''}
                        >
                            <FaArrowUp className="w-4 h-4" />
                        </button>
                        <button
                            onClick={clearChatHistory}
                            disabled={isClearing}
                            className="px-6 py-4 rounded-full transition-colors shadow-sm bg-gray-500 text-white hover:bg-gray-600"
                            title="Clear Chat History"
                        >
                            <FaTrash className="w-4 h-4" />
                        </button>
                        <button
                            onClick={shareLocation}
                            disabled={isFetchingUserLocation}
                            className="px-6 py-4 rounded-full transition-colors shadow-sm bg-gray-500 text-white hover:bg-gray-600"
                            title="Share Location"
                        >
                            <FaLocationCrosshairs className="w-4 h-4" />
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}


export default LinkkiAiAssistant