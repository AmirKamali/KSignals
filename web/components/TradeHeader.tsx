import { MarketDetail } from "@/lib/kalshi";
import styles from "./TradeHeader.module.css";

interface TradeHeaderProps {
    market: MarketDetail;
}

export default function TradeHeader({ market }: TradeHeaderProps) {
    return (
        <section className={styles.header}>
            <div className="container">
                <div className={styles.breadcrumbs}>
                    Markets / {market.category} / {market.ticker}
                </div>

                <h1 className={styles.title}>{market.title}</h1>
                <p className={styles.subtitle}>{market.subtitle}</p>

                <div className={styles.stats}>
                    <div className={styles.statItem}>
                        <span className={styles.label}>Yes Price</span>
                        <span className={styles.price}>{market.yes_price}¢</span>
                    </div>
                    <div className={styles.statItem}>
                        <span className={styles.label}>Volume</span>
                        <span className={styles.value}>{market.volume.toLocaleString()}</span>
                    </div>
                    <div className={styles.statItem}>
                        <span className={styles.label}>Open Interest</span>
                        <span className={styles.value}>{market.open_interest.toLocaleString()}</span>
                    </div>
                </div>
            </div>
        </section>
    );
}
