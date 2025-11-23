import styles from "./OddsVisualizer.module.css";

interface OddsVisualizerProps {
    yesPrice: number;
}

export default function OddsVisualizer({ yesPrice }: OddsVisualizerProps) {
    const noPrice = 100 - yesPrice;

    return (
        <div className={styles.container}>
            <h3 className={styles.heading}>Market Probability</h3>

            <div className={styles.barContainer}>
                <div className={styles.barLabel}>
                    <span>YES</span>
                    <span>{yesPrice}%</span>
                </div>
                <div className={styles.track}>
                    <div
                        className={styles.fillYes}
                        style={{ width: `${yesPrice}%` }}
                    />
                </div>
            </div>

            <div className={styles.barContainer}>
                <div className={styles.barLabel}>
                    <span>NO</span>
                    <span>{noPrice}%</span>
                </div>
                <div className={styles.track}>
                    <div
                        className={styles.fillNo}
                        style={{ width: `${noPrice}%` }}
                    />
                </div>
            </div>
        </div>
    );
}
