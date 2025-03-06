import React, { useState } from 'react';
import './NicknameForm.css';

interface NicknameFormProps {
    onSubmit: (nickname: string) => void;
    loading: boolean;
}

export const NicknameForm: React.FC<NicknameFormProps> = ({ onSubmit, loading }) => {
    const [nickname, setNickname] = useState('');
    const [error, setError] = useState('');

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault();

        if (!nickname.trim()) {
            setError('Please enter a nickname');
            return;
        }

        if (nickname.length < 3) {
            setError('Nickname must be at least 3 characters');
            return;
        }

        if (nickname.length > 15) {
            setError('Nickname must be less than 15 characters');
            return;
        }

        setError('');
        onSubmit(nickname);
    };

    return (
        <div className="nickname-form-container">
            <h2>Enter Your Nickname</h2>
            <form onSubmit={handleSubmit} className="nickname-form">
                <div className="form-group">
                    <label htmlFor="nickname">Nickname:</label>
                    <input
                        type="text"
                        id="nickname"
                        value={nickname}
                        onChange={(e) => setNickname(e.target.value)}
                        placeholder="Your game nickname"
                        disabled={loading}
                        autoFocus
                    />
                    {error && <div className="error-message">{error}</div>}
                </div>
                <button type="submit" disabled={loading} className="join-button">
                    {loading ? 'Joining...' : 'Join Game'}
                </button>
            </form>
        </div>
    );
};