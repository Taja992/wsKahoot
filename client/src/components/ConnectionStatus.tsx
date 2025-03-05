import React from 'react';

interface ConnectionStatusProps {
    readyState: number;
    connectionStatus: string;
}

export const ConnectionStatus: React.FC<ConnectionStatusProps> = ({ readyState, connectionStatus }) => {
    return (
        <div className={`connection-status ${readyState === 1 ? 'connected' : 'disconnected'}`}>
    {connectionStatus}
    </div>
        );
};