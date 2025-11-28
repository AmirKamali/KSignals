// Inline Firebase Initialization
const firebaseConfig = {
    apiKey: "AIzaSyB1hIE0843ovq2tDJM6hc_z9BX2uE4uX5M",
    authDomain: "ksignals-5caa3.firebaseapp.com",
    projectId: "ksignals-5caa3",
    storageBucket: "ksignals-5caa3.firebasestorage.app",
    messagingSenderId: "561592439844",
    appId: "1:561592439844:web:b5d4330a7cf04d2ab274b1",
    measurementId: "G-393TTYJYMY"
};

if (window.firebase) {
    console.log("Login.js: Initializing Firebase...");
    try {
        firebase.initializeApp(firebaseConfig);
        console.log("Login.js: Firebase initialized successfully");
    } catch (e) {
        if (e.code === 'app/duplicate-app') {
            console.log("Login.js: Firebase already initialized");
        } else {
            console.error("Login.js: Firebase initialization failed", e);
        }
    }
} else {
    console.error("Login.js: window.firebase not found!");
}

if (!window.backendBaseUrl) {
    console.warn("Login.js: backendBaseUrl not found, some features may not work.");
}

document.addEventListener('DOMContentLoaded', () => {
    console.log("Login.js - DOM loaded");
    console.log("Firebase available:", !!window.firebase);
    console.log("Backend URL:", window.backendBaseUrl);

    if (!window.firebase) {
        console.error("Firebase not initialized - waiting...");
        return;
    }


    // Initialize Firebase auth
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

    console.log("Login button found:", !!loginBtn);
    console.log("Login status found:", !!loginStatus);

    async function syncUser(user) {
        try {
            const token = await user.getIdToken();
            const nameParts = (user.displayName || "").split(" ");
            const firstName = nameParts.shift() || "";
            const lastName = nameParts.join(" ");

            await fetch(`${window.backendBaseUrl.replace(/\/$/, "")}/api/users/register`, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "Authorization": `Bearer ${token}`
                },
                body: JSON.stringify({
                    firebaseId: user.uid,
                    username: user.displayName || user.email || user.uid,
                    firstName,
                    lastName,
                    email: user.email,
                    isComnEmailOn: true
                })
            });
        } catch (err) {
            console.warn("Failed to sync user with backend", err);
        }
    }

    async function loginToBackend(user) {
        try {
            const token = await user.getIdToken();
            const nameParts = (user.displayName || "").split(" ");
            const firstName = nameParts.shift() || "";
            const lastName = nameParts.join(" ");

            const res = await fetch(`${window.backendBaseUrl.replace(/\/$/, "")}/api/users/login`, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "Authorization": `Bearer ${token}`
                },
                body: JSON.stringify({
                    firebaseId: user.uid,
                    username: user.displayName || user.email || user.uid,
                    firstName,
                    lastName,
                    email: user.email
                })
            });

            if (!res.ok) {
                throw new Error("Backend login failed");
            }

            const json = await res.json();
            if (json?.token) {
                localStorage.setItem("ksignals_jwt", json.token);
                localStorage.setItem("ksignals_username", json.username || "");
                localStorage.setItem("ksignals_name", json.name || "");
                return true;
            }
            return false;
        } catch (err) {
            console.warn("Failed to login to backend", err);
            return false;
        }
    }

    function showStatus(message, isError = false) {
        if (loginStatus) {
            loginStatus.textContent = message;
            loginStatus.className = `login-status ${isError ? 'error' : 'success'}`;
        }
    }

    if (loginBtn) {
        console.log("Attaching click event to login button");
        loginBtn.addEventListener("click", async () => {
            console.log("Login button clicked!");
            try {
                loginBtn.disabled = true;
                loginBtn.textContent = "Signing in...";
                showStatus("Opening Google sign-in...");

                console.log("Creating Google auth provider...");
                const provider = new firebase.auth.GoogleAuthProvider();
                console.log("Opening sign-in popup...");
                const result = await auth.signInWithPopup(provider);
                console.log("Sign-in successful:", !!result.user);

                if (result.user) {
                    showStatus("Authenticating with backend...");
                    await syncUser(result.user);
                    const loginSuccess = await loginToBackend(result.user);

                    if (loginSuccess) {
                        showStatus("Login successful! Redirecting...");
                        const redirectUrl = window.returnUrl || "/";
                        console.log("Redirecting to:", redirectUrl);
                        setTimeout(() => {
                            window.location.href = redirectUrl;
                        }, 500);
                    } else {
                        throw new Error("Backend authentication failed");
                    }
                }
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

    // Check if user is already logged in
    auth.onAuthStateChanged(user => {
        if (user && localStorage.getItem("ksignals_jwt")) {
            // User is already logged in, redirect back
            console.log("User already logged in, redirecting...");
            window.location.href = window.returnUrl || "/";
        }
    });
});
