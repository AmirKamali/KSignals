(() => {
    document.addEventListener("change", (event) => {
        const target = event.target;
        if (!(target instanceof HTMLSelectElement)) return;
        if (!target.dataset.hrefPrefix) return;

        // Determine which parameter to update based on the select class
        const isSortSelect = target.classList.contains("sortSelect");
        const isDateSelect = target.classList.contains("dateSelect");

        const params = {
            date: isDateSelect ? target.value : (target.dataset.date ?? "next_30_days"),
            category: target.dataset.category ?? "",
            tag: target.dataset.tag ?? "",
            sort_type: isSortSelect ? target.value : (target.dataset.sort ?? "Volume24H"),
            page: "1",
            pageSize: target.dataset.pagesize ?? "20",
            query: target.dataset.query ?? "",
        };

        // Only include direction if we're NOT changing the sort type
        // This allows the backend to apply the appropriate default direction for each sort type
        if (!isSortSelect && target.dataset.direction) {
            params.direction = target.dataset.direction;
        }

        const searchParams = new URLSearchParams(params);
        window.location.href = `${target.dataset.hrefPrefix}?${searchParams.toString()}`;
    });
})();

// Account Dropdown Management
(() => {
    function getCookie(name) {
        const cookies = document.cookie.split(';');
        const cookie = cookies.find(c => c.trim().startsWith(name + '='));
        return cookie ? decodeURIComponent(cookie.split('=')[1]) : null;
    }

    function updateAccountUI() {
        console.log("Updating account UI...");
        // JWT may be httpOnly now; rely on readable cookies or username
        const token = getCookie("ksignals_jwt");
        const username = getCookie("ksignals_username");

        console.log("Token readable:", !!token, "Username:", username);

        const loginBtn = document.getElementById("ksignals-login-btn");
        const accountDropdown = document.getElementById("ksignals-account-dropdown");

        // Check if user has a valid username (not email, not null)
        const hasValidUsername = username && !username.includes('@') && username !== 'null';
        const isAuthenticated = !!token || hasValidUsername;

        if (isAuthenticated && hasValidUsername) {
            // User is logged in - show account dropdown
            console.log("Showing account dropdown");
            if (loginBtn) loginBtn.style.display = "none";
            if (accountDropdown) accountDropdown.style.display = "block";

            // Populate account info
            const usernameEl = document.getElementById("ksignals-account-username");

            if (usernameEl) usernameEl.textContent = `@${username}`;
        } else {
            // User is not logged in - show login button
            console.log("Showing login button");
            if (loginBtn) loginBtn.style.display = "inline-flex";
            if (accountDropdown) accountDropdown.style.display = "none";
        }
    }

    function setupAccountDropdown() {
        const accountBtn = document.getElementById("ksignals-account-btn");
        const accountMenu = document.getElementById("ksignals-account-menu");
        const logoutBtn = document.getElementById("ksignals-logout-btn");

        if (accountBtn && accountMenu) {
            // Toggle dropdown on click
            accountBtn.addEventListener("click", (e) => {
                e.stopPropagation();
                accountMenu.classList.toggle("open");
            });

            // Close dropdown when clicking outside
            document.addEventListener("click", (e) => {
                if (!accountBtn.contains(e.target) && !accountMenu.contains(e.target)) {
                    accountMenu.classList.remove("open");
                }
            });
        }

        if (logoutBtn) {
            logoutBtn.addEventListener("click", async () => {
                // Ask server to clear HttpOnly cookies
                try {
                    await fetch("/auth/logout", { method: "POST", credentials: "include" });
                } catch (err) {
                    console.warn("Logout request failed", err);
                }

                // Clear readable cookies
                document.cookie = "ksignals_jwt=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;";
                document.cookie = "ksignals_firebase_id=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;";
                document.cookie = "ksignals_username=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;";
                document.cookie = "ksignals_name=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;";
                document.cookie = "ksignals_email=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;";

                // Sign out from Firebase if available
                if (window.firebase && window.firebase.auth) {
                    window.firebase.auth().signOut().catch(err => {
                        console.error("Firebase signout error:", err);
                    });
                }

                // Redirect to home with signout parameter
                window.location.href = "/?signout=true";
            });
        }
    }

    // Initialize on DOM load
    document.addEventListener("DOMContentLoaded", () => {
        console.log("DOM loaded, initializing account UI");
        updateAccountUI();
        setupAccountDropdown();
    });

    // Update UI if it's already loaded
    if (document.readyState === "complete" || document.readyState === "interactive") {
        console.log("Document already loaded, initializing account UI");
        setTimeout(() => {
            updateAccountUI();
            setupAccountDropdown();
        }, 100);
    }

    // Listen for storage changes (in case another tab logs in/out)
    window.addEventListener("storage", (e) => {
        if (e.key && e.key.startsWith("ksignals_")) {
            console.log("Storage changed:", e.key);
            updateAccountUI();
        }
    });

    // Expose updateAccountUI globally so other scripts can call it
    window.updateAccountUI = updateAccountUI;
})();
