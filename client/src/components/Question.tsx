import React, { useState, useEffect } from 'react';

interface QuestionOption {
    optionText?: string;
    OptionText?: string;
    isCorrect?: boolean;
    IsCorrect?: boolean;
}

interface QuestionProps {
    question: any;
    timeLeft: number | null;
    onAnswerSelect: (optionText: string | undefined, isCorrect: boolean | undefined) => void;
    showResultFeedback: boolean;
    selectedAnswer: string | null;
    isCorrect: boolean;
    score: number;
}

export const Question: React.FC<QuestionProps> = ({
    question,
    timeLeft,
    onAnswerSelect,
    showResultFeedback,
    selectedAnswer,
    isCorrect,
    score
}) => {
    // Use our own countdown timer that updates every second
    const [displayTime, setDisplayTime] = useState(10);
    
    // Reset and start countdown when a new question appears
    useEffect(() => {
        // Initialize to 10 seconds for each new question
        if (question) {
            console.log("New question detected, resetting timer to 10 seconds");
            setDisplayTime(10);
            
            // Simple countdown implementation using a fixed interval
            const countdown = setInterval(() => {
                setDisplayTime(prevTime => {
                    // Stop at 0
                    if (prevTime <= 0 || showResultFeedback) {
                        clearInterval(countdown);
                        return 0;
                    }
                    return prevTime - 1;
                });
            }, 1000);
            
            // Clean up interval on question change or component unmount
            return () => {
                console.log("Cleaning up timer interval");
                clearInterval(countdown);
            };
        }
    }, [question?.id, showResultFeedback]);
    
    // If server signals time is up, make sure our display shows 0
    useEffect(() => {
        if (timeLeft === 0) {
            setDisplayTime(0);
        }
    }, [timeLeft]);

    const getOptionColor = (optionText: string | undefined, isCorrect: boolean | undefined) => {
        if (!showResultFeedback) {
            // During question time
            if (selectedAnswer === optionText) return 'selected';
            return '';
        } else {
            // After time is up, show correct/incorrect
            if (isCorrect) return 'correct';
            if (selectedAnswer === optionText && !isCorrect) return 'incorrect';
            return '';
        }
    };

    return (
        <div className="question-container">
            {/* Simple countdown display */}
            <div className="countdown-timer">
                Time remaining: <span className="time-value">{displayTime}</span> seconds
            </div>

            <h2>{question.questionText || question.QuestionText}</h2>
            <div className="options">
                {(question?.options || question?.Options || []).map((option: QuestionOption, index: number) => {
                    const optText = option.optionText || option.OptionText;
                    const isOpt = option.isCorrect !== undefined ? option.isCorrect : option.IsCorrect;

                    return (
                        <button
                            key={index}
                            onClick={() => !showResultFeedback && optText && onAnswerSelect(optText, isOpt)}
                            className={`option ${getOptionColor(optText, isOpt)}`}
                            disabled={showResultFeedback || selectedAnswer !== null}
                        >
                            {optText || 'No text'}
                        </button>
                    );
                })}
            </div>

            {showResultFeedback && (
                <div className={`feedback ${isCorrect ? 'correct' : 'incorrect'}`}>
                    <h3>{isCorrect ? 'Correct!' : 'Incorrect!'}</h3>
                </div>
            )}

            <div className="score">
                <h3>Current Score: {score}</h3>
            </div>
        </div>
    );
};