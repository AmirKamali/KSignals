(() => {
    if (!window.firebase || !window.backendBaseUrl) {
        console.error("Firebase or backend URL not initialized");
        return;
    }

    const auth = firebase.auth();
    const loginBtn = document.getElementById("google-login-btn");
    const loginStatus = document.getElementById("login-status");

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
        loginBtn.addEventListener("click", async () => {
            try {
                loginBtn.disabled = true;
                loginBtn.textContent = "Signing in...";
                showStatus("Opening Google sign-in...");

                const provider = new firebase.auth.GoogleAuthProvider();
                const result = await auth.signInWithPopup(provider);

                if (result.user) {
                    showStatus("Authenticating with backend...");
                    await syncUser(result.user);
                    const loginSuccess = await loginToBackend(result.user);

                    if (loginSuccess) {
                        showStatus("Login successful! Redirecting...");
                        setTimeout(() => {
                            window.location.href = returnUrl || "/";
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
            window.location.href = returnUrl || "/";
        }
    });
})();
