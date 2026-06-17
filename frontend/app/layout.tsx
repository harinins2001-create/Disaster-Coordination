import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import { ColorSchemeScript, mantineHtmlProps } from "@mantine/core";
import "@mantine/core/styles.css";
import "./globals.css";
import Providers from "./Providers";
import AuthCheckLayer from "./AuthCheckLayer";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "DRCS — Department Resource Coordination System",
  description: "University department resource booking and tracking",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="en"
      {...mantineHtmlProps}
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
    >
      <head>
        <ColorSchemeScript forceColorScheme="light" />
      </head>
      <body className="min-h-full flex flex-col">
        <Providers>
          <AuthCheckLayer>{children}</AuthCheckLayer>
        </Providers>
      </body>
    </html>
  );
}
