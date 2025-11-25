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

                        <div className={styles.metaRow}>
                            <div className={styles.pillGroup}>
                                <span className={styles.pill}>AI probability shifts</span>
                                <span className={styles.pill}>Order book pressure</span>
                                <span className={styles.pill}>Headline-aware signals</span>
                            </div>
                        </div>

                        <div className={styles.actions}>
                            <button className="btn btn-primary">
                                Start Trading <ArrowRight size={18} style={{ marginLeft: "0.5rem" }} />
                            </button>
                            <button className="btn btn-outline">
                                View All Markets
                            </button>
                        </div>

                        <div className={styles.statRow}>
                            <div className={styles.statCard}>
                                <div className={styles.statValue}>120+</div>
                                <div className={styles.statLabel}>Markets tracked live</div>
                            </div>
                            <div className={styles.statCard}>
                                <div className={styles.statValue}>93%</div>
                                <div className={styles.statLabel}>Signal coverage on volume</div>
                            </div>
                            <div className={styles.statCard}>
                                <div className={styles.statValue}><span className={styles.positive}>+7.2%</span></div>
                                <div className={styles.statLabel}>Avg. edge vs. raw order book</div>
                            </div>
                        </div>

                        <div className={styles.previewPanel}>
                            <div className={styles.previewHeader}>
                                <div>
                                    <div className={styles.previewLabel}>Signal preview</div>
                                    <div className={styles.previewSub}>Micro-moves we are watching right now</div>
                                </div>
                                <div className={styles.previewTag}>Live feed</div>
                            </div>
                            <div className={styles.previewGrid}>
                                <div className={styles.previewItem}>
                                    <div className={styles.previewTitle}>Election turnout</div>
                                    <div className={styles.previewMeta}>Yes probability 58%</div>
                                    <div className={styles.previewDelta}>+2.3% in last hour</div>
                                </div>
                                <div className={styles.previewItem}>
                                    <div className={styles.previewTitle}>Rate cut by June</div>
                                    <div className={styles.previewMeta}>Order book leaning NO</div>
                                    <div className={styles.previewDelta}>Spread tightening</div>
                                </div>
                                <div className={styles.previewItem}>
                                    <div className={styles.previewTitle}>Weekly jobless claims</div>
                                    <div className={styles.previewMeta}>Signal confidence High</div>
                                    <div className={styles.previewDelta}>Volatility watchlist</div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </section>
    );
}
