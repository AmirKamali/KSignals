"use client";

import { useState, useEffect } from "react";
import { Sparkles, CheckCircle2, AlertCircle, BrainCircuit } from "lucide-react";
import styles from "./SignalSection.module.css";

interface SignalSectionProps {
    ticker: string;
}

interface SignalData {
    prediction: "YES" | "NO";
    confidence: number;
    reasoning: string[];
}

export default function SignalSection({ ticker }: SignalSectionProps) {
    const [signal, setSignal] = useState<SignalData | null>(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        const fetchSignal = async () => {
            try {
                // Simulate AI processing time
                await new Promise(resolve => setTimeout(resolve, 1500));

                const res = await fetch(`/api/signals?ticker=${ticker}`);
                if (res.ok) {
                    const data = await res.json();
                    setSignal(data);
                }
            } catch (error) {
                console.error("Failed to fetch signal", error);
            } finally {
                setLoading(false);
            }
        };

        fetchSignal();
    }, [ticker]);

    if (loading) {
        return (
            <div className={styles.container}>
                <div className={styles.loadingState}>
                    <BrainCircuit className={styles.pulseIcon} size={48} />
                    <p>Analyzing market data...</p>
                </div>
            </div>
        );
    }

    if (!signal) return null;

    return (
        <div className={styles.container}>
            <div className={styles.header}>
                <Sparkles className={styles.icon} />
                <h2 className={styles.title}>Kalshi Signal Prediction</h2>
            </div>

            <div className={styles.content}>
                <div className={styles.predictionCard}>
                    <span className={styles.predictionLabel}>AI Forecast</span>
                    <div className={`${styles.predictionValue} ${signal.prediction === "YES" ? styles.yes : styles.no}`}>
                        {signal.prediction}
                    </div>
                    <div className={styles.confidence}>
                        {signal.confidence}% Confidence
                    </div>
                </div>

                <div className={styles.reasoning}>
                    <h3 className={styles.reasoningTitle}>Key Insights</h3>
                    <ul className={styles.list}>
                        {signal.reasoning.map((reason, i) => (
                            <li key={i} className={styles.listItem}>
                                <CheckCircle2 size={16} className={styles.checkIcon} />
                                {reason}
                            </li>
                        ))}
                    </ul>
                </div>
            </div>

            <div className={styles.disclaimer}>
                <AlertCircle size={14} />
                <span>AI predictions are based on historical data and do not guarantee future results.</span>
            </div>
        </div>
    );
}
