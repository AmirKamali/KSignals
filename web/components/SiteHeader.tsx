import Link from "next/link";
import styles from "./SiteHeader.module.css";

export default function SiteHeader() {
    return (
        <header className={styles.header}>
            <div className="container">
                <div className={styles.inner}>
                    <nav className={styles.nav} aria-label="Primary navigation">
                        <Link href="/" className={styles.navLink}>Home</Link>
                        <Link href="/markets" className={styles.navLink}>Markets</Link>
                    </nav>
                </div>
            </div>
        </header>
    );
}
