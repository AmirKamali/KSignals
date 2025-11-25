const BACKEND_BASE_URL =
    process.env.BACKEND_API_BASE_URL ??
    "http://localhost:3006";
const KALSHI_BASE_URL = "https://api.elections.kalshi.com/trade-api/v2";

export interface Market {
    ticker: string;
    event_ticker: string;
    title: string;
    subtitle?: string;
    close_time?: string;
    closeTime?: string;
    yes_price: number;
    no_price?: number;
    previous_yes_price?: number;
    previous_no_price?: number;
    volume: number;
    open_interest: number;
    liquidity: number;
    status: string;
    category?: string;
}

export interface MarketDetail extends Market {
    category: string; // Override as required for details
    expiration_time: string;
}

export interface OrderBook {
    yes: [number, number][];
    no: [number, number][];
}

export interface Series {
    ticker: string;
    frequency: string;
    title: string;
    category: string;
    tags: string[];
}

export type MarketSort = "volume";
export type SortDirection = "asc" | "desc";

export interface BackendMarketsResponse {
    markets: Market[];
    totalPages: number;
    totalCount: number;
    currentPage: number;
    pageSize: number;
    sort?: MarketSort;
    direction?: SortDirection;
}

export async function getHighVolumeMarkets(limit = 100): Promise<Market[]> {
    const backend = await getBackendMarkets({ pageSize: limit });
    if (backend.markets.length > 0) {
        return backend.markets.sort((a, b) => b.volume - a.volume).slice(0, limit);
    }

    // Fallback to Kalshi directly if backend is unavailable
    try {
        const response = await fetch(`${KALSHI_BASE_URL}/markets?limit=${limit}&status=open`, {
            next: { revalidate: 60 },
        });

        if (!response.ok) {
            console.warn(`Kalshi API returned ${response.status}`);
            return [];
        }

        const data = await response.json();
        const markets: Market[] = data.markets || [];
        return markets.sort((a, b) => b.volume - a.volume).slice(0, limit);
    } catch (error) {
        console.warn("Fallback fetch from Kalshi failed:", error);
        return [];
    }
}

export async function getMarketsBySeries(seriesTicker: string, maxCloseTs: number | null = null): Promise<Market[]> {
    try {
        let url = `${KALSHI_BASE_URL}/markets?series_ticker=${seriesTicker}&status=open`;

        if (maxCloseTs) {
            url += `&max_close_ts=${maxCloseTs}`;
        }

        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 10000); // 10 second timeout

        const response = await fetch(url, {
            signal: controller.signal,
            next: { revalidate: 60 }
        });

        clearTimeout(timeoutId);

        if (!response.ok) {
            console.warn(`API returned ${response.status} for series ${seriesTicker}`);
            return [];
        }

        const data = await response.json();
        return data.markets || [];
    } catch (error) {
        if (error instanceof Error && error.name === 'AbortError') {
            console.error(`Request timeout for series ${seriesTicker}`);
        } else {
            console.error(`Error fetching series ${seriesTicker}:`, error);
        }
        return [];
    }
}

export async function getTagsByCategories(): Promise<Record<string, string[]>> {
    try {
        const response = await fetch(`${BACKEND_BASE_URL}/api/events/categories`, {
            next: { revalidate: 300 },
        });
        if (!response.ok) {
            console.warn(`Backend tags response ${response.status}`);
            throw new Error("Failed to fetch tags");
        }

        const data = await response.json();
        const tags = (data.tagsByCategories || data.tags_by_categories) as Record<string, string[]>;
        return tags || {};
    } catch (error) {
        console.warn("Error fetching tags from backend, using defaults:", error);
        return {
            "Economics": ["Interest Rates", "Inflation", "GDP"],
            "Politics": ["Elections", "Policy"],
            "Technology": ["AI", "Hardware"],
            "Other": []
        };
    }
}

export async function getSeriesByTags(tags: string): Promise<Series[]> {
    try {
        const response = await fetch(`${KALSHI_BASE_URL}/series?tags=${tags}`);
        if (!response.ok) return [];
        const data = await response.json();
        return data.series || [];
    } catch (error) {
        console.error(`Error fetching series for tags ${tags}:`, error);
        return [];
    }
}

export async function getSeriesByCategory(category: string): Promise<Series[]> {
    try {
        const res = await fetch(`${KALSHI_BASE_URL}/series?series_category=${encodeURIComponent(category)}`);
        if (!res.ok) return [];
        const data = await res.json();
        return data.series || [];
    } catch (error) {
        console.error(`Error fetching series for category ${category}:`, error);
        return [];
    }
}

export async function getMarketDetails(ticker: string): Promise<MarketDetail | null> {
    try {
        const response = await fetch(`${KALSHI_BASE_URL}/markets/${ticker}`);
        if (!response.ok) return null;
        const data = await response.json();
        return data.market;
    } catch (error) {
        console.error(`Error fetching market ${ticker}:`, error);
        return null;
    }
}

