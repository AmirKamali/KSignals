import { NextResponse } from 'next/server';

const BACKEND_BASE_URL = process.env.BACKEND_API_BASE_URL ?? "http://localhost:3006";

export async function GET(request: Request) {
    const { searchParams } = new URL(request.url);
    const category = searchParams.get('category');
    const tag = searchParams.get('tag');
    const query = searchParams.get('query');
    const closeDateType = searchParams.get('close_date_type');
    const sortType = searchParams.get('sort_type');
    const direction = searchParams.get('direction');
    const page = searchParams.get('page');
    const pageSize = searchParams.get('pageSize');

    const backendUrl = new URL(`${BACKEND_BASE_URL}/api/markets`);
    if (category) backendUrl.searchParams.set('category', category);
    if (tag) backendUrl.searchParams.set('tag', tag);
    if (query) backendUrl.searchParams.set('query', query);
    if (closeDateType) backendUrl.searchParams.set('close_date_type', closeDateType);
    if (sortType) backendUrl.searchParams.set('sort_type', sortType);
    if (direction) backendUrl.searchParams.set('direction', direction);
    if (page) backendUrl.searchParams.set('page', page);
    if (pageSize) backendUrl.searchParams.set('pageSize', pageSize);

    try {
        const response = await fetch(backendUrl.toString(), {
            cache: 'no-store',
        });

        if (!response.ok) {
            console.error(`Backend returned ${response.status} for ${backendUrl.toString()}`);
            return NextResponse.json(
                { error: `Backend returned ${response.status}` },
                { status: response.status }
            );
        }

        const data = await response.json();
        return NextResponse.json(data);
    } catch (error) {
        console.error("Error proxying to backend:", error);
        return NextResponse.json(
            { error: 'Failed to fetch from backend' },
            { status: 500 }
        );
    }
}
