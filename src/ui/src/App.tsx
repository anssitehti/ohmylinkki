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
      <header className="bg-gradient-to-r from-blue-500 to-blue-600 text-white p-3 flex items-center justify-center gap-2 shadow-sm rounded-lg">
        <BiSolidBusSchool className="h-7 w-auto" />
        <h1 className="text-xl font-bold tracking-tight">
          OhMyLinkki <span className="text-xs font-normal opacity-75">AI Agent</span>
        </h1>
      </header>

      <main className="p-4 md:p-8">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 md:gap-8">
          {/* Chat container */}
          <div className="w-full">
            <LinkkiAiAssistant userId={userId} />
          </div>

          {/* Map container */}
          <div className="w-full">
            <LinkkiMap userId={userId} />
          </div>
        </div>
      </main>
      <ToastContainer />
    </div>
  )
}

export default App
