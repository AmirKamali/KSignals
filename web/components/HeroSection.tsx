"use client";

import { useState, useEffect } from "react";
import { ArrowRight, TrendingUp, ShieldCheck, Zap } from "lucide-react";
import styles from "./HeroSection.module.css";

const SLIDES = [
    {
        title: "Trade on Real World Events",
        subtitle: "The first regulated exchange for trading on event outcomes.",
        icon: <TrendingUp size={32} />,
        color: "var(--primary)",
    },
    {
        title: "Data-Driven Signals",
        subtitle: "Get AI-powered insights and probability analysis for every trade.",
        icon: <Zap size={32} />,
        color: "var(--accent)",
    },
    {
        title: "Regulated & Secure",
        subtitle: "Trade with confidence on a CFTC-regulated exchange.",
        icon: <ShieldCheck size={32} />,
        color: "var(--success)",
    },
];

export default function HeroSection() {
    const [currentSlide, setCurrentSlide] = useState(0);

    useEffect(() => {
        const timer = setInterval(() => {
            setCurrentSlide((prev) => (prev + 1) % SLIDES.length);
        }, 5000);
        return () => clearInterval(timer);
    }, []);

    return (
        <section className={styles.hero}>
            <div className={styles.glow} />
            <div className="container">
                <div className={styles.content}>
                    <div className={styles.badge}>
                        <span className={styles.badgeDot} />
                        Live Market Signals
                    </div>

                    <div className={styles.sliderContainer}>
                        {SLIDES.map((slide, index) => (
                            <div
                                key={index}
                                className={`${styles.slide} ${index === currentSlide ? styles.active : ""}`}
                                style={{ opacity: index === currentSlide ? 1 : 0 }}
                            >
                                <h1 className={styles.title}>
                                    {slide.title.split(" ").map((word, i) => (
                                        <span key={i} className={i === 1 ? "text-gradient" : ""}>
                                            {word}{" "}
                                        </span>
                                    ))}
                                </h1>
                                <p className={styles.subtitle}>{slide.subtitle}</p>
                            </div>
                        ))}
                    </div>

                    <div className={styles.controls}>
                        {SLIDES.map((_, index) => (
                            <button
                                key={index}
                                className={`${styles.dot} ${index === currentSlide ? styles.dotActive : ""}`}
                                onClick={() => setCurrentSlide(index)}
                                aria-label={`Go to slide ${index + 1}`}
                            />
                        ))}
                    </div>

                    <div className={styles.actions}>
                        <button className="btn btn-primary">
                            Start Trading <ArrowRight size={18} style={{ marginLeft: "0.5rem" }} />
                        </button>
                        <button className="btn btn-outline">
                            View Markets
                        </button>
                    </div>
                </div>
            </div>
        </section>
    );
}
