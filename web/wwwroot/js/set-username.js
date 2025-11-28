document.addEventListener('DOMContentLoaded', async () => {
    console.log("SetUsername.js - DOM loaded");

    const firebaseIdInput = document.getElementById("firebase-id-input");

    // Check if user is authenticated with Firebase
    if (!window.firebase || !window.firebase.auth) {
        console.error("Firebase not available");
        window.location.href = "/Login";
        return;
    }

    const auth = firebase.auth();

    // Wait for auth state to be ready
    auth.onAuthStateChanged((user) => {
        if (!user) {
            console.log("No user logged in, redirecting to login");
            window.location.href = "/Login";
            return;
        }

        // Populate the hidden FirebaseId field for server-side processing
        if (firebaseIdInput) {
            firebaseIdInput.value = user.uid;
            console.log("Firebase ID set:", user.uid);
        }

        // Check if user already has a username in cookies
        const cookies = document.cookie.split(';');
        const usernameCookie = cookies.find(c => c.trim().startsWith('ksignals_username='));
        if (usernameCookie) {
            const username = usernameCookie.split('=')[1];
            if (username && username !== user.email && username !== user.uid) {
                console.log("User already has username, redirecting home");
                window.location.href = "/";
                return;
            }
        }
    });
});
