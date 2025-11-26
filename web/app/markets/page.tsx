import { getBackendMarkets, getTagsByCategories, Market } from "@/lib/kalshi";
import styles from "@/components/MarketTable.module.css";
import Link from "next/link";
import { Calendar, Search } from "lucide-react";

export const dynamic = "force-dynamic";

type DateFilterOption =
  | "all_time"
  | "next_24_hr"
  | "next_48_hr"
  | "next_7_days"
  | "next_30_days"
  | "next_90_days"
  | "this_year"
  | "next_year";

const PAGE_SIZE = 20;

function buildQuery(params: Record<string, string | number | null | undefined>) {
  const search = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value === null || value === undefined || value === "") return;
    search.set(key, String(value));
  });
  return search.toString();
}

export default async function MarketsPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const categoryParam = typeof params.category === "string" ? params.category : undefined;
  const tagParam = typeof params.tag === "string" ? params.tag : undefined;
  const dateParam = (typeof params.date === "string" ? params.date : "next_30_days") as DateFilterOption;
  const sortParam = typeof params.sort_type === "string" ? params.sort_type : "volume";
  const dirParam = typeof params.direction === "string" ? params.direction : "desc";
  const pageParam = typeof params.page === "string" ? parseInt(params.page, 10) : 1;
  const queryParam = typeof params.query === "string" ? params.query : "";

  const activeCategory = categoryParam || "All";
  const activeTag = tagParam || null;
  const activeDate = dateParam || "next_30_days";
  const activeSort = sortParam;
  const sortDirection = dirParam === "asc" ? "asc" : "desc";
  const currentPage = Math.max(1, pageParam || 1);
  const query = queryParam || "";
  const hasQuery = query.trim().length > 0;

  const tagsByCategories = await getTagsByCategories();
  const backendResponse = await getBackendMarkets({
    category: activeCategory !== "All" ? activeCategory : null,
    tag: activeTag,
    close_date_type: activeDate !== "all_time" ? activeDate : null,
    sort: activeSort as any,
    direction: sortDirection as any,
    page: currentPage,
    pageSize: PAGE_SIZE,
    query: query || null,
  });

  const categories = ["All", ...Object.keys(tagsByCategories).sort()];
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

  const subTags =
    activeCategory !== "All" && tagsByCategories[activeCategory]
      ? tagsByCategories[activeCategory]
      : [];

  const markets: Market[] = backendResponse.markets || [];
  const totalPages = Math.max(1, backendResponse.totalPages || 1);
  const totalCount = backendResponse.totalCount || markets.length;
  const normalizedQuery = hasQuery ? query.trim().toLowerCase() : null;

  const filteredMarkets = normalizedQuery
    ? markets.filter((m) => {
        const fields = [
          m.title,
          m.subtitle,
          m.ticker,
          m.event_ticker,
        ].filter(Boolean) as string[];
        return fields.some((f) => f.toLowerCase().includes(normalizedQuery));
      })
    : markets;

  const renderMarkets = filteredMarkets;
  const renderTotalCount = normalizedQuery ? filteredMarkets.length : totalCount;
  const renderTotalPages = normalizedQuery ? 1 : totalPages;
  const renderCurrentPage = normalizedQuery ? 1 : currentPage;

  const makeLink = (overrides: Record<string, string | number | null | undefined>) => {
    const queryString = buildQuery({
      category: activeCategory !== "All" ? activeCategory : null,
      tag: activeTag,
      date: activeDate,
      sort_type: activeSort,
      direction: sortDirection,
      page: currentPage,
      query,
      ...overrides,
    });
    return `/markets?${queryString}`;
  };

  return (
    <main>
      <section className={styles.section}>
        <div className="container">
          <div className={styles.filterContainer}>
            <div className={styles.controls}>
              {categories.map((cat) => (
                <Link
                  key={cat}
                  href={makeLink({ category: cat === "All" ? null : cat, tag: null, page: 1 })}
                  className={`${styles.segmentBtn} ${activeCategory === cat ? styles.active : ""}`}
                >
                  {cat}
                </Link>
              ))}
            </div>

            {subTags.length > 0 && (
              <div className={styles.controls}>
                {subTags.map((tag) => (
                  <Link
                    key={tag}
                    href={makeLink({ tag, page: 1 })}
                    className={`${styles.segmentBtn} ${activeTag === tag ? styles.active : ""}`}
                  >
                    {tag}
                  </Link>
                ))}
              </div>
            )}

            <div className={styles.dateFilterContainer}>
              <Calendar size={16} className={styles.calendarIcon} />
              <select
                name="date"
                defaultValue={activeDate}
                className={styles.dateSelect}
                data-href-prefix="/markets"
                data-category={activeCategory !== "All" ? activeCategory : ""}
                data-tag={activeTag ?? ""}
                data-sort={activeSort}
                data-direction={sortDirection}
                data-query={hasQuery ? query : ""}
                data-pagesize={PAGE_SIZE.toString()}
              >
                {dateOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <form action="/markets" method="GET" className={styles.searchBar}>
            <input type="hidden" name="category" value={activeCategory !== "All" ? activeCategory : ""} />
            <input type="hidden" name="tag" value={activeTag ?? ""} />
            <input type="hidden" name="date" value={activeDate} />
            <input type="hidden" name="sort_type" value={activeSort} />
            <input type="hidden" name="direction" value={sortDirection} />
            <input type="hidden" name="page" value="1" />
            <input type="hidden" name="pageSize" value={PAGE_SIZE} />
            <Search size={18} className={styles.searchIcon} aria-hidden />
            <input
              type="text"
              name="query"
              defaultValue={query}
              placeholder="Search markets by title or ticker"
              className={styles.searchInput}
            />
            {hasQuery && (
              <Link href={makeLink({ query: null, page: 1 })} className={styles.clearButton}>
                Clear
              </Link>
            )}
            <button type="submit" className={styles.searchButton}>
              Search
            </button>
          </form>

          <div className={styles.tableContainer}>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>Market</th>
                  <th>Volume</th>
                  <th>Yes Price</th>
                  <th>No Price</th>
                </tr>
              </thead>
              <tbody>
                {renderMarkets.map((market, index) => {
                  const rowKey = market.ticker || market.event_ticker || `market-${index}`;
                  const yesPrice = market.yes_price;
                  const noPrice = market.no_price ?? Math.max(0, 100 - yesPrice);
                  return (
                    <tr key={rowKey}>
                      <td className={styles.titleCell}>
                        <Link href={`/trade/${market.ticker}`} className={styles.titleLink}>
                          <div className={styles.marketTitle}>{market.title}</div>
                        </Link>
                      </td>
                      <td>{market.volume.toLocaleString()}</td>
                      <td>{yesPrice}¢</td>
                      <td>{noPrice}¢</td>
                    </tr>
                  );
                })}
                {renderMarkets.length === 0 && (
                  <tr>
                    <td colSpan={4} style={{ textAlign: "center", padding: "2rem" }}>
                      No markets found
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          {renderTotalPages > 1 && (
            <div className={styles.pagination}>
              <Link
                className={styles.pageButton}
                href={makeLink({ page: Math.max(1, renderCurrentPage - 1) })}
                aria-disabled={renderCurrentPage <= 1}
              >
                Previous
              </Link>
              <span className={styles.pageIndicator}>
                Page {renderCurrentPage} of {renderTotalPages} {renderTotalCount > 0 ? `• ${renderTotalCount} markets` : ""}
              </span>
              <Link
                className={styles.pageButton}
                href={makeLink({ page: Math.min(renderTotalPages, renderCurrentPage + 1) })}
                aria-disabled={renderCurrentPage >= renderTotalPages}
              >
                Next
              </Link>
            </div>
          )}
        </div>
      </section>
    </main>
  );
}
