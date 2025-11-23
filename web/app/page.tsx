import { getHighVolumeMarkets, getTagsByCategories } from "@/lib/kalshi";
import HeroSection from "@/components/HeroSection";
import VolumeGrid from "@/components/VolumeGrid";
import MarketTable from "@/components/MarketTable";

export default async function Home() {
  const markets = await getHighVolumeMarkets(100); // Fetch more to ensure we have coverage
  const tagsByCategories = await getTagsByCategories();

  const topMarkets = markets.slice(0, 6);
  const tableMarkets = markets.slice(6);

  return (
    <main>
      <HeroSection />
      <VolumeGrid markets={topMarkets} />
      <MarketTable markets={tableMarkets} tagsByCategories={tagsByCategories} />
    </main>
  );
}
