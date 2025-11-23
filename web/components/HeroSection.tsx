"use client";

import { ArrowRight } from "lucide-react";
import styles from "./HeroSection.module.css";

export default function HeroSection() {
    return (
        <section className={styles.hero}>
            <div className={styles.backgroundEffects}>
                <div className={styles.gridPattern} />
                <div className={styles.glowPrimary} />
                <div className={styles.glowSecondary} />
            </div>

            <div className="container">
                <div className={styles.layout}>
                    <div className={styles.content}>
                        <div className={styles.badge}>
                            <span className={styles.badgeDot} />
                            <span className={styles.badgeText}>Live Market Signals Active</span>
                        </div>

                        <h1 className={styles.title}>
                            Trade on <span className="text-gradient">Real World</span> Events
                        </h1>
                        
                        <p className={styles.subtitle}>
                            Access AI-powered insights and probability analysis for the first regulated 
                            exchange dedicated to trading event outcomes.
                        </p>

                        <div className={styles.actions}>
                            <button className="btn btn-primary">
                                Start Trading <ArrowRight size={18} style={{ marginLeft: "0.5rem" }} />
                            </button>
                            <button className="btn btn-outline">
                                View All Markets
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </section>
    );
}
