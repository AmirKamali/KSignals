import { Suspense } from "react";
import { getHighVolumeMarkets, getTagsByCategories } from "@/lib/kalshi";
import MarketTable from "@/components/MarketTable";

export const dynamic = "force-dynamic";

export default async function MarketsPage() {
  // Reuse the same data sources as home but focus purely on the table view
  const markets = await getHighVolumeMarkets(200);
  const tagsByCategories = await getTagsByCategories();

  return (
    <main>
      <Suspense fallback={null}>
        <MarketTable markets={markets} tagsByCategories={tagsByCategories} />
      </Suspense>
    </main>
  );
}
