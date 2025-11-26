"use client";

import { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import { useSearchParams, usePathname, useRouter } from "next/navigation";
import { Loader2, Calendar, Triangle } from "lucide-react";
import { Market, getBackendMarkets, MarketSort, SortDirection } from "@/lib/kalshi";
import FormattedTitle from "./FormattedTitle";
import styles from "./MarketTable.module.css";

interface MarketTableProps {
    markets: Market[];
    tagsByCategories: Record<string, string[]>;
}

type DateFilterOption = "all_time" | "next_24_hr" | "next_48_hr" | "next_7_days" | "next_30_days" | "next_90_days" | "this_year" | "next_year";
type SortOption = MarketSort;

const PAGE_SIZE = 20;

function formatCloseTime(closeTime?: string): string {
    if (!closeTime) return "Close time unavailable";

    const close = new Date(closeTime);
    if (Number.isNaN(close.getTime())) return "Close time unavailable";

    const now = new Date();
    const diffMs = close.getTime() - now.getTime();
    const absMinutes = Math.max(1, Math.round(Math.abs(diffMs) / (1000 * 60)));

    let value = absMinutes;
    let unit = "minutes";

    if (absMinutes >= 60) {
        const hours = Math.round(absMinutes / 60);
        if (hours < 48) {
            value = hours;
            unit = hours === 1 ? "hour" : "hours";
        } else {
            const days = Math.round(hours / 24);
            value = days;
            unit = days === 1 ? "day" : "days";
        }
    } else if (absMinutes === 1) {
        unit = "minute";
    }

    if (diffMs >= 0) {
        return `Close time in ${value} ${unit}`;
    }

    return `Closed ${value} ${unit} ago`;
}

type TrendDirection = "up" | "down" | "flat";

function getTrendInfo(current?: number, previous?: number): { direction: TrendDirection; diff: number } | null {
    if (current === undefined || current === null) return null;
    if (previous === undefined || previous === null) return null;

    const diff = current - previous;
    if (!Number.isFinite(diff)) return null;

    if (diff === 0) {
        return { direction: "flat", diff };
    }

    return { direction: diff > 0 ? "up" : "down", diff };
}

function formatDelta(diff: number): string {
    const abs = Math.abs(diff);
    if (abs >= 1) {
        return Math.round(abs).toString();
    }
    return abs.toFixed(2);
}

export default function MarketTable({ markets: initialMarkets, tagsByCategories }: MarketTableProps) {
    const searchParams = useSearchParams();
    const pathname = usePathname();
    const { replace } = useRouter();

    const initialCategory = searchParams.get("category") || "All";
    const initialTag = searchParams.get("tag");
    const initialDate = (searchParams.get("date") as DateFilterOption) || "next_30_days";
    const initialSort = (searchParams.get("sort_type") as SortOption) || "volume";
    const initialDirection = (searchParams.get("direction") as SortDirection) || "desc";
    const initialPage = Math.max(1, parseInt(searchParams.get("page") || "1", 10));
    const initialPageSize = Math.max(1, parseInt(searchParams.get("pageSize") || `${PAGE_SIZE}`, 10));

    const [activeCategory, setActiveCategory] = useState(initialCategory);
    const [activeSubTag, setActiveSubTag] = useState<string | null>(initialTag);
    const [activeDate, setActiveDate] = useState<DateFilterOption>(initialDate);
    const [activeSort, setActiveSort] = useState<SortOption>(initialSort);
    const [sortDirection, setSortDirection] = useState<SortDirection>(initialDirection);
    const [currentPage, setCurrentPage] = useState(initialPage);
    const [pageSize] = useState(initialPageSize);
    const [totalPages, setTotalPages] = useState(1);
    const [totalCount, setTotalCount] = useState(0);
    
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
    const [isLoading, setIsLoading] = useState(true);

    const categories = ["All", ...Object.keys(tagsByCategories).sort()];

    const fetchMarkets = useCallback(async (
        category: string,
        tag: string | null,
        dateFilter: DateFilterOption,
        sort: SortOption,
        direction: SortDirection,
        page: number,
        size: number
    ) => {
        setIsLoading(true);
        try {
            const response = await getBackendMarkets({
                category: category !== "All" ? category : null,
                tag,
                close_date_type: dateFilter !== "all_time" ? dateFilter : null,
                sort,
                direction,
                page,
                pageSize: size
            });

            setDisplayedMarkets(response.markets);
            setTotalPages(Math.max(1, response.totalPages || 1));
            setTotalCount(response.totalCount || response.markets.length);
            setCurrentPage(response.currentPage || 1);
            setActiveSort(response.sort || sort);
            setSortDirection(response.direction || direction);
        } catch (error) {
            console.error("Error loading markets:", error);
            // Fallback to local filtering if fetch fails
            if (!tag && category !== "All") {
                const filtered = initialMarkets.filter(m => m.category === category || (!m.category && category === "Other"));
                setDisplayedMarkets(filtered);
                setTotalPages(1);
                setTotalCount(filtered.length);
            } else {
                setTotalPages(1);
                setTotalCount(initialMarkets.length);
            }
        } finally {
            setIsLoading(false);
        }
    }, [initialMarkets]);

    useEffect(() => {
        const cat = searchParams.get("category") || "All";
        const tag = searchParams.get("tag");
        const date = (searchParams.get("date") as DateFilterOption) || "next_30_days";
        const sort = (searchParams.get("sort_type") as SortOption) || "volume";
        const direction = (searchParams.get("direction") as SortDirection) || "desc";
        const page = Math.max(1, parseInt(searchParams.get("page") || "1", 10));
        const size = Math.max(1, parseInt(searchParams.get("pageSize") || `${pageSize}`, 10));

        setActiveCategory(cat);
        setActiveSubTag(tag);
        setActiveDate(date);
        setActiveSort(sort);
        setSortDirection(direction);
        setCurrentPage(page);

        fetchMarkets(cat, tag, date, sort, direction, page, size);
    }, [searchParams, fetchMarkets, pageSize]);

    const updateUrl = (newCategory: string, newTag: string | null, newDate?: DateFilterOption, options?: { sort?: SortOption; direction?: SortDirection; page?: number; resetPage?: boolean }) => {
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

        const dateToUse = newDate !== undefined ? newDate : activeDate;
        if (dateToUse && dateToUse !== "all_time") {
            params.set("date", dateToUse);
        } else {
            params.delete("date");
        }

        const sortToUse = options?.sort ?? activeSort;
        const directionToUse = options?.direction ?? sortDirection;
        const pageToUse = options?.resetPage ? 1 : (options?.page ?? currentPage);

        params.set("sort_type", sortToUse);
        params.set("direction", directionToUse);

        if (pageToUse && pageToUse > 1) {
            params.set("page", String(pageToUse));
        } else {
            params.delete("page");
        }

        if (pageSize && pageSize !== PAGE_SIZE) {
            params.set("pageSize", String(pageSize));
        } else {
            params.delete("pageSize");
        }

        replace(`${pathname}?${params.toString()}`, { scroll: false });
    };

    const handleCategoryClick = (cat: string) => {
        // When category changes, clear the tag
        updateUrl(cat, null, undefined, { resetPage: true });
    };

    const handleSubTagClick = (tag: string) => {
        updateUrl(activeCategory, tag, undefined, { resetPage: true });
    };

    const handleDateChange = (date: DateFilterOption) => {
        updateUrl(activeCategory, activeSubTag, date, { resetPage: true });
    };

    const handleSortClick = (sort: SortOption) => {
        const nextDirection: SortDirection = sort === activeSort && sortDirection === "desc" ? "asc" : "desc";
        setActiveSort(sort);
        setSortDirection(nextDirection);
        setCurrentPage(1);
        updateUrl(activeCategory, activeSubTag, undefined, { sort, direction: nextDirection, resetPage: true });
    };

    const handlePageChange = (page: number) => {
        const safePage = Math.max(1, Math.min(page, totalPages));
        updateUrl(activeCategory, activeSubTag, undefined, { page: safePage });
    };

    const subTags = activeCategory !== "All" && tagsByCategories[activeCategory]
        ? tagsByCategories[activeCategory]
        : [];

    const dateOptions: { value: DateFilterOption; label: string }[] = [
        { value: "all_time", label: "All time" },
        { value: "next_24_hr", label: "Next 24 Hours" },
        { value: "next_48_hr", label: "Next 48 Hours" },
        { value: "next_7_days", label: "Next 7 Days" },
        { value: "next_30_days", label: "Next 30 Days" },
        { value: "next_90_days", label: "Next 90 Days" },
        { value: "this_year", label: "This Year" },
        { value: "next_year", label: "Next Year" },
    ];

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

                    <div className={styles.dateFilterContainer}>
                        <Calendar size={16} className={styles.calendarIcon} />
                        <select
                            value={activeDate}
                            onChange={(e) => handleDateChange(e.target.value as DateFilterOption)}
                            className={styles.dateSelect}
                        >
                            {dateOptions.map((option) => (
                                <option key={option.value} value={option.value}>
                                    {option.label}
                                </option>
                            ))}
                        </select>
                    </div>
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
                                <th>
                                    <button
                                        type="button"
                                        className={`${styles.sortButton} ${activeSort === "volume" ? styles.sortActive : ""}`}
                                        onClick={() => handleSortClick("volume")}
                                        aria-label={`Sort by volume ${sortDirection === "desc" ? "descending" : "ascending"}`}
                                    >
                                        Volume
                                        <Triangle
                                            size={12}
                                            className={styles.sortIcon}
                                            style={sortDirection === "desc" ? undefined : { transform: "rotate(180deg)" }}
                                        />
                                    </button>
                                </th>
                                <th>Yes Price</th>
                                <th>No Price</th>
                            </tr>
                        </thead>
                        <tbody>
                                {displayedMarkets.map((market, index) => {
                                    const rowKey = market.ticker || market.event_ticker || `market-${index}`;
                                    const closeLabel = formatCloseTime(market.close_time || market.closeTime);
                                    const noPrice = market.no_price ?? Math.max(0, 100 - market.yes_price);
                                    const previousYes = market.previous_yes_price;
                                    const previousNo = market.previous_no_price ?? (typeof previousYes === "number" ? 100 - previousYes : undefined);
                                    const yesTrend = getTrendInfo(market.yes_price, previousYes);
                                    const noTrend = getTrendInfo(noPrice, previousNo);
                                    return (
                                <tr key={rowKey}>
                                    <td className={styles.titleCell}>
                                        <Link href={`/trade/${market.ticker}`} className={styles.titleLink}>
                                            <div className={styles.marketTitle}>
                                                <FormattedTitle text={market.title} />
                                            </div>
                                            <div className={styles.marketSubtitle}>{closeLabel}</div>
                                        </Link>
                                    </td>
                                    <td>{market.volume.toLocaleString()}</td>
                                    <td>
                                        <div className={styles.priceCell}>
                                            <span className={styles.price}>{market.yes_price}¢</span>
                                            {yesTrend && yesTrend.diff !== 0 && (
                                                <span className={`${styles.trend} ${yesTrend.direction === "up" ? styles.trendUp : styles.trendDown}`}>
                                                    <Triangle
                                                        size={10}
                                                        className={styles.trendIcon}
                                                        style={yesTrend.direction === "down" ? { transform: "rotate(180deg)" } : undefined}
                                                    />
                                                    <span className={styles.delta}>{formatDelta(yesTrend.diff)}¢</span>
                                                </span>
                                            )}
                                        </div>
                                    </td>
                                    <td>
                                        <div className={styles.priceCell}>
                                            <span className={styles.noPrice}>{noPrice}¢</span>
                                            {noTrend && noTrend.diff !== 0 && (
                                                <span className={`${styles.trend} ${noTrend.direction === "up" ? styles.trendUp : styles.trendDown}`}>
                                                    <Triangle
                                                        size={10}
                                                        className={styles.trendIcon}
                                                        style={noTrend.direction === "down" ? { transform: "rotate(180deg)" } : undefined}
                                                    />
                                                    <span className={styles.delta}>{formatDelta(noTrend.diff)}¢</span>
                                                </span>
                                            )}
                                        </div>
                                    </td>
                                </tr>
                                    );
                            })}
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
                {!isLoading && totalPages > 1 && (
                    <div className={styles.pagination}>
                        <button
                            className={styles.pageButton}
                            onClick={() => handlePageChange(currentPage - 1)}
                            disabled={currentPage <= 1}
                        >
                            Previous
                        </button>
                        <span className={styles.pageIndicator}>
                            Page {currentPage} of {totalPages} {totalCount > 0 ? `• ${totalCount} markets` : ""}
                        </span>
                        <button
                            className={styles.pageButton}
                            onClick={() => handlePageChange(currentPage + 1)}
                            disabled={currentPage >= totalPages}
                        >
                            Next
                        </button>
                    </div>
                )}
            </div>
        </section>
    );
}
