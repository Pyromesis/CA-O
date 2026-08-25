import type { Metadata, Viewport } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";
import { Toaster } from "@/components/ui/toaster";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "CA-O | Optimización Avanzada de Sistema",
  description: "CA-O - Herramienta de optimización avanzada para Windows con diseño inspirado en macOS. Mejora el rendimiento de tu sistema.",
  keywords: ["CA-O", "optimización", "Windows", "rendimiento", "sistema", "tweaks", "gaming"],
  authors: [{ name: "CA-O Team" }],
  icons: {
    icon: "/assets/ca-o-logo.svg",
    apple: { url: "/assets/ca-o-logo.svg" },
  },
  manifest: "/manifest.json",
  openGraph: {
    title: "CA-O | Optimización Avanzada de Sistema",
    description: "Optimiza tu Windows al máximo con CA-O",
    type: "website",
  },
};

export const viewport: Viewport = {
  themeColor: "#FF6B35",
  width: "device-width",
  initialScale: 1,
  maximumScale: 1,
  userScalable: false,
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="es" suppressHydrationWarning>
      <head>
        <link rel="manifest" href="/manifest.json" />
        <meta name="apple-mobile-web-app-capable" content="yes" />
        <meta name="apple-mobile-web-app-status-bar-style" content="black-translucent" />
        <meta name="apple-mobile-web-app-title" content="CA-O" />
      </head>
      <body
        className={`${geistSans.variable} ${geistMono.variable} antialiased bg-background text-foreground`}
      >
        {children}
        <Toaster />
        
        {/* Service Worker Registration */}
        <script
          dangerouslySetInnerHTML={{
            __html: `
              if ('serviceWorker' in navigator) {
                window.addEventListener('load', () => {
                  navigator.serviceWorker.register('/sw.js').catch((e) => console.log('SW registration failed:', e));
                });
              }
            `,
          }}
        />
      </body>
    </html>
  );
}
