import type { Metadata } from "next";
import { Inter } from "next/font/google";

import "./globals.css";

const inter = Inter({ variable: "--font-inter", subsets: ["latin"] });

export const metadata: Metadata = {
  title: { default: "Soccer Vision", template: "%s · Soccer Vision" },
  description: "Football management with a deterministic match engine.",
};

/** Root shell only. Each route group brings its own header: (site) and auth. */
export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en" className={`${inter.variable} h-full antialiased`}>
      <body className="flex min-h-full flex-col bg-bg" suppressHydrationWarning>
        {children}
      </body>
    </html>
  );
}
