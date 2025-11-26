(() => {
    document.addEventListener("change", (event) => {
        const target = event.target;
        if (!(target instanceof HTMLSelectElement)) return;
        if (!target.dataset.hrefPrefix) return;

        const params = new URLSearchParams({
            date: target.value,
            category: target.dataset.category ?? "",
            tag: target.dataset.tag ?? "",
            sort_type: target.dataset.sort ?? "volume",
            direction: target.dataset.direction ?? "desc",
            page: "1",
            pageSize: target.dataset.pagesize ?? "20",
            query: target.dataset.query ?? "",
        });

        window.location.href = `${target.dataset.hrefPrefix}?${params.toString()}`;
    });
})();
