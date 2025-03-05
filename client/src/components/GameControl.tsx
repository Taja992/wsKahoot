import React, { useState, useEffect } from 'react';
import { toast } from 'react-hot-toast';
import { StringConstants, GameDto } from '../generated-client';
import './GameControl.css'; // Create this file for styling

interface GameControlProps {
    isAdmin: boolean;
    sendRequest: (request: any, responseType: string) => Promise<any>;
}

export const GameControl: React.FC<GameControlProps> = ({ isAdmin, sendRequest }) => {
    const [games, setGames] = useState<GameDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [selectedGame, setSelectedGame] = useState<GameDto | null>(null);
    const [showConfirmation, setShowConfirmation] = useState(false);

    // Fetch available games when component mounts
    useEffect(() => {
        if (!isAdmin) return;

        const fetchGames = async () => {
            try {
                setLoading(true);
                console.log("Fetching games...");
                const response = await sendRequest({
                    eventType: StringConstants.GetGamesRequestDto,
                    requestId: "get-games-" + Date.now()
                }, StringConstants.GetGamesResponseDto);

                console.log("Games response:", response);
                setGames(response.games || []);
            } catch (error) {
                console.error("Error fetching games:", error);
                toast.error("Failed to fetch games: " + (error instanceof Error ? error.message : String(error)));
            } finally {
                setLoading(false);
            }
        };

        fetchGames();
    }, [isAdmin, sendRequest]);

    const handleGameSelect = (game: GameDto) => {
        setSelectedGame(game);
        setShowConfirmation(true);
    };

    const handleStartGame = async () => {
        if (!selectedGame || !isAdmin) return;

        try {
            const response = await sendRequest({
                eventType: StringConstants.StartGameRequestDto,
                requestId: "start-game",
                gameId: selectedGame.id
            }, StringConstants.StartGameResponseDto);

            if (response.success) {
                toast.success(`Game "${selectedGame.name}" started!`);
                setShowConfirmation(false);
            } else {
                toast.error("Failed to start game: " + response.message);
            }
        } catch (error) {
            toast.error("Error starting game: " + (error instanceof Error ? error.message : String(error)));
        }
    };

    const handleCancel = () => {
        setSelectedGame(null);
        setShowConfirmation(false);
    };

    if (!isAdmin) {
        return null;
    }

    if (loading) {
        return <div className="game-control loading">Loading available games...</div>;
    }

    if (games.length === 0) {
        return <div className="game-control no-games">No games available.</div>;
    }

    return (
        <div className="game-control">
            <h2>Start Game</h2>

            {showConfirmation ? (
                <div className="confirmation-dialog">
                    <h3>Start game: {selectedGame?.name}?</h3>
                    <div className="confirmation-buttons">
                        <button
                            className="confirm-button"
                            onClick={handleStartGame}
                        >
                            Yes
                        </button>
                        <button
                            className="cancel-button"
                            onClick={handleCancel}
                        >
                            No
                        </button>
                    </div>
                </div>
            ) : (
                <div className="games-list">
                    {games.map((game) => (
                        <button
                            key={game.id}
                            className="game-button"
                            onClick={() => handleGameSelect(game)}
                        >
                            {game.name}
                        </button>
                    ))}
                </div>
            )}
        </div>
    );
};