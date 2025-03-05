import {WsClientProvider} from "ws-request-hook";
import {Toaster} from "react-hot-toast";
import Lobby from "./Lobby.tsx";

export default function App() {
    const wsPort = import.meta.env.VITE_WS_PORT || 8080;


    return (
        <WsClientProvider url={`ws://localhost:${wsPort}`}>
            <Toaster />
            <Lobby />
        </WsClientProvider>
    );
}