(() => {
    if (!window.firebase || !window.backendBaseUrl) return;

    const firebaseConfig = {
        apiKey: "AIzaSyB1hIE0843ovq2tDJM6hc_z9BX2uE4uX5M",
        authDomain: "ksignals-5caa3.firebaseapp.com",
        projectId: "ksignals-5caa3",
        storageBucket: "ksignals-5caa3.firebasestorage.app",
        messagingSenderId: "561592439844",
        appId: "1:561592439844:web:b5d4330a7cf04d2ab274b1",
        measurementId: "G-393TTYJYMY"
    };

    firebase.initializeApp(firebaseConfig);

    const auth = firebase.auth();

    async function syncUser(user) {
        try {
            const token = await user.getIdToken();
            const nameParts = (user.displayName || "").split(" ");
            const firstName = nameParts.shift() || "";
            const lastName = nameParts.join(" ");

            await fetch(`${window.backendBaseUrl.replace(/\\/$/, "")}/api/users/register`, {
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

            const res = await fetch(`${window.backendBaseUrl.replace(/\\/$/, "")}/api/users/login`, {
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

            if (!res.ok) return;
            const json = await res.json();
            if (json?.token) {
                localStorage.setItem("ksignals_jwt", json.token);
                localStorage.setItem("ksignals_username", json.username || "");
                localStorage.setItem("ksignals_name", json.name || "");
                updateUi(json.name || json.username || user.email || "Logged in");
            }
        } catch (err) {
            console.warn("Failed to login to backend", err);
        }
    }

    function updateUi(label) {
        const loginBtn = document.getElementById("ksignals-login-btn");
        if (loginBtn) {
            loginBtn.textContent = label;
            loginBtn.style.display = "none";
        }

        const userDisplay = document.getElementById("ksignals-user-display");
        if (userDisplay) {
            userDisplay.textContent = label;
        }
    }

    auth.onAuthStateChanged(user => {
        if (user) {
            syncUser(user);
            loginToBackend(user);
            updateUi(user.displayName || user.email || "Logged in");
        }
    });

    window.ksignalsSyncUser = syncUser;
})();
