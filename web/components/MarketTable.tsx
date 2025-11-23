"use client";

import { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import { useSearchParams, usePathname, useRouter } from "next/navigation";
import { ArrowRight, Loader2 } from "lucide-react";
import { Market, getSeriesByTags, getMarketsBySeries, getSeriesByCategory } from "@/lib/kalshi";
import styles from "./MarketTable.module.css";

interface MarketTableProps {
    markets: Market[];
    tagsByCategories: Record<string, string[]>;
}

export default function MarketTable({ markets: initialMarkets, tagsByCategories }: MarketTableProps) {
    const searchParams = useSearchParams();
    const pathname = usePathname();
    const { replace } = useRouter();

    const initialCategory = searchParams.get("category") || "All";
    const initialTag = searchParams.get("tag");

    const [activeCategory, setActiveCategory] = useState(initialCategory);
    const [activeSubTag, setActiveSubTag] = useState<string | null>(initialTag);
    
    // Initialize displayed markets based on synchronous category filtering
    // Only use initialMarkets fallback if we are on "All" or if we want to show *something* while fetching
    // But since we are moving to async fetching for categories too, we might want to start with empty or initial if matches.
    const [displayedMarkets, setDisplayedMarkets] = useState<Market[]>(() => {
        if (initialCategory === "All") {
            return initialMarkets;
        }
        // If category is set but no tag, we initially show fallback filtering 
        // until the async fetch completes (handled in useEffect).
        // This provides better UX than empty table.
        return initialMarkets.filter(m => m.category === initialCategory || (!m.category && initialCategory === "Other"));
    });
    
    // We are loading if there is a tag OR a category (that is not All) because now we fetch for categories too
    const [isLoading, setIsLoading] = useState(!!initialTag || (initialCategory !== "All"));

    const categories = ["All", ...Object.keys(tagsByCategories).sort()];

    const fetchMarkets = useCallback(async (category: string, tag: string | null) => {
        setIsLoading(true);
        try {
            let seriesList;
            
            if (tag) {
                // 1. Get series for the tag
                seriesList = await getSeriesByTags(tag);
            } else if (category !== "All") {
                // 1. Get series for the category
                seriesList = await getSeriesByCategory(category);
            } else {
                // "All" category - we use initialMarkets, no need to fetch series
                setDisplayedMarkets(initialMarkets);
                setIsLoading(false);
                return;
            }

            // 2. Get markets for each series
            // Limit to first 10 series to avoid too many requests if series list is huge
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
            console.error("Error loading markets:", error);
            // Fallback to local filtering if fetch fails
             if (!tag && category !== "All") {
                const filtered = initialMarkets.filter(m => m.category === category || (!m.category && category === "Other"));
                setDisplayedMarkets(filtered);
            }
        } finally {
            setIsLoading(false);
        }
    }, [initialMarkets]);

    useEffect(() => {
        const cat = searchParams.get("category") || "All";
        const tag = searchParams.get("tag");

        setActiveCategory(cat);
        setActiveSubTag(tag);

        if (tag || cat !== "All") {
            fetchMarkets(cat, tag);
        } else {
            setDisplayedMarkets(initialMarkets);
        }
    }, [searchParams, initialMarkets, fetchMarkets]);

    const updateUrl = (newCategory: string, newTag: string | null) => {
        const params = new URLSearchParams(searchParams);
        
        if (newCategory && newCategory !== "All") {
            params.set("category", newCategory);
        } else {
            params.delete("category");
        }

        if (newTag) {
            params.set("tag", newTag);
        } else {
            params.delete("tag");
        }

        replace(`${pathname}?${params.toString()}`, { scroll: false });
    };

    const handleCategoryClick = (cat: string) => {
        // When category changes, clear the tag
        updateUrl(cat, null);
    };

    const handleSubTagClick = (tag: string) => {
        updateUrl(activeCategory, tag);
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
