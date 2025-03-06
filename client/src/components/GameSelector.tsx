import React from 'react';
import { GameDto } from '../generated-client';
import './GameSelector.css';

interface GameSelectorProps {
    games: GameDto[];
    onSelectGame: (gameId: string, gameName: string) => void;
    loading: boolean;
}

export const GameSelector: React.FC<GameSelectorProps> = ({ games, onSelectGame, loading }) => {
    if (loading) {
        return <div className="game-selector loading">Loading available games...</div>;
    }

    if (games.length === 0) {
        return <div className="game-selector no-games">
            <h2>No Games Available</h2>
            <p>There are currently no games available to join.</p>
        </div>;
    }

    return (
        <div className="game-selector">
            <h2>Select a Game to Join</h2>
            <div className="games-grid">
                {games.map(game => (
                    <button
                        key={game.id}
                        className="game-card"
                        onClick={() => onSelectGame(game.id!, game.name!)}
                    >
                        {game.name}
                    </button>
                ))}
            </div>
        </div>
    );
};