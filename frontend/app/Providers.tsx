"use client";

import { MantineProvider, createTheme } from "@mantine/core";
import { SessionProvider } from "next-auth/react";
import type { ReactNode } from "react";

const theme = createTheme({
  fontFamily:
    "var(--font-geist-sans), -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif",
  fontFamilyMonospace:
    "var(--font-geist-mono), ui-monospace, SFMono-Regular, Menlo, monospace",
  headings: {
    fontFamily:
      "var(--font-geist-sans), -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif",
    fontWeight: "700",
    sizes: {
      h1: { fontSize: "2rem", lineHeight: "1.2" },
      h2: { fontSize: "1.5rem", lineHeight: "1.3" },
      h3: { fontSize: "1.25rem", lineHeight: "1.35" },
      h4: { fontSize: "1.05rem", lineHeight: "1.4" },
    },
  },
  defaultRadius: "md",
  primaryColor: "blue",
  cursorType: "pointer",
});

export default function Providers({ children }: { children: ReactNode }) {
  return (
    <SessionProvider>
      <MantineProvider theme={theme} forceColorScheme="light">
        {children}
      </MantineProvider>
    </SessionProvider>
  );
}
