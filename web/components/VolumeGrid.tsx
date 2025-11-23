import { Market } from "@/lib/kalshi";
import MarketCard from "./MarketCard";
import styles from "./VolumeGrid.module.css";

interface VolumeGridProps {
    markets: Market[];
}

export default function VolumeGrid({ markets }: VolumeGridProps) {
    return (
        <section className={styles.section}>
            <div className="container">
                <div className={styles.header}>
                    <h2 className={styles.heading}>High Volume Trades</h2>
                    <p className={styles.subheading}>Top active markets right now</p>
                </div>

                <div className={styles.grid}>
                    {markets.map((market) => (
                        <MarketCard key={market.ticker} market={market} />
                    ))}
                </div>
            </div>
        </section>
    );
}
