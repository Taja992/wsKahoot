import React, { useState } from 'react';
import { toast } from 'react-hot-toast';
import { StringConstants } from '../generated-client';

interface AdminLoginProps {
    onAdminLogin: (isAdmin: boolean) => void;
    sendRequest: (request: any, responseType: string) => Promise<any>;
}

export const AdminLogin: React.FC<AdminLoginProps> = ({ onAdminLogin, sendRequest }) => {
    const [adminPassword, setAdminPassword] = useState("");

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
                toast.success("You are now the admin!");
            } else {
                toast.error("Admin login failed: " + (response.message || "Invalid password"));
            }
        } catch (error) {
            toast.dismiss("admin-login");
            toast.error("Error sending admin request: " + (error instanceof Error ? error.message : String(error)));
        }
    };

    return (
        <div className="admin-login">
            <h2>Admin Login</h2>
            <input
                type="password"
                placeholder="Enter admin password"
                value={adminPassword}
                onChange={(e) => setAdminPassword(e.target.value)}
            />
            <button onClick={handleAdminLogin}>Login as Admin</button>
        </div>
    );
};