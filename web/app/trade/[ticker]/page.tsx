import { getMarketDetails } from "@/lib/kalshi";
import TradeHeader from "@/components/TradeHeader";
import SignalSection from "@/components/SignalSection";
import OddsVisualizer from "@/components/OddsVisualizer";
import styles from "./page.module.css";

interface PageProps {
    params: Promise<{ ticker: string }>;
}

export default async function TradePage({ params }: PageProps) {
    const { ticker } = await params;
    const market = await getMarketDetails(ticker);

    if (!market) {
        return <div className="container" style={{ paddingTop: "4rem" }}>Market not found</div>;
    }

    return (
        <main className={styles.main}>
            <TradeHeader market={market} />

            <div className="container">
                <div className={styles.grid}>
                    <div className={styles.leftCol}>
                        <OddsVisualizer yesPrice={market.yes_price} />
                        {/* Additional details could go here */}
                    </div>

                    <div className={styles.rightCol}>
                        <SignalSection ticker={ticker} />
                    </div>
                </div>
            </div>
        </main>
    );
}
