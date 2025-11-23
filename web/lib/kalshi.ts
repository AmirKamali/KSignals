"use server";

const BASE_URL = "https://api.elections.kalshi.com/trade-api/v2";

export interface Market {
    ticker: string;
    event_ticker: string;
    title: string;
    subtitle?: string;
    yes_price: number;
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

export async function getHighVolumeMarkets(limit = 100): Promise<Market[]> {
    try {
        const response = await fetch(`${BASE_URL}/markets?limit=${limit}&status=open`, {
            next: { revalidate: 60 },
        });

        if (!response.ok) {
            throw new Error("Failed to fetch markets");
        }

        const data = await response.json();
        let markets: Market[] = data.markets || [];

        // Assign categories heuristically since API returns empty string
        markets = markets.map(m => ({
            ...m,
            category: assignCategory(m)
        }));

        return markets
            .sort((a, b) => b.volume - a.volume)
            .slice(0, limit);
    } catch (error) {
        console.error("Error fetching high volume markets:", error);
        return [];
    }
}

function assignCategory(market: Market): string {
    const text = `${market.title} ${market.ticker} ${market.event_ticker}`.toLowerCase();

    if (text.includes("fed") || text.includes("inflation") || text.includes("rate") || text.includes("gdp") || text.includes("economy") || text.includes("spx") || text.includes("nasdaq") || text.includes("treasur")) return "Economics";
    if (text.includes("trump") || text.includes("biden") || text.includes("harris") || text.includes("election") || text.includes("senate") || text.includes("house") || text.includes("president") || text.includes("gov") || text.includes("cabinet")) return "Politics";
    if (text.includes("apple") || text.includes("tesla") || text.includes("ai") || text.includes("gpt") || text.includes("tech") || text.includes("musk") || text.includes("nvidia")) return "Science and Technology";
    if (text.includes("temp") || text.includes("rain") || text.includes("snow") || text.includes("hurricane") || text.includes("climate") || text.includes("weather") || text.includes("degree")) return "Climate and Weather";
    if (text.includes("bitcoin") || text.includes("btc") || text.includes("eth") || text.includes("crypto") || text.includes("solana")) return "Crypto";
    if (text.includes("movie") || text.includes("music") || text.includes("oscar") || text.includes("grammy") || text.includes("box office") || text.includes("spotify")) return "Entertainment";
    if (text.includes("football") || text.includes("nfl") || text.includes("nba") || text.includes("sport") || text.includes("game")) return "Sports";
    if (text.includes("disease") || text.includes("health") || text.includes("covid") || text.includes("vaccine")) return "Health";
    if (text.includes("financial") || text.includes("stock") || text.includes("market")) return "Financials";

    return "Other";
}

export async function getMarketsBySeries(seriesTicker: string): Promise<Market[]> {
    try {
        const response = await fetch(`${BASE_URL}/markets?series_ticker=${seriesTicker}&status=open`);
        if (!response.ok) return [];
        const data = await response.json();
        return data.markets || [];
    } catch (error) {
        console.error(`Error fetching series ${seriesTicker}:`, error);
        return [];
    }
}

export async function getTagsByCategories(): Promise<Record<string, string[]>> {
    try {
        const response = await fetch(`${BASE_URL}/search/tags_by_categories`);
        if (!response.ok) throw new Error("Failed to fetch tags");

        const data = await response.json();
        return data.tags_by_categories || {};
    } catch (error) {
        console.error("Error fetching tags:", error);
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
        const response = await fetch(`${BASE_URL}/series?tags=${tags}`);
        if (!response.ok) return [];
        const data = await response.json();
        return data.series || [];
    } catch (error) {
        console.error(`Error fetching series for tags ${tags}:`, error);
        return [];
    }
}

export async function getMarketDetails(ticker: string): Promise<MarketDetail | null> {
    try {
        const response = await fetch(`${BASE_URL}/markets/${ticker}`);
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
        const response = await fetch(`${BASE_URL}/markets/${ticker}/orderbook`);
        if (!response.ok) return null;
        const data = await response.json();
        return data.orderbook;
    } catch (error) {
        console.error(`Error fetching orderbook for ${ticker}:`, error);
        return null;
    }
}
