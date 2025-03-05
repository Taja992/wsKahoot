import { useWsClient } from "ws-request-hook";
import { useEffect, useState } from "react";
import { toast } from "react-hot-toast";
import {
    QuestionDto,
    StringConstants
} from "./generated-client";
import './Lobby.css';

// Import components
import { ConnectionStatus } from './components/ConnectionStatus';
import { PlayersList } from './components/PlayersList';
import { AdminLogin } from './components/AdminLogin';
import { GameControl } from './components/GameControl';
import { Question } from './components/Question';
import { GameOver } from './components/GameOver';

export default function Lobby() {
    const { onMessage, sendRequest, send, readyState } = useWsClient();
    const [connectedClients, setConnectedClients] = useState<string[]>([]);
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
    const [questionTimer, setQuestionTimer] = useState<NodeJS.Timeout | null>(null);

    // Handle connection status
    useEffect(() => {
        switch (readyState) {
            case 0: // CONNECTING
                setConnectionStatus("Connecting to game server...");
                break;
            case 1: // OPEN
                setConnectionStatus("Connected");
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

    // Setup message handlers and send initial lobby message
    useEffect(() => {
        if (readyState !== 1) return;

        // Enter lobby when connected
        sendRequest({
            eventType: StringConstants.ClientEntersLobbyDto,
            requestId: "enter-lobby"
        }, StringConstants.ServerConfirmsDto)
            .catch(error => {
                toast.error("Failed to enter lobby: " + error);
            });

        const handleMessage = (data: any) => {
            if (!data || typeof data.eventType !== 'string') {
                return;
            }

            switch(data.eventType) {
                case "ServerPutsClientInLobbyAndBroadcastsToEveryoneDto":
                case "ServerPutsClientInLobbyAndBroadcastsToEveryone":
                    const clientList = data.allClientIds || data.AllClientIds || [];
                    setConnectedClients(clientList);
                    break;

                case "AdminResponseDto":
                    const adminResponse = data as any;
                    toast.dismiss("admin-login");
                    const isAdmin = adminResponse.isAdmin !== undefined ?
                        adminResponse.isAdmin : adminResponse.IsAdmin;
                    const message = adminResponse.message || adminResponse.Message;

                    if (isAdmin) {
                        setIsAdmin(true);
                        toast.success("You are now the admin!");
                    } else {
                        toast.error("Admin login failed: " + (message || "Invalid password"));
                    }
                    break;

                case "QuestionDto":
                    // Reset state for new question
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

                    setCurrentQuestion(questionData);
                    setTimeLeft(10); // 10 seconds per question

                    // Clear any existing timer
                    if (questionTimer) {
                        clearInterval(questionTimer);
                    }

                    // Start a new timer
                    const timer = setInterval(() => {
                        setTimeLeft(prev => {
                            if (prev === null || prev <= 1) {
                                clearInterval(timer);
                                // Show feedback when time is up
                                setShowResultFeedback(true);
                                return 0;
                            }
                            return prev - 1;
                        });
                    }, 1000);

                    setQuestionTimer(timer);
                    break;

                case "GameCompleteDto":
                    setGameComplete(true);
                    // Handle both property names
                    const playerData = data.players || data.Players || [];
                    setPlayers(playerData);
                    break;
            }
        };

        onMessage("ServerPutsClientInLobbyAndBroadcastsToEveryone", handleMessage);
        onMessage("ServerPutsClientInLobbyAndBroadcastsToEveryoneDto", handleMessage);
        onMessage("AdminResponseDto", handleMessage);
        onMessage("QuestionDto", handleMessage);
        onMessage("GameCompleteDto", handleMessage);

        return () => {
            if (questionTimer) clearInterval(questionTimer);
        };
    }, [readyState, onMessage, sendRequest, questionTimer]);

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
            questionId: currentQuestion?.id || currentQuestion?.id, // Handle both casing options
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

    // Render game over screen
    if (gameComplete) {
        return <GameOver players={players} score={score} />;
    }

    return (
        <div className="lobby-container">
            <h1>Kahoot Clone</h1>

            <ConnectionStatus readyState={readyState} connectionStatus={connectionStatus} />
            <PlayersList connectedClients={connectedClients} />

            {!currentQuestion ? (
                <>
                    {!isAdmin && <AdminLogin onAdminLogin={setIsAdmin} sendRequest={sendRequest} />}
                    {isAdmin && <GameControl isAdmin={isAdmin} sendRequest={sendRequest} />}
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