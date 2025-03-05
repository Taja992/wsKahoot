import React from 'react';

interface PlayersListProps {
    connectedClients: string[];
}

export const PlayersList: React.FC<PlayersListProps> = ({ connectedClients }) => {
    return (
        <div className="connected-clients">
        <h2>Connected Players: {connectedClients.length}</h2>
        <ul>
    {connectedClients.map((client, index) => (
        <li key={index}>{client}</li>
        ))}
    </ul>
        </div>
        );
};