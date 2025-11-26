import type { Metadata } from "next";
import { Outfit } from "next/font/google";
import "./globals.css";
import SiteHeader from "@/components/SiteHeader";

const outfit = Outfit({
  subsets: ["latin"],
  variable: "--font-outfit",
  display: "swap",
});

export const metadata: Metadata = {
  title: "Kalshi Signals",
  description: "Advanced market signals and analytics for Kalshi trades",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className={`${outfit.variable} antialiased`}>
        <SiteHeader />
        {children}
        <script src="/date-redirect.js" />
      </body>
    </html>
  );
}
