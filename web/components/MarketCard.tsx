import Link from "next/link";
import { ArrowUpRight, BarChart3 } from "lucide-react";
import { Market } from "@/lib/kalshi";
import FormattedTitle from "./FormattedTitle";
import styles from "./MarketCard.module.css";

interface MarketCardProps {
    market: Market;
}

export default function MarketCard({ market }: MarketCardProps) {
    return (
        <Link href={`/trade/${market.ticker}`} className={styles.card}>
            <div className={styles.header}>
                <span className={styles.category}>Event</span>
                <ArrowUpRight size={18} className={styles.icon} />
            </div>

            <h3 className={styles.title}>
                <FormattedTitle text={market.title} />
            </h3>

            <div className={styles.stats}>
                <div className={styles.stat}>
                    <span className={styles.label}>Yes Price</span>
                    <span className={styles.value}>{market.yes_price}¢</span>
                </div>
                <div className={styles.stat}>
                    <span className={styles.label}>Volume</span>
                    <span className={styles.value}>
                        <BarChart3 size={14} style={{ marginRight: 4 }} />
                        {market.volume.toLocaleString()}
                    </span>
                </div>
            </div>

            <div className={styles.progressContainer}>
                <div
                    className={styles.progressBar}
                    style={{ width: `${market.yes_price}%` }}
                />
            </div>
        </Link>
    );
}
