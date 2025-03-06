import React, { useState } from 'react';
import { toast } from 'react-hot-toast';
import { StringConstants } from '../generated-client';
import './AdminLogin.css';

interface AdminLoginProps {
    onAdminLogin: (isAdmin: boolean) => void;
    sendRequest: (request: any, responseType: string) => Promise<any>;
}

export const AdminLogin: React.FC<AdminLoginProps> = ({ onAdminLogin, sendRequest }) => {
    const [adminPassword, setAdminPassword] = useState("");
    const [showAdminLogin, setShowAdminLogin] = useState(false);

    const handleAdminLogin = async () => {
        if (!adminPassword) {
            toast.error("Please enter an admin password");
            return;
        }

        try {
            toast.loading("Logging in as admin...", { id: "admin-login" });
            const response = await sendRequest({
                eventType: StringConstants.AdminRequestDto,
                requestId: "admin-login-" + Date.now(),
                password: adminPassword
            }, StringConstants.AdminResponseDto);

            toast.dismiss("admin-login");

            if (response.isAdmin) {
                onAdminLogin(true);
                setAdminPassword("");
                setShowAdminLogin(false);
                toast.success("You are now the admin!");
            } else {
                toast.error("Admin login failed: " + (response.message || "Invalid password"));
            }
        } catch (error) {
            toast.dismiss("admin-login");
            toast.error("Error sending admin request: " + (error instanceof Error ? error.message : String(error)));
        }
    };

    const handleKeyDown = (e: React.KeyboardEvent) => {
        if (e.key === 'Enter') {
            handleAdminLogin();
        }
    };

    return (
        <div className="admin-login-container">
            {showAdminLogin ? (
                <div className="admin-login">
                    <input
                        type="password"
                        placeholder="Enter admin password"
                        value={adminPassword}
                        onChange={(e) => setAdminPassword(e.target.value)}
                        onKeyDown={handleKeyDown}
                        autoFocus
                    />
                    <div className="admin-login-buttons">
                        <button onClick={handleAdminLogin}>Login</button>
                        <button 
                            className="cancel-button"
                            onClick={() => setShowAdminLogin(false)}
                        >
                            Cancel
                        </button>
                    </div>
                </div>
            ) : (
                <button 
                    className="admin-toggle-button"
                    onClick={() => setShowAdminLogin(true)}
                >
                    Admin Login
                </button>
            )}
        </div>
    );
};