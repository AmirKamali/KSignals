document.addEventListener("DOMContentLoaded", async () => {
    console.log("Username.js - DOM loaded");

    const firebaseIdInput = document.getElementById("firebase-id-input");

    const getCookie = (name) => {
        const cookie = document.cookie.split(";").find(c => c.trim().startsWith(name + "="));
        return cookie ? decodeURIComponent(cookie.split("=")[1]) : null;
    };

    const firebaseIdFromCookie = getCookie("ksignals_firebase_id");
    if (firebaseIdInput && firebaseIdFromCookie) {
        firebaseIdInput.value = firebaseIdFromCookie;
        console.log("Firebase ID populated from cookie");
    }

    if (firebaseIdInput?.value) {
        return;
    }

    // Fallback to Firebase auth if available
    if (window.firebase?.auth) {
        const auth = firebase.auth();
        auth.onAuthStateChanged((user) => {
            if (!user) {
                console.log("No user logged in, redirecting to login");
                window.location.href = "/Login";
                return;
            }

            if (firebaseIdInput) {
                firebaseIdInput.value = user.uid;
                console.log("Firebase ID set from auth state:", user.uid);
            }
        });
    } else {
        console.warn("Firebase not available and no cookie set; redirecting to login");
        window.location.href = "/Login";
    }
});
