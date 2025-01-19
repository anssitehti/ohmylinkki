import { useState } from 'react'
import './App.css'

import { ToastContainer } from 'react-toastify';
import 'react-toastify/dist/ReactToastify.css';
import LinkkiMap from './LinkkiMap';
import LinkkiAiAssistant from './LinkkiAiAssistant';
import { BiSolidBusSchool } from 'react-icons/bi';

function App() {
  const [userId] = useState(() => crypto.randomUUID())

  return (
    <div className="flex flex-col bg-gray-100">
      <header className="bg-gradient-to-r from-blue-500 to-blue-600 text-white p-6 flex items-center justify-center gap-3 shadow-sm rounded-xl">
        <BiSolidBusSchool className="h-10 w-auto" />
        <h1 className="text-2xl font-bold tracking-tight">
          OhMyLinkki <span className="text-sm font-normal opacity-75">AI Assistant</span>
        </h1>
      </header>

      <main className="p-8">
        <div className="flex gap-8">
          {/* Chat container */}
          <LinkkiAiAssistant userId={userId} />

          {/* Map container */}
          <LinkkiMap userId={userId} />
        </div>
      </main>
      <ToastContainer />
    </div>
  )
}

export default App