export async function getOrderBook(ticker: string): Promise<OrderBook | null> {
    try {
        const response = await fetch(`${KALSHI_BASE_URL}/markets/${ticker}/orderbook`);
        if (!response.ok) return null;
        const data = await response.json();
        return data.orderbook;
    } catch (error) {
        console.error(`Error fetching orderbook for ${ticker}:`, error);
        return null;
    }
}

export async function getBackendMarkets(params?: {
    category?: string | null;
    tag?: string | null;
    close_date_type?: string | null;
    sort?: MarketSort;
    direction?: SortDirection;
    page?: number;
    pageSize?: number;
}): Promise<BackendMarketsResponse> {
    // Use Next.js API route for client-side calls, direct backend for server-side
    const isServer = typeof window === 'undefined';
    const baseUrl = isServer ? BACKEND_BASE_URL : '';
    const endpoint = isServer ? '/api/markets' : '/api/markets';

    const url = new URL(endpoint, isServer ? baseUrl : window.location.origin);
    if (params?.category) url.searchParams.set("category", params.category);
    if (params?.tag) url.searchParams.set("tag", params.tag);
    if (params?.close_date_type) url.searchParams.set("close_date_type", params.close_date_type);
    url.searchParams.set("sort_type", params?.sort ?? "volume");
    url.searchParams.set("direction", params?.direction ?? "desc");
    if (params?.page && params.page > 1) url.searchParams.set("page", params.page.toString());
    if (params?.pageSize) url.searchParams.set("pageSize", params.pageSize.toString());

    try {
        const response = await fetch(url.toString(), {
            cache: 'no-store'
        });
        if (!response.ok) {
            console.warn(`Backend markets response ${response.status}`);
            return {
                markets: [],
                totalPages: 0,
                totalCount: 0,
                currentPage: params?.page ?? 1,
                pageSize: params?.pageSize ?? 50,
            };
        }

        const data = await response.json();
        const markets: Market[] = (data.markets || []).map(normalizeBackendMarket);
        const totalPages = Number(data.totalPages ?? 0);
        const totalCount = Number(data.count ?? markets.length);
        const currentPage = Number(data.currentPage ?? params?.page ?? 1);
        const pageSize = Number(data.pageSize ?? params?.pageSize ?? 50);
        return {
            markets,
            totalPages,
            totalCount,
            currentPage,
            pageSize,
            sort: (data.sort_type ?? data.sort ?? params?.sort ?? "volume") as MarketSort,
            direction: data.direction ?? (params?.direction ?? "desc"),
        };
    } catch (error) {
        console.warn("Falling back: unable to fetch backend markets", error instanceof Error ? error.message : error);
        return {
            markets: [],
            totalPages: 0,
            totalCount: 0,
            currentPage: params?.page ?? 1,
            pageSize: params?.pageSize ?? 50,
        };
    }
}

type BackendMarket = Record<string, unknown>;

function normalizeBackendMarket(raw: BackendMarket): Market {
    const ticker = (raw.tickerId ?? raw.ticker) as string | undefined;
    const seriesTicker = (raw.seriesTicker ?? raw.eventTicker ?? ticker) as string | undefined;
    // Prefer lastPrice over yesBid as yesBid is often 0
    const lastPrice = raw.lastPrice as number | undefined;
    const noBid = (raw.noBid as number | undefined) ?? (raw.no_price as number | undefined) ?? (raw.noPrice as number | undefined);
    const noAsk = (raw.noAsk as number | undefined) ?? (raw.no_ask as number | undefined);
    const noPrice = noBid ?? noAsk;
    const yesBid = (raw.yesBid as number | undefined) ?? (raw.yes_price as number | undefined) ?? (raw.yesPrice as number | undefined);
    const yesPrice = lastPrice ?? yesBid ?? (typeof noPrice === "number" ? Math.max(0, 100 - noPrice) : 0);
    const previousYesPrice = (raw.previousPrice as number | undefined)
        ?? (raw.previousYesBid as number | undefined)
        ?? (raw.previous_yes_price as number | undefined)
        ?? (raw.previous_yes_bid as number | undefined);
    const previousNoPrice = (raw.previousNoBid as number | undefined)
        ?? (raw.previous_no_price as number | undefined)
        ?? (raw.previous_no_bid as number | undefined)
        ?? (typeof previousYesPrice === "number" ? 100 - previousYesPrice : undefined);
    const liquidity = raw.liquidity as number | undefined;
    const volume = raw.volume as number | undefined;
    const closeTime = (raw.closeTime ?? raw.close_time) as string | undefined;

    return {
        ticker: ticker ?? "",
        event_ticker: seriesTicker ?? "",
        title: (raw.title as string) ?? "",
        subtitle: (raw.subtitle as string) ?? "",
        yes_price: yesPrice,
        no_price: noPrice ?? (typeof yesPrice === "number" ? Math.max(0, 100 - yesPrice) : undefined),
        previous_yes_price: previousYesPrice,
        previous_no_price: previousNoPrice,
        volume: volume ?? 0,
        open_interest: liquidity ?? 0,
        liquidity: liquidity ?? 0,
        status: (raw.status as string) ?? "",
        category: (raw.category as string) ?? seriesTicker ?? "",
        close_time: closeTime,
        closeTime: closeTime,
    };
}
