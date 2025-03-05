import React from 'react';

interface Player {
    id: string;
    score: number;
}

interface GameOverProps {
    players: Player[];
    score: number;
}

export const GameOver: React.FC<GameOverProps> = ({ players, score }) => {
    return (
        <div className="lobby-container">
            <h1>Game Over!</h1>
            <div className="game-over">
                <h2>Final Scores</h2>
                <div className="scores-list">
                    {players.sort((a, b) => b.score - a.score).map((player, index) => (
                        <div key={player.id} className={`player-score ${index === 0 ? 'winner' : ''}`}>
                            <span>{index + 1}. {player.id}</span>
                            <span>{player.score} points</span>
                        </div>
                    ))}
                </div>
                <h3>Your Score: {score}</h3>
            </div>
        </div>
    );
};