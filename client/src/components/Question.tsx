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
            {timeLeft !== null && (
                <div className="timer">
                    <div className="timer-bar" style={{ width: `${(timeLeft / 10) * 100}%` }}></div>
                    <div className="timer-text">{timeLeft}s</div>
                </div>
            )}

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