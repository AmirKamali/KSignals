"use client";

import { useState } from "react";
import Link from "next/link";
import { ArrowRight, Loader2 } from "lucide-react";
import { Market, getSeriesByTags, getMarketsBySeries } from "@/lib/kalshi";
import styles from "./MarketTable.module.css";

interface MarketTableProps {
    markets: Market[];
    tagsByCategories: Record<string, string[]>;
}

export default function MarketTable({ markets: initialMarkets, tagsByCategories }: MarketTableProps) {
    const [activeCategory, setActiveCategory] = useState("All");
    const [activeSubTag, setActiveSubTag] = useState<string | null>(null);
    const [displayedMarkets, setDisplayedMarkets] = useState<Market[]>(initialMarkets);
    const [isLoading, setIsLoading] = useState(false);

    const categories = ["All", ...Object.keys(tagsByCategories).sort()];

    const handleCategoryClick = (cat: string) => {
        setActiveCategory(cat);
        setActiveSubTag(null);

        if (cat === "All") {
            setDisplayedMarkets(initialMarkets);
        } else {
            // Filter initial markets by category as a fallback/initial view
            const filtered = initialMarkets.filter(m => m.category === cat || (!m.category && cat === "Other"));
            setDisplayedMarkets(filtered);
        }
    };

    const handleSubTagClick = async (tag: string) => {
        setActiveSubTag(tag);
        setIsLoading(true);
        try {
            // 1. Get series for the tag
            const seriesList = await getSeriesByTags(tag);

            // 2. Get markets for each series
            // Limit to first 10 series to avoid too many requests if series list is huge
            // Prioritize series with more recent activity if possible, but we don't have that info yet.
            const targetSeries = seriesList.slice(0, 10);

            const marketsPromises = targetSeries.map(series => getMarketsBySeries(series.ticker));
            const marketsArrays = await Promise.all(marketsPromises);

            const newMarkets = marketsArrays.flat();

            // Remove duplicates
            const uniqueMarkets = Array.from(new Map(newMarkets.map(m => [m.ticker, m])).values());

            // Sort by volume descending
            uniqueMarkets.sort((a, b) => b.volume - a.volume);

            setDisplayedMarkets(uniqueMarkets);
        } catch (error) {
            console.error("Error loading markets by tag:", error);
        } finally {
            setIsLoading(false);
        }
    };

    const subTags = activeCategory !== "All" && tagsByCategories[activeCategory]
        ? tagsByCategories[activeCategory]
        : [];

    return (
        <section className={styles.section}>
            <div className="container">
                <div className={styles.filterContainer}>
                <div className={styles.controls}>
                        {categories.map((cat) => (
                        <button
                            key={cat}
                            className={`${styles.segmentBtn} ${activeCategory === cat ? styles.active : ""}`}
                                onClick={() => handleCategoryClick(cat)}
                        >
                            {cat}
                        </button>
                    ))}
                    </div>

                    {subTags.length > 0 && (
                        <div className={styles.controls}>
                            {subTags.map((tag) => (
                                <button
                                    key={tag}
                                    className={`${styles.segmentBtn} ${activeSubTag === tag ? styles.active : ""}`}
                                    onClick={() => handleSubTagClick(tag)}
                                >
                                    {tag}
                                </button>
                            ))}
                        </div>
                    )}
                </div>

                <div className={styles.tableContainer}>
                    {isLoading ? (
                        <div className={styles.loadingState}>
                            <Loader2 className={styles.spin} size={32} />
                        </div>
                    ) : (
                    <table className={styles.table}>
                        <thead>
                            <tr>
                                <th>Market</th>
                                <th>Volume</th>
                                <th>Yes Price</th>
                                <th>Action</th>
                            </tr>
                        </thead>
                        <tbody>
                                {displayedMarkets.slice(0, 20).map((market) => (
                                <tr key={market.ticker}>
                                    <td className={styles.titleCell}>
                                        <div className={styles.marketTitle}>{market.title}</div>
                                        <div className={styles.marketSubtitle}>{market.event_ticker}</div>
                                    </td>
                                    <td>{market.volume.toLocaleString()}</td>
                                    <td>
                                        <span className={styles.price}>{market.yes_price}¢</span>
                                    </td>
                                    <td>
                                        <Link href={`/trade/${market.ticker}`} className={styles.detailsBtn}>
                                            Details <ArrowRight size={14} />
                                        </Link>
                                    </td>
                                </tr>
                            ))}
                                {displayedMarkets.length === 0 && (
                                    <tr>
                                        <td colSpan={4} style={{ textAlign: "center", padding: "2rem" }}>
                                            No markets found
                                        </td>
                                    </tr>
                                )}
                        </tbody>
                    </table>
                    )}
                </div>
            </div>
        </section>
    );
}
