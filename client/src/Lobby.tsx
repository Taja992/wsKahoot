import {BaseDto, useWsClient} from "ws-request-hook";
import { useEffect, useState } from "react";
import { toast } from "react-hot-toast";
import {
    QuestionDto,
    StringConstants,
    GameDto
} from "./generated-client";
import './Lobby.css';

// Import components
import { ConnectionStatus } from './components/ConnectionStatus';
import { AdminLogin } from './components/AdminLogin';
import { GameControl } from './components/GameControl';
import { Question } from './components/Question';
import { GameOver } from './components/GameOver';
import { GameSelector } from './components/GameSelector';
import { NicknameForm } from './components/NicknameForm';

export default function Lobby() {
    const { onMessage, sendRequest, send, readyState } = useWsClient();
    const [isAdmin, setIsAdmin] = useState(false);
    const [currentQuestion, setCurrentQuestion] = useState<QuestionDto | null>(null);
    const [selectedAnswer, setSelectedAnswer] = useState<string | null>(null);
    const [score, setScore] = useState(0);
    const [showResultFeedback, setShowResultFeedback] = useState(false);
    const [isCorrect, setIsCorrect] = useState(false);
    const [connectionStatus, setConnectionStatus] = useState("Connecting...");
    const [gameComplete, setGameComplete] = useState(false);
    const [players, setPlayers] = useState<{id: string, score: number}[]>([]);
    const [timeLeft, setTimeLeft] = useState<number | null>(null);
    const [questionTimer, setQuestionTimer] = useState<ReturnType<typeof setTimeout> | null>(null);

    // New states for game selection flow
    const [availableGames, setAvailableGames] = useState<GameDto[]>([]);
    const [selectedGameId, setSelectedGameId] = useState<string | null>(null);
    const [selectedGameName, setSelectedGameName] = useState<string>("");
    const [nickname, setNickname] = useState<string>("");
    const [isJoiningGame, setIsJoiningGame] = useState(false);
    const [hasJoinedGame, setHasJoinedGame] = useState(false);
    const [loadingGames, setLoadingGames] = useState(false);

    // Handle connection status
    useEffect(() => {
        switch (readyState) {
            case 0: // CONNECTING
                setConnectionStatus("Connecting to game server...");
                break;
            case 1: // OPEN
                setConnectionStatus("Connected");
                // Fetch games when connected instead of joining lobby
                fetchAvailableGames();
                break;
            case 2: // CLOSING
                setConnectionStatus("Connection closing...");
                break;
            case 3: // CLOSED
                setConnectionStatus("Connection closed. Reconnecting...");
                break;
            default:
                setConnectionStatus("Unknown connection state");
        }
    }, [readyState]);


    useEffect(() => {
        const handleBeforeUnload = () => {
            // Only send disconnect if connected AND has joined a game
            if (readyState === 1 && hasJoinedGame) {
                console.log("Sending disconnect message");
                
                try {
                    send({
                        eventType: StringConstants.PlayerDisconnectDto,
                        requestId: "disconnect-" + Date.now()
                        // No need to send any IDs - server will identify the player from the socket
                    });
                } catch (error) {
                    console.error("Error sending disconnect message:", error);
                }
            }
        };
      
        window.addEventListener("beforeunload", handleBeforeUnload);
        
        return () => {
            window.removeEventListener("beforeunload", handleBeforeUnload);
        };
    }, [readyState, send, hasJoinedGame]);


    const fetchAvailableGames = async () => {
        if (readyState !== 1) return;

        try {
            setLoadingGames(true);
            const response = await sendRequest({
                eventType: StringConstants.GetGamesRequestDto,
                requestId: "get-games-" + Date.now()
            }, StringConstants.GetGamesResponseDto);

            // Add type assertion to inform TypeScript about the expected shape
            const typedResponse = response as BaseDto & { games: GameDto[] };
            setAvailableGames(typedResponse.games || []);
        } catch (error) {
            toast.error("Failed to fetch games: " + (error instanceof Error ? error.message : String(error)));
        } finally {
            setLoadingGames(false);
        }
    };

    const handleGameSelect = (gameId: string, gameName: string) => {
        setSelectedGameId(gameId);
        setSelectedGameName(gameName);
    };

    const handleNicknameSubmit = async (submittedNickname: string) => {
        if (!selectedGameId || !submittedNickname.trim()) return;

        setNickname(submittedNickname);
        setIsJoiningGame(true);

        try {
            // New request to join a specific game with nickname
            const response = await sendRequest({
                eventType: StringConstants.JoinGameRequestDto,
                requestId: "join-game-" + Date.now(),
                gameId: selectedGameId,
                nickname: submittedNickname
            }, StringConstants.JoinGameResponseDto);

            // Add type assertion for the response
            const typedResponse = response as BaseDto & { success: boolean; message?: string };

            if (typedResponse.success) {
                setHasJoinedGame(true);
                toast.success(`Joined game: ${selectedGameName} as ${submittedNickname}`);
            } else {
                toast.error("Failed to join game: " + (typedResponse.message || "Unknown error"));
                setIsJoiningGame(false);
            }
        } catch (error) {
            toast.error("Error joining game: " + (error instanceof Error ? error.message : String(error)));
            setIsJoiningGame(false);
        }
    };

    // Setup message handlers once joined a game
    useEffect(() => {
        if (readyState !== 1 || !hasJoinedGame) return;

        console.log("Setting up WebSocket message handlers");

        // Add this catch-all handler to see ALL incoming messages
        const handleAnyMessage = (data: any) => {
            console.log("Received WebSocket message:", data);

            // You can analyze the raw messages here to see what's coming in
            if (data && data.eventType === "QuestionDto") {
                console.log("FOUND A QUESTION IN RAW HANDLER:", data);
            }
        };

        const handleMessage = (data: any) => {
            console.log("Handling message with eventType:", data?.eventType);

            if (!data || typeof data.eventType !== 'string') {
                console.warn("Received invalid message format:", data);
                return;
            }

            switch(data.eventType) {
                // Other cases...

                case "PrepareForQuestionDto":
                    // Clear any existing question data and timer
                    if (questionTimer) {
                        clearInterval(questionTimer);
                        setQuestionTimer(null);
                    }
                    setCurrentQuestion(null);
                    setSelectedAnswer(null);
                    setShowResultFeedback(false);
                    setTimeLeft(data.secondsUntilQuestion);
                    
                    // Simple countdown until the next question
                    const prepTimer = setInterval(() => {
                        setTimeLeft(prev => {
                            if (prev === null || prev <= 1) {
                                clearInterval(prepTimer);
                                return null;
                            }
                            return prev - 1;
                        });
                    }, 1000);
                    setQuestionTimer(prepTimer);
                    break;

                case "QuestionDto":
                case "Question":  // Handle both formats
                    console.log("Processing Question:", data);

                    // Reset state for new question
                    if (questionTimer) {
                        clearInterval(questionTimer);
                        setQuestionTimer(null);
                    }
                    setSelectedAnswer(null);
                    setShowResultFeedback(false);

                    // Create a normalized question object with consistent property names
                    const questionData = {
                        id: data.id || data.Id,
                        questionText: data.questionText || data.QuestionText,
                        options: (data.options || data.Options || []).map((opt: any) => ({
                            optionText: opt.optionText || opt.OptionText,
                            isCorrect: opt.isCorrect !== undefined ? opt.isCorrect : opt.IsCorrect
                        }))
                    };

                    console.log("Normalized question data:", questionData);
                    // Just set the question and let the Question component handle its own timer
                    setCurrentQuestion(questionData);
                    setTimeLeft(10);
                    break;

                case "QuestionTimeUpDto":
                    // Time's up for the current question
                    if (questionTimer) {
                        clearInterval(questionTimer);
                        setQuestionTimer(null);
                    }
                    setTimeLeft(0);
                    setShowResultFeedback(true);
                    break;

                case "GameEndedDto":
                    // Handle game ended message by disconnecting
                    toast.custom("The game has ended. All players have been disconnected.");
                    
                    // Send disconnect message to server
                    send({
                        eventType: StringConstants.PlayerDisconnectDto,
                        requestId: "disconnect-" + Date.now()
                    });
                    
                    // Reset game state
                    setGameComplete(true);
                    
                    // We'll keep the results showing rather than reset everything
                    break;

                case "GameCompleteDto":
                    setGameComplete(true);
                    // Handle both property names
                    const playerData = data.players || data.Players || [];
                    setPlayers(playerData);
                    
                    // After showing game results, send a GameEndedDto to server
                    // to indicate this game should be cleaned up
                    if (isAdmin && selectedGameId) {
                        setTimeout(() => {
                            send({
                                eventType: "GameEndedDto",
                                gameId: selectedGameId
                            });
                        }, 5000); // Send after 5 seconds so players can see results
                    }
                    break;
            }
        };

        // Register a catch-all handler
        onMessage("*", handleAnyMessage);

        // Register handlers for game-related events
        onMessage("GamePlayersUpdateDto", handleMessage);
        onMessage("AdminResponseDto", handleMessage);
        onMessage("QuestionDto", handleMessage);
        onMessage("Question", handleMessage);
        onMessage("GameCompleteDto", handleMessage);
        // Add handlers for the new message types
        onMessage("PrepareForQuestionDto", handleMessage);
        onMessage("QuestionTimeUpDto", handleMessage);
        onMessage("GameEndedDto", handleMessage);

        // Debug current state
        console.log("Current lobby state:", {
            hasJoinedGame,
            isAdmin,
            selectedGameId,
            readyState
        });

        return () => {
            console.log("Cleaning up WebSocket message handlers");
            if (questionTimer) clearInterval(questionTimer);
            // Consider unregistering handlers if needed
        };
    }, [readyState, onMessage, questionTimer, hasJoinedGame, isAdmin, selectedGameId, send]);

    useEffect(() => {
        console.log("Current question changed:", currentQuestion);
    }, [currentQuestion]);

    const handleAnswerSelection = (optionText: string | undefined, isCorrect: boolean | undefined) => {
        if (optionText === undefined) return;

        setSelectedAnswer(optionText);
        setIsCorrect(!!isCorrect);

        // Update score if correct
        if (isCorrect === true) {
            setScore(prevScore => prevScore + 1);
        }

        // Send answer to the server with the correct eventType
        send({
            eventType: "AnswerSubmissionDto",
            questionId: currentQuestion?.id || currentQuestion?.id,
            selectedOption: optionText,
            timeRemaining: timeLeft
        });

        // Show feedback immediately after selection
        setShowResultFeedback(true);

        // Clear the timer to stop counting down
        if (questionTimer) {
            clearInterval(questionTimer);
            setQuestionTimer(null);
        }
    };

    // Render game over screen with a "New Game" button
    if (gameComplete) {
        return (
            <GameOver 
                players={players} 
                score={score} 
            />
        );
    }

    // Render initial game selection screen
    if (!selectedGameId) {
        return (
            <div className="lobby-container">
                <h1>Kahoot Clone</h1>
                <ConnectionStatus readyState={readyState} connectionStatus={connectionStatus} />

                <GameSelector
                    games={availableGames}
                    onSelectGame={handleGameSelect}
                    loading={loadingGames}
                />

                {/* Admin login is always visible here */}
                <AdminLogin onAdminLogin={setIsAdmin} sendRequest={sendRequest} />
                
                {/* Show admin controls if user is admin */}
                {isAdmin && <GameControl isAdmin={isAdmin} sendRequest={sendRequest} />}
            </div>
        );
    }

    // Render nickname input screen
    if (!hasJoinedGame) {
        return (
            <div className="lobby-container">
                <h1>Kahoot Clone</h1>
                <ConnectionStatus readyState={readyState} connectionStatus={connectionStatus} />

                <div className="selected-game-info">
                    <h2>Selected Game: {selectedGameName}</h2>
                    <button onClick={() => setSelectedGameId(null)} className="back-button">
                        Choose Another Game
                    </button>
                </div>

                <NicknameForm
                    onSubmit={handleNicknameSubmit}
                    loading={isJoiningGame}
                />
                
                {/* Added admin login to nickname screen as well */}
                <AdminLogin onAdminLogin={setIsAdmin} sendRequest={sendRequest} />
                
                {/* Show admin controls if user is admin */}
                {isAdmin && <GameControl isAdmin={isAdmin} sendRequest={sendRequest} />}
            </div>
        );
    }

    // Render main game interface after joining
    return (
        <div className="lobby-container">
            <h1>Kahoot Clone: {selectedGameName}</h1>
            <div className="player-info">
                Playing as: <strong>{nickname}</strong>
                {isAdmin && <span className="admin-badge">Admin</span>}
            </div>

            <ConnectionStatus readyState={readyState} connectionStatus={connectionStatus} />

            {!currentQuestion ? (
                <>
                    {/* Show admin login if not currently in a game question */}
                    {!timeLeft && <AdminLogin onAdminLogin={setIsAdmin} sendRequest={sendRequest} />}
                    
                    {isAdmin && <GameControl isAdmin={isAdmin} sendRequest={sendRequest} />}
                    
                    {!isAdmin && (
                        <div className="waiting-room">
                            {timeLeft !== null ? (
                                <div className="prepare-question">
                                    <h2>Get Ready!</h2>
                                    <p>Next question in {timeLeft} seconds...</p>
                                </div>
                            ) : (
                                <>
                                    <h2>Waiting for the game to start...</h2>
                                    <p>The admin will start the game shortly.</p>
                                </>
                            )}
                        </div>
                    )}
                </>
            ) : (
                <Question
                    question={currentQuestion}
                    timeLeft={timeLeft}
                    onAnswerSelect={handleAnswerSelection}
                    showResultFeedback={showResultFeedback}
                    selectedAnswer={selectedAnswer}
                    isCorrect={isCorrect}
                    score={score}
                />
            )}
        </div>
    );
}