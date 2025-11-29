// Firebase configuration is loaded from wwwroot/js/firebase_config.js
const firebaseConfig = window.firebaseConfig;
if (!firebaseConfig) {
    console.error("Login.js: window.firebaseConfig missing; ensure firebase_config.js is loaded.");
}

const getCookie = (name) => {
    const cookie = document.cookie.split(";").find(c => c.trim().startsWith(name + "="));
    return cookie ? decodeURIComponent(cookie.split("=")[1]) : null;
};

function initializeFirebase() {
    if (!window.firebase) {
        console.error("Login.js: window.firebase not found!");
        return null;
    }

    try {
        if (firebase.apps?.length) {
            console.log("Login.js: Firebase already initialized");
            return firebase.app();
        }

        console.log("Login.js: Initializing Firebase...");
        const app = firebase.initializeApp(firebaseConfig);
        console.log("Login.js: Firebase initialized successfully");
        return app;
    } catch (e) {
        console.error("Login.js: Firebase initialization failed", e);
        return null;
    }
}

document.addEventListener("DOMContentLoaded", () => {
    console.log("Login.js - DOM loaded");
    const app = initializeFirebase();
    if (!app) return;

    let auth;
    try {
        auth = firebase.auth();
        console.log("Firebase auth initialized:", !!auth);
    } catch (err) {
        console.error("Failed to initialize Firebase auth:", err);
        return;
    }

    const loginBtn = document.getElementById("google-login-btn");
    const loginStatus = document.getElementById("login-status");

    function showStatus(message, isError = false) {
        if (loginStatus) {
            loginStatus.textContent = message;
            loginStatus.className = `login-status ${isError ? "error" : "success"}`;
        }
    }

    async function createSession(idToken) {
        const res = await fetch("/auth/firebase/login", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                idToken,
                returnUrl: window.returnUrl
            })
        });

        const json = await res.json().catch(() => ({}));
        if (!res.ok) {
            const message = json?.error || `Login failed (${res.status})`;
            throw new Error(message);
        }

        return json;
    }

    if (loginBtn) {
        loginBtn.addEventListener("click", async () => {
            try {
                loginBtn.disabled = true;
                loginBtn.textContent = "Signing in...";
                showStatus("Opening Google sign-in...");

                const provider = new firebase.auth.GoogleAuthProvider();
                const result = await auth.signInWithPopup(provider);

                if (!result.user) {
                    throw new Error("No user returned from Firebase");
                }

                showStatus("Finalizing login...");
                const idToken = await result.user.getIdToken();
                const session = await createSession(idToken);

                const redirectUrl = session?.redirectUrl || window.returnUrl || "/";
                showStatus("Login successful! Redirecting...");
                window.location.href = redirectUrl;
            } catch (err) {
                console.error("Login failed", err);
                showStatus("Login failed. Please try again.", true);
                loginBtn.disabled = false;
                loginBtn.innerHTML = `
                    <svg width="18" height="18" viewBox="0 0 18 18" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M17.64 9.2c0-.637-.057-1.251-.164-1.84H9v3.481h4.844c-.209 1.125-.843 2.078-1.796 2.717v2.258h2.908c1.702-1.567 2.684-3.874 2.684-6.615z" fill="#4285F4"/>
                        <path d="M9.003 18c2.43 0 4.467-.806 5.956-2.18L12.05 13.56c-.806.54-1.836.86-3.047.86-2.344 0-4.328-1.584-5.036-3.711H.957v2.332C2.438 15.983 5.482 18 9.003 18z" fill="#34A853"/>
                        <path d="M3.964 10.712c-.18-.54-.282-1.117-.282-1.71 0-.593.102-1.17.282-1.71V4.96H.957C.347 6.175 0 7.55 0 9.002c0 1.452.348 2.827.957 4.042l3.007-2.332z" fill="#FBBC05"/>
                        <path d="M9.003 3.58c1.321 0 2.508.454 3.44 1.345l2.582-2.58C13.464.891 11.426 0 9.003 0 5.482 0 2.438 2.017.957 4.958L3.964 7.29c.708-2.127 2.692-3.71 5.036-3.71z" fill="#EA4335"/>
                    </svg>
                    Sign in with Google
                `;
            }
        });
    }

    auth.onAuthStateChanged(user => {
        if (!user) return;
        const username = getCookie("ksignals_username");
        if (username) {
            const redirectUrl = window.returnUrl || "/";
            window.location.href = redirectUrl;
        }
    });
});
